using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Library.Locking.Tests
{
    [TestFixture]
    [Platform("Win")]
    public class RunLockTests
    {
        [Test]
        public void Test_That_We_Can_AcquireAndRelease()
        {
            string path = LockPath();
            LockAcquisition acquisition = RunLock.Acquire(path, TimeSpan.Zero);
            Assert.That(acquisition.Refusal, Is.Null);
            Assert.That(acquisition.Handle.Path, Is.EqualTo(path));
            Assert.That(File.Exists(path), Is.True);
            acquisition.Handle.Dispose();
            LockAcquisition reused = RunLock.Acquire(path, TimeSpan.Zero);
            Assert.That(reused.Refusal, Is.Null);
            reused.Handle.Dispose();
        }

        [Test]
        public void Test_That_We_Can_RefuseASecondAcquireWithAZeroBudgetWithTheSharedCode()
        {
            string path = LockPath();
            LockAcquisition first = RunLock.Acquire(path, TimeSpan.Zero);
            LockAcquisition second = RunLock.Acquire(path, TimeSpan.Zero);
            Assert.That(first.Refusal, Is.Null);
            Assert.That(second.Refusal, Is.Not.Null);
            Assert.That(second.Refusal.Name, Is.EqualTo(LockRefusalNames.WaitBudgetExpired));
            Assert.That(second.Refusal.ExitCode, Is.EqualTo(3));
            Assert.That(second.Refusal.Message, Does.Contain(path));
            Assert.That(second.Refusal.Message, Does.Contain("within 0 seconds"));
            first.Handle.Dispose();
        }

        [Test]
        public void Test_That_We_Can_AcquireWithASufficientBudgetAfterTheFirstHolderReleases()
        {
            string path = LockPath();
            LockAcquisition first = RunLock.Acquire(path, TimeSpan.Zero);
            Task release = Task.Run(() => ReleaseAfter(first.Handle, 400));
            Task<LockAcquisition> waiting = Task.Run(() => RunLock.Acquire(path, TimeSpan.FromSeconds(5)));
            Assert.That(waiting.Wait(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(release.Wait(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(waiting.Result.Refusal, Is.Null);
            waiting.Result.Handle.Dispose();
        }

        [Test]
        public void Test_That_We_Can_AcquireAfterAHolderProcessIsKilled()
        {
            string path = LockPath();
            using (Process holder = StartHolder(path))
            {
                Assert.That(holder.StandardOutput.ReadLine(), Is.EqualTo("held"));
                LockAcquisition whileHeld = RunLock.Acquire(path, TimeSpan.Zero);
                Assert.That(whileHeld.Refusal, Is.Not.Null);
                holder.Kill(true);
                holder.WaitForExit();
                LockAcquisition after = RunLock.Acquire(path, TimeSpan.FromSeconds(2));
                Assert.That(after.Refusal, Is.Null, "a lock held by a killed process must not block the next acquire");
                after.Handle.Dispose();
            }
        }

        [Test]
        public void Test_That_We_Can_ReleaseTheLockByDisposing()
        {
            string path = LockPath();
            LockAcquisition first = RunLock.Acquire(path, TimeSpan.Zero);
            LockAcquisition refused = RunLock.Acquire(path, TimeSpan.Zero);
            Assert.That(refused.Refusal, Is.Not.Null);
            first.Handle.Dispose();
            LockAcquisition granted = RunLock.Acquire(path, TimeSpan.Zero);
            Assert.That(granted.Refusal, Is.Null);
            granted.Handle.Dispose();
            granted.Handle.Dispose();
        }

        [Test]
        public void Test_That_We_Can_AcquireTheSamePathTwiceInOneProcessOnlyWhenTheFirstHandleIsDisposed()
        {
            string path = LockPath();
            LockAcquisition first = RunLock.Acquire(path, TimeSpan.Zero);
            LockAcquisition second = RunLock.Acquire(path, TimeSpan.Zero);
            Assert.That(second.Handle, Is.Null);
            Assert.That(second.Refusal.Name, Is.EqualTo(LockRefusalNames.WaitBudgetExpired));
            first.Handle.Dispose();
            LockAcquisition third = RunLock.Acquire(path, TimeSpan.Zero);
            Assert.That(third.Refusal, Is.Null);
            third.Handle.Dispose();
        }

        private static void ReleaseAfter(LockHandle handle, int milliseconds)
        {
            Thread.Sleep(milliseconds);
            handle.Dispose();
        }

        private static string LockPath()
        {
            return Path.Combine(AppContext.BaseDirectory, "lock-" + Guid.NewGuid().ToString("N") + ".lock");
        }

        private static Process StartHolder(string lockPath)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("dotnet");
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.WorkingDirectory = AppContext.BaseDirectory;
            startInfo.ArgumentList.Add(HolderAssemblyPath());
            startInfo.ArgumentList.Add(lockPath);
            Process process = new Process();
            process.StartInfo = startInfo;
            process.Start();
            return process;
        }

        private static string HolderAssemblyPath()
        {
            DirectoryInfo frameworkDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            string buildDirectory = frameworkDirectory.Parent.Parent.Parent.Parent.FullName;
            return Path.Combine(buildDirectory, "Coop.Library.Locking.Holder", "bin", frameworkDirectory.Parent.Name, frameworkDirectory.Name, "Coop.Library.Locking.Holder.dll");
        }
    }
}
