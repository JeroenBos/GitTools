using JBSnorro;
using JBSnorro.Diagnostics;
using JBSnorro.Extensions;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;
using Task = System.Threading.Tasks.Task;

namespace JBSnorro.GitTools.CI
{
	/// <summary>
	/// This program copies a solution, builds it and runs all its tests.
	/// </summary>
	public sealed class Program
	{
		/// <summary>
		/// Gets the number of processes spawned for testing. At most one process is used per test project.
		/// </summary>
		public static readonly int TEST_PROCESSES_COUNT = ConfigurationManagerExtensions.ParseAppSettingInt("TEST_PROCESSES_COUNT", ifMissing: 1);

		private static readonly string Configuration = "Debug";
		private static readonly string Platform = "x86";

		private const string TaskCanceledMessage = "Task canceled";

#if DEBUG
		/// <summary>
		/// Debugging flag to disable copying the solution.
		/// </summary>
		private static readonly bool skipCopySolution = false;
		/// <summary>
		/// Debugging flag to disable building.
		/// </summary>
		private static readonly bool skipBuild = skipCopySolution && true;
		/// <summary>
		/// Debugging flag to disable the check if a hash already exists in the .testresults.
		/// </summary>
		private static readonly bool disregardTestResultsFile = true;
#else
        private static readonly bool skipBuild = false;
        private static readonly bool skipCopySolution = false;
        private static readonly bool disregardTestResultsFile = false;
#endif
		/// <param name="args"> Must contain the path of the solution file, and the directory where to copy the solution to. </param>
		static void Main(string[] args)
		{
			if (args == null) { throw new ArgumentNullException(nameof(args)); }
			if (args.Length != 2) { throw new ArgumentException("Two arguments must be specified", nameof(args)); }

			string solutionFilePath = args[0];
			string destinationDirectory = args[1];

			foreach ((Status status, string message) in CopySolutionAndExecuteTests(solutionFilePath, destinationDirectory))
			{
				Console.WriteLine(message);
				Debug.Write(message);
			}
			Console.ReadLine();
		}

