using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Coop.Library.Artefacts;
using Coop.Library.Common;
using Coop.Library.Execution;
using Coop.Library.Hashing;
using Coop.Library.Locking;
using Coop.Library.Records;
using Microsoft.CodeAnalysis.CSharp;
using ModelContextProtocol.Protocol;

namespace Coop.Services.Surface
{
    public sealed class ExecuteTool
    {
        private const string TargetFramework = "net10.0";

        private const string LanguageVersion = "Latest";

        private const string OutputKind = "ConsoleApplication";

        private const string OptimizationLevel = "Debug";

        private const string MissingNamespaceId = "CS0234";

        private const string MissingTypeId = "CS0246";

        private readonly SurfaceConfiguration configuration;

        private readonly ArtefactStore artefacts;

        private readonly ResultStore results;

        private readonly RecordStore records;

        public ExecuteTool(SurfaceConfiguration configuration)
        {
            this.configuration = configuration;
            artefacts = new ArtefactStore(configuration.ArtefactStoreRoot);
            results = new ResultStore(configuration.ArtefactStoreRoot, configuration.Version);
            records = new RecordStore(configuration.RecordStoreRoot);
        }

        public CallToolResult Execute(JsonObject arguments)
        {
            ExecuteArguments parsed = ExecuteArguments.Parse(arguments);
            if (parsed.Refusal != null)
            {
                return ToolResults.Refusal(parsed.Refusal);
            }
            LockAcquisition acquisition = RunLock.Acquire(configuration.LockPath);
            if (acquisition.Refusal != null)
            {
                return ToolResults.Refusal(Refusals.Reason(acquisition.Refusal.Message));
            }
            string executionId = Guid.NewGuid().ToString("N");
            ExecutionReport report;
            using (acquisition.Handle)
            {
                report = Run(executionId, parsed);
            }
            report.RunnerVersion = configuration.Version;
            return ToolResults.Json(report.ToJsonObject());
        }

        private ExecutionReport Run(string executionId, ExecuteArguments arguments)
        {
            string artefactKey = ArtefactKeyOf(arguments);
            string resultKey = ResultKeyOf(artefactKey, arguments);
            ArtefactLookup artefact = artefacts.Read(artefactKey);
            ResultLookup stored = results.Read(resultKey);
            if (stored.Found)
            {
                return Reused(executionId, arguments, artefact, stored, artefactKey, resultKey);
            }
            ExecutionRecord running = RecordOf(executionId, arguments, null);
            running.Status = RecordStatuses.Running;
            records.Write(running);
            CompileReport compile = new CompileReport();
            compile.Diagnostics = new List<CompileDiagnostic>();
            string assemblyPath;
            string workingDirectory;
            if (artefact.Found)
            {
                assemblyPath = artefact.AssemblyPath;
                workingDirectory = artefact.DirectoryPath;
                compile.Success = true;
            }
            else
            {
                string stagingDirectory = artefacts.NewStagingDirectory();
                long startTicks = Stopwatch.GetTimestamp();
                CompilationOutcome compilation = SubmissionCompiler.Compile(arguments.Source, SourceResolver.DefaultDiagnosticsPath, stagingDirectory, arguments.References.ToArray());
                compile.ElapsedMilliseconds = (long)Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
                compile.Success = compilation.Success;
                compile.Diagnostics = compilation.Diagnostics;
                if (compilation.Success == false)
                {
                    if (NamesMissingMember(arguments.References, compilation.Diagnostics))
                    {
                        compile.MissingSiblings = ReferenceClosure.Missing(arguments.References);
                        compile.UndeclaredSiblings = ReferenceClosure.Undeclared(arguments.References);
                    }
                    return Terminal(executionId, arguments, compile, null, artefactKey, resultKey, artefact.Found);
                }
                CopyReferences(arguments.References, stagingDirectory);
                ArtefactLookup claimed = artefacts.Claim(artefactKey, stagingDirectory);
                assemblyPath = claimed.AssemblyPath;
                workingDirectory = claimed.DirectoryPath;
            }
            ChildProcessOutcome process = ChildProcess.Run("dotnet", CommandLineOf(assemblyPath, arguments.Parameters), workingDirectory, ExecutionDeadline.FromSeconds(arguments.TimeoutSeconds), null);
            ExecutionOutcome outcome = OutcomeOf(executionId, process);
            if (process.Started && outcome.DeadlineExpired == false)
            {
                results.Write(ResultOf(resultKey, artefactKey, executionId, outcome));
            }
            return Terminal(executionId, arguments, compile, outcome, artefactKey, resultKey, artefact.Found);
        }

        private ExecutionReport Reused(string executionId, ExecuteArguments arguments, ArtefactLookup artefact, ResultLookup stored, string artefactKey, string resultKey)
        {
            ExecutionOutcome outcome = stored.Reuse(executionId);
            ExecutionRecord record = RecordOf(executionId, arguments, outcome);
            records.Write(record);
            ExecutionReport report = new ExecutionReport();
            report.Outcome = outcome;
            report.Compile = CompileReport.Reused(outcome);
            report.ArtefactKey = artefactKey;
            report.ResultKey = resultKey;
            report.ArtefactReused = artefact.Found;
            report.ResultReused = true;
            return report;
        }

