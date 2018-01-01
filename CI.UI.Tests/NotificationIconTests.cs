using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JBSnorro.GitTools.CI;
using NUnit.Framework;
using static CI.UI.NotificationIcon;

namespace CI.UI.Tests
{
    [TestFixture]
    public class NotificationIconTests
    {
        static void Main(string[] args)
        {
            new NotificationIconTests().RetryButtonRemovedOnInvocation();
            new DispatcherTests().MessagesAreHandledAfterCancellation();


            Console.WriteLine("Done");
            Console.ReadLine();
        }
        [SetUp]
        public void ClearParentMessages()
        {
            ParentFailedTracker.Clear();
        }

        [Test]
        public void TestIconStatusAfterBuildError()
        {
            using (var icon = new NotificationIcon())
            {
                var log = new(Status, string)[]
                {
                    (Status.BuildError, "test")
                };

                Program.HandleCommit(new MockCopyBuildTestSolutions(log), icon);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Bad);
                Assert.AreEqual(actual: icon.Percentage, expected: 1);
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }
        [Test]
        public void TestIconStatusAfterBuildingOneOutOfTwoProjects()
        {
            using (var icon = new NotificationIcon())
            {
                IEnumerable<(Status, string)> getLog()
                {
                    yield return (Status.BuildSuccess, "test");

                    assertBeforeFinallyBlockInHandleCommit();
                };

                Program.HandleCommit(new MockCopyBuildTestSolutions(getLog, projectCount: 2), icon);

                void assertBeforeFinallyBlockInHandleCommit()
                {
                    Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Working);
                    Assert.IsTrue(icon.Text != null && icon.Text.StartsWith("1/2"));
                    Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit | NotificationIconContextMenuItems.Cancel);
                }
            }
        }
        [Test]
        public void TestIconStatusAfterBuildingTwoOutOfTwoProjects()
        {
            using (var icon = new NotificationIcon())
            {
                var log = new(Status, string)[]
                {
                    (Status.BuildSuccess, "test"),
                    (Status.BuildSuccess, "test"),
                };

                Program.HandleCommit(new MockCopyBuildTestSolutions(log, projectCount: 2), icon);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Ok);
                Assert.IsTrue(icon.Text != null && icon.Text.StartsWith("2/2"));
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }
        [Test]
        public void TestIconStatusAfterTestSucceeds()
        {
            using (var icon = new NotificationIcon())
            {
                var log = new(Status, string)[]
                {
                    (Status.TestSuccess, null),
                };

                Program.HandleCommit(new MockCopyBuildTestSolutions(log), icon);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Ok);
                Assert.AreEqual(1, icon.Percentage);
                Assert.IsTrue(icon.Text != null && icon.Text.StartsWith("1/"));
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }
        [Test]
        public void TestIconStatusAfterTestFails()
        {
            using (var icon = new NotificationIcon())
            {
                var log = new(Status, string)[]
                {
                    (Status.TestError, null),
                };

                Program.HandleCommit(new MockCopyBuildTestSolutions(log), icon);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Bad);
                Assert.AreEqual(1, icon.Percentage);
                Assert.IsTrue(icon.Text != null && icon.Text.StartsWith("1/"));
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }
        [Test]
        public void TestIconStatusAfterOneTestFailsAndAnotherOneSucceeds()
        {
            using (var icon = new NotificationIcon())
            {
                var log = new(Status, string)[]
                {
                    (Status.TestError, null),
                    (Status.TestSuccess, null),
                };

                Program.HandleCommit(new MockCopyBuildTestSolutions(log), icon);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Bad);
                Assert.AreEqual(1, icon.Percentage);
                Assert.IsTrue(icon.Text != null && icon.Text.StartsWith("1/"));
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }
        [Test]
        public void TestIconStatusAfterOneTestSucceedsOneFailsAndAnotherOneSucceeds()
        {
            using (var icon = new NotificationIcon())
            {
                var log = new(Status, string)[]
                {
                    (Status.TestSuccess, null),
                    (Status.TestError, null),
                    (Status.TestSuccess, null),
                };

                Program.HandleCommit(new MockCopyBuildTestSolutions(log), icon);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Bad);
                Assert.AreEqual(1, icon.Percentage);
                Assert.IsTrue(icon.Text != null && icon.Text.StartsWith("1/"));
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }
        [Test]
        public void TestIconStatusAfterCancellation()
        {
            using (var tokenSource = new CancellationTokenSource())
            using (var icon = new NotificationIcon())
            {
                IEnumerable<(Status, string)> getLog()
                {
                    yield return (Status.BuildSuccess, "test");

                    tokenSource.Cancel();

                    yield return (Status.Skipped, null); //dummy
                };

                Program.HandleCommit(new MockCopyBuildTestSolutions(getLog), icon, externalCancellationToken: tokenSource.Token);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Default);
                Assert.AreEqual(1, icon.Percentage);
                Assert.IsNull(icon.Text);
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }
        [Test]
        public void TestCancelButtonExistence()
        {
            using (var icon = new NotificationIcon())
            {
                IEnumerable<(Status, string)> getLog()
                {
                    yield return (Status.BuildSuccess, "test");
                    Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit | NotificationIconContextMenuItems.Cancel);
                    yield return (Status.TestSuccess, "test");
                    Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit | NotificationIconContextMenuItems.Cancel);
                    yield return (Status.TestSuccess, "test");
                    Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit | NotificationIconContextMenuItems.Cancel);
                    yield return (Status.TestError, "test");
                    Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit | NotificationIconContextMenuItems.Cancel);
                    yield return (Status.TestError, "test");
                    Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit | NotificationIconContextMenuItems.Cancel);
                };


                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);

                Program.HandleCommit(new MockCopyBuildTestSolutions(getLog), icon);

                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }

        [Test]
        public void SkipBecauseParentFailed()
        {
            using (var icon = new NotificationIcon())
            {
                Program.HandleCommit(new MockCopyBuildTestSolutions(Status.ParentFailed), icon);

                Assert.AreEqual(icon.Status, NotificationIconStatus.BadParent);
            }
        }
        [Test]
        public void RetryButtonIsAddedOnParentFailed()
        {
            using (var icon = new NotificationIcon())
            {
                Program.HandleCommit(new MockCopyBuildTestSolutions(Status.ParentFailed), icon);

                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit | NotificationIconContextMenuItems.DisregardTestResults);
            }
        }
        [Test]
        public void FailedParentIsAddedToTracker()
        {
            using (var icon = new NotificationIcon())
            {
                Program.HandleCommit(new MockCopyBuildTestSolutions(Status.ParentFailed), icon);
            }

            Assert.AreEqual(actual: ParentFailedTracker.Count, expected: 1);
        }

        [Test]
        public void RetryButtonRehandlesFailedParentMessage()
        {
            using (var icon = new NotificationIcon())
            {
                bool logHandled = false;
                IEnumerable<(Status, string)> getLog()
                {
                    logHandled = true;
                    yield break;
                };

                Program.HandleCommit(new MockCopyBuildTestSolutions(Status.ParentFailed, getLog, secondPreworkStatus: Status.Success), icon);

                Assert.IsFalse(logHandled);

                ParentFailedTracker.RedoCanceledMessagesBecauseParentFailed(icon);

                Assert.IsTrue(logHandled);
            }
        }

        [Test]
        public void RetryButtonDoesntTriggerLogIfSecondPreworkFailsToo()
        {
            using (var icon = new NotificationIcon())
            {
                bool logHandled = false;
                IEnumerable<(Status, string)> getLog()
                {
                    logHandled = true;
                    yield break;
                };

                Program.HandleCommit(new MockCopyBuildTestSolutions(Status.ParentFailed, getLog, secondPreworkStatus: Status.ArgumentError), icon);

                Assert.IsFalse(logHandled);

                ParentFailedTracker.RedoCanceledMessagesBecauseParentFailed(icon);

                Assert.IsFalse(logHandled);
            }
        }
        [Test]
        public void RetryButtonRemovedOnInvocation()
        {
            using (var icon = new NotificationIcon())
            {
                Program.HandleCommit(new MockCopyBuildTestSolutions(Status.ParentFailed, secondPreworkStatus: Status.ArgumentError), icon);

                ParentFailedTracker.RedoCanceledMessagesBecauseParentFailed(icon);

                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }
    }
}
