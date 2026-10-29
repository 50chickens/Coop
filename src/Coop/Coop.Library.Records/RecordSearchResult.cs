using System.Collections.Generic;

namespace Coop.Library.Records
{
    public sealed class RecordSearchResult
    {
        public List<ExecutionRecord> Records { get; set; } = new List<ExecutionRecord>();

        public bool Stale { get; set; }

        public bool Rebuilt { get; set; }

        public string Notice { get; set; }
    }
}
