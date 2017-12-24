﻿using Microsoft.Build.Construction;
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
using System.Threading;
using AppDomainToolkit;
using AppDomainContext = AppDomainToolkit.AppDomainContext<AppDomainToolkit.AssemblyTargetLoader, AppDomainToolkit.PathBasedAssemblyResolver>;
using System.Configuration;
using JBSnorro.Diagnostics;

namespace JBSnorro.GitTools.CI
{
    /// <summary>
    /// This program copies a solution, builds it and runs all its tests.
    /// </summary>
    public sealed class Program
    {
        /// <summary>
        /// The maximum number of milliseconds a test may take before it is canceled and considered failed.
        /// </summary>
        public const int MaxTestDuration = 5000;
#if DEBUG
        /// <summary>
        /// Debugging flag to disable copying the solution.
        /// </summary>
        private static readonly bool skipCopySolution = true;
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

            foreach((Status status, string message) in CopySolutionAndExecuteTests(solutionFilePath, destinationDirectory))
            {
                Console.WriteLine(message);
                Debug.Write(message);
            }
            Console.ReadLine();
        }

        /// <summary>
        /// Copies the solution to a temporary destination directory, build the solution and executes the tests and returns any errors.
        /// </summary>
        /// <param name="solutionFilePath"> The path of the .sln file of the solution to run tests of. </param>
        /// <param name="baseDestinationDirectory"> The temporary directory to copy the solution to. </param>
        /// <param name="hash "> The hash of the commit to execute the tests on. Specifiy null to indicate the current commit. </param>
        public static IEnumerable<(Status, string)> CopySolutionAndExecuteTests(string solutionFilePath, string baseDestinationDirectory, string hash = null, bool writeToTestsFile = true)
        {
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
            if (hash == null)
            {
                var (currentCommitHash, error1) = RetrieveCommitHash(Path.GetDirectoryName(solutionFilePath));
                if (hash == null)
                {
                    hash = currentCommitHash;
                }
                else if (currentCommitHash != hash)
                {
                    mustDoCheckout = true;
                    hash = currentCommitHash;
                }
                if (error1 != null)
                {
                    return (Status.MiscellaneousError, error1).ToSingleton();
                }
            }


            (string sourceDirectory, string destinationDirectory) = GetDirectories(solutionFilePath, baseDestinationDirectory);
            using (TestResultsFile resultsFile = TestResultsFile.TryReadFile(sourceDirectory, out error))
            {
                if (error != null)
                {
                    return (Status.MiscellaneousError, error).ToSingleton();
                }

                IEnumerable<(Status Status, string Error)> results = buildAndTest(resultsFile, out string commitMessage);
                if (writeToTestsFile)
                {
                    results = resultsFile.Append(results, hash, commitMessage);
                }
                return results;
            }

            IEnumerable<(Status Status, string Error)> buildAndTest(TestResultsFile resultsFile, out string commitMessage)
            {
                var (skip, error_) = CheckCommitMessage(sourceDirectory, hash, resultsFile, out commitMessage);
                if (skip)
                {
                    return (Status.Skipped, "The specified commit does not satisfy the conditions to be built and tested. " + error_).ToSingleton();
                }
                else if (error_ != null)
                {
                    return (Status.MiscellaneousError, error_).ToSingleton();
                }

                var (destinationSolutionFile, error2) = TryCopySolution(solutionFilePath, destinationDirectory);
                if (error2 != null)
                {
                    return (Status.MiscellaneousError, error2).ToSingleton();
                }

                if (mustDoCheckout)
                    GitCommandLine.Checkout(destinationDirectory, hash);

                var (projectsInBuildOrder, error3) = TryBuildSolution(destinationSolutionFile);
                if (error3 != null)
                {
                    return (Status.BuildError, error3).ToSingleton();
                }

                var (totalCount, error4) = RunTests(projectsInBuildOrder);
                if (error4 != null)
                {
                    return (Status.TestError, error4).ToSingleton();
                }

                return (Status.Success, totalCount.ToString() + " run successfully").ToSingleton();
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
        private static (string commitHash, string error) RetrieveCommitHash(string solutionDirectory)
        {
            try
            {
                var branchName = File.ReadAllText(Path.Combine(solutionDirectory, @".git\HEAD"));
                if (!branchName.StartsWith("ref: refs")) throw new Exception("Assertion failed");

                branchName = branchName.Substring("ref: ".Length, branchName.Length - "\n".Length - "ref: ".Length);
                var commitHash = File.ReadAllText(Path.Combine(solutionDirectory, @".git\", branchName)).Substring(0, 40);
                return (commitHash, null);
            }
            catch (DirectoryNotFoundException e) when (e.Message.Contains(".git"))
            {
                // if there is no git, don't copy to a directory called "no-git"
                return ("no-git", null);
            }
            catch (Exception e)
            {
                return (null, e.Message);
            }
        }

        private static (string sourceDirectory, string destinationDirectory) GetDirectories(string solutionFilePath, string baseDestinationDirectory)
        {
            string sourceDirectory = Path.GetDirectoryName(solutionFilePath);
            string solutionName = sourceDirectory.Substring(Path.GetDirectoryName(solutionFilePath).LastIndexOf(Path.DirectorySeparatorChar) + 1);
            string destinationDirectory = Path.Combine(baseDestinationDirectory, solutionName);
            return (sourceDirectory, destinationDirectory);

        }
        private static (bool skip, string error) CheckCommitMessage(string sourceDirectory, string hash, TestResultsFile resultsFile, out string commitMessage)
        {
            if (resultsFile.Hashes.ContainsKey(hash) && !disregardTestResultsFile)
            {
                commitMessage = null;
                return (true, "It is present in .testresults");
            }

            try
            {
                commitMessage = GitCommandLine.GetCommitMessage(sourceDirectory, hash);
                bool skip = GetAllIgnorePrefixes().Any(commitMessage.StartsWith);
                return (skip, skip ? "The commit message starts with a prefix signaling to ignore" : null);
            }
            catch (GitCommandException e) when (e.Message == "fatal: bad object " + hash + "\n")
            {
                throw new ArgumentException("The specified hash does not exist");
            }
            catch (Exception e)
            {
                commitMessage = null;
                return (false, e.Message);
            }
        }
        private static IEnumerable<string> GetAllIgnorePrefixes()
        {
            return ConfigurationManager.AppSettings.AllKeys
                                                   .Where(key => key.StartsWith("ignore_prefix"))
                                                   .Select(key => ConfigurationManager.AppSettings[key]);
        }
        private static (string destinationSolutionFile, string error) TryCopySolution(string solutionFilePath, string destinationDirectory)
        {
            if (!skipCopySolution)
            {
                try
                {
                    Directory.Delete(destinationDirectory, recursive: true);
                }
                catch { }
            }
            try
            {

                if (!skipCopySolution)
                {
                    //TODO: maybe generalize/parameterize everything that should be excluded. Below .vs is hardcoded 
                    CopyDirectory(new DirectoryInfo(Path.GetDirectoryName(solutionFilePath)), new DirectoryInfo(destinationDirectory));
                }
                return (Path.Combine(destinationDirectory, Path.GetFileName(solutionFilePath)), null);
            }
            catch (Exception e)
            {
                return (null, e.Message);
            }
        }
        /// <remarks> https://stackoverflow.com/questions/58744/copy-the-entire-contents-of-a-directory-in-c-sharp </remarks>
        private static void CopyDirectory(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                if (!(dir.Name.StartsWith(".vs") || dir.Name == "bin" || dir.Name == "obj"))
                    CopyDirectory(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
                if (file.Name != ".testresults") // can't copy because this program currently has a filestream opened on it
                    file.CopyTo(Path.Combine(target.FullName, file.Name), true);
        }

        /// <summary>
        /// Tries to build the solution and returns the projects if successful; otherwise an error message.
        /// </summary>
        private static (IEnumerable<Project>, string) TryBuildSolution(string destinationSolutionFile)
        {
            try
            {
                using (var projects = new ProjectCollection(new Dictionary<string, string> { ["configuration"] = "Debug", ["Platform"] = "x86" }) { IsBuildEnabled = true })
                {
                    foreach (var projectPath in GetProjectFilesIn(destinationSolutionFile))
                    {
                        projects.LoadProject(projectPath.AbsolutePath);
                    }
                    var projectsInBuildOrder = GetInBuildOrder(projects.LoadedProjects);

                    if (!skipBuild)
                    {
#pragma warning disable CS0162 // Unreachable code detected
                        foreach (var project in projectsInBuildOrder)
                        {
                            bool success = project.Build(new ConsoleLogger());
                            if (!success)
                            {
                                return (null, "Build failed");
                            }
                        }
#pragma warning restore CS0162 // Unreachable code detected
                    }

                    return (projectsInBuildOrder, null);
                }
            }
            catch (Exception e)
            {
                return (null, e.Message);
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

        private static (int totalTestCount, string error) RunTests(IEnumerable<Project> projectsInBuildOrder)
        {
            try
            {
                int successCount = 0;
                var loopResult = Parallel.ForEach(projectsInBuildOrder, (project, state) =>
                {
                    int result = RunTests(project);
                    if (result == -1)
                    {
                        state.Break();
                    }
                    else
                    {
                        Interlocked.Add(ref successCount, result);
                    }
                });

                if (loopResult.LowestBreakIteration != null)
                    return (-1, "At least one test failed");

                return (successCount, null);
            }
            catch (Exception e)
            {
                return (-1, e.InnerException.Message);
            }
        }

        private static int RunTests(Project assembly)
        {
            string assemblyPath = GetAssemblyPath(assembly);
            string appDomainBase = Path.GetDirectoryName(assemblyPath);

            CopyDependenciesToNewAppDomainBaseDirectory(appDomainBase);

            using (AppDomainContext testerDomain = AppDomainToolkit.AppDomainContext.Create(new AppDomainSetup() { ApplicationBase = appDomainBase, }))
            {
                SerializableTestResults result = RemoteFunc.Invoke(testerDomain.Domain, assemblyPath, assemblyPathLocal =>
                {
                    try
                    {
                        Contract.Assert(AppDomain.CurrentDomain.BaseDirectory == Path.GetDirectoryName(assemblyPathLocal), $"{AppDomain.CurrentDomain.BaseDirectory} != {Path.GetDirectoryName(assemblyPathLocal)}");
                        Console.WriteLine("Testing " + Path.GetFileName(assemblyPathLocal));

                        var testTypes = Assembly.LoadFrom(assemblyPathLocal)
                                                .GetTypes()
                                                .Where(TestClassExtensions.IsTestType)
                                                .ToList();
                        if (testTypes.Count == 0)
                            return new SerializableTestResults(0);

                        int totalTestCount = 0;
                        foreach (var successCount in testTypes.Select(RunTests))
                        {
                            if (successCount == -1)
                                return new SerializableTestResults(-1);
                            totalTestCount += successCount;
                        }
                        return new SerializableTestResults(totalTestCount);
                    }
                    catch (Exception e)
                    {
                        return new SerializableTestResults(e.Message);
                    }
                });

                if (result.Error != null)
                {
                    // rethrow exception in main AppDomain
                    throw new Exception(result.Error);
                }
                else
                {
                    return result.TotalTestCount;
                }
            }
        }

        private static void CopyDependenciesToNewAppDomainBaseDirectory(string appDomainBase)
        {
            foreach (string source in Directory.GetFiles(Environment.CurrentDirectory))
            {
                if (source.EndsWith(".dll") || source.EndsWith(".exe"))
                {
                    string destination = Path.Combine(appDomainBase, Path.GetFileName(source));

                    if (source.EndsWith("JBSnorro.dll") && File.Exists(destination))
                        continue;

                    File.Copy(source, destination, overwrite: true);
                }
            }
        }


        static (int, int) Add((int, int) a, (int, int) b)
        {
            return (a.Item1 + b.Item1, a.Item2 + b.Item2);
        }
        /// <returns>the number of tests if all ran successfully; otherwise -1; </returns>
        private static int RunTests(Type testType)
        {
            int totalTestCount = 0;
            foreach (var testMethod in testType.GetMethods().Where(TestClassExtensions.IsTestMethod))
            {
                bool result = RunTest(testMethod);
                if (!result)
                {
                    return -1;
                }
                totalTestCount++;
            }
            return totalTestCount;
        }
        private static bool RunTest(MethodInfo testMethod)
        {
            if (testMethod == null) throw new ArgumentNullException(nameof(testMethod));

            var testClassInstance = testMethod.DeclaringType.GetConstructor(new Type[0]).Invoke(new object[0]);

            bool success = true;
            if (!new Action(run).InvokeWithTimeout(MaxTestDuration, ApartmentState.STA))
                return false;

            return success;


            void run()
            {
                TestClassExtensions.RunInitializationMethod(testClassInstance);
                try
                {
                    testMethod.Invoke(testClassInstance, new object[0]);
                }
                catch (Exception e)
                {
                    if (!TestClassExtensions.IsExceptionExpected(testMethod, e.InnerException))
                        success = false;
                }
                finally
                {
                    TestClassExtensions.RunCleanupMethod(testClassInstance);
                }
            }
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
