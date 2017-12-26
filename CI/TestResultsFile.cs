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
        /// Gets the estimate in seconds of how long processing the associated solution takes.
        /// </summary>
        public int Estimate { get; }
        /// <summary>
        /// Gets the number of tests reported by the last entry in this file.
        /// </summary>
        public int TestCount { get; }

        /// <summary>
        /// Opens or creates the file at the specified path and reads the existing hashes and results.
        /// </summary>
        public static TestResultsFile Read(string path)
        {
            Contract.Requires(!string.IsNullOrEmpty(path));

            if (Directory.Exists(path))
            {
                path = Path.Combine(path, RelativePath);
            }

            if (!File.Exists(path))
                Console.WriteLine("Creating " + path);


            var stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var timingEstimator = new TimingEstimator();
            var hashes = new Dictionary<string, TestResult>();
            int testCount = 0;
            try
            {
                var reader = new StreamReader(stream);
                while (!reader.EndOfStream)
                {
                    var (key, result, timing, testCountEntry) = TestResultExtensions.FromLine(reader.ReadLine());
                    hashes[key] = result;
                    timingEstimator.Add(timing, result);

                    // use latest all tests successful result, or, if larger, the number of failures an attempt after that
                    testCount = result == TestResult.Success ? testCountEntry : Math.Max(testCount, testCountEntry);
                }
            }
            catch
            {
                // release the stream, because an error has occurred and the file will otherwise not be disposed of
                // if no exception is thrown, the stream will be released through disposing the returned IDisposable
                stream.Dispose();
                throw;
            }

            return new TestResultsFile(new StreamWriter(stream), new ReadOnlyDictionary<string, TestResult>(hashes), timingEstimator.Estimate, testCount);
        }

        private TestResultsFile(StreamWriter stream, ReadOnlyDictionary<string, TestResult> hashes, int estimate, int testCount)
        {
            this.stream = stream;
            this.Hashes = hashes;
            this.Estimate = estimate;
            this.TestCount = testCount;
        }
        public static TestResultsFile TryReadFile(string path, out string errorMessage)
        {
            try
            {
                errorMessage = null;
                return Read(path);
            }
            catch (Exception e)
            {
                errorMessage = $"Error in reading file {path}.testresults. " + e.Message;
                return null;
            }
        }
        /// <summary>
        /// Writes the specified result and key to the current file.
        /// </summary>
        public void Append(string hash, TestResult result, string commitMessage, int timing, int testCount)
        {
            string line = TestResultExtensions.ToLine(hash, result, commitMessage, timing, testCount);
            if (line != null)
            {
                this.stream.WriteLine(line);
            }
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

        private const string format = "yy-MMM-dd hh:mm";
        private const int previewLength = 7;
        public static string ToLine(string hash, TestResult result, string commitMessage, int seconds, int testCount)
        {
            if (result == TestResult.Ignored)
                return null;

            return $"{DateTime.Now.ToString(format)} - {hash.Substring(0, previewLength)} - {result.ToAbbreviation()} - {commitMessage} - ({hash}) - {seconds}s - {testCount}";
        }
        public static (string Hash, TestResult Result, int Timing, int TestCount) FromLine(string line)
        {
            if (line == null) throw new ArgumentException(nameof(line));
            if (line.Substring(format.Length, 3) != " - ")
                throw new FormatException($"The line '{line}' does not start with a date in the format '{format}'. ");
            if (line.Substring(format.Length + " - ".Length + previewLength, 3) != " - ")
                throw new FormatException($"The line '{line}' does not start with a date and a {previewLength}-character hash. ");

            string[] split = line.Split(' ');
            if (split.Length < 10)
                throw new FormatException($"Line '{line}' is expected to have a date, hash summary, test result, commit message, full hash, timing and test count separated by ' - '");

            string hash = split.FirstOrDefault(s => s.StartsWith("("));
            if (hash == null || hash.Length == 0 || hash.Last() != ')')
                throw new FormatException($"The full hash in '{line}' is invalid");

            hash = hash.Substring("(".Length, hash.Length - "()".Length);
            if (!GitCommandLine.IsValidCommitHash(hash))
                throw new FormatException($"The full hash in '{line}' is invalid");

            string timingString = split[split.Length - 3];
            if (timingString.Length <= 1 || !timingString.EndsWith("s"))
                throw new FormatException($"The timing in '{line}' is invalid");
            if (!int.TryParse(timingString.Substring(0, timingString.Length - 1), out int timing))
                throw new FormatException($"The timing in '{line}' is invalid");

            string testCountString = split.Last();
            if (!int.TryParse(testCountString, out int testCount))
                throw new FormatException($"The test count in '{line}' is invalid");

            TestResult testResult;
            try
            {
                testResult = FromAbbreviation(split[5]);
            }
            catch (DefaultSwitchCaseUnreachableException)
            {
                throw new FormatException($"The test result in '{line}' is invalid");
            }

            return (hash, testResult, timing, testCount);
        }
    }
}