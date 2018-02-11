using JBSnorro.Diagnostics;
using JBSnorro.GitTools;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JBSnorro.AutoGitHotkey
{
    class Program
    {
        const string POP = "pop";
        const string STASH = "stash";
        const string POP_ANYWAY = "pop_anyway";
        const string STASH_INDEX = "stash_unstaged";
        const string POP_AND_ASK_FOR_ANYWAY = "pop_and_ask_for_anyway";
        const string AMEND_STASH = "amend_stash";

        private static IEnumerable<string> allowedArguments
        {
            get
            {
                yield return STASH;
                yield return STASH_INDEX;
                yield return POP;
                yield return POP_ANYWAY;
                yield return POP_AND_ASK_FOR_ANYWAY;
                yield return AMEND_STASH;
            }
        }
        static void Main(string[] args)
        {
            if (args == null || args.Length != 1)
                throw new ArgumentException("Wrong number of arguments specified");

            string operation = args[0];
            if (!allowedArguments.Contains(operation))
                throw new ArgumentException("Invalid argument specified");

            string path = GetRepositoryPath(new ActiveWindowTitleGetter())
                        ?? GetDefaultRepositoryPath();

            if (path == null)
            {
                MessageBox.Show($"The operation '{operation}' could not be applied because the repository could not be determined");
            }
            else
            {
                Operate(operation, path);
            }
        }

        internal static void Operate(string operation, string path)
        {
            Contract.Requires(!string.IsNullOrEmpty(operation), $"{nameof(operation)} is null or empty");
            Contract.Requires(!string.IsNullOrEmpty(path), $"{nameof(path)} is null or empty");

            var cli = new GitCommandLine(path);
            switch (operation)
            {
                case STASH:
                    cli.StashAll();
                    break;
                case STASH_INDEX:
                    cli.StashIndex();
                    break;
                case POP:
                    cli.PopStash();
                    break;
                case POP_ANYWAY:
                    cli.PopStashAnyway();
                    break;
                case POP_AND_ASK_FOR_ANYWAY:
                    bool success = cli.PopStash();
                    if (!success)
                    {
                        var result = MessageBox.Show("Would you like to stash pop into unsaved changes? ", "Unsaved changes", MessageBoxButtons.OKCancel);
                        if (result == DialogResult.OK)
                        {
                            goto case POP_ANYWAY;
                        }
                    }
                    break;
                case AMEND_STASH:
                    cli.AmendStashAll();
                    break;
                default:
                    throw new DefaultSwitchCaseUnreachableException();
            }
        }

        /// <summary>
        /// Gets the path of the repository to apply the shortcut to.
        /// </summary>
        internal static string GetRepositoryPath(IActiveWindowTitle activeWindowTitleGetter)
        {
            Contract.Requires(activeWindowTitleGetter != null);

            string title = activeWindowTitleGetter.GetActiveWindowTitle();
            string result = GetRepositoryPath(title);

            return result;
        }
        /// <summary>
        /// Extracts the relevant git repository path from the specified window title. This knows about the git CLI and Visual Studio. 
        /// </summary>
        private static string GetRepositoryPath(string windowTitle)
        {
            const string gitTitleStart = "Administrator: posh~git ~ ";
            const string vsTitleEnd = "Microsoft Visual Studio  (Administrator)";

            string projectStart = null;
            if (windowTitle.StartsWith(gitTitleStart))
            {
                projectStart = windowTitle.Substring(gitTitleStart.Length);
            }
            else if (windowTitle.EndsWith(vsTitleEnd))
            {
                projectStart = windowTitle.Substring(0, windowTitle.Length - vsTitleEnd.Length);
            }
            else
            {
                return null;
            }

            string key = ConfigurationManager.AppSettings.AllKeys.FirstOrDefault(projectStart.StartsWith);
            if (key != null)
            {
                return ConfigurationManager.AppSettings[key];
            }

            return null;
        }

        /// <summary>
        /// Gets the default repository if no repository could be determined from the active window.
        /// This is a potentially dangerous operation as the user may mess with an unintended repository. 
        /// </summary>
        internal static string GetDefaultRepositoryPath()
        {
            return ConfigurationManager.AppSettings["DefaultRepository"];
        }
    }
}
