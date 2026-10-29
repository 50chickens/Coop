using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Coop.Library.Common;
using Coop.Library.Execution;

namespace Coop.Library.Artefacts.Tests
{
    [TestFixture]
    public class ResultStoreTests
    {
        private const string RunnerVersion = "0.1.0-alpha.1";

        private List<string> createdRoots;

        [SetUp]
        public void SetUp()
        {
            createdRoots = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            createdRoots
                .Where(path => Directory.Exists(path))
                .ToList()
                .ForEach(path => Directory.Delete(path, true));
        }

        [Test]
        public void Test_That_We_Can_StoreAResultAndReadItBack()
        {
            ResultStore store = new ResultStore(NewRoot(), RunnerVersion);
            string resultKey = Key("result one");
            ResultRecord record = Record(resultKey, FreshOutcome("producer"));

            store.Write(record);

            ResultLookup lookup = store.Read(resultKey);
            Assert.That(lookup.Found, Is.True);
            Assert.That(lookup.ResultKey, Is.EqualTo(resultKey));
            Assert.That(lookup.ArtefactKey, Is.EqualTo(record.ArtefactKey));
            Assert.That(lookup.ProducingExecutionId, Is.EqualTo("producer"));
            Assert.That(lookup.RunnerVersion, Is.EqualTo(RunnerVersion));
            Assert.That(DateTimeOffset.TryParse(lookup.RecordedAt, out DateTimeOffset recorded), Is.True);
            Assert.That(recorded.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(File.Exists(store.DocumentPathOf(resultKey)), Is.True);
        }

        [Test]
        public void Test_That_We_Can_MissOnAResultKeyThatIsNotStored()
        {
            ResultStore store = new ResultStore(NewRoot(), RunnerVersion);
            string resultKey = Key("never stored");

            ResultLookup lookup = store.Read(resultKey);

            Assert.That(lookup.Found, Is.False);
            Assert.That(lookup.ProducingExecutionId, Is.Null);
            Assert.That(File.Exists(store.DocumentPathOf(resultKey)), Is.False);
        }

        [Test]
        public void Test_That_We_Can_MarkAReusedOutcomeWithTheProducingExecution()
        {
            ResultStore store = new ResultStore(NewRoot(), RunnerVersion);
            string resultKey = Key("reused");
            store.Write(Record(resultKey, FreshOutcome("producer")));

            ExecutionOutcome reused = store.Read(resultKey).Reuse("this run");

            Assert.That(reused.Reused, Is.Not.Null);
            Assert.That(reused.Reused.ExecutionId, Is.EqualTo("producer"));
            Assert.That(reused.Reused.RunnerVersion, Is.EqualTo(RunnerVersion));
            Assert.That(reused.Reused.RecordedAt, Is.EqualTo(store.Read(resultKey).RecordedAt));
            Assert.That(reused.Reused.Hint, Does.Contain("producer"));
            Assert.That(reused.Reused.Hint, Does.Contain("not of this run"));
            Assert.That(reused.ExecutionId, Is.EqualTo("this run"));
            Assert.That(reused.StandardOutput, Is.EqualTo("output of producer"));
        }

        [Test]
        public void Test_That_We_Can_LeaveAFreshOutcomeWithoutAMarker()
        {
            ResultStore store = new ResultStore(NewRoot(), RunnerVersion);
            string resultKey = Key("fresh");
            ResultRecord record = Record(resultKey, FreshOutcome("producer"));

            store.Write(record);

            Assert.That(record.Outcome.Reused, Is.Null);
            JsonDocument document = JsonDocument.Parse(File.ReadAllText(store.DocumentPathOf(resultKey)));
            Assert.That(document.RootElement.GetProperty("outcome").TryGetProperty("reused", out JsonElement ignored), Is.False);
            Assert.That(store.Read(resultKey).Reuse("this run").Reused, Is.Not.Null);
        }

        [Test]
        public void Test_That_We_Can_RefuseToStoreAnOutcomeThatCarriesAReuseMarker()
        {
            ResultStore store = new ResultStore(NewRoot(), RunnerVersion);
            string resultKey = Key("already marked");
            ExecutionOutcome marked = FreshOutcome("producer");
            store.Write(Record(resultKey, marked));
            ExecutionOutcome reused = store.Read(resultKey).Reuse("this run");
            ResultRecord second = Record(Key("second"), reused);

            Assert.That(reused.Reused, Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => store.Write(second));
            Assert.That(File.Exists(store.DocumentPathOf(second.ResultKey)), Is.False);
        }

        [Test]
        public void Test_That_We_Can_ReachAStoredOutcomeOnlyThroughAReuseMarker()
        {
            List<string> properties = typeof(ResultLookup)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.PropertyType == typeof(ExecutionOutcome))
                .Select(property => property.Name)
                .ToList();
            List<string> methods = typeof(ResultLookup)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.ReturnType == typeof(ExecutionOutcome))
                .Select(method => method.Name)
                .ToList();

