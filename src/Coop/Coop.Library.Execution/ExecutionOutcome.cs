using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Coop.Library.Execution
{
    [JsonConverter(typeof(ExecutionOutcomeConverter))]
    public sealed class ExecutionOutcome
    {
        public string ExecutionId { get; set; }

        public string Status { get; set; }

        public int? ExitStatus { get; set; }

        public string StandardOutput { get; set; }

        public string StandardError { get; set; }

        public bool DeadlineExpired { get; set; }

        public long? ElapsedMilliseconds { get; set; }

        public long? ProcessorMilliseconds { get; set; }

        public long? PeakWorkingSetBytes { get; set; }

        public List<CompileDiagnostic> Diagnostics { get; set; }

        public ReuseMarker Reused { get; set; }

        public ExecutionRefusal Refusal { get; set; }
    }
}
