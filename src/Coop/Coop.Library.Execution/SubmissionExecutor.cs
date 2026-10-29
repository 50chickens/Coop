using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coop.Library.Common;

namespace Coop.Library.Execution
{
    public static class SubmissionExecutor
    {
        public static ExecutionOutcome Execute(ExecutionRequest request)
        {
            ExecutionOutcome outcome = NewOutcome(request);
            SourceResolution source = SourceResolver.Resolve(request.Submission);
            if (source.RefusalName != null)
            {
                outcome.Refusal = new ExecutionRefusal();
                outcome.Refusal.Name = source.RefusalName;
                outcome.Refusal.Message = source.RefusalMessage;
                return outcome;
            }
            string missingReference = MissingReferenceOf(request.AdditionalReferences);
            if (missingReference != null)
            {
                outcome.Refusal = new ExecutionRefusal();
                outcome.Refusal.Name = SubmissionRefusalNames.MissingReference;
                outcome.Refusal.Message = "the request names the reference '" + missingReference + "', which does not exist";
                return outcome;
            }
            List<string> references = ReferenceClosure.Resolve(request.AdditionalReferences);
            CompilationOutcome compilation = SubmissionCompiler.Compile(source.Code, source.DiagnosticsPath, request.OutputDirectory, references.ToArray());
            if (!compilation.Success)
            {
                outcome.Diagnostics = compilation.Diagnostics;
                return outcome;
            }
            CopyReferences(references, compilation.AssemblyPath);
            ChildProcessOutcome process = ChildProcess.Run("dotnet", CommandLine(compilation.AssemblyPath, request.Submission.Parameters), request.OutputDirectory, ExecutionDeadline.FromSeconds(request.TimeoutSeconds), null);
            return Completed(outcome, process);
        }

        private static void CopyReferences(List<string> references, string assemblyPath)
        {
            string destinationFolder = Path.GetDirectoryName(assemblyPath);
            references
                .ForEach(path => File.Copy(path, Path.Combine(destinationFolder, Path.GetFileName(path)), true));
        }

        private static string MissingReferenceOf(string[] additionalReferences)
        {
            if (additionalReferences == null)
            {
                return null;
            }
            return additionalReferences.FirstOrDefault(path => File.Exists(path) == false);
        }

        private static ExecutionOutcome NewOutcome(ExecutionRequest request)
        {
            ExecutionOutcome outcome = new ExecutionOutcome();
            outcome.ExecutionId = ExecutionIdOf(request.ExecutionId);
            outcome.Status = ExecutionStatuses.Failed;
            outcome.StandardOutput = "";
            outcome.StandardError = "";
            outcome.Diagnostics = new List<CompileDiagnostic>();
            outcome.Reused = request.Reused;
            return outcome;
        }

        private static string ExecutionIdOf(string requested)
        {
            if (!string.IsNullOrEmpty(requested))
            {
                return requested;
            }
            return Guid.NewGuid().ToString("N");
        }

        private static List<string> CommandLine(string assemblyPath, Dictionary<string, string> parameters)
        {
            List<string> arguments = new List<string>();
            arguments.Add("exec");
            arguments.Add(assemblyPath);
            arguments.AddRange(ParameterArguments.Build(parameters));
            return arguments;
        }

        private static ExecutionOutcome Completed(ExecutionOutcome outcome, ChildProcessOutcome process)
        {
            outcome.StandardOutput = process.StandardOutput;
            outcome.StandardError = process.StandardError;
            if (!process.Started)
            {
                return outcome;
            }
            outcome.ElapsedMilliseconds = process.ElapsedMilliseconds;
            if (process.TimedOut)
            {
                outcome.DeadlineExpired = true;
                return outcome;
            }
            outcome.ExitStatus = process.ExitStatus;
            if (process.ExitStatus == 0)
            {
                outcome.Status = ExecutionStatuses.Completed;
            }
            return outcome;
        }
    }
}