		public static IEnumerable<(Status, string)> CopySolutionAndExecuteTests(string solutionFilePath, string baseDestinationDirectory, string hash = null, CancellationToken cancellationToken = default)
		{
			TestResultsFile resultsFile = null;
			try
			{
				throw new NotImplementedException("CopySolutionAndExecuteTests");
				//return CopySolutionAndExecuteTests(solutionFilePath, baseDestinationDirectory, out resultsFile, out string commitMessage, out int projectCount, hash, cancellationToken);
			}
			finally
			{
				if (resultsFile != null)
					resultsFile.Dispose();
			}
		}
		/// <summary>
		/// Does work that is actually part of the preamble of <see cref="CopySolutionAndExecuteTests(string, string, bool, out int, string, CancellationToken)"/>
		/// </summary>
		public static Prework Prework(string solutionFilePath, string baseDestinationDirectory, string hash, bool ignoreParentFailed)
		{
			Contract.Requires(solutionFilePath != null, nameof(solutionFilePath));
			Contract.Requires(baseDestinationDirectory != null, nameof(baseDestinationDirectory));

			string error = ValidateSolutionFilePath(solutionFilePath);
			if (error != null)
			{
				return new Prework(Status.ArgumentError, error);
			}

			error = ValidateDestinationDirectory(baseDestinationDirectory);
			if (error != null)
			{
				return new Prework(Status.ArgumentError, error);
			}

			(string sourceDirectory, string destinationDirectory) = GetDirectories(solutionFilePath, baseDestinationDirectory);
			TestResultsFile resultsFile = TestResultsFile.TryReadFile(sourceDirectory, out error);
			bool disposeResultsFile = true;
			try
			{
				if (error != null)
				{
					return new Prework(Status.MiscellaneousError, error);
				}

				hash = hash ?? RetrieveCommitHash(Path.GetDirectoryName(solutionFilePath), out error);
				if (error != null)
				{
					return new Prework(Status.MiscellaneousError, error);
				}

				bool skipCommit = CheckCommitMessage(sourceDirectory, hash, resultsFile, out string commitMessage, out error);
				if (skipCommit)
				{
					return new Prework(Status.Skipped, error);
				}
				else if (error != null)
				{
					return new Prework(Status.MiscellaneousError, error);
				}

				if (!ignoreParentFailed)
				{
					bool parentCommitFailed = CheckParentCommit(sourceDirectory, hash, resultsFile, out error);
					if (parentCommitFailed)
					{
						return new Prework(Status.ParentFailed, error);
					}
					else if (error != null)
					{
						return new Prework(Status.MiscellaneousError, error);
					}
				}
				disposeResultsFile = false;
				return new Prework(resultsFile, commitMessage, destinationDirectory);
			}
			finally
			{
				if (disposeResultsFile)
					resultsFile?.Dispose();
			}
		}
		/// <summary>
		/// Copies the solution to a temporary destination directory, build the solution and executes the tests and returns any errors.
		/// </summary>
		/// <param name="solutionFilePath"> The path of the .sln file of the solution to run tests of. </param>
		/// <param name="hash "> The hash of the commit to execute the tests on. Specifiy null to indicate the current commit. </param>
		public static IEnumerable<(Status, string)> CopySolutionAndExecuteTests(string solutionFilePath,
																				string destinationDirectory,
																				out int projectCount,
																				string hash = null,
																				CancellationToken cancellationToken = default)
		{
			projectCount = -1;

			string destinationSolutionFile = TryCopySolution(solutionFilePath, destinationDirectory, cancellationToken, out string error);
			if (error != null)
			{
				return (Status.CopyingError, error).ToSingleton();
			}

			if (hash != null)
			{
				new GitCommandLine(destinationDirectory).CheckoutHard(hash);
			}

			IEnumerable<(Status, string)> loadAndBuildSolutionMessages = LoadAndBuildSolution(destinationSolutionFile, cancellationToken, out IReadOnlyList<IProject> projectsInBuildOrder);
			projectsInBuildOrder = projectsInBuildOrder.Where(x => !x.AssemblyPath.EndsWith("SemanticsEngine.Tests.dll")).ToList();
			IEnumerable<(Status, string)> testMessages = EnumerableExtensions.EvaluateLazily(() => RunTests(projectsInBuildOrder, cancellationToken));

			return loadAndBuildSolutionMessages.Concat(testMessages)
											   .TakeWhile(t => t.Item1.IsSuccessful(), t => !t.Item1.IsSuccessful()); // take all successes, and, in case of an error, all consecutive errors
		}
		/// <summary>
		/// Yields the elements of the second sequence only if all elements in the first sequence match the specified predicate.
		/// </summary>
		public static IEnumerable<T> ConcatIfAllPreviouses<T>(IEnumerable<T> firstSequence, Func<T, bool> predicate, Func<IEnumerable<T>> secondSequence)
		{
			Contract.Requires(firstSequence != null);
			Contract.Requires(predicate != null);
			Contract.Requires(secondSequence != null);

			bool allMatchPredicate = true;
			foreach (T element in firstSequence)
			{
				allMatchPredicate = allMatchPredicate && predicate(element);
				yield return element;
			}
			if (allMatchPredicate)
			{
				foreach (T element in secondSequence())
				{
					yield return element;
				}
			}
		}
		private static string ValidateSolutionFilePath(string solutionFilePath)
		{
			if (solutionFilePath == null) { throw new ArgumentNullException(nameof(solutionFilePath)); }
			if (!solutionFilePath.EndsWith(".sln")) { throw new ArgumentException("No solution file was provided", nameof(solutionFilePath)); }

			try
			{
				if (File.Exists(solutionFilePath))
				{
					return null;
				}
				else
				{
					return "The specified solution file could not be found";
				}
			}
			catch (Exception e)
			{
				return e.Message;
			}
		}
		private static string ValidateDestinationDirectory(string destinationDirectory)
		{
			try
			{
				if (Directory.Exists(destinationDirectory))
					return null;

				if (File.Exists(destinationDirectory))
					return "The specified path is not a directory";

				Directory.CreateDirectory(destinationDirectory); // this gets deleted again shortly. It's just part of the check
				return null;
			}
			catch (Exception e)
			{
				return e.Message;
			}
		}
		private static string RetrieveCommitHash(string solutionDirectory, out string error)
		{
			error = null;
			try
			{
				var branchName = File.ReadAllText(Path.Combine(solutionDirectory, @".git\HEAD"));
				Contract.Assume(branchName.StartsWith("ref: refs"));

				branchName = branchName.Substring("ref: ".Length, branchName.Length - "\n".Length - "ref: ".Length);
				var commitHash = File.ReadAllText(Path.Combine(solutionDirectory, @".git\", branchName)).Substring(0, 40);
				return commitHash;
			}
			catch (DirectoryNotFoundException e) when (e.Message.Contains(".git"))
			{
				// if there is no git, don't copy to a directory called "no-git"
				return "no-git";
			}
			catch (Exception e)
			{
				error = e.Message;
				return null;
			}
		}

		private static (string sourceDirectory, string destinationDirectory) GetDirectories(string solutionFilePath, string baseDestinationDirectory)
		{
			string sourceDirectory = Path.GetFullPath(Path.GetDirectoryName(solutionFilePath));
			string solutionName = sourceDirectory.Substring(sourceDirectory.LastIndexOf(Path.DirectorySeparatorChar) + 1);
			Contract.Assert(!solutionName.EndsWith(".."), "Path error");
			string destinationDirectory = Path.Combine(baseDestinationDirectory, solutionName);
			return (sourceDirectory, destinationDirectory);
		}
		private static bool CheckCommitMessage(string sourceDirectory, string hash, TestResultsFile resultsFile, out string commitMessage, out string error)
		{
			if (resultsFile.Hashes.ContainsKey(hash) && !disregardTestResultsFile)
			{
				commitMessage = null;
				error = "It is present in .testresults";
				return true;
			}

			try
			{
				commitMessage = new GitCommandLine(sourceDirectory).GetCommitMessage(hash);
				bool skip = GetAllIgnorePrefixes().Any(commitMessage.StartsWith);
				error = skip ? "The commit message starts with a prefix signaling to ignore" : null;
				return skip;
			}
			catch (GitCommandException e) when (e.Message == "fatal: bad object " + hash + "\n")
			{
				throw new ArgumentException("The specified hash does not exist");
			}
			catch (Exception e)
			{
				commitMessage = null;
				error = e.Message;
				return false;
			}
		}
		/// <summary>
		/// Determines the parent commit of the specified commit is not registed as having failed in the results file. 
		/// If it has failed, the solution corresponding to the specified hash should not be copied, built and tested, because its parent already failed.
		/// </summary>
		private static bool CheckParentCommit(string sourceDirectory, string hash, TestResultsFile resultsFile, out string error)
		{
			try
			{
				string parentHash = new GitCommandLine(sourceDirectory).GetParentCommitHash(hash);
				if (resultsFile.Hashes.TryGetValue(parentHash, out TestResult testResult) && testResult == TestResult.Failure)
				{
					error = "The parent commit already failed";
					return true;
				}
				else
				{
					error = null;
					return false;
				}
			}
			catch (GitCommandException e) when (e.Message == "fatal: bad object " + hash + "\n")
			{
				throw new ArgumentException("The specified hash does not exist");
			}
			catch (Exception e)
			{
				error = e.Message;
				return false;
			}
		}
		private static IEnumerable<string> GetAllIgnorePrefixes()
		{
			return ConfigurationManager.AppSettings.AllKeys
												   .Where(key => key.StartsWith("ignore_prefix"))
												   .Select(key => ConfigurationManager.AppSettings[key]);
		}
		private static string TryCopySolution(string solutionFilePath, string destinationDirectory, CancellationToken cancellationToken, out string error)
		{
			error = null;

			if (!skipCopySolution)
			{
				string sourceDirectory = Path.GetFullPath(Path.GetDirectoryName(solutionFilePath));
				try
				{
					DeleteDirectory(destinationDirectory);
				}
				catch (Exception e)
				{
					error = e.Message;
				}
				try
				{
					new GitCommandLine(sourceDirectory).Clone(destinationDirectory);
				}
				catch (Exception e)
				{
					error = e.Message;
				}
				try
				{
					CopyDirectoryIfExists(new DirectoryInfo(Path.Combine(sourceDirectory, "packages")), new DirectoryInfo(Path.Combine(destinationDirectory, "packages")), cancellationToken);
					error = null;
				}
				catch (Exception e)
				{
					error = e.Message;
				}
			}
			return Path.Combine(destinationDirectory, Path.GetFileName(solutionFilePath));
		}

		public static void CopyDirectoryIfExists(DirectoryInfo source, DirectoryInfo target, CancellationToken cancellationToken)
		{
			if (source.Exists)
			{
				CopyDirectory(source, target, cancellationToken);
			}
		}
		/// <remarks> https://stackoverflow.com/questions/58744/copy-the-entire-contents-of-a-directory-in-c-sharp </remarks>
		public static void CopyDirectory(DirectoryInfo source, DirectoryInfo target, CancellationToken cancellationToken)
		{
			foreach (DirectoryInfo dir in source.GetDirectories())
			{
				CopyDirectory(dir, target.CreateSubdirectory(dir.Name), cancellationToken);
			}
			foreach (FileInfo file in source.GetFiles())
			{
				if (cancellationToken.IsCancellationRequested)
					throw new TaskCanceledException();
				file.CopyTo(Path.Combine(target.FullName, file.Name), true);
			}
		}
		/// <remarks> https://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true </remarks>
		public static void DeleteDirectory(string path)
		{
			// this cryptic error message means some file in some directory is open somewhere
			const string SOME_FILE_IS_OPEN_ERROR_MESSAGE = "The directory is not empty.\r\n";

			if (Directory.Exists(path))
			{
				foreach (string nestedDirectory in Directory.GetDirectories(path))
				{
					DeleteDirectory(nestedDirectory);
				}

				try
				{
					Directory.Delete(path, recursive: true);
				}
				catch (IOException e) when (e.Message == SOME_FILE_IS_OPEN_ERROR_MESSAGE)
				{
					Thread.Sleep(10); // Some programs like Windows Explorer use this time to release the handle 
					try
					{
						Directory.Delete(path, recursive: true);
					}
					catch (IOException ex) when (ex.Message == SOME_FILE_IS_OPEN_ERROR_MESSAGE)
					{
						throw new Exception("An unknown file is opened somewhere and cannot be deleted");
					}
				}
				catch (UnauthorizedAccessException)
				{
					SetAttributesNormal(new DirectoryInfo(path));
					try
					{
						Directory.Delete(path, recursive: true);
					}
					catch (UnauthorizedAccessException)
					{
						const int attemptCount = 12;
						for (int i = 0; i < attemptCount; i++)
						{
							Thread.Sleep(1000);
							try
							{
								Directory.Delete(path, recursive: true);
								return;
							}
							catch (UnauthorizedAccessException e)
							{
								if (i + 1 == attemptCount)
								{
									if (e.Message.Count(c => c == '\'') == 2)
									{
										int indexOfOpeningQuote = e.Message.IndexOf('\'');
										int indexOfClosingQuote = e.Message.IndexOf('\'', indexOfOpeningQuote + 1);
										string file = e.Message.Substring(indexOfOpeningQuote + 1, indexOfClosingQuote - (indexOfOpeningQuote + 1));

										throw new UnauthorizedAccessException($"Access to the path '{Path.Combine(path, file)}' is denied.", e);
									}
									throw;
								}
							}
						}
					}
				}
			}

			void SetAttributesNormal(DirectoryInfo directory)
			{
				foreach (var nestedDirectory in directory.GetDirectories())
				{
					SetAttributesNormal(nestedDirectory);
				}
				foreach (var file in directory.GetFiles())
				{
					file.Attributes = FileAttributes.Normal;
				}
			}
		}

		private static IReadOnlyList<string> GetProjectPaths(SolutionFile file, out string error)
		{
			error = null;
			try
			{
				return file.ProjectsInOrder
						   .Where(project => File.Exists(project.AbsolutePath))
						   .Select(project => project.AbsolutePath)
						   .ToReadOnlyList();
			}
			catch (Exception e)
			{
				error = e.Message;
				return null;
			}
		}
		private static IEnumerable<(Status Status, string Message)> LoadAndBuildSolution(string destinationSolutionFilePath, CancellationToken cancellationToken, out IReadOnlyList<IProject> projectsInBuildOrder)
		{
			var solutionFile = SolutionFile.Parse(destinationSolutionFilePath);
			var projectFilePaths = GetProjectPaths(solutionFile, out var error);
			if (error != null)
			{
				projectsInBuildOrder = null;
				return (Status.ProjectLoadingError, error).ToSingleton();
			}

			var tempDir = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), Guid.NewGuid().ToString());


			//// $(MSBuildRuntimeType)' == 'Core' exists, see https://github.com/aspnet/BuildTools/blob/9ea72bcf88063cee9afbe53835681702e2efd720/src/Internal.AspNetCore.BuildTools.Tasks/build/Internal.AspNetCore.BuildTools.Tasks.props#L2-L6

			List<(Status, string)> messages = new List<(Status, string)>() { (Status.Info, "dotnet.exe is working") };
			bool tryLegacy;
			try
			{
				messages = BuildViaDotnetTool(destinationSolutionFilePath, tempDir, cancellationToken).ToList();
				tryLegacy = false;
			}
			catch (AppSettingNotFoundException)
			{
				throw;
			}
			catch
			{
				tryLegacy = true;
			}

			if (tryLegacy)
			{
				Logger.Log("Failed building via dotnet.exe. Maybe it's a .NET Framework solution? Trying legacy compilation.");
				return legacyBuild(out projectsInBuildOrder);
			}

			projectsInBuildOrder = solutionFile.ProjectsInOrder
											   .Where(path => path.ProjectType != SolutionProjectType.SolutionFolder)
											   .Select(TryResolve)
											   .ToList();
			return messages;

			IProject TryResolve(ProjectInSolution project)
			{
				try
				{
					return CoreProject.Resolve(project, solutionFile, tempDir);
				}
				catch (Exception e)
				{
					messages.Add((Status.ProjectLoadingError, e.Message));
					return null;
				}
			}

			IEnumerable<(Status, string)> legacyBuild(out IReadOnlyList<IProject> projectsInBuildOrder)
			{
				// try legacy: 
				IEnumerable<(Status, string)> loadSolutionMessages = LoadSolution(projectFilePaths, cancellationToken, out projectsInBuildOrder);
				IEnumerable<(Status, string)> buildSolutionMessages = BuildSolution(projectsInBuildOrder, destinationSolutionFilePath, cancellationToken);
				return loadSolutionMessages.Concat(buildSolutionMessages);
			}
		}
		private static IEnumerable<(Status Status, string Message)> LoadSolution(IReadOnlyList<string> projectFilePaths, CancellationToken cancellationToken, out IReadOnlyList<IProject> projectsInBuildOrder)
		{
			var inBuildOrder = new List<IProject>();
			projectsInBuildOrder = new ReadOnlyCollection<IProject>(inBuildOrder);

			var projects = new ProjectCollection(new Dictionary<string, string> { ["configuration"] = Configuration, ["Platform"] = Platform }) { IsBuildEnabled = true };
			try
			{
				var result = messages().ToList();
				inBuildOrder.AddRange(ToBuildOrder(projects.LoadedProjects.Select(p => new FrameworkProject(p)).ToList()));
				if (inBuildOrder.Count == 0)
					Logger.Log("The specified solution has no projects. Maybe something went wrong?");
				return result;
			}
			catch
			{
				projectsInBuildOrder = null;
				throw;
			}
			finally
			{
				projects.Dispose();
			}
			IEnumerable<(Status, string)> messages()
			{
				foreach (var projectPath in projectFilePaths)
				{
					string errorMessage = null;
					try
					{
						projects.LoadProject(projectPath);
					}
					catch (InvalidProjectFileException e)
					{
						// maybe it's a .NET Core project
					}
					catch (Exception e)
					{
						errorMessage = e.Message;
					}

					if (cancellationToken.IsCancellationRequested)
					{
						yield return (Status.Canceled, TaskCanceledMessage);
						yield break;
					}
					else if (errorMessage == null)
					{
						yield return (Status.ProjectLoadSuccess, $"Assembly {Path.GetFileName(projectPath)} loaded successfully");
					}
					else
					{
						yield return (Status.ProjectLoadingError, errorMessage);
						yield break;
					}
				}
			}
		}
		/// <summary>
		/// Tries to build the solution and returns the projects if successful; otherwise an error message.
		/// </summary>
		private static IEnumerable<(Status Status, string Message)> BuildSolution(IReadOnlyList<IProject> projectsInBuildOrder, string destinationSolutionFile, CancellationToken cancellationToken)
		{
			if (skipBuild)
				yield break;

			NuGetRestore(destinationSolutionFile);

			foreach (var project in projectsInBuildOrder)
			{
				string errorMessage = null;
				bool success = false;
				try
				{
					success = project.Build(new ConsoleLogger());
				}
				catch (Exception e)
				{
					errorMessage = e.Message;
				}

				if (cancellationToken.IsCancellationRequested)
				{
					yield return (Status.Canceled, TaskCanceledMessage);
					yield break;
				}
				else if (success)
				{
					yield return (Status.BuildSuccess, $"Assembly {Path.GetFileName(project.FullPath)} built successfully");
				}
				else
				{
					yield return (Status.BuildError, errorMessage ?? "Compilation failed");
					yield break;
				}
			}
		}

