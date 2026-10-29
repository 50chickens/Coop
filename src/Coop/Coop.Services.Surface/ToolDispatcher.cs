using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Coop.Services.Surface
{
    public sealed class ToolDispatcher
    {
        private readonly ExecuteTool execute;

        private readonly TemplateTool template;

        private readonly RecordReader records;

        public ToolDispatcher(SurfaceConfiguration configuration)
        {
            execute = new ExecuteTool(configuration);
            template = new TemplateTool(configuration);
            records = new RecordReader(configuration);
        }

        public CallToolResult Call(CallToolRequestParams request)
        {
            JsonObject arguments = ArgumentsOf(request);
            if (request.Name == ToolCatalog.ExecuteName)
            {
                return execute.Execute(arguments);
            }
            if (request.Name == ToolCatalog.TemplateName)
            {
                return template.Template();
            }
            if (request.Name == ToolCatalog.GetName)
            {
                return records.Get(arguments);
            }
            if (request.Name == ToolCatalog.SearchName)
            {
                return records.Search(arguments);
            }
            return ToolResults.Refusal(Refusals.Reason("the tool '" + request.Name + "' is not one of execute, template, get, search"));
        }

        private static JsonObject ArgumentsOf(CallToolRequestParams request)
        {
            if (request.Arguments == null)
            {
                return new JsonObject();
            }
            JsonNode node = JsonSerializer.SerializeToNode(request.Arguments);
            if (node == null)
            {
                return new JsonObject();
            }
            if (node.GetValueKind() != JsonValueKind.Object)
            {
                return new JsonObject();
            }
            return node.AsObject();
        }
    }
}
