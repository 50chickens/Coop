using System.Globalization;
using System.Text.Json;

namespace Coop.Demo.AgentTurn
{
    public sealed class ExecutionFacts
    {
        public string ExecutionId { get; set; }

        public string Status { get; set; }

        public bool CompileSuccess { get; set; }

        public int DiagnosticCount { get; set; }

        public int? ExitStatus { get; set; }

        public long? ElapsedMilliseconds { get; set; }

        public bool DeadlineExpired { get; set; }

        public string StandardOutput { get; set; }

        public static ExecutionFacts Read(string toolResult)
        {
            ExecutionFacts facts = new ExecutionFacts();
            using (JsonDocument document = JsonDocument.Parse(toolResult))
            {
                JsonElement root = document.RootElement;
                facts.ExecutionId = root.GetProperty("executionId").GetString();
                facts.Status = root.GetProperty("status").GetString();

                JsonElement compile = root.GetProperty("compile");
                facts.CompileSuccess = compile.GetProperty("success").GetBoolean();
                facts.DiagnosticCount = compile.GetProperty("diagnosticCount").GetInt32();

                JsonElement run;
                if (root.TryGetProperty("run", out run))
                {
                    facts.ExitStatus = run.GetProperty("exitStatus").GetInt32();
                    facts.ElapsedMilliseconds = run.GetProperty("elapsedMilliseconds").GetInt64();
                    facts.DeadlineExpired = run.GetProperty("deadlineExpired").GetBoolean();
                    facts.StandardOutput = run.GetProperty("standardOutput").GetString();
                }
            }

            if (facts.StandardOutput == null)
            {
                facts.StandardOutput = "";
            }
            return facts;
        }
    }
}