		private static List<IProject> ToBuildOrder(IEnumerable<IProject> projects)
		{
			var remaining = projects.ToList();
			var result = new List<IProject>();
			while (remaining.Count != 0)
			{
				var next = findTop(remaining);
				result.Add(next);
				remaining.Remove(next);
			}
			return result;


			// returns the guid of a project in the specified list that has no dependencies on the specified projects
			IProject findTop(List<IProject> unbuiltProjects)
			{
				return unbuiltProjects.First(project =>
				{
					var dependencies = GetProjectReferenceGuids(project);
					return dependencies.All(dependency => !unbuiltProjects.Select(GetGuid).Contains(dependency));
				});
			}

			// Gets the guids of the project references upon which the specified project depends
			IEnumerable<string> GetProjectReferenceGuids(IProject p)
			{
				return p.Items
						.Where(i => i.ItemType == "ProjectReference")
						.Select(item => item.GetMetadata("Project").EvaluatedValue)
						.Select(s => s.ToUpper())
						.EnsureSingleEnumerationDEBUG();
			}

			// Gets the guid of the specified project
			string GetGuid(IProject project)
			{
				return project.GetProperty("ProjectGuid").EvaluatedValue.ToUpper();
			}
		}

		private static IEnumerable<(Status, string)> RunTests(IReadOnlyList<IProject> projectsInBuildOrder, CancellationToken cancellationToken)
		{
			try
			{
				foreach (var project in projectsInBuildOrder)
				{
					string distDirectory = Path.GetDirectoryName(GetAssemblyPath(project));
					CopyDependenciesToNewAppDomainBaseDirectory(distDirectory, project.TargetFrameworkMoniker);
				}

				int processedProjectsCount = 0;
				var remainingProjects = new ConcurrentQueue<IProject>(projectsInBuildOrder);

				for (int i = 0; i < TEST_PROCESSES_COUNT; i++)
				{
					Task.Run(processRemainingProjects);
					void processRemainingProjects()
					{
						while (remainingProjects.TryDequeue(out IProject project) && !cancellationToken.IsCancellationRequested)
						{
							Interlocked.Increment(ref processedProjectsCount);
							StartProcessStarter(GetAssemblyPath(project), project.TargetFrameworkMoniker);
						}
					}
				}

				return NamedPipesServerStream.Read(Parse, PIPE_NAME, s => s.StartsWith(STOP_CODON), projectsInBuildOrder.Count, cancellationToken);
			}
			catch (TaskCanceledException)
			{
				return (Status.Canceled, TaskCanceledMessage).ToSingleton();
			}
			catch (Exception e)
			{
				return (Status.MiscellaneousError, e.Message).ToSingleton();
			}
		}

