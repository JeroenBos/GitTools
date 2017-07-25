﻿using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                foreach(var project in GetProjectFilesIn(destinationSolutionFile))
                {
                    bool success = project.Build();
                    if (!success)
                        return false;
                }
                return true;
            }
            catch (Exception e)
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
            throw new NotImplementedException();
        }
    }
}
