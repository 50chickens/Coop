namespace Coop.Library.Execution
{
    public sealed class ReuseMarker
    {
        public string ExecutionId { get; set; }

        public string RecordedAt { get; set; }

        public string RunnerVersion { get; set; }

        public string Hint { get; set; }
    }
}
