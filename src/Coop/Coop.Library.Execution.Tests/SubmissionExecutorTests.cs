using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Coop.Library.Common;

namespace Coop.Library.Execution.Tests
{
    [TestFixture]
    public class SubmissionExecutorTests
    {
        private List<string> createdDirectories;

        [SetUp]
        public void SetUp()
        {
            createdDirectories = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            createdDirectories
                .Where(path => Directory.Exists(path))
                .ToList()
                .ForEach(DeleteWithRetry);
        }

        private static void DeleteWithRetry(string path)
        {
            int attempt = 0;
            while (attempt < 40)
            {
                attempt++;
                try
                {
                    Directory.Delete(path, true);
                    return;
                }
                catch (IOException exception)
                {
                    if (attempt == 40)
                    {
                        TestContext.Out.WriteLine($"left {path} in place: {exception.Message}");
                        return;
                    }
                    System.Threading.Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException exception)
                {
                    if (attempt == 40)
                    {
                        TestContext.Out.WriteLine($"left {path} in place: {exception.Message}");
                        return;
                    }
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        [Test]
        public void Test_That_We_Can_CaptureTheStandardOutputOfATrivialProgram()
        {
            ExecutionOutcome outcome = Execute(Source("hello from coop"), null, null);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatuses.Completed));
            Assert.That(outcome.ExitStatus, Is.EqualTo(0));
            Assert.That(outcome.StandardOutput, Is.EqualTo("hello from coop" + Environment.NewLine));
            Assert.That(outcome.StandardError, Is.EqualTo(""));
            Assert.That(outcome.DeadlineExpired, Is.False);
            Assert.That(outcome.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(outcome.Diagnostics, Is.Empty);
            Assert.That(outcome.Refusal, Is.Null);
            Assert.That(outcome.ExecutionId.Length, Is.EqualTo(32));
        }

        [Test]
        public void Test_That_We_Can_WriteTheArtefactWithItsRuntimeConfiguration()
        {
            ExecutionOutcome outcome = Execute(Source("configured"), null, null, out string outputDirectory);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatuses.Completed));
            Assert.That(File.Exists(Path.Combine(outputDirectory, "submission.dll")), Is.True);
            string runtimeConfiguration = File.ReadAllText(Path.Combine(outputDirectory, "submission.runtimeconfig.json"));
            Assert.That(runtimeConfiguration, Does.Contain("\"tfm\":\"net10.0\""));
            Assert.That(runtimeConfiguration, Does.Contain("\"name\":\"Microsoft.NETCore.App\""));
            Assert.That(runtimeConfiguration, Does.Contain("\"version\":\"10.0.0\""));
            Assert.That(runtimeConfiguration, Does.Contain("\"rollForward\":\"LatestMinor\""));
        }

