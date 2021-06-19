using Common;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using WebUI.Shared;

namespace WebUI.Server.Controllers
{
	[ApiController]
	[Route("/api/settings")]
	public class SettingsController : Controller
	{
		private IAppConfiguration _config;

		public SettingsController(IAppConfiguration config)
		{
			_config = config;
		}

		[HttpGet]
		public IActionResult Index()
		{
			var response = SettingsGetResponse.Convert(_config);
			response.SettingsFilePath = Path.Join(Environment.CurrentDirectory, "configuration.local.json");

			return Ok(response);
		}
	}
}
