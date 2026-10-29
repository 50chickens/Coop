using System.Linq;
using System.Text.Json.Nodes;
using Coop.Library.Execution;
using ModelContextProtocol.Protocol;

namespace Coop.Tool
{
    public static class Outcome
    {
        public static string TextOf(CallToolResult result)
        {
            TextContentBlock block = result.Content.OfType<TextContentBlock>().First();
            return block.Text;
        }

        public static int ExitCodeOf(CallToolResult result)
        {
            if (result.IsError == true)
            {
                return Program.NotCompletedExitCode;
            }
            return ExitCodeOfText(TextOf(result));
        }

        public static int ExitCodeOfText(string text)
        {
            JsonNode document = JsonNode.Parse(text);
            if (document["status"].GetValue<string>() == ExecutionStatuses.Completed)
            {
                return 0;
            }
            return Program.NotCompletedExitCode;
        }
    }
}
