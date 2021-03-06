﻿using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;

namespace JBSnorro.GitTools.CI
{
	public interface ICopyBuildTestSolutions
	{
		string Hash { get; }
		Prework Prework(bool ignoreParentFailed);
		/// <remarks><see cref="Program.CopySolutionAndExecuteTests(string, string, out int, string, CancellationToken)"/></remarks>
		IEnumerable<(Status Status, string Message)> CopySolutionAndExecuteTests(CancellationToken cancellationToken, out int projectCount);
	}
	public sealed class Prework : IDisposable
	{
		public Status Status { get; }
		public string Message { get; }
		public string CommitMessage { get; }
		public TestResultsFile TestResultsFile { get; }
		public string DestinationDirectory { get; }

		public bool HasError => Message != null;

		internal Prework(Status status, string message)
		{
			Contract.Requires(message != null || status == Status.Success);
			Contract.RequiresEnumIsDefined(status);

			this.Status = status;
			this.Message = message;
		}
		internal Prework(TestResultsFile resultsFile, string commitMessage, string destinationDirectory)
		{
			this.Status = Status.Success;
			this.TestResultsFile = resultsFile;
			this.CommitMessage = commitMessage;
			this.DestinationDirectory = destinationDirectory;
		}

		public void Dispose()
		{
			this.TestResultsFile?.Dispose();
		}
	}
}
