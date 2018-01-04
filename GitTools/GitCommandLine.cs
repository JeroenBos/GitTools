using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JBSnorro.GitTools
{
    public static class GitCommandLine
    {
        public const int CommitHashLength = 40;
        private static readonly Regex hashRegex = new Regex($"^[a-fA-F0-9]{CommitHashLength}$");
        /// <summary>
        /// Gets whether the specified string contains a valid commit hash.
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public static bool IsValidCommitHash(string hash)
        {
            return hash != null && hashRegex.Match(hash) != null;
        }
        /// <summary>
        /// Gets or sets the path of the git executable.
        /// </summary>
        public static string GitPath = @"C:\Program Files\Git\bin\git.exe";
        /// <summary>
        /// Invokes the specified commands on the specified repository and returns the results or an error.
        /// </summary>
        /// <param name="commands"> The commands to execute. Should exclude the keyword 'git'. </param>
        public static (IReadOnlyList<string> result, string error) Execute(string repositoryPath, params string[] commands)
        {
            Contract.Requires(!string.IsNullOrEmpty(repositoryPath));
            Contract.Requires(commands != null);
            Contract.RequiresForAll(commands, command => command != null);
            Contract.RequiresForAll(commands, command => !command.TrimStart().StartsWith("git "), "Git commands mustn't specify the word 'git' explicitly");

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

            bool repeatedCommand = false;
            bool firstCommand = true;
            // invoke the commands
            using (Process gitProcess = new Process())
            {
                foreach (string command in commands)
                {
                    info.Arguments = command;

                    gitProcess.StartInfo = info;
                    gitProcess.Start();

                    string error = gitProcess.StandardError.ReadToEnd();
                    gitProcess.WaitForExit();
                    if (!string.IsNullOrEmpty(error) && firstCommand && !repeatedCommand)
                    {
                        Thread.Sleep(100);
                        repeatedCommand = true;
                        gitProcess.Start(); // start with same parameters
                        error = gitProcess.StandardError.ReadToEnd();
                        gitProcess.WaitForExit();
                    }

                    results.Add(gitProcess.StandardOutput.ReadToEnd());
                    if (!string.IsNullOrEmpty(error))
                        return (results, error);
                    firstCommand = false;
                }
            }

            return (results, null);
        }
        /// <summary>
        /// Invokes the specified commands on the specified repository and returns the results; or throws the error if one occurred.
        /// </summary>
        /// <param name="commands"> The commands to execute. Should exclude the keyword 'git'. </param>
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
        public static string GetCurrentCommitHash(string repositoryPath)
        {
            return ExecuteWithThrow(repositoryPath, "rev-parse head").First();
        }
        /// <summary>
        /// Checks out the specified commit in the repository.
        /// </summary>
        public static void Checkout(string repositoryPath, string hash)
        {
            Contract.Requires(IsValidCommitHash(hash));

            ExecuteWithThrow(repositoryPath, "checkout " + hash);
        }
        /// <summary>
        /// Checks out the specified commit in the repository with option '--hard'.
        /// </summary>
        public static void CheckoutHard(string repositoryPath, string hash)
        {
            ResetHard(repositoryPath, hash);
        }
        /// <summary>
        /// Checks out the specified commit in the repository with option '--hard'.
        /// </summary>
        public static void ResetHard(string repositoryPath, string hash)
        {
            Contract.Requires(IsValidCommitHash(hash));

            ExecuteWithThrow(repositoryPath, "reset --hard " + hash);
        }
        /// <summary>
        /// Gets the commit message of the commit with the specified hash.
        /// </summary>
        public static string GetCommitMessage(string repositoryPath, string hash)
        {
            Contract.Requires(IsValidCommitHash(hash));

            return ExecuteWithThrow(repositoryPath, "log -1 --pretty=format:%s " + hash).First();
        }
        /// <summary>
        /// Gets the hash of the parent of the specified hash.
        /// </summary>
        public static string GetParentCommitHash(string repositoryPath, string hash)
        {
            Contract.Requires(IsValidCommitHash(hash));

            string parentHash = ExecuteWithThrow(repositoryPath, $"log -1 --pretty=format:%P \"{hash}\"").First();
            Contract.Ensures(IsValidCommitHash(parentHash));
            return parentHash;
        }
    }
}
