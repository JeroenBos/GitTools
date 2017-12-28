using Microsoft.Build.Construction;
using System.Threading;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Logging;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using JBSnorro;
using JBSnorro.Extensions;
using System.Diagnostics;
using AppDomainToolkit;
using AppDomainContext = AppDomainToolkit.AppDomainContext<AppDomainToolkit.AssemblyTargetLoader, AppDomainToolkit.PathBasedAssemblyResolver>;
using System.Configuration;
using JBSnorro.Diagnostics;
using System.IO.Pipes;
using Task = System.Threading.Tasks.Task;
using System.Collections.Concurrent;

namespace JBSnorro.GitTools.CI
{
    /// <summary>
    /// This program copies a solution, builds it and runs all its tests.
    /// </summary>
    public sealed class Program
    {
        /// <summary>
        /// Gets the number of threads used for testing. At most one thread is used per test project.
        /// </summary>
        const int TEST_THREAD_COUNT = 2;
        /// <summary>
        /// The maximum number of milliseconds a test may take before it is canceled and considered failed.
        /// </summary>
        public const int MaxTestDuration = 5000;

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

        public static IEnumerable<(Status, string)> CopySolutionAndExecuteTests(string solutionFilePath, string baseDestinationDirectory, string hash = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            TestResultsFile resultsFile = null;
            try
            {
                return CopySolutionAndExecuteTests(solutionFilePath, baseDestinationDirectory, out resultsFile, out string commitMessage, out int projectCount, hash, cancellationToken);
            }
            finally
            {
                if (resultsFile != null)
                    resultsFile.Dispose();
            }
        }
        /// <summary>
        /// Copies the solution to a temporary destination directory, build the solution and executes the tests and returns any errors.
        /// </summary>
        /// <param name="solutionFilePath"> The path of the .sln file of the solution to run tests of. </param>
        /// <param name="baseDestinationDirectory"> The temporary directory to copy the solution to. </param>
        /// <param name="hash "> The hash of the commit to execute the tests on. Specifiy null to indicate the current commit. </param>
        public static IEnumerable<(Status, string)> CopySolutionAndExecuteTests(string solutionFilePath, string baseDestinationDirectory, out TestResultsFile resultsFile, out string commitMessage, out int projectCount, string hash = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            projectCount = -1;
            resultsFile = null;
            commitMessage = "";
            var error = ValidateSolutionFilePath(solutionFilePath);
            if (error != null)
            {
                return (Status.ArgumentError, error).ToSingleton();
            }

            error = ValidateDestinationDirectory(baseDestinationDirectory);
            if (error != null)
            {
                return (Status.ArgumentError, error).ToSingleton();
            }

            bool mustDoCheckout = false;
            (string sourceDirectory, string destinationDirectory) = GetDirectories(solutionFilePath, baseDestinationDirectory);
            resultsFile = TestResultsFile.TryReadFile(sourceDirectory, out error);

            if (hash == null)
            {
                string currentCommitHash = RetrieveCommitHash(Path.GetDirectoryName(solutionFilePath), out error);
                if (hash == null)
                {
                    hash = currentCommitHash;
                }
                else if (currentCommitHash != hash)
                {
                    mustDoCheckout = true;
                    hash = currentCommitHash;
                }
                if (error != null)
                {
                    return (Status.MiscellaneousError, error).ToSingleton();
                }
            }

            if (error != null)
            {
                return (Status.MiscellaneousError, error).ToSingleton();
            }

            bool skipCommit = CheckCommitMessage(sourceDirectory, hash, resultsFile, out commitMessage, out error);
            if (skipCommit)
            {
                return (Status.Skipped, error).ToSingleton();
            }
            else if (error != null)
            {
                return (Status.MiscellaneousError, error).ToSingleton();
            }

            string destinationSolutionFile = TryCopySolution(solutionFilePath, destinationDirectory, cancellationToken, out error);
            if (error != null)
            {
                return (Status.MiscellaneousError, error).ToSingleton();
            }

            if (mustDoCheckout)
                GitCommandLine.Checkout(destinationDirectory, hash);

            var projectsInBuildOrder = LoadSolution(destinationSolutionFile, cancellationToken, out error);
            if (error != null)
            {
                return (Status.ProjectLoadingError, error).ToSingleton();
            }

            projectCount = projectsInBuildOrder.Count;
            return ConcatIfAllPreviouses(BuildSolution(projectsInBuildOrder, cancellationToken), buildMessage => buildMessage.Item1 == Status.BuildSuccess, () => RunTests(projectsInBuildOrder, cancellationToken));
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
                if (!branchName.StartsWith("ref: refs")) throw new Exception("Assertion failed");

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
            string sourceDirectory = Path.GetDirectoryName(solutionFilePath);
            string solutionName = sourceDirectory.Substring(Path.GetDirectoryName(solutionFilePath).LastIndexOf(Path.DirectorySeparatorChar) + 1);
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
                commitMessage = GitCommandLine.GetCommitMessage(sourceDirectory, hash);
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
        private static IEnumerable<string> GetAllIgnorePrefixes()
        {
            return ConfigurationManager.AppSettings.AllKeys
                                                   .Where(key => key.StartsWith("ignore_prefix"))
                                                   .Select(key => ConfigurationManager.AppSettings[key]);
        }
        private static string TryCopySolution(string solutionFilePath, string destinationDirectory, CancellationToken cancellationToken, out string error)
        {
            if (!skipCopySolution)
            {
                try
                {
                    SetAttributesNormal(destinationDirectory);
                    Directory.Delete(destinationDirectory, recursive: true);
                }
                catch { }
            }
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }
                if (!skipCopySolution)
                {
                    //TODO: maybe generalize/parameterize everything that should be excluded. Below .vs is hardcoded 
                    CopyDirectory(new DirectoryInfo(Path.GetDirectoryName(solutionFilePath)), new DirectoryInfo(destinationDirectory), cancellationToken);
                    SetAttributesNormal(destinationDirectory);
                }
                error = null;
                return Path.Combine(destinationDirectory, Path.GetFileName(solutionFilePath));
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }
        /// <remarks> https://stackoverflow.com/questions/58744/copy-the-entire-contents-of-a-directory-in-c-sharp </remarks>
        private static void CopyDirectory(DirectoryInfo source, DirectoryInfo target, CancellationToken cancellationToken)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException();