		public const string PIPE_NAME = "CI_internal_pipe";
		public const string SUCCESS_CODON = "SUCCESS_CODON";
		public const string ERROR_CODON = "ERROR___CODON";
		public const string STOP_CODON = "STOPS___CODON";
		public const string STARTED_CODON = "STARTED_CODON";

		public static IEnumerable<(Status, string)> Parse(IEnumerable<string> lines)
		{
#if DEBUG
			var cache = new List<string>();
			lines = lines.Select(s => { cache.Add(s); return s; });
#endif
			bool hasErrors = false;
			List<int> totalSuccessCounts = new List<int>();
			foreach (string line in lines)
			{
				string codon = line.Substring(0, ERROR_CODON.Length);
				string message = line.Substring(ERROR_CODON.Length).Trim('\r', '\n', '\t', ' ');

				switch (codon)
				{
					case STARTED_CODON:
						yield return (Status.TestStarted, message);
						break;
					case SUCCESS_CODON:
						yield return (Status.TestSuccess, message);
						break;
					case ERROR_CODON:
						hasErrors = true;
						yield return (Status.TestError, message);
						break;
					case STOP_CODON:
						int successCount = int.Parse(message);
						if (successCount != 0)
							totalSuccessCounts.Add(successCount);
						break;
					default:
						throw new ContractException("Wrong codon received");
				}
			}
			if (!hasErrors)
			{
				yield return (Status.Success, $"{totalSuccessCounts.Sum()} tests run successfully");
			}
		}

