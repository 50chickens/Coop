using Coop.Library.Execution;

namespace Coop.Library.Artefacts
{
    public sealed class ResultRecord
    {
        public string ResultKey { get; set; }

        public string ArtefactKey { get; set; }

        public string RunnerVersion { get; set; }

        public string ExecutionId { get; set; }

        public string RecordedAt { get; set; }

        public ExecutionOutcome Outcome { get; set; }
    }
}
