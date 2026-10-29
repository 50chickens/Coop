using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coop.Library.Execution;

namespace Coop.Library.Records.Tests
{
    [TestFixture]
    public class RecordStoreTests
    {
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
                .Where(root => Directory.Exists(root))
                .ToList()
                .ForEach(root => Directory.Delete(root, true));
        }

        [Test]
        public void Test_That_We_Can_Write_A_Record_And_Read_It_By_Execution_Id()
        {
            string root = Root();
            string executionId = "41122878127a4658b652d85b3338b07c";
            ExecutionRecord record = TestRecords.Completed(executionId, "prime-check", "agent", "2026-10-23T11:15:00+11:00");
            record.ShouldRunAsync = true;
            record.Reused = new ReuseMarker();
            record.Reused.ExecutionId = "e5852e86abfd47babaa9e6de7c03ea0c";
            record.Reused.RecordedAt = "2026-10-23T10:40:39+00:00";
            RecordStore store = new RecordStore(root);
            RecordWrite write = store.Write(record);
            Assert.That(write.Refusal, Is.Null);
            Assert.That(File.Exists(Path.Combine(root, executionId + ".json")), Is.True);
            RecordLookup lookup = store.Read(executionId);
            Assert.That(lookup.Refusal, Is.Null);
            ExecutionRecord read = lookup.Record;
            Assert.That(read.ExecutionId, Is.EqualTo(executionId));
            Assert.That(read.Name, Is.EqualTo("prime-check"));
            Assert.That(read.Description, Is.EqualTo(record.Description));
            Assert.That(read.Category, Is.EqualTo("agent"));
            Assert.That(read.Timestamp, Is.EqualTo("2026-10-23T11:15:00+11:00"));
            Assert.That(read.ShouldRunAsync, Is.True);
            Assert.That(read.Submission.SourceText, Is.EqualTo(record.Submission.SourceText));
            Assert.That(read.Submission.Parameters["count"], Is.EqualTo("3"));
            Assert.That(read.Packages.Count, Is.EqualTo(1));
            Assert.That(read.Packages[0].Id, Is.EqualTo("Newtonsoft.Json"));
            Assert.That(read.Status, Is.EqualTo(RecordStatuses.Completed));
            Assert.That(read.Response.ExitStatus, Is.EqualTo(0));
            Assert.That(read.Response.StandardOutput, Is.EqualTo("hello"));
            Assert.That(read.Reused.ExecutionId, Is.EqualTo("e5852e86abfd47babaa9e6de7c03ea0c"));
        }

        [Test]
        public void Test_That_We_Refuse_An_Unknown_Execution_Id_By_Name_And_Code_Six()
        {
            string root = Root();
            RecordStore store = new RecordStore(root);
            RecordLookup lookup = store.Read("0123456789abcdef0123456789abcdef");
            Assert.That(lookup.Record, Is.Null);
            Assert.That(lookup.Refusal.Name, Is.EqualTo(RecordRefusalNames.UnknownExecution));
            Assert.That(lookup.Refusal.Code, Is.EqualTo(6));
            Assert.That(lookup.Refusal.Message, Does.Contain("0123456789abcdef0123456789abcdef"));
            Assert.That(lookup.Refusal.Message, Does.Contain(root));
        }

        [Test]
        public void Test_That_We_Can_Read_A_Record_From_A_Second_Store_Instance_Over_The_Same_Root()
        {
            string root = Root();
            string executionId = "41122878127a4658b652d85b3338b07c";
            RecordStore writer = new RecordStore(root);
            writer.Write(TestRecords.Completed(executionId, "survives", "agent", "2026-10-23T11:15:00+11:00"));
            RecordStore reader = new RecordStore(root);
            RecordLookup lookup = reader.Read(executionId);
            Assert.That(lookup.Refusal, Is.Null);
            Assert.That(lookup.Record.Name, Is.EqualTo("survives"));
        }

        [Test]
        public void Test_That_We_Can_Read_A_Record_With_No_Outcome_While_Running()
        {
            string root = Root();
            string executionId = "41122878127a4658b652d85b3338b07c";
            RecordStore store = new RecordStore(root);
            store.Write(TestRecords.Record(executionId, "in-flight", "agent", "2026-10-23T11:15:00+11:00", RecordStatuses.Running));
            RecordLookup lookup = store.Read(executionId);
            Assert.That(lookup.Record.Status, Is.EqualTo(RecordStatuses.Running));
            Assert.That(lookup.Record.Response, Is.Null);
        }

        [Test]
        [Platform("Win")]
        public void Test_That_We_Refuse_An_Execution_Id_That_Cannot_Name_A_Document()
        {
            string root = Root();
            RecordStore store = new RecordStore(root);
            List<string> unusable = new List<string> { "../escape", "a/b", "bad:name", "index" };
            List<RecordWrite> writes = unusable
                .Select(executionId => store.Write(TestRecords.Record(executionId, "escape", "agent", "2026-10-23T11:15:00+11:00", RecordStatuses.Pending)))
                .ToList();
            List<RecordLookup> reads = unusable
                .Select(executionId => store.Read(executionId))
                .ToList();
            Assert.That(writes.All(write => write.Refusal != null), Is.True);
            Assert.That(writes.All(write => write.Refusal.Name == RecordRefusalNames.UnusableExecutionId), Is.True);
            Assert.That(reads.All(read => read.Refusal != null), Is.True);
            Assert.That(reads.All(read => read.Refusal.Name == RecordRefusalNames.UnusableExecutionId), Is.True);
            Assert.That(Directory.GetFiles(root, "*.json").Length, Is.EqualTo(0));
        }

        private string Root()
        {
            string root = TestRecords.NewRoot();
            createdRoots.Add(root);
            return root;
        }
    }
}