		/// <summary>
		/// Performs a nuget restore operation synchronously.
		/// </summary>
		private static void NuGetRestore(string destinationSolutionFile)
		{
			string nugetExe = ConfigurationManager.AppSettings["nuget.exe"] ?? throw new AppSettingNotFoundException("nuget.exe");
			Contract.Requires<FileNotFoundException>(File.Exists(nugetExe), "File not found", nugetExe);

			Logger.Log("Invoking nuget restore");
			ProcessExtensions.StartIndependentlyInvisiblyAsync(nugetExe, destinationSolutionFile).Wait();
			Logger.Log("Invoked nuget restore");
		}

		/// <summary>
		/// Performs a nuget restore operation.
		/// </summary>
		private static void StartProcessStarter(string testAssemblyPath, string tfm)
		{
			Console.WriteLine($"Starting process starter with argument '{testAssemblyPath}'");

			string processStarterPath = getProcessStarterFiles(tfm).First();

			TryCopyDepsJson();
			var process = ProcessExtensions.WaitForExitAndReadOutputAsync(processStarterPath, testAssemblyPath);
			process.Wait();
			if (process.Result.ExitCode != 0)
				throw new Exception(process.Result.ErrorOutput);
			// just in case I want to know this again:
			int.TryParse(process.Result.StandardOutput, out int _writtenMessagesCount);

			void TryCopyDepsJson()
			{
				string processsStarterPathWithoutExtension = Path.Combine(Path.GetDirectoryName(testAssemblyPath), Path.GetFileNameWithoutExtension(processStarterPath));
				string testAssemblyPathWithoutExtension = PathWithoutExtension(testAssemblyPath);

				var extensions = new[] { ".deps.json", ".runtimeconfig.json" }.Take(0);
				foreach (var extension in extensions)
				{
					string source = testAssemblyPathWithoutExtension + extension;
					if (!File.Exists(source))
					{
						throw new FileNotFoundException($"Cannot find a '{extension}' file for project '{Path.GetFileNameWithoutExtension(testAssemblyPath)}'", source);
					}

					string destination = processsStarterPathWithoutExtension + extension;
					const bool overwrite = true; // otherwise File.Copy throws 'already exists' exception
					File.Copy(source, destination, overwrite);
				}
			}
			static string PathWithoutExtension(string path)
			{
				return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
			}
		}
		private static string GetAppContextTargetFrameworkMoniker()
		{
			Contract.Requires(AppContext.TargetFrameworkName != null);

			string name = AppContext.TargetFrameworkName.ToLowerInvariant().Replace(" ", "");

			if (name.Length > 3 && name[0] == '.' && name[name.Length - 2] == '.')
			{
				int endOfType = name.IndexOf(",version=v");
				if (endOfType != -1)
				{
					string type = name.Substring(".".Length, endOfType - ".".Length);
					string version = name.Substring(endOfType + ",version=v".Length);
					string result = type + version;
					return result;
				}
			}
			return "?";
		}
		private static IEnumerable<string> getProcessStarterFiles(string tfm)
		{
			const string fallbackDirKey = "processstarterDir";
			const string fallbackFilesKey = "processstarterFiles";
			string dirKey = $"{fallbackDirKey}_{tfm}";
			string filesKey = $"{fallbackFilesKey}_{tfm}";

			string processStarterDir = ConfigurationManager.AppSettings[dirKey] ?? ConfigurationManager.AppSettings[fallbackDirKey] ?? throw new AppSettingNotFoundException($"{dirKey}' or '{fallbackDirKey}");
			var processStarterFiles = (ConfigurationManager.AppSettings[filesKey] ?? ConfigurationManager.AppSettings[fallbackFilesKey] ?? throw new AppSettingNotFoundException($"{filesKey}' or '{fallbackFilesKey}"))
										?.Split(',');

			return processStarterFiles.Where(_ => !string.IsNullOrWhiteSpace(_))
									  .Select(fileName => Path.GetFullPath(Path.Combine(processStarterDir, fileName)));
		}

