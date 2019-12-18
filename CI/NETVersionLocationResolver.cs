using Microsoft.Build.Utilities;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace JBSnorro.GitTools.CI
{
	public static class NETVersionLocationResolver
	{
		/// <summary>
		/// Gets the path to a .NET framework version on this machine with at least the specified version.
		/// </summary>
		public static string DirectoryToNETFrameworkOver(TargetDotNetFrameworkVersion minVersion)
		{
			for (TargetDotNetFrameworkVersion version = TargetDotNetFrameworkVersion.Version48; version >= minVersion; version--)
			{
				string result = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(version);
				if (result != null)
					return result;
			}
			throw new Exception($"No .NET Framework reference assemblies with version in the range [{minVersion}, 4.8] could be found");
		}

		/// <summary>
		/// Gets the path to a .NET framework version on this machine closest to the specified version (equal or higher).
		/// </summary>
		public static string DirectoryToNETFramework(TargetDotNetFrameworkVersion preferredVersion)
		{
			for (TargetDotNetFrameworkVersion version = preferredVersion; version <= TargetDotNetFrameworkVersion.Latest; version++)
			{
				string result = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(version);
				if (result != null)
					return result;
			}
			throw new Exception($"No .NET Framework reference assemblies with version in the range [{preferredVersion}, 4.8] could be found");
		}

		/// <summary>
		/// Gets a minimal selection of meta data references given a reference assemblies directory.
		/// </summary>
		internal static string[] Framework(string referenceAssembliesPath)
		{
			var assemblies = CrucialAssemblies
				.Select(name => Path.Combine(referenceAssembliesPath, name + ".dll"))
				.Where(File.Exists)
				.ToArray();
			return assemblies;
		}

		public static readonly ReadOnlyCollection<string> CrucialAssemblies = new ReadOnlyCollection<string>(new[] { "mscorlib", "System", "System.Core", "Facades/netstandard", "Facades/System.Runtime" });


		//public static string ResolveTargetFrameworkLocation(string targetFramework)
		//{
		//	switch (targetFramework.ToLowerInvariant())
		//	{
		//		case "netstandard1.0":
		//		case "netstandard1.1":
		//		case "netstandard1.2":
		//		case "netstandard1.3":
		//		case "netstandard1.4":
		//		case "netstandard1.5":
		//		case "netstandard1.6":
		//		case "netstandard2.1":
		//		case "netstandard2.2":

		//		case "netcoreapp1.0":
		//		case "netcoreapp1.1":
		//		case "netcoreapp2.0":
		//		case "netcoreapp2.1":
		//		case "netcoreapp2.2":
		//		case "netcoreapp3.0":
		//		case "netcoreapp3.1":
		//			break;
		//		case "v11":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version11);
		//		case "v20":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version20);
		//		case "v35":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version35);
		//		case "v40":
		//		case "v403":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version40);
		//		case "v45":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version45);
		//		case "v451":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version451);
		//		case "v452":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version452);
		//		case "v46":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version46);
		//		case "v461":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version461);
		//		case "v462":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version462);
		//		case "v47":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version47);
		//		case "v471":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version471);
		//		case "v472":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version472);
		//		case "v48":
		//			return DirectoryToNETFramework(TargetDotNetFrameworkVersion.Version48);


		//		default:
		//			break;
		//	}
		//}
	}
}
