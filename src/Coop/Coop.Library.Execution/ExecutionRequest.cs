namespace Coop.Library.Execution
{
    public sealed class ExecutionRequest
    {
        public string ExecutionId { get; set; }

        public Submission Submission { get; set; }

        public string OutputDirectory { get; set; }

        public int? TimeoutSeconds { get; set; }

        public ReuseMarker Reused { get; set; }

        public string[] AdditionalReferences { get; set; }
    }
}
