using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Coop.Services.Surface
{
    public static class ToolResults
    {
        private static readonly JsonSerializerOptions options = CreateOptions();

        public static CallToolResult Text(string text)
        {
            TextContentBlock block = new TextContentBlock { Text = text };
            List<ContentBlock> content = new List<ContentBlock>();
            content.Add(block);
            CallToolResult result = new CallToolResult();
            result.Content = content;
            return result;
        }

        public static CallToolResult Json(JsonNode document)
        {
            return Text(document.ToJsonString(options));
        }

        public static CallToolResult Refusal(JsonObject refusal)
        {
            CallToolResult result = Text(refusal.ToJsonString(options));
            result.IsError = true;
            return result;
        }

        private static JsonSerializerOptions CreateOptions()
        {
            JsonSerializerOptions created = new JsonSerializerOptions();
            created.WriteIndented = false;
            created.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            return created;
        }
    }
}
