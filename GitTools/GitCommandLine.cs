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
        /// <summary>
        /// Gets or sets the path of the git executable.
        /// </summary>
        public static string GitPath = @"C:\Program Files\Git\bin\git.exe";
        /// <summary>
        /// Invokes the specified commands on the specified repository and results the results.
        /// </summary>
        public static (IEnumerable<string> result, string error) Execute(string repositoryPath, params string[] commands)
        {
            Contract.Requires(!string.IsNullOrEmpty(repositoryPath));
            Contract.Requires(commands != null);

            if (commands.Length == 0)
                return (Enumerable.Empty<string>(), null);

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
        /// Gets the hash of the current commit; or throws if somehow an error is thrown during execution.
        /// </summary>
        public static string GetCurrentCommitHash()
        {
            var (results, error) = Execute("git rev-parse head");
            if (error != null)
                throw new Exception(error);
            else
                return results.First();
        }
    }
}
