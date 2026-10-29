using System;
using System.Collections.Generic;
using System.IO;
using Coop.Library.Execution;
using Microsoft.Extensions.AI;

namespace Coop.Services.Agent
{
    public static class CoopRunCSharp
    {
        public const string ToolName = "run_csharp";

        public const string ToolDescription = "Compiles one C# submission and runs it in a contained child process under a deadline, then returns one JSON string. The source is C# compiled as a single-file console application whose entry point is static int Main(string[] args); parameters arrive as key=value command-line arguments rather than being stitched into the source. Returns executionId, status, reused, cache, compile (success, diagnosticCount, diagnostics) and run (exitStatus, standardOutput, standardError, deadlineExpired, elapsedMilliseconds). A compile error, a non-zero exit and an expired deadline are outcomes in that JSON, not exceptions. The result is cached by source, parameters and deadline, so a repeated identical submission is served from the cache and reused says the timings came from that earlier execution and not from this call.";

        public static AIFunction CreateFunction(CoopExecutionOptions options)
        {
            return AIFunctionFactory.Create(
                (string source, Dictionary<string, string> parameters = null, int? timeoutSeconds = null) => Execute(options, source, parameters, timeoutSeconds),
                new AIFunctionFactoryOptions
                {
                    Name = ToolName,
                    Description = ToolDescription,
                });
        }

        public static string Execute(CoopExecutionOptions options, string source, Dictionary<string, string> parameters, int? timeoutSeconds)
        {
            ExecutionRequest request = new ExecutionRequest();
            request.Submission = new Submission();
            request.Submission.SourceText = source;
            request.Submission.Parameters = parameters;
            request.OutputDirectory = Path.Combine(options.WorkingRoot, Guid.NewGuid().ToString("N"));
            request.TimeoutSeconds = timeoutSeconds;
            if (request.TimeoutSeconds == null)
            {
                request.TimeoutSeconds = options.TimeoutSeconds;
            }
            ExecutionOutcome outcome = SubmissionExecutor.Execute(request);
            return CoopOutcomeJson.Write(outcome);
        }
    }
}
