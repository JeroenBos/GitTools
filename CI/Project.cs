using JBSnorro.Diagnostics;
using JBSnorro.Extensions;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JBSnorro.GitTools.CI
{
	/// <summary>
	/// Abstracts over the building, loading and runnings tests of a a .NET Core or .NET Framework project.
	/// </summary>
	interface IProject
	{
		string DirectoryPath { get; }
		string FullPath { get; }
		string? AssemblyPath { get; }
		string TargetFrameworkMoniker { get; }
		ICollection<IProjectItem> AllEvaluatedItems { get; }
		ICollection<IProjectItem> Items { get; }
		bool Build(ILogger logger);
		IProjectProperty GetProperty(string name);
	}
	class FrameworkProject : IProject
	{
		private readonly Project project;
		public FrameworkProject(Project project) => this.project = project;

		public string DirectoryPath => project.DirectoryPath;
		public ICollection<IProjectItem> AllEvaluatedItems => this.project.AllEvaluatedItems.Select(i => new FrameworkProjectItem(i)).ToList<IProjectItem>();
		public string FullPath => project.FullPath;
		public ICollection<IProjectItem> Items => project.Items.Select(i => new FrameworkProjectItem(i)).ToList<IProjectItem>();

		public string AssemblyPath
		{
			get
			{
				if (!this.AllEvaluatedItems.Where(item => item.ItemType == "IntermediateAssembly").First().EvaluatedInclude.StartsWith("obj"))
					throw new NotImplementedException("FrameworkProject.AssemblyPath");

				//couldn't find it in the projects' AlLEvaluatedItems, so I'm hacking this together:
				string relativePath = "bin" + project.AllEvaluatedItems.Where(item => item.ItemType == "IntermediateAssembly").First().EvaluatedInclude.Substring("obj".Length);
				string path = Path.Combine(project.DirectoryPath, relativePath);

				return path;
			}
		}

		public string TargetFrameworkMoniker => project.GetPropertyValue("TargetFramework");

		public bool Build(ILogger logger) => project.Build(logger);
		public IProjectProperty GetProperty(string name) => new FrameworkProjectProperty(project.GetProperty(name));

	}
	class CoreProject : IProject
	{
		public string FullPath { get; }
		public string AssemblyPath { get; }
		public string TargetFrameworkMoniker { get; }
		string IProject.DirectoryPath => Path.GetDirectoryName(FullPath);

		public static IProject Resolve(ProjectInSolution csprojPath, SolutionFile solution, string outputDir)
		{
			Contract.Requires(csprojPath.ProjectType != SolutionProjectType.SolutionFolder);
			var config = solution.GetDefaultConfigurationName();
			var platform = solution.GetDefaultPlatformName();

			var csprojContents = File.ReadAllText(csprojPath.AbsolutePath).ToList().Where(c => c != ' ' && c != '\n' && c != '\t' && c != '\r').Join("");
			var assemblyName = GetAssemblyName(csprojContents, Path.GetFileName(csprojPath.AbsolutePath));
			var extension = GetAssemblyExtension(csprojContents, Path.GetFileName(csprojPath.AbsolutePath));
			var assemblyFileName = assemblyName + "." + extension;
			var assemblyPath = Path.Combine(outputDir, assemblyFileName);
			Contract.Assert(File.Exists(assemblyPath), $"Something went wrong in inferring the assembly name '{assemblyFileName}'");
			string tfm = GetTargetFrameworkMoniker(csprojContents, Path.GetFileName(csprojPath.AbsolutePath));

			return new CoreProject(csprojPath.AbsolutePath, assemblyPath, tfm);
		}
		private static string GetAssemblyName(string csprojContents, string fileName)
		{
			const string tagName = "AssemblyName";
			var assemblyName = GetTagContents(csprojContents, tagName, fileName);
			if (assemblyName != null)
				return assemblyName;

			var defaultAssemblyName = Path.GetFileNameWithoutExtension(fileName);
			return defaultAssemblyName;
		}

		private static string GetAssemblyExtension(string csprojContents, string fileName)
		{
			var outputType = GetTagContents(csprojContents, "OutputType", fileName)
						  ?? GetTagContents(csprojContents, "WinOutputType", fileName);

			switch (outputType?.ToUpperInvariant())
			{
				case null:
				case "EXE" when csprojContents.Contains("Microsoft.NET.Sdk.Web"): // when Sdk.Web is used, the output type may be omitted, and is then .dll 
				case "LIBRARY":
					return "dll";
				case "EXE":
					return "exe";
				default:
					throw new NotImplementedException($"Unrecognized output type '{outputType}'");
			}
		}

		internal static string GetTargetFrameworkMoniker(string csprojContents, string fileName)
		{
			const string tagName = "TargetFramework";
			const string altTagName = "TargetFrameworkVersion";
			var moniker = (GetTagContents(csprojContents, tagName, fileName)
						?? GetTagContents(csprojContents, altTagName, fileName))?.ToLowerInvariant();

			Contract.Assert<InvalidProjectFileException>(moniker != null, $"Expected '<{tagName}>' or '<{altTagName}>' in '{fileName}'");
			Contract.Assert<InvalidProjectFileException>(moniker.StartsWith("netstandard") || moniker.StartsWith("netcoreapp") || moniker.StartsWith("v"), $"Unknown target framework  in '{fileName}'");

			return moniker;
		}

		internal static string GetTagContents(string contents, string tagName, string fileName)
		{
			int i = contents.IndexOf($"<{tagName}>");
			if (i == -1)
				return null;
			int j = contents.IndexOf($"</{tagName}>");
			if (j == -1)
				throw new FormatException($"XML tag '<{tagName}>' was not closed in '{fileName}'");
			if (contents.IndexOf($"<{tagName}>", i + 1) != -1)
				throw new NotImplementedException($"Multiple tags '<{tagName}>' found in '{fileName}'");

			int length = j - i - $"<{tagName}>".Length;
			return contents.Substring(i + $"<{tagName}>".Length, length);
		}
		private CoreProject(string projPath, string assemblyPath, string tfm)
		{
			Contract.Requires(!string.IsNullOrEmpty(projPath));
			Contract.Requires(!string.IsNullOrEmpty(assemblyPath));

			this.FullPath = projPath;
			this.AssemblyPath = assemblyPath;
			this.TargetFrameworkMoniker = tfm;
		}

		ICollection<IProjectItem> IProject.Items => throw new NotImplementedException("CoreProject.Items");
		ICollection<IProjectItem> IProject.AllEvaluatedItems => throw new NotImplementedException("CoreProject.AllEvaluatedItems");



		bool IProject.Build(ILogger logger) => throw new NotImplementedException("CoreProject.Build");
		IProjectProperty IProject.GetProperty(string name) => throw new NotImplementedException("CoreProject.GetProperty");
	}
	interface IProjectItem
	{
		string ItemType { get; }
		string EvaluatedInclude { get; }
		IProjectMetadata GetMetadata(string name);
	}
	class FrameworkProjectItem : IProjectItem
	{
		private readonly ProjectItem item;

		public FrameworkProjectItem(ProjectItem item)
		{
			this.item = item;
		}

		public string ItemType => item.ItemType;
		public string EvaluatedInclude => item.EvaluatedInclude;
		public IProjectMetadata GetMetadata(string name) => new FrameworkProjectMetadata(item.GetMetadata(name));
	}
	interface IProjectMetadata
	{
		string EvaluatedValue { get; }
	}
	class FrameworkProjectMetadata : IProjectMetadata
	{
		private ProjectMetadata projectMetadata;

		public FrameworkProjectMetadata(ProjectMetadata projectMetadata)
		{
			this.projectMetadata = projectMetadata;
		}

		public string EvaluatedValue => throw new NotImplementedException("FrameworkProjectMetadata.EvaluatedValue");
	}

	interface IProjectProperty
	{
		string EvaluatedValue { get; }
	}
	class FrameworkProjectProperty : IProjectProperty
	{
		private ProjectProperty projectProperty;

		public FrameworkProjectProperty(ProjectProperty projectProperty)
		{
			this.projectProperty = projectProperty;
		}

		public string EvaluatedValue => projectProperty.EvaluatedValue;
	}
}
