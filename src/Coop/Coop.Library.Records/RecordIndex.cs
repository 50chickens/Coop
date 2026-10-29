using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Coop.Library.Common;

namespace Coop.Library.Records
{
    public sealed class RecordIndex
    {
        public const int MaximumLimit = 200;

        private readonly RecordStore store;

        public RecordIndex(RecordStore store)
        {
            this.store = store;
        }

        public RecordStore Store
        {
            get
            {
                return store;
            }
        }

        public RecordIndexState Inspect()
        {
            RecordIndexDocument index;
            return Inspect(out index);
        }

        public RecordIndexRebuild Rebuild()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<RecordFile> files = store.Files();
            RecordIndexDocument index = new RecordIndexDocument();
            index.GeneratedAt = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            index.DocumentCount = files.Count;
            index.DocumentSignature = SignatureOf(files);
            index.Entries = files
                .Select(file => EntryOf(file))
                .OrderBy(entry => TimestampOf(entry.Timestamp))
                .ThenBy(entry => entry.ExecutionId, StringComparer.Ordinal)
                .ToList();
            AtomicFile.Write(IndexPath(), JsonSerializer.Serialize(index, CoopJson.Options));
            stopwatch.Stop();
            RecordIndexRebuild rebuild = new RecordIndexRebuild();
            rebuild.Index = index;
            rebuild.DocumentsRead = index.Entries.Count;
            rebuild.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            return rebuild;
        }

        public List<RecordIndexEntry> Entries()
        {
            RecordIndexDocument index;
            RecordIndexState state = Inspect(out index);
            if (state.Stale)
            {
                return Rebuild().Index.Entries;
            }
            return index.Entries;
        }

        public RecordLookup Find(string executionId)
        {
            RecordIndexDocument index;
            RecordIndexState state = Inspect(out index);
            if (state.Stale)
            {
                index = Rebuild().Index;
            }
            bool indexed = index.Entries.Any(entry => string.Equals(entry.ExecutionId, executionId, StringComparison.OrdinalIgnoreCase));
            if (indexed == false)
            {
                return store.RefusedUnknown(executionId);
            }
            return store.Read(executionId);
        }

        public RecordSearchResult Search(RecordSearchQuery query)
        {
            RecordIndexDocument index;
            RecordIndexState state = Inspect(out index);
            bool stale = state.Stale;
            RecordIndexRebuild rebuild = null;
            if (stale && query.RefreshWhenStale)
            {
                rebuild = Rebuild();
                index = rebuild.Index;
                stale = false;
            }
            RecordSearchResult result = new RecordSearchResult();
            result.Records = RecordsOf(index, query);
            result.Stale = stale;
            result.Rebuilt = rebuild != null;
            result.Notice = NoticeOf(state, rebuild, stale);
            return result;
        }

        private RecordIndexState Inspect(out RecordIndexDocument index)
        {
            List<RecordFile> files = store.Files();
            index = ReadIndex();
            RecordIndexState state = new RecordIndexState();
            state.DocumentCount = files.Count;
            state.DocumentSignature = SignatureOf(files);
            if (index == null)
            {
                state.Exists = false;
                state.Stale = true;
                return state;
            }
            state.Exists = true;
            state.IndexedCount = index.DocumentCount;
            state.IndexedSignature = index.DocumentSignature;
            state.Stale = IsStale(index, files, state.DocumentSignature);
            return state;
        }

        private List<ExecutionRecord> RecordsOf(RecordIndexDocument index, RecordSearchQuery query)
        {
            if (index == null)
            {
                return new List<ExecutionRecord>();
            }
            int limit = Math.Clamp(query.Limit, 1, MaximumLimit);
            return index.Entries
                .Where(entry => Matches(entry, query))
                .OrderByDescending(entry => TimestampOf(entry.Timestamp))
                .ThenBy(entry => entry.ExecutionId, StringComparer.Ordinal)
                .Select(entry => store.ReadFile(entry.FileName))
                .Where(record => record != null)
                .Take(limit)
                .ToList();
        }

        private RecordIndexEntry EntryOf(RecordFile file)
        {
            ExecutionRecord record = store.ReadFile(file.FileName);
            RecordIndexEntry entry = new RecordIndexEntry();
            entry.ExecutionId = record.ExecutionId;
            entry.FileName = file.FileName;
            entry.Name = record.Name;
            entry.Description = record.Description;
            entry.Category = record.Category;
            entry.Timestamp = record.Timestamp;
            entry.Status = record.Status;
            entry.Tokens = RecordTokens.Of(record.Name, record.Description, record.Category);
            return entry;
        }

        private RecordIndexDocument ReadIndex()
        {
            string path = IndexPath();
            if (File.Exists(path) == false)
            {
                return null;
            }
            return JsonSerializer.Deserialize<RecordIndexDocument>(File.ReadAllText(path), CoopJson.Options);
        }

        private string IndexPath()
        {
            return Path.Combine(store.Root, ExecutionIds.IndexFileName);
        }

        private static bool IsStale(RecordIndexDocument index, List<RecordFile> files, long signature)
        {
            if (index.DocumentCount != files.Count)
            {
                return true;
            }
            if (index.DocumentSignature != signature)
            {
                return true;
            }
            List<string> fileNames = files.Select(file => file.FileName).ToList();
            List<string> entryNames = index.Entries.Select(entry => entry.FileName).ToList();
            if (entryNames.Any(name => fileNames.Contains(name, StringComparer.OrdinalIgnoreCase) == false))
            {
                return true;
            }
            return fileNames.Any(name => entryNames.Contains(name, StringComparer.OrdinalIgnoreCase) == false);
        }

        private static bool Matches(RecordIndexEntry entry, RecordSearchQuery query)
        {
            if (query.ExecutionId != null && string.Equals(entry.ExecutionId, query.ExecutionId, StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }
            if (query.Name != null && Contains(entry.Name, query.Name) == false)
            {
                return false;
            }
            if (query.Category != null && string.Equals(entry.Category, query.Category, StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }
            if (query.Status != null && string.Equals(entry.Status, query.Status, StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }
            return true;
        }

        private static bool Contains(string text, string value)
        {
            if (text == null)
            {
                return false;
            }
            return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NoticeOf(RecordIndexState state, RecordIndexRebuild rebuild, bool stale)
        {
            if (stale)
            {
                return "the index was stale and was not refreshed: it holds " + state.IndexedCount + " entries for " + state.DocumentCount + " documents, so a document written after it was built is absent from this answer";
            }
            if (rebuild != null)
            {
                return "the index was stale and was rebuilt from " + rebuild.DocumentsRead + " documents before this answer";
            }
            return null;
        }

        private static long SignatureOf(List<RecordFile> files)
        {
            return files.Aggregate(0L, (signature, file) => unchecked(signature + file.LastWriteTicks + file.Length));
        }

        private static DateTimeOffset TimestampOf(string timestamp)
        {
            DateTimeOffset parsed;
            if (DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed;
            }
            return DateTimeOffset.MinValue;
        }
    }
}
