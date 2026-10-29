using System;
using Coop.Library.Execution;

namespace Coop.Library.Artefacts
{
    public sealed class ResultLookup
    {
        public bool Found { get; set; }

        public string ResultKey { get; set; }

        public string ArtefactKey { get; set; }

        public string ProducingExecutionId { get; set; }

        public string RecordedAt { get; set; }

        public string RunnerVersion { get; set; }

        internal ExecutionOutcome Stored { get; set; }

        public ExecutionOutcome Reuse(string executionId)
        {
            if (!Found)
            {
                throw new InvalidOperationException("The result key " + ResultKey + " is not stored, so there is no outcome to reuse");
            }
            ReuseMarker marker = new ReuseMarker();
            marker.ExecutionId = ProducingExecutionId;
            marker.RecordedAt = RecordedAt;
            marker.RunnerVersion = RunnerVersion;
            marker.Hint = "the timings in this response are those of execution " + ProducingExecutionId + " recorded at " + RecordedAt + ", not of this run";
            Stored.ExecutionId = executionId;
            Stored.Reused = marker;
            return Stored;
        }
    }
}
