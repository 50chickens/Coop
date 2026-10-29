using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Coop.Library.Common;

namespace Coop.Library.Records
{
    public sealed class RecordStore
    {
        public RecordStore(string root)
        {
            Root = root;
            Directory.CreateDirectory(root);
        }

        public string Root { get; }

        public RecordWrite Write(ExecutionRecord record)
        {
            RecordRefusal unusable = UnusableRefusal(record.ExecutionId);
            if (unusable != null)
            {
                RecordWrite refused = new RecordWrite();
                refused.Refusal = unusable;
                return refused;
            }
            string path = Path.Combine(Root, record.ExecutionId + ".json");
            AtomicFile.Write(path, JsonSerializer.Serialize(record, CoopJson.Options));
            return RecordWrite.Written(path);
        }

        public RecordLookup Read(string executionId)
        {
            RecordRefusal unusable = UnusableRefusal(executionId);
            if (unusable != null)
            {
                RecordLookup refused = new RecordLookup();
                refused.Refusal = unusable;
                return refused;
            }
            string path = Path.Combine(Root, executionId + ".json");
            if (File.Exists(path) == false)
            {
                return RefusedUnknown(executionId);
            }
            return RecordLookup.Found(Deserialize(path));
        }

        public List<RecordFile> Files()
        {
            return Directory
                .EnumerateFiles(Root, "*.json", SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(Path.GetFileName(path), ExecutionIds.IndexFileName, StringComparison.OrdinalIgnoreCase) == false)
                .Select(path => FileOf(path))
                .ToList();
        }

        public ExecutionRecord ReadFile(string fileName)
        {
            string path = Path.Combine(Root, fileName);
            if (File.Exists(path) == false)
            {
                return null;
            }
            return Deserialize(path);
        }

        internal RecordLookup RefusedUnknown(string executionId)
        {
            return RecordLookup.Refused(
                RecordRefusalNames.UnknownExecution,
                "no execution record '" + executionId + "' in the record store at " + Root,
                RecordRefusalCodes.UnknownExecution);
        }

        private RecordRefusal UnusableRefusal(string executionId)
        {
            if (ExecutionIds.Usable(executionId))
            {
                return null;
            }
            RecordRefusal refusal = new RecordRefusal();
            refusal.Name = RecordRefusalNames.UnusableExecutionId;
            refusal.Message = "the execution identifier '" + executionId + "' cannot name a document in the record store at " + Root;
            refusal.Code = RecordRefusalCodes.UnknownExecution;
            return refusal;
        }

        private static RecordFile FileOf(string path)
        {
            FileInfo info = new FileInfo(path);
            RecordFile file = new RecordFile();
            file.FileName = info.Name;
            file.ExecutionId = Path.GetFileNameWithoutExtension(info.Name);
            file.LastWriteTicks = info.LastWriteTimeUtc.Ticks;
            file.Length = info.Length;
            return file;
        }

        private static ExecutionRecord Deserialize(string path)
        {
            return JsonSerializer.Deserialize<ExecutionRecord>(File.ReadAllText(path), CoopJson.Options);
        }
    }
}
