using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Coop.Library.Common.Tests.Helper
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                return 64;
            }

            switch (args[0])
            {
                case "exit":
                    return ExitCode(args);
                case "bulk-stderr":
                    return BulkStandardError();
                case "spawn-grandchild":
                    return SpawnGrandchild(args);
                case "env":
                    return PrintEnvironment(args);
                case "env-pair":
                    return PrintEnvironmentPair(args);
                case "wait":
                    return Wait();
                case "spam":
                    return Spam();
                default:
                    return 65;
            }
        }

        private static int ExitCode(string[] args)
        {
            return int.Parse(args[1], CultureInfo.InvariantCulture);
        }

        private static int BulkStandardError()
        {
            Console.Error.Write(new string('e', 300000));
            Console.Error.Flush();
            Console.Out.Write("stdout-marker");
            Console.Out.Flush();
            return 0;
        }

        private static int SpawnGrandchild(string[] args)
        {
            string pidFile = args[1];
            ProcessStartInfo startInfo = new ProcessStartInfo("dotnet");
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.ArgumentList.Add(SelfPath());
            startInfo.ArgumentList.Add("wait");

            using (Process grandchild = Process.Start(startInfo))
            {
                File.WriteAllText(pidFile, grandchild.Id.ToString(CultureInfo.InvariantCulture));
            }

            Thread.Sleep(TimeSpan.FromSeconds(120));
            return 0;
        }

        private static int PrintEnvironment(string[] args)
        {
            Console.Out.Write(Environment.GetEnvironmentVariable(args[1]) ?? "");
            Console.Out.Flush();
            return 0;
        }

        private static int PrintEnvironmentPair(string[] args)
        {
            string first = Environment.GetEnvironmentVariable(args[1]) ?? "";
            string second = Environment.GetEnvironmentVariable(args[2]) ?? "";
            Console.Out.Write("[" + first + "][" + second + "]");
            Console.Out.Flush();
            return 0;
        }

        private static int Wait()
        {
            Thread.Sleep(TimeSpan.FromSeconds(120));
            return 0;
        }

        private static int Spam()
        {
            while (true)
            {
                Console.Out.WriteLine("eeeeeeee");
                Console.Out.Flush();
            }
        }

        private static string SelfPath()
        {
            return typeof(Program).Assembly.Location;
        }
    }
}
