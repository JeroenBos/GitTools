using JBSnorro.GitTools;
using System;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Threading.Tasks;

namespace CI.ProcessStarter
{
	class ProcessStarter
	{
		static ProcessStarter() => ResolveJBSnorroDll.Resolve();

		public const string PIPE_NAME = "CI_internal_pipe";
		public const string SUCCESS_CODON = "SUCCESS_CODON";
		public const string ERROR_CODON = "ERROR___CODON";
		public const string STOP_CODON = "STOPS___CODON";
		public const string STARTED_CODON = "STARTED_CODON";

		static int Main(string[] args)
		{
			if (args.Length != 1)
				throw new ArgumentException("Expected 1 argument");

			string assemblyPath = args[0];
			if (!File.Exists(assemblyPath))
				throw new FileNotFoundException(assemblyPath);

			try
			{
				using (var outPipe = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out))
				{
					outPipe.Connect();
					using (var writer = new StreamWriter(outPipe) { AutoFlush = true })
					{
						int totalTestCount = 0;
						int messagesCount = 0;
						try
						{
							foreach (MethodInfo method in TestClassExtensions.GetTestMethods(assemblyPath))
							{
								writer.WriteLine(STARTED_CODON + $"{method.DeclaringType.FullName}.{method.Name}");
								messagesCount++;

								string methodError = RunTest(method);

								if (!outPipe.IsConnected)
									break;

								totalTestCount++;
								if (methodError == null)
								{
									const string successMessage = "";
									writer.WriteLine(SUCCESS_CODON + successMessage);
									messagesCount++;
								}
								else
								{
									string message = string.Format($"{method.DeclaringType.FullName}.{method.Name}: {RemoveLineBreaks(methodError)}");
									writer.WriteLine(ERROR_CODON + message);
									messagesCount++;
								}
							}
						}
						catch (ReflectionTypeLoadException e)
						{
							foreach (var loadException in e.LoaderExceptions)
							{
								writer.WriteLine(ERROR_CODON + RemoveLineBreaks(loadException.Message));
								messagesCount++;
							}
						}
						catch (TargetInvocationException te)
						{
							Exception e = te.InnerException;
							if (e.InnerException != null)
							{
								writer.WriteLine(ERROR_CODON + "Inner message: " + RemoveLineBreaks($"{e.Message}\n{e.StackTrace}"));
								messagesCount++;
							}
							else
							{
								writer.WriteLine(ERROR_CODON + RemoveLineBreaks($"{e.Message}\n{e.StackTrace}"));
								messagesCount++;
							}
						}
						catch (Exception e)
						{
							writer.WriteLine(ERROR_CODON + RemoveLineBreaks($"An unexpected error occurred: {e.Message}"));
							messagesCount++;
						}
						finally
						{
							writer.WriteLine(STOP_CODON + totalTestCount.ToString());
							messagesCount++;
						}
						Console.Write(messagesCount);
					}
				}
			}
			catch (ObjectDisposedException e) { Console.WriteLine(e.Message); }
			catch (IOException e) { Console.WriteLine(e.Message); }
			return 0;
		}

		private static string RemoveLineBreaks(string s)
		{
			if (SUCCESS_CODON.Length != STOP_CODON.Length) throw new Exception();
			if (ERROR_CODON.Length != STOP_CODON.Length) throw new Exception();
			if (STARTED_CODON.Length != STOP_CODON.Length) throw new Exception();

			return s?.Replace('\n', '-').Replace('\r', '-');
		}

		/// <returns>null means the test succeeded; otherwise the error message. </returns>
		private static string RunTest(MethodInfo testMethod)
		{
			if (testMethod == null) throw new ArgumentNullException(nameof(testMethod));

			object testClassInstance = null;
			try
			{
				testClassInstance = testMethod.DeclaringType.GetConstructor(new Type[0]).Invoke(new object[0]);
				TestClassExtensions.RunInitializationMethod(testClassInstance);
				int? timeout = TestClassExtensions.GetTestMethodTimeout(testMethod);
				Action invocation = () => testMethod.Invoke(testClassInstance, new object[0]);
				if (timeout != null)
				{
					var task = Task.Run(invocation);
					var whenAnyTask = Task.WhenAny(task, Task.Delay(timeout.Value));
					whenAnyTask.Wait();
					if (whenAnyTask.Result == task)
					{
						return null;
					}
					else
					{
						return "{0} timed out";
					}
				}
				else
				{
					invocation();
				}
			}
			catch (TargetInvocationException e)
			{
				if (TestClassExtensions.IsExceptionExpected(testMethod, e.InnerException))
				{
					return null;
				}
				throw;
			}
			finally
			{
				TestClassExtensions.RunCleanupMethod(testClassInstance);
			}

			return null;
		}
	}
}
