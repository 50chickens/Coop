using System;
using System.Threading;

namespace Coop.Library.Locking.Holder
{
    public static class Program
    {
        public static int Main(string[] arguments)
        {
            LockAcquisition acquisition = RunLock.Acquire(arguments[0]);
            if (acquisition.Refusal != null)
            {
                Console.Error.WriteLine(acquisition.Refusal.Message);
                return acquisition.Refusal.ExitCode;
            }
            using (acquisition.Handle)
            {
                Console.Out.WriteLine("held");
                Console.Out.Flush();
                Thread.Sleep(TimeSpan.FromSeconds(30));
            }
            return 0;
        }
    }
}
