using Common;
using Common.Database;
using Conversion;
using Garmin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Peloton;
using Prometheus;
using Serilog;
using Serilog.Enrichers.Span;
using System;
using System.Diagnostics;
using System.Reflection;

namespace WebUI.Server
{
	public class Startup
	{
		private static readonly Gauge BuildInfo = Prometheus.Metrics.CreateGauge("p2g_build_info", "Build info for the running instance.", new GaugeConfiguration()
		{
			LabelNames = new[] { Common.Metrics.Label.Version, Common.Metrics.Label.Os, Common.Metrics.Label.OsVersion, Common.Metrics.Label.DotNetRuntime }
		});

		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		// For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSingleton<IAppConfiguration>((serviceProvider) =>
			{
				var config = new Configuration();
				Configuration.GetSection(nameof(App)).Bind(config.App);
				Configuration.GetSection(nameof(Format)).Bind(config.Format);
				Configuration.GetSection(nameof(Peloton)).Bind(config.Peloton);
				Configuration.GetSection(nameof(Garmin)).Bind(config.Garmin);
				Configuration.GetSection(nameof(Observability)).Bind(config.Observability);
				Configuration.GetSection(nameof(Developer)).Bind(config.Developer);

				ChangeToken.OnChange(() => Configuration.GetReloadToken(), () =>
				{
					Log.Information("Config change detected, reloading config values.");
					Configuration.GetSection(nameof(App)).Bind(config.App);
					Configuration.GetSection(nameof(Format)).Bind(config.Format);
					Configuration.GetSection(nameof(Peloton)).Bind(config.Peloton);
					Configuration.GetSection(nameof(Garmin)).Bind(config.Garmin);
					Configuration.GetSection(nameof(Developer)).Bind(config.Developer);
					Log.Information("Config reloaded. Changes will take effect at the end of the current sleeping cycle.");
				});

				return config;
			});

			services.AddSingleton<IDbClient, DbClient>();
			services.AddSingleton<IFileHandling, IOWrapper>();
			services.AddSingleton<IPelotonApi, Peloton.ApiClient>();
			services.AddSingleton<IPelotonService, PelotonService>();
			services.AddSingleton<IConverter, FitConverter>(); // TODO: Switch to strategy pattern
			services.AddSingleton<IGarminUploader, GarminUploader>();

			Log.Logger = new LoggerConfiguration()
					.ReadFrom.Configuration(Configuration, sectionName: $"{nameof(Observability)}:Serilog")
					.Enrich.WithSpan()
					.CreateLogger();

			services.AddControllersWithViews();
			services.AddRazorPages();
			services.AddSwaggerGen(c => 
			{
				c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo() { Title = "P2G Api", Version = "v1" });
			});
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
				app.UseWebAssemblyDebugging();
			}
			else
			{
				app.UseExceptionHandler("/Error");
				// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
				app.UseHsts();
			}

			var observability = new Observability();
			Configuration.GetSection(nameof(Observability)).Bind(observability);

			app.UseSerilogRequestLogging();

			app.UseHttpsRedirection();
			app.UseBlazorFrameworkFiles();
			app.UseStaticFiles();
			app.UseSwagger();
			app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "P2G Api v1"));

			app.UseRouting();

			if (observability.Prometheus.Enabled)
			{
				app.UseHttpMetrics(options =>
				{
					// This identifies the page when using Razor Pages.
					options.AddRouteParameter("page");
				});
			}
			
			app.UseEndpoints(endpoints =>
			{
				endpoints.MapRazorPages();
				endpoints.MapControllers();
				endpoints.MapFallbackToFile("index.html");

				if (observability.Prometheus.Enabled)
				{
					endpoints.MapMetrics();
				}				
			});

			var runtimeVersion = Environment.Version.ToString();
			var os = Environment.OSVersion.Platform.ToString();
			var osVersion = Environment.OSVersion.VersionString;
			var assembly = Assembly.GetExecutingAssembly();
			var versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
			var version = versionInfo.ProductVersion;

			BuildInfo.WithLabels(version, os, osVersion, runtimeVersion).Set(1);
			Log.Debug("P2G Version: {@Version}", version);
			Log.Debug("Operating System: {@Os}", osVersion);
			Log.Debug("DotNet Runtime: {@DotnetRuntime}", runtimeVersion);
		}
	}
}