		private static IEnumerable<(Status, string)> BuildViaDotnetTool(string destinationSolutionFile, string outputDirectory, CancellationToken cancellationToken)
		{
			string dotnetExe = ConfigurationManager.AppSettings["dotnet.exe"] ?? throw new AppSettingNotFoundException("dotnet.exe");

			string executable = dotnetExe;
			const string runtimeId = "win-x86";
			var arguments = new string[] { "publish", destinationSolutionFile, $"--runtime \"{runtimeId}\" --output \"{outputDirectory}\"" };

			var startInfo = new ProcessStartInfo(executable, string.Join(" ", arguments));
			startInfo.UseShellExecute = false;
			startInfo.RedirectStandardOutput = true;
			startInfo.RedirectStandardError = true;
			var process = Process.Start(startInfo);

			string output = process.StandardOutput.ReadToEnd();
			string errorOutput = process.StandardError.ReadToEnd();
			process.WaitForExit();

			//var result = task.Result;
			var result = (ExitCode: process.ExitCode, StandardOutput: output, ErrorOutput: errorOutput);
			if (result.ExitCode == 0)
			{
				yield return (Status.ProjectLoadSuccess, $"Solution {Path.GetFileName(destinationSolutionFile)} loaded successfully");
				if (!string.IsNullOrEmpty(result.StandardOutput))
					yield return (Status.Info, result.StandardOutput);
			}
			else
			{
				if (!string.IsNullOrEmpty(result.ErrorOutput))
					yield return (Status.ProjectLoadingError, result.ErrorOutput);
				if (!string.IsNullOrEmpty(result.StandardOutput))
					yield return (Status.Info, result.StandardOutput);
				yield return (Status.ProjectLoadingError, $"Solution {Path.GetFileName(destinationSolutionFile)} failed loading with exit code " + result.ExitCode);
			}
		}

