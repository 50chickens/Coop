using System.Collections.Generic;

namespace Coop.Library.Records
{
    public sealed class RecordIndexEntry
    {
        public string ExecutionId { get; set; }

        public string FileName { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Category { get; set; }

        public string Timestamp { get; set; }

        public string Status { get; set; }

        public List<string> Tokens { get; set; } = new List<string>();
    }
}
