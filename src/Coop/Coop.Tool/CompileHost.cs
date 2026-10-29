using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Coop.Library.Execution;
using Coop.Services.Surface;
using ModelContextProtocol.Protocol;

namespace Coop.Tool
{
    [SupportedOSPlatform("windows")]
    public sealed class CompileHost
    {
        public const string PipeName = "coop-compile";

        public const string PipeNameVariable = "COOP_PIPE_NAME";

        public const string StopCommand = "coop-stop";

        private const int ExitDelayMilliseconds = 500;

        private const int Instances = 4;

        private const int BufferBytes = 65536;

        private const string WarmSource = "System.Console.WriteLine(\"warm\");";

        private const string WarmFolder = "warm";

        private readonly SurfaceConfiguration configuration;

        private readonly string pipeName;

        private readonly ExecuteTool tool;

        public CompileHost(SurfaceConfiguration configuration, string pipeName)
        {
            this.configuration = configuration;
            this.pipeName = pipeName;
            tool = new ExecuteTool(configuration);
        }

        public void Run()
        {
            Warm();
            Announce();
            Task[] workers = Enumerable.Range(0, Instances)
                .Select(worker => Task.Run(() => ServeForever()))
                .ToArray();
            Task.WaitAll(workers);
        }

        public void Warm()
        {
            string directory = Path.Combine(configuration.DataRoot, WarmFolder);
            SubmissionCompiler.Compile(WarmSource, SourceResolver.DefaultDiagnosticsPath, directory, new string[0]);
        }

        public void ServeOnce(NamedPipeServerStream server)
        {
            server.WaitForConnection();
            string request = Frames.Read(server);
            Frames.Write(server, Answer(request));
            server.WaitForPipeDrain();
            server.Disconnect();
        }

        public static string PipeNameOf()
        {
            string configured = Environment.GetEnvironmentVariable(PipeNameVariable);
            if (string.IsNullOrWhiteSpace(configured))
            {
                return PipeName;
            }
            return configured;
        }

        public static NamedPipeServerStream Open(string pipeName)
        {
            PipeSecurity security = new PipeSecurity();
            security.SetSecurityDescriptorSddlForm(SecurityDescriptor(), AccessControlSections.All);
            return NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, Instances, PipeTransmissionMode.Byte, PipeOptions.None, BufferBytes, BufferBytes, security);
        }

        private void ServeForever()
        {
            while (true)
            {
                using (NamedPipeServerStream server = Open(pipeName))
                {
                    ServeOnce(server);
                }
            }
        }

        private string Answer(string request)
        {
            JsonObject arguments = JsonNode.Parse(request).AsObject();
            if (arguments.ContainsKey(StopCommand))
            {
                Task.Run(() => Exit());
                JsonObject stopped = new JsonObject();
                stopped["status"] = StopCommand;
                stopped["runnerVersion"] = configuration.Version;
                return stopped.ToJsonString();
            }
            CallToolResult result = tool.Execute(arguments);
            return Outcome.TextOf(result);
        }

        private static void Exit()
        {
            Thread.Sleep(ExitDelayMilliseconds);
            Environment.Exit(0);
        }

        private void Announce()
        {
            Console.Error.WriteLine("coop-host " + configuration.Version);
            Console.Error.WriteLine("pipe: " + PipePath());
            Console.Error.WriteLine("data root: " + configuration.DataRoot);
            Console.Error.WriteLine("ready");
            Console.Error.Flush();
        }

        private string PipePath()
        {
            return @"\\.\pipe\" + pipeName;
        }

        private static string SecurityDescriptor()
        {
            string identity = WindowsIdentity.GetCurrent().User.Value;
            return "G:SYD:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GWGR;;;" + identity + ")";
        }
    }
}
