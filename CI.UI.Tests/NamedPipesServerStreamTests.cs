﻿using JBSnorro.GitTools.CI;
using NUnit.Framework;
using System.Threading;

namespace CI.UI.Tests
{
	[TestFixture]
	public class NamedPipesServerStreamTests
	{
		[Test, Timeout(500)]
		public void DisposalRemovesPipes()
		{
			const string QUIT = "quit";
			NamedPipesServerStream pipe;
			using (pipe = new NamedPipesServerStream(nameof(DisposalRemovesPipes) + "_pipe", s => s == QUIT, 1))
			{
			}
			while (pipe.Pipes.Count != 0) { }
		}
		[Test, Timeout(500)]
		public void CancellingRemovesPipes()
		{
			const string QUIT = "quit";
			NamedPipesServerStream pipe;
			using (var cancellationSource = new CancellationTokenSource())
			using (pipe = new NamedPipesServerStream(nameof(DisposalRemovesPipes) + "_pipe", s => s == QUIT, 1, cancellationSource.Token))
			{
				Thread.Sleep(100);
				cancellationSource.Cancel();
				while (pipe.Pipes.Count != 0) { }

			}
		}
	}
}
