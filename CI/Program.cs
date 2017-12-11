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

namespace JBSnorro.GitTools.CI
{
    /// <summary>
    /// This program copies a solution, builds it and runs all its tests.
    /// </summary>
    class Program
    {
        /// <param name="args"> Must contain the path of the solution file, and the directory where to copy the solution to. </param>
        static void Main(string[] args)
        {
            if (args == null) { throw new ArgumentNullException(nameof(args)); }
            if (args.Length != 2) { throw new ArgumentException("Two arguments must be specified", nameof(args)); }

            string solutionFilePath = args[0];
            string destinationDirectory = args[1];

            var (status, message) = CopySolutionAndExecuteTests(solutionFilePath, destinationDirectory);
            Console.WriteLine(message);
            Console.ReadLine();
        }

        /// <summary>
        /// Copies the solution to a temporary destination directory, build the solution and executes the tests and returns any errors.
        /// </summary>
        /// <param name="solutionFilePath"> The path of the .sln file of the solution to run tests of. </param>
        /// <param name="destinationDirectory"> The temporary directory to copy the solution to. </param>
        public static (Status, string) CopySolutionAndExecuteTests(string solutionFilePath, string destinationDirectory)
        {
            var error = ValidateSolutionFilePath(solutionFilePath);
            if (error != null)
            {
                return (Status.ArgumentError, error);
            }

            error = ValidateDestinationDirectory(destinationDirectory);
            if (error != null)
            {
                return (Status.ArgumentError, error);
            }

            var (commitHash, error1) = RetrieveCommitHash(Path.GetDirectoryName(solutionFilePath));
            if (error1 != null)
            {
                return (Status.MiscellaneousError, error1);
            }

            var (destinationSolutionFile, error2) = TryCopySolution(solutionFilePath, Path.Combine(destinationDirectory, commitHash));
            if (error2 != null)
            {
                return (Status.MiscellaneousError, error2);
            }

            var error3 = TryBuildSolution(destinationSolutionFile);
            if (error3 != null)
            {
                return (Status.BuildError, error3);
            }

            var (totalCount, error4) = RunSolutionTests(destinationSolutionFile);
            if (error4 != null)
            {
                return (Status.TestError, error4);
            }

            return (Status.Success, totalCount.ToString() + " run successfully");
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
            catch (Exception e)
            {
                return (null, e.Message);
            }
        }

        private static (string destinationSolutionFile, string error) TryCopySolution(string solutionFilePath, string destinationDirectory)
        {
            try
            {
                SetAttributesNormal(destinationDirectory);
                Directory.Delete(destinationDirectory, recursive: true);
            }
            catch { }
            try
            {
                //TODO: maybe generalize/parameterize everything that should be excluded. Below .vs is hardcoded 
                CopyDirectory(new DirectoryInfo(Path.GetDirectoryName(solutionFilePath)), new DirectoryInfo(destinationDirectory));
                SetAttributesNormal(destinationDirectory);
                return (Path.Combine(destinationDirectory, Path.GetFileName(solutionFilePath)), null);
            }
            catch (Exception e)
            {
                return (null, e.Message);
            }


            /// <remarks> https://stackoverflow.com/questions/58744/copy-the-entire-contents-of-a-directory-in-c-sharp </remarks>
            void CopyDirectory(DirectoryInfo source, DirectoryInfo target)
            {
                foreach (DirectoryInfo dir in source.GetDirectories())
                    if (!(dir.Name.StartsWith(".vs") || dir.Name == "bin" || dir.Name == "obj"))
                        CopyDirectory(dir, target.CreateSubdirectory(dir.Name));
                foreach (FileInfo file in source.GetFiles())
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

        /// <summary>
        /// Tries to build the solution and returns null if successful; otherwise the error message.
        /// </summary>
        private static string TryBuildSolution(string destinationSolutionFile)
        {
            SetAttributesNormal(Path.GetDirectoryName(destinationSolutionFile));
            try
            {
                var projects = new ProjectCollection() { IsBuildEnabled = true };
                foreach (var projectPath in GetProjectFilesIn(destinationSolutionFile))
                {
                    var project = projects.LoadProject(projectPath);
                    project.Build(new ConsoleLogger());
                }
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }

        }
        private static IEnumerable<string> GetProjectFilesIn(string solutionPath)
        {
            return SolutionFile.Parse(solutionPath)
                               .ProjectsInOrder
                               .Select(project => project.AbsolutePath)
                               .Where(File.Exists);
        }
        private static (int totalTestCount, string error) RunSolutionTests(string solutionPath)
        {
            try
            {
                var (totalTestCount, successCount) = GetProjectFilesIn(solutionPath).AsParallel()
                                                                                    .Select(RunProjectsTests)
                                                                                    .Aggregate(Add);
                if (totalTestCount == successCount)
                    return (totalTestCount, null);
                else
                    return (totalTestCount, $"{totalTestCount - successCount}/{totalTestCount} tests failed");
            }
            catch (Exception e)
            {
                return (-1, e.Message);
            }
        }
        private static (int totalTestCount, int successfulTestCount) RunProjectsTests(string projectFilePath)
        {
            if (projectFilePath == null) throw new ArgumentNullException(nameof(projectFilePath));


            return Assembly.Load(GetAssemblyPath(projectFilePath))
                           .GetTypes()
                           .Where(TestClassExtensions.IsTestType)
                           .AsParallel()
                           .Select(RunTests)
                           .Aggregate(Add);
        }
        static (int, int) Add((int, int) a, (int, int) b)
        {
            return (a.Item1 + b.Item1, a.Item2 + b.Item2);
        }
        private static (int totalTestCount, int successfulTestCount) RunTests(Type testType)
        {
            var successes = testType.GetMethods().Where(TestClassExtensions.IsTestMethod).AsParallel().Select(RunTest).ToList();
            return (successes.Count, successes.Count(_ => _));
        }
        private static bool RunTest(MethodInfo testMethod)
        {
            if (testMethod == null) throw new ArgumentNullException(nameof(testMethod));

            var testClassInstance = testMethod.DeclaringType.GetConstructor(new Type[0]).Invoke(new object[0]);
            TestClassExtensions.RunInitializationMethod(testClassInstance);
            try
            {

                testMethod.Invoke(testClassInstance, new object[0]);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                TestClassExtensions.RunCleanupMethod(testClassInstance);
            }


        }
        private static string GetAssemblyPath(string project)
        {
            throw new NotImplementedException();
        }
    }
}
