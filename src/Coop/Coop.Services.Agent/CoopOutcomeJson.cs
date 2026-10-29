using System.Text.Json;
using System.Text.Json.Nodes;
using Coop.Library.Common;
using Coop.Library.Execution;

namespace Coop.Services.Agent
{
    public static class CoopOutcomeJson
    {
        public static string Write(ExecutionOutcome outcome)
        {
            JsonObject document = new JsonObject();
            document["executionId"] = JsonSerializer.SerializeToNode(outcome.ExecutionId, CoopJson.Options);
            document["status"] = JsonSerializer.SerializeToNode(outcome.Status, CoopJson.Options);
            document["reused"] = Reused(outcome);
            document["cache"] = null;
            document["compile"] = Compile(outcome);
            document["run"] = Run(outcome);
            JsonNode refusal = JsonSerializer.SerializeToNode(outcome.Refusal, CoopJson.Options);
            if (refusal != null)
            {
                document["refusal"] = refusal;
            }
            return document.ToJsonString();
        }

        private static JsonNode Reused(ExecutionOutcome outcome)
        {
            return JsonSerializer.SerializeToNode(outcome.Reused, CoopJson.Options);
        }

        private static JsonObject Compile(ExecutionOutcome outcome)
        {
            JsonObject compile = new JsonObject();
            compile["success"] = JsonSerializer.SerializeToNode(outcome.Diagnostics.Count == 0, CoopJson.Options);
            compile["diagnosticCount"] = JsonSerializer.SerializeToNode(outcome.Diagnostics.Count, CoopJson.Options);
            compile["diagnostics"] = JsonSerializer.SerializeToNode(outcome.Diagnostics, CoopJson.Options);
            return compile;
        }

        private static JsonObject Run(ExecutionOutcome outcome)
        {
            if (outcome.Diagnostics.Count > 0)
            {
                return null;
            }
            JsonObject run = new JsonObject();
            run["exitStatus"] = JsonSerializer.SerializeToNode(outcome.ExitStatus, CoopJson.Options);
            run["standardOutput"] = JsonSerializer.SerializeToNode(outcome.StandardOutput, CoopJson.Options);
            run["standardError"] = JsonSerializer.SerializeToNode(outcome.StandardError, CoopJson.Options);
            run["deadlineExpired"] = JsonSerializer.SerializeToNode(outcome.DeadlineExpired, CoopJson.Options);
            run["elapsedMilliseconds"] = JsonSerializer.SerializeToNode(outcome.ElapsedMilliseconds, CoopJson.Options);
            run["processorMilliseconds"] = JsonSerializer.SerializeToNode(outcome.ProcessorMilliseconds, CoopJson.Options);
            run["peakWorkingSetBytes"] = JsonSerializer.SerializeToNode(outcome.PeakWorkingSetBytes, CoopJson.Options);
            return run;
        }
    }
}