        [Test]
        public void Test_That_We_Can_ReportAnExitStatusOfThree()
        {
            ExecutionOutcome outcome = Execute(SourceWithReturn(3), null, null);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatuses.Failed));
            Assert.That(outcome.ExitStatus, Is.EqualTo(3));
            Assert.That(outcome.DeadlineExpired, Is.False);
        }

        [Test]
        public void Test_That_We_Can_CaptureStandardErrorSeparatelyFromStandardOutput()
        {
            ExecutionOutcome outcome = Execute(StandardErrorSource, null, null);
            Assert.That(outcome.StandardOutput, Is.EqualTo("out" + Environment.NewLine));
            Assert.That(outcome.StandardError, Is.EqualTo("err" + Environment.NewLine));
            Assert.That(outcome.ExitStatus, Is.EqualTo(0));
        }

        [Test]
        public void Test_That_We_Can_CaptureOutputWrittenBeforeAFailure()
        {
            ExecutionOutcome outcome = Execute(WriteThenThrowSource, null, null);
            Assert.That(outcome.StandardOutput, Is.EqualTo("before the failure" + Environment.NewLine));
            Assert.That(outcome.StandardError, Does.Contain("boom"));
            Assert.That(outcome.ExitStatus, Is.Not.EqualTo(0));
            Assert.That(outcome.ExitStatus, Is.Not.Null);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatuses.Failed));
        }

        [Test]
        public void Test_That_We_Can_ReportCompileDiagnosticsWithoutStartingAChild()
        {
            string outputDirectory = NewOutputDirectory();
            ExecutionOutcome outcome = ExecuteUnknownIdentifier(outputDirectory);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatuses.Failed));
            Assert.That(outcome.Diagnostics, Is.Not.Empty);
            Assert.That(outcome.Diagnostics.Select(diagnostic => diagnostic.Id), Does.Contain("CS0103"));
            CompileDiagnostic diagnostic = outcome.Diagnostics.First();
            Assert.That(diagnostic.Severity, Is.EqualTo("Error"));
            Assert.That(diagnostic.FilePath, Is.EqualTo(SourceResolver.DefaultDiagnosticsPath));
            Assert.That(diagnostic.Line, Is.EqualTo(5));
            Assert.That(diagnostic.Column, Is.GreaterThan(0));
            Assert.That(diagnostic.Message, Does.Contain("NothingHere"));
            Assert.That(outcome.ExitStatus, Is.Null);
            Assert.That(outcome.ElapsedMilliseconds, Is.Null);
            Assert.That(outcome.ProcessorMilliseconds, Is.Null);
            Assert.That(outcome.PeakWorkingSetBytes, Is.Null);
            Assert.That(outcome.StandardOutput, Is.EqualTo(""));
            Assert.That(Directory.Exists(outputDirectory), Is.False);
        }

        [Test]
        public void Test_That_We_Can_RefuseAnAssemblyOutsideTheReferenceSet()
        {
            ExecutionOutcome outcome = Execute(OutsideAssemblySource, null, null);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatuses.Failed));
            Assert.That(outcome.ExitStatus, Is.Null);
            Assert.That(outcome.Diagnostics.Select(diagnostic => diagnostic.Id), Does.Contain("CS0234"));
            Assert.That(outcome.Diagnostics.Select(diagnostic => diagnostic.Message), Has.Some.Contains("CodeAnalysis"));
        }

        [Test]
        public void Test_That_We_Can_KillAProgramThatLoopsForeverAtTheDeadline()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ExecutionOutcome outcome = Execute(LoopForeverSource, null, 2, out string outputDirectory);
            stopwatch.Stop();
            TestContext.Out.WriteLine("timeout test wall clock: " + stopwatch.ElapsedMilliseconds + "ms, reported elapsed: " + outcome.ElapsedMilliseconds + "ms");
            Assert.That(outcome.DeadlineExpired, Is.True);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatuses.Failed));
            Assert.That(outcome.ExitStatus, Is.Null);
            Assert.That(outcome.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(1900));
            Assert.That(outcome.StandardOutput, Does.Contain("looping"));
            int processId = RecordedProcessId(outputDirectory);
            Assert.That(IsRunning(processId), Is.False);
        }

        [Test]
        public void Test_That_We_Can_DeliverParametersSortedByKeyWithValuesIntact()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters["zeta"] = "2";
            parameters["beta"] = "x=y z";
            parameters["alpha"] = "1";
            ExecutionOutcome outcome = Execute(EchoArgumentsSource, parameters, null);
            Assert.That(Lines(outcome.StandardOutput), Is.EqualTo(new string[] { "alpha=1", "beta=x=y z", "zeta=2" }));
        }

        [Test]
        public void Test_That_We_Can_SendNoArgumentsForNoParameters()
        {
            ExecutionOutcome outcome = Execute(CountArgumentsSource, new Dictionary<string, string>(), null);
            Assert.That(Lines(outcome.StandardOutput), Is.EqualTo(new string[] { "0" }));
        }

        [Test]
        public void Test_That_We_Can_RefuseASubmissionWithNeitherSourceField()
        {
            Submission submission = new Submission();
            submission.Parameters = new Dictionary<string, string>();
            ExecutionOutcome outcome = Execute(submission, null);
            Assert.That(outcome.Refusal, Is.Not.Null);
            Assert.That(outcome.Refusal.Name, Is.EqualTo(SubmissionRefusalNames.NoSourceSupplied));
            Assert.That(outcome.Refusal.Message, Does.Contain("sourceText"));
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatuses.Failed));
            Assert.That(outcome.ExitStatus, Is.Null);
            Assert.That(outcome.Diagnostics, Is.Empty);
            Assert.That(outcome.StandardOutput, Is.EqualTo(""));
            string json = JsonSerializer.Serialize(outcome, CoopJson.Options);
            Assert.That(json, Does.Contain("\"refusal\":{\"name\":\"submission.noSource\""));
        }

        [Test]
        public void Test_That_We_Can_PreferSourceTextWhenBothSourceFieldsAreSupplied()
        {
            string sourceDirectory = NewOutputDirectory();
            Directory.CreateDirectory(sourceDirectory);
            string sourcePath = Path.Combine(sourceDirectory, "labelled.cs");
            File.WriteAllText(sourcePath, Source("from the path"));

            Submission submission = new Submission();
            submission.SourceText = Source("from the text");
            submission.SourcePath = sourcePath;
            submission.Parameters = new Dictionary<string, string>();

            SourceResolution resolution = SourceResolver.Resolve(submission);
            Assert.That(resolution.RefusalName, Is.Null);
            Assert.That(resolution.CodeField, Is.EqualTo("sourceText"));
            Assert.That(resolution.DiagnosticsPath, Is.EqualTo(sourcePath));

            ExecutionOutcome outcome = Execute(submission, null);
            Assert.That(outcome.StandardOutput, Is.EqualTo("from the text" + Environment.NewLine));
            Assert.That(outcome.ExitStatus, Is.EqualTo(0));
        }

        [Test]
        public void Test_That_We_Can_ReadTheSourceFromAPathWhenNoTextIsSupplied()
        {
            string sourceDirectory = NewOutputDirectory();
            Directory.CreateDirectory(sourceDirectory);
            string sourcePath = Path.Combine(sourceDirectory, "program.cs");
            File.WriteAllText(sourcePath, Source("from the path"));

            Submission submission = new Submission();
            submission.SourcePath = sourcePath;
            submission.Parameters = new Dictionary<string, string>();

            SourceResolution resolution = SourceResolver.Resolve(submission);
            Assert.That(resolution.CodeField, Is.EqualTo("sourcePath"));

            ExecutionOutcome outcome = Execute(submission, null);
            Assert.That(outcome.StandardOutput, Is.EqualTo("from the path" + Environment.NewLine));
        }

        [Test]
        public void Test_That_We_Can_ClampADeadlineAboveTheCeiling()
        {
            Assert.That(ExecutionDeadline.FromSeconds(null).TotalSeconds, Is.EqualTo(60));
            Assert.That(ExecutionDeadline.FromSeconds(2).TotalSeconds, Is.EqualTo(2));
            Assert.That(ExecutionDeadline.FromSeconds(5000).TotalSeconds, Is.EqualTo(600));
            ExecutionOutcome outcome = Execute(Source("clamped"), null, 5000);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatuses.Completed));
            Assert.That(outcome.StandardOutput, Is.EqualTo("clamped" + Environment.NewLine));
        }

        [Test]
        public void Test_That_We_Can_SerialiseAnUnmeasuredNumberAsNull()
        {
            ExecutionOutcome outcome = ExecuteUnknownIdentifier(NewOutputDirectory());
            string json = JsonSerializer.Serialize(outcome, CoopJson.Options);
            TestContext.Out.WriteLine(json);
            Assert.That(json, Does.Contain("\"exitStatus\":null"));
            Assert.That(json, Does.Contain("\"elapsedMilliseconds\":null"));
            Assert.That(json, Does.Contain("\"processorMilliseconds\":null"));
            Assert.That(json, Does.Contain("\"peakWorkingSetBytes\":null"));
            Assert.That(json, Does.Not.Contain("unavailable"));
            Assert.That(json, Does.Not.Contain("\"reused\""));
            ExecutionOutcome restored = JsonSerializer.Deserialize<ExecutionOutcome>(json, CoopJson.Options);
            Assert.That(restored.ExitStatus, Is.Null);
            Assert.That(restored.ElapsedMilliseconds, Is.Null);
            Assert.That(restored.Diagnostics.Count, Is.EqualTo(outcome.Diagnostics.Count));
        }

        [Test]
        public void Test_That_We_Can_ReportProcessorTimeAndWorkingSetAsUnmeasuredBecauseTheSharedRunnerCreatesNoJobObject()
        {
            ExecutionOutcome outcome = Execute(Source("no containment"), null, null);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatuses.Completed));
            Assert.That(outcome.ExitStatus, Is.EqualTo(0));
            Assert.That(outcome.ProcessorMilliseconds, Is.Null);
            Assert.That(outcome.PeakWorkingSetBytes, Is.Null);
        }

        [Test]
        public void Test_That_We_Can_SerialiseAMeasuredNumberAsANumber()
        {
            ExecutionOutcome outcome = Execute(Source("measured"), null, null);
            string json = JsonSerializer.Serialize(outcome, CoopJson.Options);
            Assert.That(json, Does.Contain("\"exitStatus\":0"));
            Assert.That(json, Does.Not.Contain("\"elapsedMilliseconds\":null"));
            Assert.That(json, Does.Contain("\"status\":\"completed\""));
            Assert.That(json, Does.Not.Contain("\"reused\""));
            ExecutionOutcome restored = JsonSerializer.Deserialize<ExecutionOutcome>(json, CoopJson.Options);
            Assert.That(restored.ExitStatus, Is.EqualTo(0));
            Assert.That(restored.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Test_That_We_Can_CarryAReuseMarkerIntoTheOutcome()
        {
            ReuseMarker marker = new ReuseMarker();
            marker.ExecutionId = "0123456789abcdef0123456789abcdef";
            marker.RecordedAt = "2026-10-23T10:40:39.7720305+00:00";
            marker.RunnerVersion = "1.0.0+abc";
            marker.Hint = "the timings in this response are those of execution 0123";
            Submission submission = new Submission();
            submission.SourceText = Source("reused");
            submission.Parameters = new Dictionary<string, string>();
            ExecutionRequest request = new ExecutionRequest();
            request.Submission = submission;
            request.OutputDirectory = NewOutputDirectory();
            request.Reused = marker;
            ExecutionOutcome outcome = SubmissionExecutor.Execute(request);
            Assert.That(outcome.Reused, Is.SameAs(marker));
            string json = JsonSerializer.Serialize(outcome, CoopJson.Options);
            Assert.That(json, Does.Contain("\"reused\":{\"executionId\":\"0123456789abcdef0123456789abcdef\""));
        }

        [Test]
        public void Test_That_We_Can_UseTheExecutionIdTheCallerSupplied()
        {
            Submission submission = new Submission();
            submission.SourceText = Source("identified");
            submission.Parameters = new Dictionary<string, string>();
            ExecutionRequest request = new ExecutionRequest();
            request.ExecutionId = "41122878127a4658b652d85b3338b07c";
            request.Submission = submission;
            request.OutputDirectory = NewOutputDirectory();
            ExecutionOutcome outcome = SubmissionExecutor.Execute(request);
            Assert.That(outcome.ExecutionId, Is.EqualTo("41122878127a4658b652d85b3338b07c"));
        }

        [Test]
        public void Test_That_We_Can_Refuse_A_Missing_Reference_By_Name()
        {
            string missing = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"), "missing.dll");
            Submission submission = new Submission();
            submission.SourceText = Source("reference");
            submission.Parameters = new Dictionary<string, string>();
            ExecutionRequest request = new ExecutionRequest();
            request.Submission = submission;
            request.OutputDirectory = NewOutputDirectory();
            request.AdditionalReferences = new string[] { missing };
            ExecutionOutcome outcome = SubmissionExecutor.Execute(request);
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(ExecutionStatuses.Failed));
                Assert.That(outcome.Refusal.Name, Is.EqualTo(SubmissionRefusalNames.MissingReference));
                Assert.That(outcome.Refusal.Message, Does.Contain(missing));
            });
        }

        [Test]
        public void Test_That_We_Can_Compile_Against_A_Dependency_That_Sits_Beside_The_Reference()
        {
            Submission submission = new Submission();
            submission.SourceText = ShaSource;
            submission.Parameters = new Dictionary<string, string>();
            ExecutionRequest request = new ExecutionRequest();
            request.Submission = submission;
            request.OutputDirectory = NewOutputDirectory();
            request.AdditionalReferences = new string[] { typeof(SubmissionCompiler).Assembly.Location };
            ExecutionOutcome outcome = SubmissionExecutor.Execute(request);
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(ExecutionStatuses.Completed));
                Assert.That(outcome.Diagnostics, Is.Empty);
                Assert.That(outcome.StandardOutput.Trim(), Is.EqualTo(Coop.Library.Common.Sha.HexOfUtf8("abc")));
            });
        }

        [Test]
        public void Test_That_We_Can_Leave_A_Declared_Dependency_That_Is_Absent_To_The_Compiler()
        {
            string folder = NewDirectory();
            string reference = Path.Combine(folder, "Coop.Library.Execution.dll");
            File.Copy(typeof(SubmissionCompiler).Assembly.Location, reference);
            Submission submission = new Submission();
            submission.SourceText = ShaSource;
            submission.Parameters = new Dictionary<string, string>();
            ExecutionRequest request = new ExecutionRequest();
            request.Submission = submission;
            request.OutputDirectory = NewOutputDirectory();
            request.AdditionalReferences = new string[] { reference };
            ExecutionOutcome outcome = SubmissionExecutor.Execute(request);
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(ExecutionStatuses.Failed));
                Assert.That(outcome.Diagnostics.Select(diagnostic => diagnostic.Id), Does.Contain("CS0234"));
                Assert.That(outcome.Refusal, Is.Null);
            });
        }

        [Test]
        public void Test_That_We_Can_EachProduceASeparateOutcomeForTheSameSource()
        {
            Dictionary<string, string> first = new Dictionary<string, string>();
            first["value"] = "one";
            Dictionary<string, string> second = new Dictionary<string, string>();
            second["value"] = "two";
            ExecutionOutcome firstOutcome = Execute(EchoArgumentsSource, first, null);
            ExecutionOutcome secondOutcome = Execute(EchoArgumentsSource, second, null);
            Assert.That(firstOutcome.ExecutionId, Is.Not.EqualTo(secondOutcome.ExecutionId));
            Assert.That(Lines(firstOutcome.StandardOutput), Is.EqualTo(new string[] { "value=one" }));
            Assert.That(Lines(secondOutcome.StandardOutput), Is.EqualTo(new string[] { "value=two" }));
        }

        private static string ShaSource
        {
            get
            {
                return "public static class Program\n"
                    + "{\n"
                    + "    public static int Main(string[] args)\n"
                    + "    {\n"
                    + "        System.Console.WriteLine(Coop.Library.Common.Sha.HexOfUtf8(\"abc\"));\n"
                    + "        return 0;\n"
                    + "    }\n"
                    + "}\n";
            }
        }

        private static string StandardErrorSource
        {
            get
            {
                return "public static class Program\n"
                    + "{\n"
                    + "    public static int Main(string[] args)\n"
                    + "    {\n"
                    + "        System.Console.Out.WriteLine(\"out\");\n"
                    + "        System.Console.Error.WriteLine(\"err\");\n"
                    + "        return 0;\n"
                    + "    }\n"
                    + "}\n";
            }
        }

        private static string WriteThenThrowSource
        {
            get
            {
                return "public static class Program\n"
                    + "{\n"
                    + "    public static int Main(string[] args)\n"
                    + "    {\n"
                    + "        System.Console.WriteLine(\"before the failure\");\n"
                    + "        System.Console.Error.WriteLine(\"boom\");\n"
                    + "        throw new System.InvalidOperationException(\"boom\");\n"
                    + "    }\n"
                    + "}\n";
            }
        }

        private static string LoopForeverSource
        {
            get
            {
                return "public static class Program\n"
                    + "{\n"
                    + "    public static int Main(string[] args)\n"
                    + "    {\n"
                    + "        System.IO.File.WriteAllText(\"child.pid\", System.Environment.ProcessId.ToString());\n"
                    + "        System.Console.WriteLine(\"looping\");\n"
                    + "        while (true)\n"
                    + "        {\n"
                    + "            System.Threading.Thread.Sleep(50);\n"
                    + "        }\n"
                    + "    }\n"
                    + "}\n";
            }
        }

        private static string EchoArgumentsSource
        {
            get
            {
                return "public static class Program\n"
                    + "{\n"
                    + "    public static int Main(string[] args)\n"
                    + "    {\n"
                    + "        System.Array.ForEach(args, argument => System.Console.WriteLine(argument));\n"
                    + "        return 0;\n"
                    + "    }\n"
                    + "}\n";
            }
        }

        private static string OutsideAssemblySource
        {
            get
            {
                return "public static class Program\n"
                    + "{\n"
                    + "    public static int Main(string[] args)\n"
                    + "    {\n"
                    + "        Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree tree = null;\n"
                    + "        return tree == null ? 0 : 1;\n"
                    + "    }\n"
                    + "}\n";
            }
        }

        private static string CountArgumentsSource
        {
            get
            {
                return "public static class Program\n"
                    + "{\n"
                    + "    public static int Main(string[] args)\n"
                    + "    {\n"
                    + "        System.Console.WriteLine(args.Length.ToString());\n"
                    + "        return 0;\n"
                    + "    }\n"
                    + "}\n";
            }
        }

        private static string Source(string line)
        {
            return "public static class Program\n"
                + "{\n"
                + "    public static int Main(string[] args)\n"
                + "    {\n"
                + "        System.Console.WriteLine(\"" + line + "\");\n"
                + "        return 0;\n"
                + "    }\n"
                + "}\n";
        }

        private static string SourceWithReturn(int exitStatus)
        {
            return "public static class Program\n"
                + "{\n"
                + "    public static int Main(string[] args)\n"
                + "    {\n"
                + "        return " + exitStatus + ";\n"
                + "    }\n"
                + "}\n";
        }

        private string NewOutputDirectory()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "execution-" + Guid.NewGuid().ToString("N"));
            createdDirectories.Add(path);
            return path;
        }

        private string NewDirectory()
        {
            string path = NewOutputDirectory();
            Directory.CreateDirectory(path);
            return path;
        }

        private ExecutionOutcome ExecuteUnknownIdentifier(string outputDirectory)
        {
            Submission submission = new Submission();
            submission.SourceText = "public static class Program\n"
                + "{\n"
                + "    public static int Main(string[] args)\n"
                + "    {\n"
                + "        return NothingHere();\n"
                + "    }\n"
                + "}\n";
            submission.Parameters = new Dictionary<string, string>();
            ExecutionRequest request = new ExecutionRequest();
            request.Submission = submission;
            request.OutputDirectory = outputDirectory;
            return SubmissionExecutor.Execute(request);
        }

        private ExecutionOutcome Execute(string sourceText, Dictionary<string, string> parameters, int? timeoutSeconds)
        {
            Submission submission = new Submission();
            submission.SourceText = sourceText;
            submission.Parameters = parameters;
            return Execute(submission, timeoutSeconds);
        }

        private ExecutionOutcome Execute(Submission submission, int? timeoutSeconds)
        {
            ExecutionOutcome outcome = Execute(submission, timeoutSeconds, out string outputDirectory);
            return outcome;
        }

        private ExecutionOutcome Execute(string sourceText, Dictionary<string, string> parameters, int? timeoutSeconds, out string outputDirectory)
        {
            Submission submission = new Submission();
            submission.SourceText = sourceText;
            submission.Parameters = parameters;
            return Execute(submission, timeoutSeconds, out outputDirectory);
        }

        private ExecutionOutcome Execute(Submission submission, int? timeoutSeconds, out string outputDirectory)
        {
            outputDirectory = NewOutputDirectory();
            ExecutionRequest request = new ExecutionRequest();
            request.Submission = submission;
            request.OutputDirectory = outputDirectory;
            request.TimeoutSeconds = timeoutSeconds;
            return SubmissionExecutor.Execute(request);
        }

        private static int RecordedProcessId(string outputDirectory)
        {
            return int.Parse(File.ReadAllText(Path.Combine(outputDirectory, "child.pid")).Trim());
        }

        private static bool IsRunning(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    return !process.WaitForExit(5000);
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static string[] Lines(string text)
        {
            return text
                .Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .ToArray();
        }
    }
}
