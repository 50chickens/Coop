using System;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Coop.Tool
{
    public static class HostStop
    {
        public const string StopRequest = "{\"coop-stop\":true}";

        public const int ConnectTimeoutMilliseconds = 2000;

        public const int StopTimeoutMilliseconds = 5000;

        public static bool Send(string pipeName)
        {
            Task<bool> sent = Task.Run(() => Answered(pipeName));
            try
            {
                return sent.Wait(StopTimeoutMilliseconds) && sent.Result;
            }
            catch (AggregateException)
            {
                return false;
            }
        }

        private static bool Answered(string pipeName)
        {
            using (NamedPipeClientStream client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
            {
                client.Connect(ConnectTimeoutMilliseconds);
                Frames.Write(client, StopRequest);
                Frames.Read(client);
                return true;
            }
        }
    }
}
