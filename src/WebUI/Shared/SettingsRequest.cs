using Common;

namespace WebUI.Shared
{
	public class SettingsGetResponse : Configuration
	{
		public string SettingsFilePath { get; set; }

		public static SettingsGetResponse Convert(IAppConfiguration config)
		{
			var response =  new SettingsGetResponse() 
			{
				App = config.App,
				Format = config.Format,
				Observability = config.Observability,
				Developer = config.Developer
			};

			response.Peloton.ExcludeWorkoutTypes = config.Peloton.ExcludeWorkoutTypes;
			response.Peloton.NumWorkoutsToDownload = config.Peloton.NumWorkoutsToDownload;
			response.Peloton.Email = "******" + config.Peloton.Email.Substring(6);
			response.Peloton.Password = "******";

			response.Garmin.Upload = config.Garmin.Upload;
			response.Garmin.FormatToUpload = config.Garmin.FormatToUpload;
			response.Garmin.Email = "******" + config.Peloton.Email.Substring(6);
			response.Garmin.Password = "******";

			return response;
		}
	}
}
