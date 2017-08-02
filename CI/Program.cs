using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
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
            var error = ValidateSolutionFilePath(solutionFilePath);
            if (error != null)
            {
                Console.WriteLine(error);
                return;
            }

            string destinationDirectory = args[1];
            error = ValidateDestinationDirectory(destinationDirectory);
            if (error != null)
            {
                Console.WriteLine(error);
                return;
            }

            var (commitHash, error1) = RetrieveCommitHash(destinationDirectory);
            if (error1 != null)
            {
                Console.WriteLine(error);
                return;
            }

            var (destinationSolutionFile, error2) = TryCopySolution(solutionFilePath, destinationDirectory);
            if (error2 != null)
            {
                Console.WriteLine(error);
                return;
            }

            var error3 = TryBuildSolution(destinationSolutionFile);
            if (!error3)
            {
                Console.WriteLine("Build failed");
                return;
            }

            var (success, error4) = RunTests(destinationSolutionFile);
            if (error4 != null)
            {
                Console.WriteLine(error);
                return;
            }

            Console.WriteLine(success);
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

                if (destinationDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    destinationDirectory += Path.DirectorySeparatorChar.ToString();

                string originalDestinationDirectory = destinationDirectory;
                destinationDirectory = Path.GetDirectoryName(destinationDirectory);

                if (originalDestinationDirectory != destinationDirectory)
                    return "The specified destination directory was not a directory";
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
                var (result, error) = GitCommandLine.Execute(solutionDirectory, "status");
                return (result?.FirstOrDefault(), error);
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
                CopyDirectory(new DirectoryInfo(Path.GetDirectoryName(solutionFilePath)), new DirectoryInfo(destinationDirectory));

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
                    CopyDirectory(dir, target.CreateSubdirectory(dir.Name));
                foreach (FileInfo file in source.GetFiles())
                    file.CopyTo(Path.Combine(target.FullName, file.Name));
            }
        }
        /// <summary>
        /// Tries to build the solution and returns whether it succeeded.
        /// </summary>
        private static bool TryBuildSolution(string destinationSolutionFile)
        {
            try
            {
                foreach (var project in GetProjectFilesIn(destinationSolutionFile))
                {
                    bool success = project.Build();
                    if (!success)
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }

        }
        private static IEnumerable<ProjectInstance> GetProjectFilesIn(string solutionPath)
        {
            return SolutionFile.Parse(solutionPath).ProjectsInOrder.Select(path => new ProjectInstance(path.AbsolutePath));
        }
        private static (object success, string error) RunTests(string solutionPath)
        {
            try
            {
                var result = GetProjectFilesIn(solutionPath).AsParallel()
                                                            .Select(RunTests)
                                                            .Aggregate(Add);
                return (result, null);
            }
            catch (Exception e)
            {
                return (null, e.Message);
            }
        }
        private static (int totalTestCount, int successfulTestCount) RunTests(ProjectInstance project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            return Assembly.Load(GetAssemblyPath(project))
                           .GetTypes()
                           .Where(IsTestType)
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
            var successes = testType.GetMethods().Where(IsTestMethod).AsParallel().Select(RunTest).ToList();
            return (successes.Count, successes.Count(_ => _));
        }
        private static bool RunTest(MethodInfo testMethod)
        {
            if (testMethod == null) throw new ArgumentNullException(nameof(testMethod));

            var testClassInstance = testMethod.DeclaringType.GetConstructor(new Type[0]).Invoke(new object[0]);
            RunInitializationMethod(testClassInstance);
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
                RunCleanupMethod(testClassInstance);
            }

            void RunInitializationMethod(object instance)
            {
                GetInitializationMethod(instance)?.Invoke(instance, new object[0]);
            }
            void RunCleanupMethod(object instance)
            {
                GetCleanupMethod(instance)?.Invoke(instance, new object[0]);
            }
        }
        private static string GetAssemblyPath(ProjectInstance project)
        {
            throw new NotImplementedException();
        }
        private static bool IsTestType(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            return type.CustomAttributes.Any(attribute => GetBaseTypes(attribute.AttributeType).Any(attributeType => TestClassFullNames.Contains(attributeType.FullName)));
        }

        private static bool IsTestMethod(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            return HasAttribute(method, TestMethodAttributeFullNames);
        }
        private static bool HasAttribute(MethodInfo method, IList<string> attibuteFullNames)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (attibuteFullNames == null) throw new ArgumentNullException(nameof(attibuteFullNames));

            return method.CustomAttributes.Any(attribute => GetBaseTypes(attribute.AttributeType).Any(attributeType => attibuteFullNames.Contains(attributeType.FullName)));
        }
        private static bool IsTestInitializationMethod(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            return HasAttribute(method, TestMethodInitializationAttributeFullNames);
        }
        private static bool IsTestCleanupMethod(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            return HasAttribute(method, TestMethodCleanupAttributeFullNames);
        }
        private static MethodInfo GetInitializationMethod(object testTypeInstance)
        {
            if (testTypeInstance == null) throw new ArgumentNullException(nameof(testTypeInstance));

            return testTypeInstance.GetType()
                                   .GetMethods()
                                   .Where(IsTestInitializationMethod)
                                   .FirstOrDefault();
        }
        private static MethodInfo GetCleanupMethod(object testTypeInstance)
        {
            if (testTypeInstance == null) throw new ArgumentNullException(nameof(testTypeInstance));

            return testTypeInstance.GetType()
                                   .GetMethods()
                                   .Where(IsTestCleanupMethod)
                                   .FirstOrDefault();
        }

        private static List<string> TestClassFullNames = new List<string> { "Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute" };
        private static List<string> TestMethodAttributeFullNames = new List<string> { "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute" };
        private static List<string> TestMethodInitializationAttributeFullNames = new List<string> { "Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute" };
        private static List<string> TestMethodCleanupAttributeFullNames = new List<string> { "Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute" };

        private static IEnumerable<Type> GetBaseTypes(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            yield return type;
            if (type.BaseType != null)
                foreach (var result in GetBaseTypes(type.BaseType))
                    yield return result;
        }

    }
}
