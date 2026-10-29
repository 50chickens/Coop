using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Coop.Library.Records.Tests
{
    [TestFixture]
    public class RecordIndexTests
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
        public void Test_That_We_Can_Rebuild_An_Index_That_Sees_Documents_Written_By_Another_Instance()
        {
            string root = Root();
            RecordStore writer = new RecordStore(root);
            writer.Write(TestRecords.Completed("11111111111111111111111111111111", "first", "agent", "2026-10-21T10:00:00+00:00"));
            writer.Write(TestRecords.Completed("22222222222222222222222222222222", "second", "agent", "2026-10-22T10:00:00+00:00"));
            writer.Write(TestRecords.Completed("33333333333333333333333333333333", "third", "runtime", "2026-10-23T10:00:00+00:00"));
            RecordIndex index = new RecordIndex(new RecordStore(root));
            Assert.That(index.Inspect().Exists, Is.False);
            RecordIndexRebuild rebuild = index.Rebuild();
            Assert.That(rebuild.DocumentsRead, Is.EqualTo(3));
            Assert.That(rebuild.Index.Entries.Count, Is.EqualTo(3));
            Assert.That(rebuild.Index.DocumentCount, Is.EqualTo(3));
            RecordIndexState state = index.Inspect();
            Assert.That(state.Exists, Is.True);
            Assert.That(state.Stale, Is.False);
            File.Delete(Path.Combine(root, ExecutionIds.IndexFileName));
            Assert.That(index.Inspect().Stale, Is.True);
            RecordSearchResult result = index.Search(new RecordSearchQuery());
            Assert.That(result.Rebuilt, Is.True);
            Assert.That(result.Records.Count, Is.EqualTo(3));
        }

        [Test]
        public void Test_That_We_Can_Report_An_Answer_From_A_Stale_Index_As_Stale()
        {
            string root = Root();
            RecordStore first = new RecordStore(root);
            first.Write(TestRecords.Completed("11111111111111111111111111111111", "first", "agent", "2026-10-21T10:00:00+00:00"));
            RecordIndex index = new RecordIndex(first);
            index.Rebuild();
            RecordStore second = new RecordStore(root);
            second.Write(TestRecords.Completed("22222222222222222222222222222222", "second", "agent", "2026-10-22T10:00:00+00:00"));
            RecordIndexState state = index.Inspect();
            Assert.That(state.Stale, Is.True);
            Assert.That(state.DocumentCount, Is.EqualTo(2));
            Assert.That(state.IndexedCount, Is.EqualTo(1));
            RecordSearchQuery staleQuery = new RecordSearchQuery();
            staleQuery.RefreshWhenStale = false;
            RecordSearchResult stale = index.Search(staleQuery);
            Assert.That(stale.Stale, Is.True);
            Assert.That(stale.Rebuilt, Is.False);
            Assert.That(stale.Notice, Is.Not.Null);
            Assert.That(stale.Records.Count, Is.EqualTo(1));
            RecordSearchResult refreshed = index.Search(new RecordSearchQuery());
            Assert.That(refreshed.Stale, Is.False);
            Assert.That(refreshed.Rebuilt, Is.True);
            Assert.That(refreshed.Records.Count, Is.EqualTo(2));
        }

        [Test]
        public void Test_That_We_Can_Search_By_Status()
        {
            string root = Root();
            RecordStore store = new RecordStore(root);
            store.Write(TestRecords.Record("11111111111111111111111111111111", "finished", "agent", "2026-10-21T10:00:00+00:00", RecordStatuses.Completed));
            store.Write(TestRecords.Record("22222222222222222222222222222222", "in-flight", "agent", "2026-10-22T10:00:00+00:00", RecordStatuses.Running));
            RecordIndex index = new RecordIndex(store);
            RecordSearchQuery query = new RecordSearchQuery();
            query.Status = "COMPLETED";
            RecordSearchResult result = index.Search(query);
            Assert.That(result.Records.Count, Is.EqualTo(1));
            Assert.That(result.Records[0].ExecutionId, Is.EqualTo("11111111111111111111111111111111"));
            Assert.That(result.Records[0].Status, Is.EqualTo(RecordStatuses.Completed));
        }

        [Test]
        public void Test_That_We_Can_Search_By_Category_Newest_First()
        {
            string root = Root();
            RecordStore store = new RecordStore(root);
            store.Write(TestRecords.Completed("11111111111111111111111111111111", "older-check", "agent", "2026-10-21T10:00:00+00:00"));
            store.Write(TestRecords.Completed("22222222222222222222222222222222", "newer-check", "agent", "2026-10-22T10:00:00+00:00"));
            store.Write(TestRecords.Completed("33333333333333333333333333333333", "runtime-check", "runtime", "2026-10-23T10:00:00+00:00"));
            RecordIndex index = new RecordIndex(store);
            RecordSearchQuery query = new RecordSearchQuery();
            query.Category = "AGENT";
            RecordSearchResult result = index.Search(query);
            Assert.That(result.Records.Count, Is.EqualTo(2));
            Assert.That(result.Records[0].ExecutionId, Is.EqualTo("22222222222222222222222222222222"));
            Assert.That(result.Records[1].ExecutionId, Is.EqualTo("11111111111111111111111111111111"));
            RecordSearchQuery named = new RecordSearchQuery();
            named.Category = "agent";
            named.Name = "older";
            RecordSearchResult namedResult = index.Search(named);
            Assert.That(namedResult.Records.Count, Is.EqualTo(1));
            Assert.That(namedResult.Records[0].ExecutionId, Is.EqualTo("11111111111111111111111111111111"));
        }

        [Test]
        public void Test_That_We_Can_Bound_The_Number_Of_Results()
        {
            string root = Root();
            RecordStore store = new RecordStore(root);
            List<string> ids = Enumerable.Range(0, 5).Select(number => number.ToString("D32")).ToList();
            ids
                .Select((executionId, position) => TestRecords.Completed(executionId, "bounded-" + position, "agent", "2026-10-2" + position + "T10:00:00+00:00"))
                .ToList()
                .ForEach(record => store.Write(record));
            RecordIndex index = new RecordIndex(store);
            index.Rebuild();
            RecordSearchQuery query = new RecordSearchQuery();
            query.Limit = 2;
            RecordSearchResult result = index.Search(query);
            Assert.That(result.Records.Count, Is.EqualTo(2));
            RecordSearchQuery unbounded = new RecordSearchQuery();
            unbounded.Limit = 500;
            Assert.That(index.Search(unbounded).Records.Count, Is.EqualTo(5));
        }

        [Test]
        public void Test_That_We_Can_Find_A_Record_By_Execution_Id_Through_The_Index()
        {
            string root = Root();
            RecordStore store = new RecordStore(root);
            store.Write(TestRecords.Completed("11111111111111111111111111111111", "findable", "agent", "2026-10-21T10:00:00+00:00"));
            RecordIndex index = new RecordIndex(store);
            RecordLookup found = index.Find("11111111111111111111111111111111");
            Assert.That(found.Refusal, Is.Null);
            Assert.That(found.Record.Name, Is.EqualTo("findable"));
            RecordLookup unknown = index.Find("99999999999999999999999999999999");
            Assert.That(unknown.Record, Is.Null);
            Assert.That(unknown.Refusal.Name, Is.EqualTo(RecordRefusalNames.UnknownExecution));
            Assert.That(unknown.Refusal.Code, Is.EqualTo(6));
        }

        [Test]
        public void Test_That_We_Can_Rebuild_After_Concurrent_Writes_Of_Different_Ids()
        {
            string root = Root();
            List<string> ids = Enumerable.Range(0, 16).Select(number => Guid.NewGuid().ToString("N")).ToList();
            List<Task<RecordWrite>> writes = ids
                .Select(executionId => Task.Run(() => new RecordStore(root).Write(TestRecords.Completed(executionId, "concurrent-" + executionId, "agent", "2026-10-21T10:00:00+00:00"))))
                .ToList();
            Task.WaitAll(writes.ToArray());
            Assert.That(writes.All(write => write.Result.Refusal == null), Is.True);
            RecordIndex index = new RecordIndex(new RecordStore(root));
            RecordIndexRebuild rebuild = index.Rebuild();
            Assert.That(rebuild.Index.Entries.Count, Is.EqualTo(16));
            List<string> indexed = rebuild.Index.Entries.Select(entry => entry.ExecutionId).ToList();
            Assert.That(ids.All(executionId => indexed.Contains(executionId)), Is.True);
            Assert.That(ids.All(executionId => index.Find(executionId).Record != null), Is.True);
        }

        [Test]
        public void Test_That_We_Can_See_A_Document_Written_After_A_Rebuild_In_The_Next_Query()
        {
            string root = Root();
            RecordStore store = new RecordStore(root);
            store.Write(TestRecords.Completed("11111111111111111111111111111111", "before", "agent", "2026-10-21T10:00:00+00:00"));
            RecordIndex index = new RecordIndex(store);
            RecordSearchResult before = index.Search(new RecordSearchQuery());
            Assert.That(before.Records.Count, Is.EqualTo(1));
            new RecordStore(root).Write(TestRecords.Completed("22222222222222222222222222222222", "after", "agent", "2026-10-22T10:00:00+00:00"));
            RecordSearchResult after = index.Search(new RecordSearchQuery());
            Assert.That(after.Records.Count, Is.EqualTo(2));
            Assert.That(after.Stale, Is.False);
        }

        private string Root()
        {
            string root = TestRecords.NewRoot();
            createdRoots.Add(root);
            return root;
        }
    }
}
