using System.Text.Json.Nodes;
using Coop.Library.Execution;

namespace Coop.Services.Surface
{
    public sealed class ExecutionReport
    {
        public string ArtefactKey { get; set; }

        public string ResultKey { get; set; }

        public bool ArtefactReused { get; set; }

        public bool ResultReused { get; set; }

        public string RunnerVersion { get; set; }

        public CompileReport Compile { get; set; }

        public ExecutionOutcome Outcome { get; set; }

        public JsonObject ToJsonObject()
        {
            JsonObject document = new JsonObject();
            document["runnerVersion"] = RunnerVersion;
            document["executionId"] = Outcome.ExecutionId;
            document["status"] = Outcome.Status;
            document["reused"] = MarkerOf(Outcome.Reused);
            document["cache"] = CacheOf();
            document["compile"] = Compile.ToJsonObject();
            document["run"] = RunOf();
            return document;
        }

        private JsonObject CacheOf()
        {
            JsonObject cache = new JsonObject();
            cache["artefactKey"] = ArtefactKey;
            cache["resultKey"] = ResultKey;
            cache["artefactReused"] = ArtefactReused;
            cache["resultReused"] = ResultReused;
            return cache;
        }

        private JsonNode RunOf()
        {
            if (Compile.Success == false)
            {
                return null;
            }
            JsonObject run = new JsonObject();
            run["exitStatus"] = MeasurementOf(Outcome.ExitStatus);
            run["standardOutput"] = Outcome.StandardOutput;
            run["standardError"] = Outcome.StandardError;
            run["deadlineExpired"] = Outcome.DeadlineExpired;
            run["elapsedMilliseconds"] = MeasurementOf(Outcome.ElapsedMilliseconds);
            run["processorMilliseconds"] = MeasurementOf(Outcome.ProcessorMilliseconds);
            run["peakWorkingSetBytes"] = MeasurementOf(Outcome.PeakWorkingSetBytes);
            return run;
        }

        private static JsonNode MarkerOf(ReuseMarker marker)
        {
            if (marker == null)
            {
                return null;
            }
            JsonObject document = new JsonObject();
            document["executionId"] = marker.ExecutionId;
            document["recordedAt"] = marker.RecordedAt;
            document["runnerVersion"] = marker.RunnerVersion;
            document["hint"] = marker.Hint;
            return document;
        }

        private static JsonNode MeasurementOf(long? measurement)
        {
            if (measurement == null)
            {
                return null;
            }
            return JsonValue.Create(measurement.Value);
        }
    }
}
