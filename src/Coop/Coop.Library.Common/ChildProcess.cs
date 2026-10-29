using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Coop.Library.Common
{
    public static class ChildProcess
    {
        public static ChildProcessOutcome Run(string executable, IReadOnlyList<string> arguments, string workingDirectory, TimeSpan deadline, IReadOnlyDictionary<string, string> environment)
        {
            long startTicks = Stopwatch.GetTimestamp();
            ChildProcessOutcome outcome = new ChildProcessOutcome();
            outcome.StandardOutput = "";
            outcome.StandardError = "";
            using (Process process = new Process())
            {
                process.StartInfo = BuildStartInfo(executable, arguments, workingDirectory, environment);
                outcome.Started = TryStart(process);
                if (outcome.Started)
                {
                    Capture(process, deadline, outcome);
                }
            }
            outcome.ElapsedMilliseconds = (long)Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
            return outcome;
        }

        private static ProcessStartInfo BuildStartInfo(string executable, IReadOnlyList<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string> environment)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(executable);
            startInfo.WorkingDirectory = workingDirectory;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            arguments.ToList().ForEach(argument => startInfo.ArgumentList.Add(argument));
            if (environment != null)
            {
                startInfo.Environment.Clear();
                environment.ToList().ForEach(entry => startInfo.Environment[entry.Key] = entry.Value);
            }
            return startInfo;
        }

        private static bool TryStart(Process process)
        {
            try
            {
                return process.Start();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private static void Capture(Process process, TimeSpan deadline, ChildProcessOutcome outcome)
        {
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            if (process.WaitForExit(MillisecondBudget(deadline)))
            {
                outcome.ExitStatus = process.ExitCode;
            }
            else
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
                process.WaitForExit();
                outcome.TimedOut = true;
            }
            outcome.StandardOutput = standardOutput.GetAwaiter().GetResult();
            outcome.StandardError = standardError.GetAwaiter().GetResult();
        }

        private static int MillisecondBudget(TimeSpan deadline)
        {
            if (deadline.TotalMilliseconds <= 0)
            {
                return 0;
            }
            if (deadline.TotalMilliseconds >= int.MaxValue)
            {
                return int.MaxValue;
            }
            return (int)deadline.TotalMilliseconds;
        }
    }
}