            Assert.That(properties, Is.Empty);
            Assert.That(methods, Is.EqualTo(new string[] { "Reuse" }));
        }

        [Test]
        public void Test_That_We_Can_RefuseToReuseAResultKeyThatIsNotStored()
        {
            ResultStore store = new ResultStore(NewRoot(), RunnerVersion);
            ResultLookup lookup = store.Read(Key("absent"));
            Assert.That(lookup.Found, Is.False);
            Assert.Throws<InvalidOperationException>(() => lookup.Reuse("this run"));
        }

        [Test]
        public void Test_That_We_Can_TreatATruncatedResultDocumentAsAMiss()
        {
            ResultStore store = new ResultStore(NewRoot(), RunnerVersion);
            string resultKey = Key("truncated");
            store.Write(Record(resultKey, FreshOutcome("producer")));
            string documentPath = store.DocumentPathOf(resultKey);
            string complete = File.ReadAllText(documentPath);
            string partial = complete.Substring(0, complete.Length / 2);
            File.WriteAllText(documentPath, partial);

            Assert.That(File.Exists(documentPath), Is.True);
            Assert.That(partial.Length, Is.GreaterThan(0));
            Assert.That(partial, Is.Not.EqualTo(complete));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ResultRecord>(partial, CoopJson.Options));

