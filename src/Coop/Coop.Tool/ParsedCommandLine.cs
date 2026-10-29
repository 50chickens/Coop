using System.Text.Json.Nodes;

namespace Coop.Tool
{
    public sealed class ParsedCommandLine
    {
        public JsonObject Arguments { get; set; }

        public JsonObject Refusal { get; set; }
    }
}
