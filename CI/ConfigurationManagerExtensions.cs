using JBSnorro.Diagnostics;
using System;
using System.Configuration;

namespace JBSnorro
{
	public static class ConfigurationManagerExtensions
	{
		/// <summary>
		/// Reads a key from the <see cref="ConfigurationManager.AppSettings"/> and parses it as integer.
		/// </summary>
		/// <param name="key"> The key of the setting to parse. </param>
		/// <param name="ifMissing"> The value to be returnen when the key is not present. </param>
		/// <exception cref="AppSettingNotFoundException">If the setting was not found and no <paramref name="ifMissing"/> was specified. </exception>
		/// <exception cref="InvalidAppSettingException">If the setting was found but could not be parsed to an integer. </exception>
		/// <exception cref="ConfigurationErrorsException">Could not retrieve an object with the application settings data.</exception>
		public static int ParseAppSettingInt(string key, int? ifMissing = null)
		{
			Contract.Requires(!string.IsNullOrEmpty(key));

			string setting = ConfigurationManager.AppSettings[key];
			if (setting == null)
			{
				return ifMissing ?? throw new AppSettingNotFoundException(nameof(key));
			}

			if (!int.TryParse(setting, out int result))
			{
				throw new InvalidAppSettingException(nameof(setting), $"A number was expected, but got '{setting}'");
			}
			return result;
		}
	}
	public class InvalidAppSettingException : Exception
	{
		public InvalidAppSettingException() : base()
		{
		}
		public InvalidAppSettingException(string settingsName, string message)
			: base(settingsName + ": " + message)
		{

		}
	}
}
