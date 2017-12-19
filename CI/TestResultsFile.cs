using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.GitTools.CI
{
    /// <summary>
    /// Wraps around a file that contains a list of hashes and whether or not tests succeeded.
    /// </summary>
    public class TestResultsFile : IDisposable
    {
        /// <summary>
        /// Gets the path of the test results file relative to source solution file directory.
        /// </summary>
        public static readonly string RelativePath = ".testresults";

        private readonly StreamWriter stream;
        public IReadOnlyDictionary<string, TestResult> Hashes { get; }

        /// <summary>
        /// Opens or creates the file at the specified path and reads the existing hashes and results.
        /// </summary>
        public TestResultsFile(string path)
        {
            Contract.Requires(!string.IsNullOrEmpty(path));

            if (Directory.Exists(path))
            {
                path = Path.Combine(path, RelativePath);
            }

            if (!File.Exists(path))
                Console.WriteLine("Creating " + path);


            var stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);

            var hashes = new Dictionary<string, TestResult>();
            this.Hashes = new ReadOnlyDictionary<string, TestResult>(hashes);

            var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                var (key, result) = TestResultExtensions.FromLine(reader.ReadLine());
                hashes[key] = result;
            }

            this.stream = new StreamWriter(stream);
        }
        public static TestResultsFile TryReadFile(string path, out string errorMessage)
        {
            try
            {
                errorMessage = null;
                return new TestResultsFile(path);
            }
            catch (Exception e)
            {
                errorMessage = $"Error in reading file {path}. " + e.Message;
                return null;
            }
        }
        /// <summary>
        /// Writes the specified result and key to the current file.
        /// </summary>
        public void Append(string hash, TestResult result, string commitMessage)
        {
            string line = TestResultExtensions.ToLine(hash, result, commitMessage);
            if (line != null)
            {
                this.stream.WriteLine(line);
            }
        }
        /// <summary>
        /// Writes the specified result and key to the current file.
        /// </summary>
        public void Append(string hash, Status status, string commitMessage)
        {
            Append(hash, status.ToTestResult(), commitMessage);
        }
        public void Dispose()
        {
            stream.Dispose();
        }
    }
    public enum TestResult
    {
        Success,
        Failure,
        Ignored
    }
    public static class TestResultExtensions
    {
        public static string ToAbbreviation(this TestResult testResult)
        {
            switch (testResult)
            {
                case TestResult.Success:
                    return "OK  "; //spaces for alignment
                case TestResult.Failure:
                    return "FAIL";
                case TestResult.Ignored:
                    throw new ArgumentException("There is no abbreviation for the ignored test result (because it shouldn't be saved)");
                default:
                    throw new DefaultSwitchCaseUnreachableException();
            }
        }
        public static TestResult FromAbbreviation(string abbreviation)
        {
            switch (abbreviation)
            {
                case "OK":
                case "OK  ":
                    return TestResult.Success;
                case "FAIL":
                    return TestResult.Failure;
                default:
                    throw new DefaultSwitchCaseUnreachableException();
            }
        }
        public static TestResult ToTestResult(this Status status)
        {
            switch (status)
            {
                case Status.Success:
                    return TestResult.Success;
                case Status.ArgumentError:
                case Status.MiscellaneousError:
                case Status.BuildError:
                case Status.TestError:
                case Status.UnhandledException:
                    return TestResult.Failure;
                case Status.Skipped:
                    return TestResult.Ignored;
                default:
                    throw new DefaultSwitchCaseUnreachableException();
            }
        }

        public static string ToLine(string hash, TestResult result, string commitMessage)
        {
            if (result == TestResult.Ignored)
                return null;

            return $"{hash.Substring(0, 8)} {result.ToAbbreviation()} {commitMessage} ({hash})";
        }
        public static (string, TestResult) FromLine(string line)
        {
            if (line == null) throw new ArgumentException(nameof(line));
            if (line.Length < 54) throw new FormatException($"Any line is expected to have at least 54 characters");
            if (line[8] != ' ') throw new FormatException($"The line '{line}' does not start with a 8-character hash. ");
            if (line[13] != ' ') throw new FormatException($"The line '{line}' does not start with a 8-character hash and a test result. ");

            string[] split = line.Split(' ');
            if (split.Length < 3)
                throw new FormatException($"Line '{line}' was expected to have a hash summary, test result, commit message and full hash");

            string hash = split.Last();
            if (hash.Length == 0 || hash[0] != '(' || hash.Last() != ')')
                throw new FormatException($"The full hash in '{line}' is invalid");

            hash = hash.Substring("(".Length, hash.Length - "()".Length);
            if (!GitCommandLine.IsValidCommitHash(hash))
                throw new FormatException($"The full hash in '{line}' is invalid");

            TestResult testResult;
            try
            {
                testResult = FromAbbreviation(split[1]);
            }
            catch (DefaultSwitchCaseUnreachableException)
            {
                throw new FormatException($"The test result in '{line}' is invalid");
            }

            return (hash, testResult);
        }
    }
}