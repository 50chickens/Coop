using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Coop.Library.Locking
{
    public static class RunLock
    {
        public const int RetryIntervalMilliseconds = 250;

        public const int WaitBudgetSeconds = 120;

        public const int RefusalExitCode = 3;

        private const int SharingViolation = unchecked((int)0x80070020);
        private const int LockViolation = unchecked((int)0x80070021);

        public static LockAcquisition Acquire(string path)
        {
            return Acquire(path, TimeSpan.FromSeconds(WaitBudgetSeconds));
        }

        public static LockAcquisition Acquire(string path, TimeSpan budget)
        {
            long startTicks = Stopwatch.GetTimestamp();
            while (true)
            {
                FileStream stream = TryOpen(path);
                if (stream != null)
                {
                    LockAcquisition acquired = new LockAcquisition();
                    acquired.Handle = new LockHandle(stream, path);
                    return acquired;
                }
                TimeSpan remaining = budget - Stopwatch.GetElapsedTime(startTicks);
                if (remaining <= TimeSpan.Zero)
                {
                    return Refused(path, budget);
                }
                Thread.Sleep(SleepMilliseconds(remaining));
            }
        }

        private static FileStream TryOpen(string path)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception) when (IsContention(exception))
            {
                return null;
            }
        }

        private static bool IsContention(IOException exception)
        {
            if (exception.HResult == SharingViolation)
            {
                return true;
            }
            return exception.HResult == LockViolation;
        }

        private static int SleepMilliseconds(TimeSpan remaining)
        {
            if (remaining.TotalMilliseconds > RetryIntervalMilliseconds)
            {
                return RetryIntervalMilliseconds;
            }
            return (int)Math.Ceiling(remaining.TotalMilliseconds);
        }

        private static LockAcquisition Refused(string path, TimeSpan budget)
        {
            LockRefusal refusal = new LockRefusal();
            refusal.Name = LockRefusalNames.WaitBudgetExpired;
            refusal.ExitCode = RefusalExitCode;
            refusal.Message = string.Format(CultureInfo.InvariantCulture, "the lock {0} was not acquired within {1} seconds", path, budget.TotalSeconds);
            LockAcquisition acquisition = new LockAcquisition();
            acquisition.Refusal = refusal;
            return acquisition;
        }
    }
}
