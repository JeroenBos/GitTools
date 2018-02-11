using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JBSnorro.GitTools
{
    public sealed class GitCommandLine
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
        public static string GitPath => ConfigurationManager.AppSettings["gitpath"] ?? throw new AppSettingNotFoundException("gitpath");

        /// <summary>
        /// Gets the directory the git commands are invoked on.
        /// </summary>
        public string RepositoryPath { get; }

        /// <param name="repositoryPath"> The directory the git commands are invoked on</param>
        public GitCommandLine(string repositoryPath)
        {
            Contract.Requires(!string.IsNullOrEmpty(repositoryPath), "The specified repository path cannot be null or empty");

            this.RepositoryPath = repositoryPath;
        }

        /// <summary>
        /// Invokes the specified commands on the specified repository and returns the results or an error.
        /// </summary>
        /// <param name="commands"> The commands to execute. Should exclude the keyword 'git'. </param>
        public (IReadOnlyList<string> Result, string Error) Execute(params string[] commands)
        {
            Contract.Requires(commands != null, "No commands were specified to execute");
            Contract.RequiresForAll(commands, command => command != null, "Commands may not be null");
            Contract.RequiresForAll(commands, command => !command.TrimStart().StartsWith("git "), "Git commands mustn't specify the word 'git' explicitly");

            if (commands.Length == 0)
                return (EmptyCollection<string>.ReadOnlyList, null);

            ProcessStartInfo info = new ProcessStartInfo()
            {
                FileName = GitPath,
                WorkingDirectory = this.RepositoryPath,
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
                    SortOutOutputs(outputs, errors);

                    if (errors.Count != 0 && firstCommand && !repeatedCommand)
                    {
                        Thread.Sleep(100);
                        repeatedCommand = true;

                        // start with same parameters
                        (outputs, errors, _) = StartWaitForExitAndReadStreams(gitProcess);
                        SortOutOutputs(outputs, errors);
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
        public IReadOnlyList<string> ExecuteWithThrow(params string[] commands)
        {
            var (results, error) = Execute(commands);
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
        /// Sometimes git writes to stderr when it should have written to stdout. 
        /// This methods aims at hardcodedly sorting that mess out by moving the erroneously written to the error stream from the errors to the outputs.
        /// </summary>
        private static void SortOutOutputs(IList<string> outputs, IList<string> errors)
        {
            Contract.Requires(outputs != null);
            Contract.Requires(errors != null);

            for (int i = errors.Count - 1; i >= 0; i--)
            {
                string error = errors[i];
                if (error.IsAnyOf("CI not enabled",
                                  "Disposing started UI")
                 || error.StartsWith("in dispatcher. args: "))
                {
                    outputs.Add(error);
                    errors.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Gets the hash of the current commit; or throws if somehow an error is thrown during execution.
        /// </summary>
        public string GetCurrentCommitHash()
        {
            var output = ExecuteWithThrow("rev-parse head").Single();
            Contract.Assert(output.Length == CommitHashLength + "\n".Length);
            Contract.Assert(output.Last() == '\n');
            var result = output.Substring(0, CommitHashLength);
            Contract.Ensures(IsValidCommitHash(result));
            return result;
        }
        /// <summary>
        /// Checks out the specified commit in the repository.
        /// </summary>
        public void Checkout(string hash)
        {
            Contract.Requires(IsValidCommitHash(hash));

            ExecuteWithThrow("checkout " + hash);
        }
        /// <summary>
        /// Checks out the specified commit in the repository with option '--hard'.
        /// </summary>
        public void CheckoutHard(string hash)
        {
            ResetHard(hash);
        }
        /// <summary>
        /// Checks out the specified commit in the repository with option '--hard'.
        /// </summary>
        public void ResetHard(string hash)
        {
            Contract.Requires(IsValidCommitHash(hash));

            ExecuteWithThrow("reset --hard " + hash);
        }
        /// <summary>
        /// Gets the commit message of the commit with the specified hash.
        /// </summary>
        public string GetCommitMessage(string hash)
        {
            Contract.Requires(IsValidCommitHash(hash));

            return ExecuteWithThrow("log -1 --pretty=format:%s " + hash).First();
        }
        /// <summary>
        /// Gets the hash of the parent of the specified hash.
        /// </summary>
        public string GetParentCommitHash(string hash)
        {
            Contract.Requires(IsValidCommitHash(hash));

            string parentHash = ExecuteWithThrow($"log -1 --pretty=format:%P \"{hash}\"").First();
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
                void onOutputReceived(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) // if signal that stream is closing
                    {
                        outputWaitHandle.Set();
                    }
                    else
                    {
                        outputs.Add(e.Data);
                    }
                }
                void onErrorReceived(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) // if signal that stream is closing
                    {
                        errorWaitHandle.Set();
                    }
                    else
                    {
                        errors.Add(e.Data);
                    }
                }

                try
                {
                    process.OutputDataReceived += onOutputReceived;
                    process.ErrorDataReceived += onErrorReceived;

                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    bool timedOut = !(process.WaitForExit(timeout)
                                  && outputWaitHandle.WaitOne(timeout)
                                  && errorWaitHandle.WaitOne(timeout));

                    return (outputs, errors, timedOut);
                }
                finally
                {
                    process.OutputDataReceived -= onOutputReceived;
                    process.ErrorDataReceived -= onErrorReceived;

                    process.CancelOutputRead();
                    process.CancelErrorRead();
                }
            }
        }

        public void Clone(string destinationPath)
        {
            Contract.Requires(!string.IsNullOrEmpty(destinationPath));
            Contract.Requires(!Directory.Exists(destinationPath) || Directory.GetFiles(destinationPath).Length == 0, "You cannot clone into a non-empty directory");

            ExecuteWithThrow($"clone --quiet --no-hardlinks \"{this.RepositoryPath}\" \"{destinationPath}\"");
        }

        public void StashAll()
        {
            var output = Execute($"stash --include-untracked --all"); //--quiet
        }
        /// <summary>
        /// Amends the top stash with all current changes. 
        /// </summary>
        public void AmendStashAll()
        {
            bool success = PopStashAnyway();
            if (success)
                StashAll();
        }

        public void StashIndex()
        {
            var output = Execute($"stash --include-untracked --keep-index"); //--quiet
        }

        /// <returns> whether the pop succeeded; otherwise false, meaning that there are unsaved changes. </returns>
        public bool PopStash()
        {
            var output = Execute($"stash pop");

            return true;
        }
        /// <summary>
        /// Pops the stash. Merges the stash with unsaved changes, if any.
        /// </summary>
        /// <returns> whether the pop stash anyway operation succeeded. </returns>
        public bool PopStashAnyway()
        {
            using (new TemporaryCIDisabler(this.RepositoryPath))
            {
                var (outputs, error) = Execute(
                    "commit -a --untracked-files --allow-empty --no-verify --no-post-rewrite --message=\"temporary pop_stash_anyway commit\"",
                    "stash apply",
                    "stash drop",
                    "reset head~ -q");

                return error == null;
            }
        }
    }
}
