using JBSnorro.Diagnostics;
using System;
using System.IO;

namespace JBSnorro.GitTools
{
	/// <summary>
	/// Sets a flag that disables the CI and reenables it upon disposal.
	/// </summary>
	public sealed class TemporaryCIDisabler : IDisposable
	{
		public static readonly string FileName = "skipCI.txt";
		/// <summary>
		/// Gets whether the CI is disabled for the specified solution directory.
		/// </summary>
		public static bool IsDisabled(string solutionDirectory)
		{
			Contract.Requires(!string.IsNullOrEmpty(solutionDirectory));

			return File.Exists(GetPath(solutionDirectory));
		}
		/// <summary>
		/// Gets the path of the file whose existence signals whether the CI should be skipped (triggered in the post-commit hook).
		/// </summary>
		public static string GetPath(string directory)
		{
			return Path.Combine(directory, ".git", FileName);
		}

		public string Directory { get; }
		public TemporaryCIDisabler(string solutionDirectory)
		{
			Contract.Requires(!string.IsNullOrEmpty(solutionDirectory));

			this.Directory = solutionDirectory;
			this.DisableCI();
		}
		public void Dispose()
		{
			EnableCI();
		}

		private void DisableCI()
		{
			File.WriteAllText(GetPath(this.Directory), "");
		}
		private void EnableCI()
		{
			File.Delete(GetPath(this.Directory));
		}
	}
}
