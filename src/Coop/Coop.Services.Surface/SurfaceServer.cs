using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Coop.Services.Surface
{
    public sealed class SurfaceServer
    {
        public const string ServerName = "coop-surface";

        private readonly McpServer server;

        private SurfaceServer(McpServer server)
        {
            this.server = server;
        }

        public static SurfaceServer Create(SurfaceConfiguration configuration)
        {
            ToolDispatcher dispatcher = new ToolDispatcher(configuration);
            McpServerOptions options = new McpServerOptions();
            options.ServerInfo = new Implementation { Name = ServerName, Version = configuration.Version };
            ServerCapabilities capabilities = new ServerCapabilities();
            capabilities.Tools = new ToolsCapability();
            options.Capabilities = capabilities;
            options.Handlers.ListToolsHandler = ListTools;
            options.Handlers.CallToolHandler = (context, cancellationToken) => new ValueTask<CallToolResult>(dispatcher.Call(context.Params));
            McpServer server = McpServer.Create(new StdioServerTransport(options, null), options, null, null);
            return new SurfaceServer(server);
        }

        public Task RunAsync()
        {
            return server.RunAsync();
        }

        private static ValueTask<ListToolsResult> ListTools(RequestContext<ListToolsRequestParams> context, CancellationToken cancellationToken)
        {
            ListToolsResult result = new ListToolsResult();
            result.Tools = ToolCatalog.All();
            return ValueTask.FromResult(result);
        }
    }
}
