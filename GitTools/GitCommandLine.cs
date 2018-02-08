using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            Contract.Requires(!string.IsNullOrEmpty(repositoryPath), "The specified repository path cannot be null or empty");
            Contract.Requires(commands != null, "No commands were specified to execute");
            Contract.RequiresForAll(commands, command => command != null, "Commands may not be null");
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

                    var (outputs, errors, _) = StartWaitForExitAndReadStreams(gitProcess);

                    if (errors.Count != 0 && firstCommand && !repeatedCommand)
                    {
                        Thread.Sleep(100);
                        repeatedCommand = true;

                        // start with same parameters
                        (outputs, errors, _) = StartWaitForExitAndReadStreams(gitProcess);
                    }

                    results.AddRange(outputs.Select(output => output + "\n"));
                    if (errors.Count != 0)
                        return (results, string.Concat(errors.Select(output => output + "\n")));
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
            var output = ExecuteWithThrow(repositoryPath, "rev-parse head").Single();
            Contract.Assert(output.Length == CommitHashLength + "\n".Length);
            Contract.Assert(output.Last() == '\n');
            var result = output.Substring(0, CommitHashLength);
            Contract.Ensures(IsValidCommitHash(result));
            return result;
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

        /// <summary>
        /// Starts the process, waits for exit and reads the output and error streams. 
        /// </summary>
        /// <param name="process"> The process to start. </param>
        /// <param name="timeout">Specify <see cref="int.MaxValue"/> for no timeout. </param>
        /// <remarks>https://stackoverflow.com/a/7608823/308451</remarks>
        /// TODO: move to JBSnorro.csproj
        public static (IList<string> Outputs, IList<string> Errors, bool TimedOut) StartWaitForExitAndReadStreams(Process process, int timeout = int.MaxValue)
        {
            Contract.Requires(process != null);

            List<string> outputs = new List<string>();
            List<string> errors = new List<string>();

            using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
            using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data == null) // if signal that stream is closing
                    {
                        outputWaitHandle.Set();
                    }
                    else
                    {
                        outputs.Add(e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data == null) // if signal that stream is closing
                    {
                        errorWaitHandle.Set();
                    }
                    else
                    {
                        errors.Add(e.Data);
                    }
                };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool timedOut = !(process.WaitForExit(timeout) &&
                                  outputWaitHandle.WaitOne(timeout) &&
                                  errorWaitHandle.WaitOne(timeout));

                return (outputs, errors, timedOut);
            }
        }

        public static void Clone(string repositoryPath, string destinationPath)
        {
            Contract.Requires(!string.IsNullOrEmpty(repositoryPath));
            Contract.Requires(!string.IsNullOrEmpty(destinationPath));
            Contract.Requires(!Directory.Exists(destinationPath) || Directory.GetFiles(destinationPath).Length == 0, "You cannot clone into a non-empty directory");

            ExecuteWithThrow(repositoryPath, $"clone --quiet --no-hardlinks \"{repositoryPath}\" \"{destinationPath}\"");
        }

        public static void StashAll(string repositoryPath)
        {
            Contract.Requires(!string.IsNullOrEmpty(repositoryPath));

            var output = Execute(repositoryPath, $"stash --include-untracked --all"); //--quiet
        }
        public static void StashIndex(string repositoryPath)
        {
            Contract.Requires(!string.IsNullOrEmpty(repositoryPath));

            var output = Execute(repositoryPath, $"stash --include-untracked --keep-index"); //--quiet
        }
        /// <summary>
        /// Stashes the files in the working stage but leaves the index and untracked files as is. 
        /// </summary>
        public static void StashWorkingStage(string repositoryPath)
        {
            Contract.Requires(!string.IsNullOrEmpty(repositoryPath));

            throw new NotImplementedException();
        }

        /// <returns> whether the pop succeeded; otherwise false, meaning that there are unsaved changes. </returns>
        public static bool PopStash(string repositoryPath)
        {
            Contract.Requires(!string.IsNullOrEmpty(repositoryPath));

            var output = Execute(repositoryPath, $"stash pop");

            return true;
        }
        /// <summary>
        /// Pops the stash. Merges the stash with unsaved changes, if any.
        /// </summary>
        public static void PopStashAnyway(string repositoryPath)
        {
            Contract.Requires(!string.IsNullOrEmpty(repositoryPath));

            throw new NotImplementedException();
        }
    }
}
