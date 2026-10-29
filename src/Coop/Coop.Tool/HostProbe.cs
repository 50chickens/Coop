using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Coop.Tool
{
    public static class HostProbe
    {
        public const int Matching = 0;

        public const int Absent = 2;

        public const int Stale = 3;

        public const int ProbeTimeoutMilliseconds = 5000;

        public const int ConnectTimeoutMilliseconds = 2000;

        public const string ProbeRequest = "{}";

        public const string Unstated = "no version";

        public static int Run(string pipeName, string ownVersion)
        {
            string serving = ServingVersion(pipeName);
            if (serving == null)
            {
                Console.Error.WriteLine("coop-host probe: no host answered on " + PathOf(pipeName) + " within " + ProbeTimeoutMilliseconds + " milliseconds");
                return Absent;
            }
            if (serving == ownVersion)
            {
                return Matching;
            }
            Console.Error.WriteLine("coop-host probe: the host on " + PathOf(pipeName) + " is " + serving + " rather than " + ownVersion);
            return Stale;
        }

        private static string ServingVersion(string pipeName)
        {
            string answer = Answer(pipeName);
            if (answer == null)
            {
                return null;
            }
            JsonNode version = JsonNode.Parse(answer)["runnerVersion"];
            if (version == null)
            {
                return Unstated;
            }
            return version.GetValue<string>();
        }

        private static string Answer(string pipeName)
        {
            Task<string> read = Task.Run(() => Read(pipeName));
            try
            {
                if (read.Wait(ProbeTimeoutMilliseconds) == false)
                {
                    return null;
                }
                return read.Result;
            }
            catch (AggregateException)
            {
                return null;
            }
        }

        private static string Read(string pipeName)
        {
            using (NamedPipeClientStream client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
            {
                client.Connect(ConnectTimeoutMilliseconds);
                Frames.Write(client, ProbeRequest);
                return Frames.Read(client);
            }
        }

        private static string PathOf(string pipeName)
        {
            return @"\\.\pipe\" + pipeName;
        }
    }
}