                if (!(dir.Name.StartsWith(".vs") || dir.Name == "bin" || dir.Name == "obj"))
                    CopyDirectory(dir, target.CreateSubdirectory(dir.Name), cancellationToken);
            }
            foreach (FileInfo file in source.GetFiles())
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException();

                if (file.Name != ".testresults") // can't copy because this program currently has a filestream opened on it
                    file.CopyTo(Path.Combine(target.FullName, file.Name), true);
            }
        }
        private static void SetAttributesNormal(string dirPath)
        {
            SetAttributesNormal(new DirectoryInfo(dirPath));
        }
        private static void SetAttributesNormal(DirectoryInfo dir)
        {
            foreach (var subDir in dir.GetDirectories())
                SetAttributesNormal(subDir);
            foreach (var file in dir.GetFiles())
            {
                file.Attributes = FileAttributes.Normal;
            }
        }

        private static IReadOnlyList<Project> LoadSolution(string destinationSolutionFile, CancellationToken cancellationToken, out string error)
        {
            try
            {
                SetAttributesNormal(Path.GetDirectoryName(destinationSolutionFile));
            }
            catch { }
            try
            {
                using (var projects = new ProjectCollection(new Dictionary<string, string> { ["configuration"] = "Debug", ["Platform"] = "x86" }) { IsBuildEnabled = true })
                {
                    foreach (var projectPath in GetProjectFilesIn(destinationSolutionFile))
                    {
                        projects.LoadProject(projectPath.AbsolutePath);

                        if (cancellationToken.IsCancellationRequested)
                            throw new TaskCanceledException();
                    }
                    IReadOnlyList<Project> projectsInBuildOrder = GetInBuildOrder(projects.LoadedProjects);
                    error = null;
                    return projectsInBuildOrder;
                }
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }
        /// <summary>
        /// Tries to build the solution and returns the projects if successful; otherwise an error message.
        /// </summary>
        private static IEnumerable<(Status Status, string Message)> BuildSolution(IReadOnlyList<Project> projectsInBuildOrder, CancellationToken cancellationToken)
        {
            if (skipBuild)
                yield break;

            foreach (var project in projectsInBuildOrder)
            {
                string errorMessage = null;
                bool success = false;
                try
                {
                    success = project.Build(new ConsoleLogger());
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch (Exception e)
                { //TODO: cancellationtoken
                    errorMessage = e.Message;
                }

                if (success)
                {
                    yield return (Status.BuildSuccess, $"Assembly {Path.GetFileName(project.FullPath)} built successfully");
                }
                else
                {
                    yield return (Status.BuildError, errorMessage ?? "Unknown error");
                    yield break;
                }
            }
            if (cancellationToken.IsCancellationRequested)
            {
                yield return (Status.Canceled, TaskCanceledMessage);
            }
        }
        private static IEnumerable<ProjectInSolution> GetProjectFilesIn(string solutionFilePath)
        {
            var file = SolutionFile.Parse(solutionFilePath);
            return file.ProjectsInOrder
                       .Where(project => File.Exists(project.AbsolutePath))
                       .EnsureSingleEnumerationDEBUG();
        }

        private static List<Project> GetInBuildOrder(IEnumerable<Project> projects)
        {
            var remaining = projects.ToList();
            var result = new List<Project>();
            while (remaining.Count != 0)
            {
                var next = findTop(remaining);
                result.Add(next);
                remaining.Remove(next);
            }
            return result;


            // returns the guid of a project in the specified list that has no dependencies on the specified projects
            Project findTop(List<Project> unbuiltProjects)
            {
                return unbuiltProjects.First(project =>
                {
                    var dependencies = GetProjectReferenceGuids(project);
                    return dependencies.All(dependency => !unbuiltProjects.Select(GetGuid).Contains(dependency));
                });
            }

            // Gets the guids of the project references upon which the specified project depends
            IEnumerable<string> GetProjectReferenceGuids(Project p)
            {
                return p.Items
                        .Where(i => i.ItemType == "ProjectReference")
                        .Select(item => item.GetMetadata("Project").EvaluatedValue)
                        .Select(s => s.ToUpper())
                        .EnsureSingleEnumerationDEBUG();
            }

            // Gets the guid of the specified project
            string GetGuid(Project project)
            {
                return project.GetProperty("ProjectGuid").EvaluatedValue.ToUpper();
            }
        }

        private static IEnumerable<(Status, string)> RunTests(IReadOnlyList<Project> projectsInBuildOrder, CancellationToken cancellationToken)
        {
            try
            {
                foreach (string appDomainBase in projectsInBuildOrder.Select(GetAssemblyPath).Select(Path.GetDirectoryName))
                {
                    CopyDependenciesToNewAppDomainBaseDirectory(appDomainBase);
                }

                int processedProjectsCount = 0;
                var remainingProjects = new ConcurrentQueue<Project>(projectsInBuildOrder);

                for (int i = 0; i < TEST_THREAD_COUNT; i++)
                {
                    new Thread(() =>
                    {
                        while (remainingProjects.TryDequeue(out Project project) && !cancellationToken.IsCancellationRequested)
                        {
                            Interlocked.Increment(ref processedProjectsCount);
                            RunTasksAndWriteMessagesBack(GetAssemblyPath(project));
                        }
                    })
                    {
                        IsBackground = true,
#pragma warning disable CS0618 // Type or member is obsolete
                        ApartmentState = ApartmentState.STA
#pragma warning restore CS0618 // Type or member is obsolete
                    }.Start();

                }

                var pipes = new NamedPipesServerStream(PIPE_NAME, s => s.StartsWith(STOP_CODON), projectsInBuildOrder.Count, cancellationToken);
                return Read(pipes);
            }
            catch (TaskCanceledException)
            {
                return (Status.Canceled, TaskCanceledMessage).ToSingleton();
            }
            catch (Exception e)
            {
                return (Status.MiscellaneousError, e.Message).ToSingleton();
            }

            void RunTasksAndWriteMessagesBack(string assemblyPath)
            {
                string appDomainBase = Path.GetDirectoryName(assemblyPath);
                using (AppDomainContext testerDomain = AppDomainToolkit.AppDomainContext.Create(new AppDomainSetup() { ApplicationBase = appDomainBase, }))
                {
                    int messagesWrittenByApp = RemoteFunc.Invoke(testerDomain.Domain, assemblyPath, assemblyPathLocal =>
                    {
                        int messagesCount = 0;
                        Contract.Assert(AppDomain.CurrentDomain.BaseDirectory == Path.GetDirectoryName(assemblyPathLocal), $"AppDomain switch failed: {AppDomain.CurrentDomain.BaseDirectory} != {Path.GetDirectoryName(assemblyPathLocal)}");

                        using (var outPipe = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out))
                        {
                            outPipe.Connect();
                            using (var writer = new StreamWriter(outPipe) { AutoFlush = true })
                            {
                                int totalTestCount = 0;
                                try
                                {
                                    var tests = Assembly.LoadFrom(assemblyPathLocal)
                                                        .GetTypes()
                                                        .Where(TestClassExtensions.IsTestType)
                                                        .SelectMany(TestClassExtensions.GetTestMethods)
                                                        .Select(testMethod => (testMethod, RunTest(testMethod)));

                                    foreach ((MethodInfo method, string methodError) in tests)
                                    {
                                        //TODO: get cancellationToken.IsCancelRequested here
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
                                catch (Exception e)
                                {
                                    writer.WriteLine(ERROR_CODON + RemoveLineBreaks(e.Message));
                                    if (e.InnerException != null)
                                    {
                                        writer.WriteLine(ERROR_CODON + "Inner message: " + RemoveLineBreaks(e.InnerException.Message));
                                        messagesCount++;
                                    }
                                }
                                finally
                                {
                                    writer.WriteLine(STOP_CODON + totalTestCount.ToString());
                                    messagesCount++;
                                }
                            }
                        }
                        return messagesCount;
                    });
                    Interlocked.Add(ref messagesWrittenCount, messagesWrittenByApp);
                }
            }
        }

        private static string RemoveLineBreaks(string s)
        {
            Contract.Assert(SUCCESS_CODON.Length == STOP_CODON.Length);
            Contract.Assert(ERROR_CODON.Length == STOP_CODON.Length);

            return s?.Replace('\n', '-').Replace('\r', '-');
        }
        private static int messagesWrittenCount;
        public const string PIPE_NAME = "CI_internal_pipe";
        public const string SUCCESS_CODON = "SUCCESS_CODON";
        public const string ERROR_CODON = "ERROR___CODON";
        public const string STOP_CODON = "STOPS___CODON";

        public static IEnumerable<(Status, string)> Read(IEnumerable<string> lines)
        {
            bool hasErrors = false;
            List<int> totalSuccessCounts = new List<int>();
            foreach (string line in lines)
            {
                string codon = line.Substring(0, ERROR_CODON.Length);
                string message = line.Substring(ERROR_CODON.Length);

                switch (codon)
                {
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

        private static void CopyDependenciesToNewAppDomainBaseDirectory(string appDomainBase)
        {
            foreach (string source in Directory.GetFiles(Environment.CurrentDirectory))
            {
                if (source.EndsWith(".dll") || source.EndsWith(".exe"))
                {
                    string destination = Path.Combine(appDomainBase, Path.GetFileName(source));

                    File.Copy(source, destination, overwrite: true);
                }
            }
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
                testMethod.Invoke(testClassInstance, new object[0]);
            }
            catch (TargetInvocationException e)
            {
                if (!TestClassExtensions.IsExceptionExpected(testMethod, e.InnerException))
                {
                    return e.InnerException.Message;
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }
            finally
            {
                TestClassExtensions.RunCleanupMethod(testClassInstance);
            }

            return null;
        }
        private static string GetAssemblyPath(Project project)
        {
            if (!project.AllEvaluatedItems.Where(item => item.ItemType == "IntermediateAssembly").First().EvaluatedInclude.StartsWith("obj"))
                throw new NotImplementedException();

            //couldn't find it in the projects' AlLEvaluatedItems, so I'm hacking this together:
            string relativePath = "bin" + project.AllEvaluatedItems.Where(item => item.ItemType == "IntermediateAssembly").First().EvaluatedInclude.Substring("obj".Length);
            string path = Path.Combine(project.DirectoryPath, relativePath);

            if (!File.Exists(path))
                throw new NotImplementedException("Couldn't find assembly " + relativePath);
            return path;
        }
    }
}
