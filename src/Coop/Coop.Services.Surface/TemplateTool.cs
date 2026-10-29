using System.Text.Json.Nodes;
using Coop.Library.Template;
using ModelContextProtocol.Protocol;

namespace Coop.Services.Surface
{
    public sealed class TemplateTool
    {
        private readonly SurfaceConfiguration configuration;

        public TemplateTool(SurfaceConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public CallToolResult Template()
        {
            return ToolResults.Text(CoopTemplate.Serialise(configuration.Version));
        }
    }
}
