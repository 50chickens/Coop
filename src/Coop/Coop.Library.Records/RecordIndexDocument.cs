using System.Collections.Generic;

namespace Coop.Library.Records
{
    public sealed class RecordIndexDocument
    {
        public string GeneratedAt { get; set; }

        public int DocumentCount { get; set; }

        public long DocumentSignature { get; set; }

        public List<RecordIndexEntry> Entries { get; set; } = new List<RecordIndexEntry>();
    }
}