            Assert.That(store.Read(resultKey).Found, Is.False);
        }

        [Test]
        public void Test_That_We_Can_MissWhenTheDocumentWasRecordedByAnotherRunnerVersion()
        {
            string root = NewRoot();
            string resultKey = Key("other build");
            ResultRecord record = Record(resultKey, FreshOutcome("producer"));
            record.RunnerVersion = "0.0.9";
            new ResultStore(root, "0.0.9").Write(record);

            ResultLookup lookup = new ResultStore(root, RunnerVersion).Read(resultKey);

            Assert.That(lookup.Found, Is.False);
            Assert.That(new ResultStore(root, "0.0.9").Read(resultKey).Found, Is.True);
        }

        [Test]
        public void Test_That_We_Can_RefuseAResultDocumentWithNoOutcome()
        {
            ResultStore store = new ResultStore(NewRoot(), RunnerVersion);
            string resultKey = Key("no outcome");
            string documentPath = store.DocumentPathOf(resultKey);
            Directory.CreateDirectory(Path.GetDirectoryName(documentPath));
            File.WriteAllText(documentPath, "{\"resultKey\":\"" + resultKey + "\",\"runnerVersion\":\"" + RunnerVersion + "\"}");

            Assert.That(File.Exists(documentPath), Is.True);
            Assert.That(store.Read(resultKey).Found, Is.False);
        }

        [Test]
        public void Test_That_We_Can_LeaveOneReadableResultWhenTwoWritersRaceOnOneKey()
        {
            ResultStore store = new ResultStore(NewRoot(), RunnerVersion);
            string resultKey = Key("raced");

            Task first = Task.Run(() => store.Write(Record(resultKey, FreshOutcome("first"))));
            Task second = Task.Run(() => store.Write(Record(resultKey, FreshOutcome("second"))));
            Task.WaitAll(first, second);

            ResultLookup lookup = store.Read(resultKey);
            Assert.That(lookup.Found, Is.True);
            Assert.That(lookup.ProducingExecutionId, Is.AnyOf("first", "second"));
            Assert.That(lookup.Reuse("this run").StandardOutput, Is.AnyOf("output of first", "output of second"));
            Assert.That(Directory.GetFiles(Path.Combine(store.Root, "results")).Length, Is.EqualTo(1));
        }

        [Test]
        public void Test_That_We_Can_RoundTripACompletedOutcomeWithEveryFieldIntact()
        {
            ResultStore store = new ResultStore(NewRoot(), RunnerVersion);
            string resultKey = Key("completed");
            store.Write(Record(resultKey, CompletedOutcome("producer")));

            ExecutionOutcome reused = store.Read(resultKey).Reuse("this run");

            Assert.That(reused.ExecutionId, Is.EqualTo("this run"));
            Assert.That(reused.Status, Is.EqualTo(ExecutionStatuses.Completed));
            Assert.That(reused.ExitStatus, Is.EqualTo(0));
            Assert.That(reused.StandardOutput, Is.EqualTo("hello" + Environment.NewLine));
            Assert.That(reused.StandardError, Is.EqualTo("a warning"));
            Assert.That(reused.DeadlineExpired, Is.False);
            Assert.That(reused.ElapsedMilliseconds, Is.EqualTo(12));
            Assert.That(reused.ProcessorMilliseconds, Is.EqualTo(7));
            Assert.That(reused.PeakWorkingSetBytes, Is.EqualTo(4096));
            Assert.That(reused.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(reused.Diagnostics[0].Id, Is.EqualTo("CS0103"));
            Assert.That(reused.Diagnostics[0].Severity, Is.EqualTo("Error"));
            Assert.That(reused.Diagnostics[0].Message, Is.EqualTo("The name does not exist"));
            Assert.That(reused.Diagnostics[0].FilePath, Is.EqualTo("submission.cs"));
            Assert.That(reused.Diagnostics[0].Line, Is.EqualTo(3));
            Assert.That(reused.Diagnostics[0].Column, Is.EqualTo(4));
            Assert.That(reused.Refusal, Is.Null);
            Assert.That(reused.Reused, Is.Not.Null);
        }

        [Test]
        public void Test_That_We_Can_RoundTripAFailedOutcomeWithItsNullsIntact()
        {
            ResultStore store = new ResultStore(NewRoot(), RunnerVersion);
            string resultKey = Key("failed");
            ExecutionOutcome outcome = new ExecutionOutcome();
            outcome.ExecutionId = "producer";
            outcome.Status = ExecutionStatuses.Failed;
            outcome.StandardOutput = "";
            store.Write(Record(resultKey, outcome));

            ExecutionOutcome reused = store.Read(resultKey).Reuse("this run");

            Assert.That(reused.Status, Is.EqualTo(ExecutionStatuses.Failed));
            Assert.That(reused.ExitStatus, Is.Null);
            Assert.That(reused.StandardError, Is.Null);
            Assert.That(reused.ElapsedMilliseconds, Is.Null);
            Assert.That(reused.ProcessorMilliseconds, Is.Null);
            Assert.That(reused.PeakWorkingSetBytes, Is.Null);
            Assert.That(reused.Diagnostics, Is.Null);
            Assert.That(reused.Refusal, Is.Null);
            Assert.That(reused.DeadlineExpired, Is.False);
            Assert.That(reused.StandardOutput, Is.EqualTo(""));
            Assert.That(reused.Reused.ExecutionId, Is.EqualTo("producer"));
        }

        private static string Key(string material)
        {
            return Sha.HexOfUtf8(material);
        }

        private static ResultRecord Record(string resultKey, ExecutionOutcome outcome)
        {
            ResultRecord record = new ResultRecord();
            record.ResultKey = resultKey;
            record.ArtefactKey = Key("artefact");
            record.RunnerVersion = RunnerVersion;
            record.ExecutionId = outcome.ExecutionId;
            record.Outcome = outcome;
            return record;
        }

        private static ExecutionOutcome FreshOutcome(string executionId)
        {
            ExecutionOutcome outcome = new ExecutionOutcome();
            outcome.ExecutionId = executionId;
            outcome.Status = ExecutionStatuses.Completed;
            outcome.ExitStatus = 0;
            outcome.StandardOutput = "output of " + executionId;
            outcome.StandardError = "";
            outcome.Diagnostics = new List<CompileDiagnostic>();
            return outcome;
        }

        private static ExecutionOutcome CompletedOutcome(string executionId)
        {
            CompileDiagnostic diagnostic = new CompileDiagnostic();
            diagnostic.Id = "CS0103";
            diagnostic.Severity = "Error";
            diagnostic.Message = "The name does not exist";
            diagnostic.FilePath = "submission.cs";
            diagnostic.Line = 3;
            diagnostic.Column = 4;
            ExecutionOutcome outcome = new ExecutionOutcome();
            outcome.ExecutionId = executionId;
            outcome.Status = ExecutionStatuses.Completed;
            outcome.ExitStatus = 0;
            outcome.StandardOutput = "hello" + Environment.NewLine;
            outcome.StandardError = "a warning";
            outcome.ElapsedMilliseconds = 12;
            outcome.ProcessorMilliseconds = 7;
            outcome.PeakWorkingSetBytes = 4096;
            outcome.Diagnostics = new List<CompileDiagnostic>();
            outcome.Diagnostics.Add(diagnostic);
            return outcome;
        }

        private string NewRoot()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "result-store-" + Guid.NewGuid().ToString("N"));
            createdRoots.Add(path);
            return path;
        }
    }
}
