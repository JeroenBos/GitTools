using JBSnorro.Diagnostics;
using JBSnorro.Extensions;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.Configuration
{
    /// <summary>
    /// Represents a <see cref="ConfigurationManager"/> per <see cref="AppDomain"/>.
    /// </summary>
    public sealed class AppDomainConfigurationManager
    {
        [ThreadStatic]
        private static AppDomain threadAppDomain;
        [ThreadStatic]
        private static IReadOnlyDictionary<string, string> appSettings;

        public static IReadOnlyDictionary<string, string> AppSettings
        {
            get
            {
                if (threadAppDomain == null)
                {
                    var settings = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None)
                                                       .AppSettings
                                                       .Settings;

                    appSettings = settings.Cast<KeyValueConfigurationElement>()
                                          .ToReadOnlyDictionary(element => element.Key, element => element.Value);

                    threadAppDomain = AppDomain.CurrentDomain;
                }

                Contract.Assume(threadAppDomain == AppDomain.CurrentDomain, "I assumed that a thread would always belogn to the same AppDomain");
                Contract.Ensures(appSettings != null);

                return appSettings;

            }
        }
    }
}