		/// <summary>
		/// Copies the dependencies of this project to the bin directory of the new app domain.
		/// </summary>
		/// <param name="binDirectory"> The bin directory where a project is to be built. </param>
		private static void CopyDependenciesToNewAppDomainBaseDirectory(string newAppDomainBaseDirectory, string tfm)
		{
			List<string> packagesDirectories = new string[] { newAppDomainBaseDirectory, AppDomain.CurrentDomain.BaseDirectory }.Select(getPackagesDirectory).Where(p => p != null).ToList();
			IEnumerable<string> dependencies = new string[]
			{
				"NUnit",
				"GitTools",
				"CI",
				"JBSnorro",
			};

			var dependencyPaths = dependencies.Select(find).ToList();
			if (packagesDirectories.Count == 0)
			{
				Contract.AssertForAll(dependencies, dependencyPath => !dependencyPath.Contains("{0}"), "Cannot find package for '{1}'");
			}

			var processStarterFiles = getProcessStarterFiles(tfm);
			var dependencyFullPaths = dependencyPaths.Select(selectPackageDirectory)
													 .Concat(processStarterFiles)
													 .ToList();
			Contract.AssertForAll(dependencyFullPaths, File.Exists, "The specified file '{1}' does not exist");

			copy(dependencyFullPaths, newAppDomainBaseDirectory);


			string selectPackageDirectory(string path)
			{
				if (!path.Contains("{0}")) //doesn't matter
				{
					string result = Path.GetFullPath(string.Format(path, "", AppDomain.CurrentDomain.BaseDirectory));
					Contract.Ensures(File.Exists(result), $"The specified file '{result}' does not exist");
					return result;
				}

				Contract.Assert(packagesDirectories.Count != 0, "No package directory was found but was required");

				foreach (string packageDirectory in packagesDirectories)
				{
					string result = Path.GetFullPath(string.Format(path, packageDirectory, AppDomain.CurrentDomain.BaseDirectory));
					if (File.Exists(result))
						return result;
				}

				throw new ContractException($"No package directory contains contains the package '{Path.GetFileName(path)}'");
			}




			// maps the dependency names to the paths where they can be found
			// the return type should be formatted with {0} the package directory, and {1} by the currently running app domain directory
			string find(string dependencyName)
			{
				switch (dependencyName)
				{
					case "NUnit":
						return @"{0}\NUnit.3.9.0\lib\net45\nunit.framework.dll";
					case "JBSnorro":
						return @"{0}\JBSnorro.CI.dll";
					case "GitTools":
						return @"{1}\JBSnorro.GitTools.dll";
					case "CI":
						return @"{1}\JBSnorro.GitTools.CI.exe";
					default:
						throw new ContractException($"Don't know where to find the dependency '{dependencyName}'");
				}
			}

			// returns null if the package directory was not found
			string getPackagesDirectory(string dir)
			{
				var debug = Directory.GetDirectories(dir);
				if (Directory.GetDirectories(dir).Any(nestedDir => nestedDir.EndsWith(Path.DirectorySeparatorChar + "packages")))
					return Path.Combine(dir, "packages");

				var parent = Directory.GetParent(dir);

				return parent == null ? null : getPackagesDirectory(parent.FullName);
			}

			void copy(IEnumerable<string> fullPaths, string destinationDirectory)
			{
				foreach (string fullPath in fullPaths)
				{
					if (File.Exists(fullPath))
					{
						string destination = Path.Combine(destinationDirectory, Path.GetFileName(fullPath));
						if (!File.Exists(destination))
							File.Copy(fullPath, destination);
					}
					else
						Logger.Log($"The depend file '{fullPath}' could not be found");
				}
			}
		}


		private static string GetAssemblyPath(IProject project)
		{
			string path = project.AssemblyPath;

			if (!File.Exists(path))
				if (skipCopySolution || skipBuild)
					throw new Exception($"Coulnd't find assembly {path}. skipCopySolution or skipBuild was true. Are you sure that is correct?");
				else
					throw new NotImplementedException("Couldn't find assembly " + path);
			return path;
		}
	}
}
