using JBSnorro.GitTools;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CI.UI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0) throw new ArgumentException("No arguments were provided");

            switch (args[0])
            {
                case "commit":
                    {
                        if (args.Length != 2)
                            throw new ArgumentException("No commit hash was provided");
                        if (args[1].Length != GitCommandLine.CommitHashLength)
                            throw new ArgumentException($"The commit hash has length {args[1].Length} where {GitCommandLine.CommitHashLength} was expected");

                        HandleCommit(args[1]);
                    }
                    break;
                default:
                    throw new ArgumentException("The first argument was not any of the expected values");
            }
        }

        static void HandleCommit(string hash)
        {
            //TODO
        }
    }
}
