using System;
using Coop.Services.Surface;
using ModelContextProtocol.Protocol;

namespace Coop.Tool
{
    public static class Program
    {
        public const int NotCompletedExitCode = 1;

        public const string TemplateCommand = "template";

        public const string HostCommand = "host";

        public const string ProbeCommand = "probe";

        public const string StopCommand = "stop";

        public static int Main(string[] args)
        {
            SurfaceConfiguration configuration = SurfaceConfiguration.Resolve();
            if (args.Length == 1 && args[0] == TemplateCommand)
            {
                CallToolResult template = new TemplateTool(configuration).Template();
                Console.Out.WriteLine(Outcome.TextOf(template));
                return 0;
            }
            if (args.Length == 1 && args[0] == HostCommand)
            {
                new CompileHost(configuration, CompileHost.PipeNameOf()).Run();
                return 0;
            }
            if (args.Length == 1 && args[0] == ProbeCommand)
            {
                return HostProbe.Run(CompileHost.PipeNameOf(), configuration.Version);
            }
            if (args.Length == 1 && args[0] == StopCommand)
            {
                bool stopped = HostStop.Send(CompileHost.PipeNameOf());
                if (stopped == false)
                {
                    return NotCompletedExitCode;
                }
                return 0;
            }
            ParsedCommandLine parsed = CommandLine.Parse(args);
            if (parsed.Refusal != null)
            {
                Console.Out.WriteLine(Outcome.TextOf(ToolResults.Refusal(parsed.Refusal)));
                return NotCompletedExitCode;
            }
            string response = CompileClient.Request(CompileHost.PipeNameOf(), parsed.Arguments.ToJsonString());
            Console.Out.WriteLine(response);
            return Outcome.ExitCodeOfText(response);
        }
    }
}