        private ExecutionReport Terminal(string executionId, ExecuteArguments arguments, CompileReport compile, ExecutionOutcome outcome, string artefactKey, string resultKey, bool artefactReused)
        {
            ExecutionOutcome reported = outcome;
            if (reported == null)
            {
                reported = FailedOutcome(executionId, compile.Diagnostics);
            }
            records.Write(RecordOf(executionId, arguments, reported));
            ExecutionReport report = new ExecutionReport();
            report.Outcome = reported;
            report.Compile = compile;
            report.ArtefactKey = artefactKey;
            report.ResultKey = resultKey;
            report.ArtefactReused = artefactReused;
            report.ResultReused = false;
            return report;
        }

        private ExecutionRecord RecordOf(string executionId, ExecuteArguments arguments, ExecutionOutcome outcome)
        {
            Submission submission = new Submission();
            submission.SourceText = arguments.Source;
            submission.Parameters = arguments.Parameters;
            ExecutionRecord record = new ExecutionRecord();
            record.ExecutionId = executionId;
            record.Name = arguments.Name;
            record.Description = arguments.Description;
            record.Category = arguments.Category;
            record.Timestamp = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
            record.Submission = submission;
            record.ShouldRunAsync = false;
            record.Packages = new List<RecordPackage>();
            record.Response = outcome;
            if (outcome == null)
            {
                record.Status = RecordStatuses.Running;
                record.Reused = null;
                return record;
            }
            record.Status = outcome.Status;
            record.Reused = outcome.Reused;
            return record;
        }

        private static ExecutionOutcome FailedOutcome(string executionId, List<CompileDiagnostic> diagnostics)
        {
            ExecutionOutcome outcome = new ExecutionOutcome();
            outcome.ExecutionId = executionId;
            outcome.Status = ExecutionStatuses.Failed;
            outcome.StandardOutput = "";
            outcome.StandardError = "";
            outcome.DeadlineExpired = false;
            outcome.Diagnostics = diagnostics;
            return outcome;
        }

        private static ExecutionOutcome OutcomeOf(string executionId, ChildProcessOutcome process)
        {
            ExecutionOutcome outcome = new ExecutionOutcome();
            outcome.ExecutionId = executionId;
            outcome.Status = ExecutionStatuses.Failed;
            outcome.StandardOutput = process.StandardOutput;
            outcome.StandardError = process.StandardError;
            outcome.Diagnostics = new List<CompileDiagnostic>();
            if (process.Started == false)
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

        private static bool NamesMissingMember(List<string> references, List<CompileDiagnostic> diagnostics)
        {
            if (references.Count == 0)
            {
                return false;
            }
            return diagnostics.Any(diagnostic => diagnostic.Id == MissingNamespaceId || diagnostic.Id == MissingTypeId);
        }

        private static void CopyReferences(List<string> references, string directory)
        {
            references.ForEach(reference => File.Copy(reference, Path.Combine(directory, Path.GetFileName(reference)), true));
        }

        private static List<string> CommandLineOf(string assemblyPath, Dictionary<string, string> parameters)
        {
            List<string> arguments = new List<string>();
            arguments.Add("exec");
            arguments.Add(assemblyPath);
            arguments.AddRange(ParameterArguments.Build(parameters));
            return arguments;
        }

        private ResultRecord ResultOf(string resultKey, string artefactKey, string executionId, ExecutionOutcome outcome)
        {
            ResultRecord record = new ResultRecord();
            record.ResultKey = resultKey;
            record.ArtefactKey = artefactKey;
            record.RunnerVersion = configuration.Version;
            record.ExecutionId = executionId;
            record.Outcome = outcome;
            return record;
        }

        private string ArtefactKeyOf(ExecuteArguments arguments)
        {
            ArtefactKeyRequest request = new ArtefactKeyRequest();
            request.SourceText = arguments.Source;
            request.TargetFramework = TargetFramework;
            request.RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier;
            request.CompilerVersion = typeof(CSharpCompilation).Assembly.GetName().Version.ToString();
            request.LanguageVersion = LanguageVersion;
            request.OutputKind = OutputKind;
            request.OptimizationLevel = OptimizationLevel;
            request.ReferenceRuntimeVersion = Environment.Version.ToString();
            request.References = arguments.References.Select(path => ReferenceOf(path)).ToList();
            KeyOutcome outcome = ArtefactKeys.Of(request);
            return outcome.Key;
        }

        private static KeyReference ReferenceOf(string path)
        {
            KeyReference reference = new KeyReference();
            reference.Path = path;
            reference.FileName = Path.GetFileName(path);
            reference.ContentHash = Sha.HexOfFile(path);
            return reference;
        }

        private string ResultKeyOf(string artefactKey, ExecuteArguments arguments)
        {
            ResultKeyRequest request = new ResultKeyRequest();
            request.ArtefactKey = artefactKey;
            request.BuildVersion = configuration.Version;
            request.Parameters = arguments.Parameters;
            KeyOutcome outcome = ResultKeys.Of(request);
            return outcome.Key;
        }
    }
}
