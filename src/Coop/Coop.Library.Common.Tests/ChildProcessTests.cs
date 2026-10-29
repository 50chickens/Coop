using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Coop.Library.Common;

namespace Coop.Library.Common.Tests
{
    [TestFixture]
    public class ChildProcessTests
    {
        [Test]
        public void Test_That_We_Can_ObserveANonZeroExit()
        {
            ChildProcessOutcome outcome = RunHelper(new string[] { "exit", "3" }, TimeSpan.FromSeconds(60), null);
            Assert.That(outcome.Started, Is.True);
            Assert.That(outcome.TimedOut, Is.False);
            Assert.That(outcome.ExitStatus, Is.EqualTo(3));
            Assert.That(outcome.StandardOutput, Is.EqualTo(""));
            Assert.That(outcome.StandardError, Is.EqualTo(""));
            Assert.That(outcome.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Test_That_We_Can_ReturnNotStartedForAMissingExecutable()
        {
            ChildProcessOutcome outcome = ChildProcess.Run("coop-absent-executable-6f2c", new string[0], WorkingDirectory, TimeSpan.FromSeconds(60), null);
            Assert.That(outcome.Started, Is.False);
            Assert.That(outcome.TimedOut, Is.False);
            Assert.That(outcome.ExitStatus, Is.Null);
            Assert.That(outcome.StandardOutput, Is.EqualTo(""));
            Assert.That(outcome.StandardError, Is.EqualTo(""));
        }

        [Test]
        public void Test_That_We_Can_CaptureLargeStandardErrorWithoutDeadlock()
        {
            ChildProcessOutcome outcome = RunBulkStandardError();
            Assert.That(outcome.Started, Is.True);
            Assert.That(outcome.TimedOut, Is.False);
            Assert.That(outcome.ExitStatus, Is.EqualTo(0));
            Assert.That(outcome.StandardError.Length, Is.GreaterThanOrEqualTo(100000));
            Assert.That(outcome.StandardOutput, Is.EqualTo("stdout-marker"));
        }

        [Test]
        public void Test_That_We_Can_KillTheProcessTreeAtTheDeadline()
        {
            string pidFile = Path.Combine(WorkingDirectory, "grandchild-" + Guid.NewGuid().ToString("N") + ".pid");
            ChildProcessOutcome outcome = RunTreeChild(pidFile, TimeSpan.FromSeconds(15));
            Assert.That(outcome.Started, Is.True);
            Assert.That(outcome.TimedOut, Is.True);
            Assert.That(outcome.ExitStatus, Is.Null);
            Assert.That(File.Exists(pidFile), Is.True);
            int grandchildId = int.Parse(File.ReadAllText(pidFile).Trim(), CultureInfo.InvariantCulture);
            bool running = IsRunning(grandchildId);
            File.Delete(pidFile);
            Assert.That(running, Is.False);
        }

        [Test]
        public void Test_That_We_Can_KillAChildThatIsStillWritingAtTheDeadline()
        {
            ChildProcessOutcome outcome = RunHelper(new string[] { "spam" }, TimeSpan.FromSeconds(2), null);
            Assert.That(outcome.Started, Is.True);
            Assert.That(outcome.TimedOut, Is.True);
            Assert.That(outcome.ExitStatus, Is.Null);
            Assert.That(outcome.StandardOutput.Length, Is.GreaterThan(0));
            Assert.That(outcome.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(1900));
        }

        [Test]
        public void Test_That_We_Can_ReplaceTheChildEnvironment()
        {
            Environment.SetEnvironmentVariable("COOP_PARENT_ONLY_VALUE", "leaked");
            Dictionary<string, string> environment = new Dictionary<string, string>();
            environment["COOP_CHILD_VALUE"] = "expected";
            if (OperatingSystem.IsWindows())
            {
                environment["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot");
            }
            else
            {
                environment["PATH"] = Environment.GetEnvironmentVariable("PATH");
            }
            ChildProcessOutcome outcome = RunHelper(new string[] { "env-pair", "COOP_CHILD_VALUE", "COOP_PARENT_ONLY_VALUE" }, TimeSpan.FromSeconds(60), environment);
            Assert.That(outcome.StandardOutput, Does.Contain("expected"));
            Assert.That(outcome.StandardOutput, Does.Not.Contain("leaked"));
        }

        [Test]
        public void Test_That_We_Can_InheritTheParentEnvironmentWhenNoneIsGiven()
        {
            Environment.SetEnvironmentVariable("COOP_PARENT_ONLY_VALUE", "inherited");
            ChildProcessOutcome outcome = RunHelper(new string[] { "env", "COOP_PARENT_ONLY_VALUE" }, TimeSpan.FromSeconds(60), null);
            Assert.That(outcome.StandardOutput, Does.Contain("inherited"));
        }

        private static string WorkingDirectory
        {
            get
            {
                return TestContext.CurrentContext.WorkDirectory;
            }
        }

        private static string[] HelperArguments(string[] arguments)
        {
            List<string> full = new List<string>();
            full.Add(HelperAssemblyPath());
            full.AddRange(arguments);
            return full.ToArray();
        }

        private static string HelperAssemblyPath()
        {
            DirectoryInfo frameworkDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            string buildDirectory = frameworkDirectory.Parent.Parent.Parent.Parent.FullName;
            return Path.Combine(buildDirectory, "Coop.Library.Common.Tests.Helper", "bin", frameworkDirectory.Parent.Name, frameworkDirectory.Name, "Coop.Library.Common.Tests.Helper.dll");
        }

        private static ChildProcessOutcome RunHelper(string[] arguments, TimeSpan deadline, IReadOnlyDictionary<string, string> environment)
        {
            return ChildProcess.Run("dotnet", HelperArguments(arguments), WorkingDirectory, deadline, environment);
        }

        private static ChildProcessOutcome RunBulkStandardError()
        {
            return RunHelper(new string[] { "bulk-stderr" }, TimeSpan.FromSeconds(120), null);
        }

        private static ChildProcessOutcome RunTreeChild(string pidFile, TimeSpan deadline)
        {
            return RunHelper(new string[] { "spawn-grandchild", pidFile }, deadline, null);
        }

        private static bool IsRunning(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    return !process.WaitForExit(2000);
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
