using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.GitTools
{
    public static class GitCommandLine
    {
        public const int CommitHashLength = 40;
        /// <summary>
        /// Gets or sets the path of the git executable.
        /// </summary>
        public static string GitPath = @"C:\Program Files\Git\bin\git.exe";
        /// <summary>
        /// Invokes the specified commands on the specified repository and returns the results or an error.
        /// </summary>
        /// <param name="commands"> Includes the keyword 'git'. </param>
        public static (IReadOnlyList<string> result, string error) Execute(string repositoryPath, params string[] commands)
        {
            Contract.Requires(!string.IsNullOrEmpty(repositoryPath));
            Contract.Requires(commands != null);

            if (commands.Length == 0)
                return (EmptyCollection<string>.ReadOnlyList, null);

            ProcessStartInfo info = new ProcessStartInfo()
            {
                FileName = GitPath,
                WorkingDirectory = repositoryPath,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            List<string> results = new List<string>();

            // invoke the commands
            using (Process gitProcess = new Process())
            {
                foreach (string command in commands)
                {
                    info.Arguments = command;

                    gitProcess.StartInfo = info;
                    gitProcess.Start();

                    string error = gitProcess.StandardError.ReadToEnd();
                    results.Add(gitProcess.StandardOutput.ReadToEnd());

                    gitProcess.WaitForExit();
                    if (!string.IsNullOrEmpty(error))
                        return (results, error);
                }
            }

            return (results, null);
        }
        /// <summary>
        /// Invokes the specified commands on the specified repository and returns the results; or throws the error if one occurred.
        /// </summary>
        public static IReadOnlyList<string> ExecuteWithThrow(string repositoryPath, params string[] commands)
        {
            var (results, error) = Execute(repositoryPath, commands);
            if (error != null)
            {
                throw new GitCommandException(error);
            }
            else
            {
                return results;
            }
        }

        /// <summary>
        /// Gets the hash of the current commit; or throws if somehow an error is thrown during execution.
        /// </summary>
        public static string GetCurrentCommitHash()
        {
            return ExecuteWithThrow("git rev-parse head").First();
        }
        /// <summary>
        /// Checks out the specified commit in the repository.
        /// </summary>
        public static void Checkout(string repositoryPath, string hash)
        {
            ExecuteWithThrow(repositoryPath, "git checkout " + hash);
        }
    }
}
