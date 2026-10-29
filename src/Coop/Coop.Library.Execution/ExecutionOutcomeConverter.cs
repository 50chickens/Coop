using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Coop.Library.Execution
{
    public sealed class ExecutionOutcomeConverter : JsonConverter<ExecutionOutcome>
    {
        public override ExecutionOutcome Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            JsonNode document = JsonNode.Parse(ref reader);
            ExecutionOutcome outcome = new ExecutionOutcome();
            outcome.ExecutionId = TextOf(document["executionId"]);
            outcome.Status = TextOf(document["status"]);
            outcome.ExitStatus = (int?)MeasurementOf(document["exitStatus"]);
            outcome.StandardOutput = TextOf(document["standardOutput"]);
            outcome.StandardError = TextOf(document["standardError"]);
            outcome.DeadlineExpired = BooleanOf(document["deadlineExpired"]);
            outcome.ElapsedMilliseconds = MeasurementOf(document["elapsedMilliseconds"]);
            outcome.ProcessorMilliseconds = MeasurementOf(document["processorMilliseconds"]);
            outcome.PeakWorkingSetBytes = MeasurementOf(document["peakWorkingSetBytes"]);
            outcome.Diagnostics = JsonSerializer.Deserialize<List<CompileDiagnostic>>(document["diagnostics"], options);
            outcome.Reused = JsonSerializer.Deserialize<ReuseMarker>(document["reused"], options);
            outcome.Refusal = JsonSerializer.Deserialize<ExecutionRefusal>(document["refusal"], options);
            return outcome;
        }

        public override void Write(Utf8JsonWriter writer, ExecutionOutcome value, JsonSerializerOptions options)
        {
            JsonObject document = new JsonObject();
            document["executionId"] = value.ExecutionId;
            document["status"] = value.Status;
            document["exitStatus"] = value.ExitStatus;
            document["standardOutput"] = value.StandardOutput;
            document["standardError"] = value.StandardError;
            document["deadlineExpired"] = value.DeadlineExpired;
            document["elapsedMilliseconds"] = value.ElapsedMilliseconds;
            document["processorMilliseconds"] = value.ProcessorMilliseconds;
            document["peakWorkingSetBytes"] = value.PeakWorkingSetBytes;
            document["diagnostics"] = JsonSerializer.SerializeToNode(value.Diagnostics, options);
            if (value.Reused != null)
            {
                document["reused"] = JsonSerializer.SerializeToNode(value.Reused, options);
            }
            if (value.Refusal != null)
            {
                document["refusal"] = JsonSerializer.SerializeToNode(value.Refusal, options);
            }
            document.WriteTo(writer);
        }

        private static long? MeasurementOf(JsonNode node)
        {
            if (node == null)
            {
                return null;
            }
            if (node.GetValueKind() == JsonValueKind.Null)
            {
                return null;
            }
            return node.GetValue<long>();
        }

        private static string TextOf(JsonNode node)
        {
            if (node == null)
            {
                return null;
            }
            if (node.GetValueKind() == JsonValueKind.Null)
            {
                return null;
            }
            return node.GetValue<string>();
        }

        private static bool BooleanOf(JsonNode node)
        {
            if (node == null)
            {
                return false;
            }
            return node.GetValue<bool>();
        }
    }
}
