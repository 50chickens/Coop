using System.Collections.Generic;
using Coop.Library.Execution;

namespace Coop.Library.Records
{
    public sealed class ExecutionRecord
    {
        public string ExecutionId { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Category { get; set; }

        public string Timestamp { get; set; }

        public Submission Submission { get; set; }

        public bool ShouldRunAsync { get; set; }

        public List<RecordPackage> Packages { get; set; } = new List<RecordPackage>();

        public ExecutionOutcome Response { get; set; }

        public string Status { get; set; }

        public ReuseMarker Reused { get; set; }
    }
}
