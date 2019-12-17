using AppDomainToolkit;
using System;

namespace JBSnorro.GitTools
{
	// This assembly refers to stuff in the .NET framework that cannot be referenced from .NET Core
	public static class BackwardsCompatibility
	{
		public static AppDomainContext<AssemblyTargetLoader, PathBasedAssemblyResolver> CreateAppDomain(string appDomainBase, string assemblyPath)
		{
			return AppDomainContext.Create(
				new AppDomainSetup()
				{
					ApplicationBase = appDomainBase,
					ConfigurationFile = assemblyPath + ".config"
				});
		}
	}
}
