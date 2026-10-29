using System;
using System.IO;
using System.IO.Pipes;
using Coop.Services.Surface;

namespace Coop.Tool
{
    public static class CompileClient
    {
        public const int ConnectTimeoutMilliseconds = 5000;

        public static string Request(string pipeName, string text)
        {
            using (NamedPipeClientStream client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
            {
                try
                {
                    client.Connect(ConnectTimeoutMilliseconds);
                    Frames.Write(client, text);
                    return Frames.Read(client);
                }
                catch (Exception exception) when (exception is TimeoutException || exception is IOException || exception is UnauthorizedAccessException)
                {
                    string reason = "no host answered on " + @"\\.\pipe\" + pipeName + ": " + exception.Message;
                    return Outcome.TextOf(ToolResults.Refusal(Refusals.Reason(reason)));
                }
            }
        }
    }
}
