using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;
using JBSnorro.GitTools.CI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static CI.UI.NotificationIcon;

namespace CI.UI.Tests
{
    [TestClass]
    public class NotificationIconTests
    {
        static void Main(string[] args)
        {
            new DispatcherTests().TestDispatchReceipt();

            new NotificationIconTests().TestCancelButtonExistence();

            Console.WriteLine("Done");
            Console.ReadLine();
        }

        [TestMethod]
        public void TestIconStatusAfterBuildError()
        {
            using (var icon = new NotificationIcon(isVisible: false))
            {
                var log = new(Status, string)[]
                {
                    (Status.BuildError, "test")
                };

                Program.HandleCommit(log, icon);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Bad);
                Assert.AreEqual(actual: icon.Percentage, expected: 1);
                Assert.AreEqual(actual: icon.Text, expected: null);
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }

        [TestMethod]
        public void TestIconStatusAfterBuildingOneOutOfTwoProjects()
        {
            using (var icon = new NotificationIcon(isVisible: false))
            {
                IEnumerable<(Status, string)> getLog()
                {
                    yield return (Status.BuildSuccess, "test");

                    assertBeforeFinallyBlockInHandleCommit();
                };

                Program.HandleCommit(getLog, icon, projectCount: 2);

                void assertBeforeFinallyBlockInHandleCommit()
                {
                    Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Working);
                    Assert.IsTrue(icon.Text != null && icon.Text.StartsWith("1/2"));
                    Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit | NotificationIconContextMenuItems.Cancel);
                }
            }
        }
        [TestMethod]
        public void TestIconStatusAfterBuildingTwoOutOfTwoProjects()
        {
            using (var icon = new NotificationIcon(isVisible: false))
            {
                var log = new(Status, string)[]
                {
                    (Status.BuildSuccess, "test"),
                    (Status.BuildSuccess, "test"),
                };

                Program.HandleCommit(log, icon, projectCount: 2);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Ok);
                Assert.IsTrue(icon.Text != null && icon.Text.StartsWith("2/2"));
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }
        [TestMethod]
        public void TestIconStatusAfterTestSucceeds()
        {
            using (var icon = new NotificationIcon(isVisible: false))
            {
                var log = new(Status, string)[]
                {
                    (Status.TestSuccess, null),
                };

                Program.HandleCommit(log, icon);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Ok);
                Assert.AreEqual(1, icon.Percentage);
                Assert.IsTrue(icon.Text != null && icon.Text.StartsWith("1/"));
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }

        [TestMethod]
        public void TestIconStatusAfterTestFails()
        {
            using (var icon = new NotificationIcon(isVisible: false))
            {
                var log = new(Status, string)[]
                {
                    (Status.TestError, null),
                };

                Program.HandleCommit(log, icon);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Bad);
                Assert.AreEqual(1, icon.Percentage);
                Assert.IsTrue(icon.Text != null && icon.Text.StartsWith("1/"));
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }

        [TestMethod]
        public void TestIconStatusAfterOneTestFailsAndAnotherOneSucceeds()
        {
            using (var icon = new NotificationIcon(isVisible: false))
            {
                var log = new(Status, string)[]
                {
                    (Status.TestError, null),
                    (Status.TestSuccess, null),
                };

                Program.HandleCommit(log, icon);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Bad);
                Assert.AreEqual(1, icon.Percentage);
                Assert.IsTrue(icon.Text != null && icon.Text.StartsWith("1/"));
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }

        [TestMethod]
        public void TestIconStatusAfterOneTestSucceedsOneFailsAndAnotherOneSucceeds()
        {
            using (var icon = new NotificationIcon(isVisible: false))
            {
                var log = new(Status, string)[]
                {
                    (Status.TestSuccess, null),
                    (Status.TestError, null),
                    (Status.TestSuccess, null),
                };

                Program.HandleCommit(log, icon);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Bad);
                Assert.AreEqual(1, icon.Percentage);
                Assert.IsTrue(icon.Text != null && icon.Text.StartsWith("1/"));
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }

        [TestMethod]
        public void TestIconStatusAfterCancellation()
        {
            using (var icon = new NotificationIcon(isVisible: false))
            {
                IEnumerable<(Status, string)> getLog()
                {
                    yield return (Status.BuildSuccess, "test");

                    icon.RequestCancellation();

                    yield return (Status.Skipped, null); //dummy
                };

                Program.HandleCommit(getLog(), icon);

                Assert.AreEqual(actual: icon.Status, expected: NotificationIconStatus.Default);
                Assert.AreEqual(1, icon.Percentage);
                Assert.IsNull(icon.Text);
                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }


        [TestMethod]
        public void TestCancelButtonExistence()
        {
            using (var icon = new NotificationIcon(isVisible: false))
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

                Program.HandleCommit(getLog(), icon);

                Assert.AreEqual(actual: icon.ContextMenuItems, expected: NotificationIconContextMenuItems.Exit);
            }
        }
    }
}
