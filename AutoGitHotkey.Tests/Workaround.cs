using System.Configuration;
using System.IO;
using System.Reflection;
using NUnit.Framework;

[SetUpFixture]
public class TestSetupFixture
{
	/// <summary> From https://gist.github.com/marclitchfield/3cbe6da847b43654d7298fbc6becec39 </summary>
	[OneTimeSetUp]
	public void InitializeTestRunnerAppConfig()
	{
		var appConfigPath = Assembly.GetExecutingAssembly().Location + ".config";

		if (!File.Exists(appConfigPath))
			return;

		var appConfig = ConfigurationManager.OpenMappedExeConfiguration(
			new ExeConfigurationFileMap { ExeConfigFilename = appConfigPath }, ConfigurationUserLevel.None);

		var activeConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

		if (appConfig == activeConfig)
			return;

		activeConfig.AppSettings.Settings.Clear();

		foreach (var key in appConfig.AppSettings.Settings.AllKeys)
			activeConfig.AppSettings.Settings.Add(appConfig.AppSettings.Settings[key]);

		activeConfig.Save();

		ConfigurationManager.RefreshSection("appSettings");
	}
}