using System.Text.Json.Nodes;
using Coop.Library.Execution;

namespace Coop.Services.Surface
{
    public static class Refusals
    {
        public const int UnknownExecutionCode = 6;

        public static JsonObject Field(string field, string reason)
        {
            JsonObject refusal = Envelope();
            refusal["field"] = field;
            refusal["reason"] = reason;
            return refusal;
        }

        public static JsonObject MissingPath(string field, string path, string reason)
        {
            JsonObject refusal = Field(field, reason);
            refusal["path"] = path;
            return refusal;
        }

        public static JsonObject MissingReference(string path)
        {
            return MissingPath("references", path, "references names a path that does not exist");
        }

        public static JsonObject Reason(string reason)
        {
            JsonObject refusal = Envelope();
            refusal["reason"] = reason;
            return refusal;
        }

        public static JsonObject UnknownExecution(string executionId, string reason)
        {
            JsonObject refusal = Envelope();
            refusal["executionId"] = executionId;
            refusal["reason"] = reason;
            refusal["code"] = UnknownExecutionCode;
            return refusal;
        }

        public static JsonObject RequiredField(string field)
        {
            return Field(field, field + " is required by the execution template and this request does not carry it");
        }

        public static JsonObject WrongKind(string field, string kind, string expected)
        {
            return Field(field, field + " is " + kind + " rather than " + expected);
        }

        private static JsonObject Envelope()
        {
            JsonObject refusal = new JsonObject();
            refusal["status"] = ExecutionStatuses.Refused;
            refusal["runnerVersion"] = SurfaceConfiguration.VersionOf();
            return refusal;
        }
    }
}
