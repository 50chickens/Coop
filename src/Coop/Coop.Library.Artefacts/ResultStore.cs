using System;
using System.IO;
using System.Text.Json;
using Coop.Library.Common;

namespace Coop.Library.Artefacts
{
    public sealed class ResultStore
    {
        private const string DocumentSchemeVersion = "1";

        private readonly string root;
        private readonly string runnerVersion;

        public ResultStore(string root, string runnerVersion)
        {
            this.root = root;
            this.runnerVersion = runnerVersion;
        }

        public string Root
        {
            get
            {
                return root;
            }
        }

        public string RunnerVersion
        {
            get
            {
                return runnerVersion;
            }
        }

        public string DocumentPathOf(string resultKey)
        {
            return Path.Combine(Path.Combine(root, "results"), DocumentNameOf(resultKey));
        }

        public ResultLookup Read(string resultKey)
        {
            ResultLookup lookup = new ResultLookup();
            lookup.ResultKey = resultKey;
            lookup.Found = false;
            string documentPath = DocumentPathOf(resultKey);
            if (!File.Exists(documentPath))
            {
                return lookup;
            }
            ResultRecord record = RecordOf(documentPath);
            if (!IsRecordedByThisRunner(record))
            {
                return lookup;
            }
            lookup.Found = true;
            lookup.ArtefactKey = record.ArtefactKey;
            lookup.ProducingExecutionId = record.ExecutionId;
            lookup.RecordedAt = record.RecordedAt;
            lookup.RunnerVersion = record.RunnerVersion;
            lookup.Stored = record.Outcome;
            return lookup;
        }

        public void Write(ResultRecord record)
        {
            if (record.Outcome.Reused != null)
            {
                throw new InvalidOperationException("A stored result is the answer a run produced, so its outcome must not already carry a reuse marker");
            }
            if (string.IsNullOrEmpty(record.ExecutionId))
            {
                throw new InvalidOperationException("A stored result must name the execution that produced it");
            }
            if (record.RunnerVersion != runnerVersion)
            {
                throw new InvalidOperationException("A stored result must record the runner version of this store: " + runnerVersion);
            }
            record.RecordedAt = DateTimeOffset.UtcNow.ToString("O");
            string documentPath = DocumentPathOf(record.ResultKey);
            Directory.CreateDirectory(Path.GetDirectoryName(documentPath));
            AtomicFile.Write(documentPath, JsonSerializer.Serialize(record, CoopJson.Options));
        }

        private bool IsRecordedByThisRunner(ResultRecord record)
        {
            if (record == null)
            {
                return false;
            }
            if (record.Outcome == null)
            {
                return false;
            }
            return record.RunnerVersion == runnerVersion;
        }

        private string DocumentNameOf(string resultKey)
        {
            ContentKeyBuilder builder = new ContentKeyBuilder();
            builder.Add("scheme.version", DocumentSchemeVersion);
            builder.Add("result.key", resultKey);
            builder.Add("runner.version", runnerVersion);
            return builder.Finish() + ".json";
        }

        private static ResultRecord RecordOf(string documentPath)
        {
            string text = File.ReadAllText(documentPath);
            ResultRecord record = null;
            try
            {
                record = JsonSerializer.Deserialize<ResultRecord>(text, CoopJson.Options);
            }
            catch (JsonException)
            {
                return null;
            }
            return record;
        }
    }
}
