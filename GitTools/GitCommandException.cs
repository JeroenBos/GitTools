﻿using System;

namespace JBSnorro.GitTools
{
	/// <summary>
	/// Represents that an error occurred when letting git execute a command.
	/// </summary>
	public class GitCommandException : Exception
	{
		public GitCommandException() : this("Git errored when executing the command")
		{
		}

		public GitCommandException(string error) : base(error)
		{
		}

		public GitCommandException(string error, Exception innerException) : base(error, innerException)
		{
		}
	}
}
