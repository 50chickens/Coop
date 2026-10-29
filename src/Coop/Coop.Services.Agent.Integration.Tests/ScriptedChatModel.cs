using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Coop.Services.Agent.Integration.Tests
{
    public sealed class ScriptedChatModel : IChatClient
    {
        public const string SubmittedSource = "public static class Program\n{\n    public static int Main(string[] args)\n    {\n        System.Console.WriteLine(\"project1 hello runner\");\n        return 0;\n    }\n}\n";

        private readonly List<List<ChatMessage>> _requests = new List<List<ChatMessage>>();

        private string _source = SubmittedSource;

        public void SubmitBrokenSource()
        {
            _source = "public static class Program\n{\n    public static int Main(string[] args)\n    {\n        return \"this is not an int\";\n    }\n}\n";
        }

        public IReadOnlyList<List<ChatMessage>> Requests
        {
            get
            {
                return _requests;
            }
        }

        public string LastToolResultText
        {
            get
            {
                List<ChatMessage> last = _requests.LastOrDefault();
                if (last == null)
                {
                    return "";
                }
                return LastToolResultIn(last);
            }
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options, CancellationToken cancellationToken = default)
        {
            List<ChatMessage> conversation = messages.ToList();
            _requests.Add(conversation);

            bool toolResultPresent = conversation.Any(message => message.Role == ChatRole.Tool);
            if (toolResultPresent == false)
            {
                return Task.FromResult(RequestExecution());
            }

            return Task.FromResult(Answer(LastToolResultIn(conversation)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatResponse response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
            {
                yield return update;
            }
        }

        public object GetService(Type serviceType, object serviceKey = null)
        {
            return null;
        }

        public void Dispose()
        {
        }

        private ChatResponse RequestExecution()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments["source"] = _source;
            FunctionCallContent call = new FunctionCallContent("call-1", "run_csharp", arguments);
            List<AIContent> contents = new List<AIContent>();
            contents.Add(call);
            ChatMessage message = new ChatMessage(ChatRole.Assistant, contents);
            return new ChatResponse(message);
        }

        private static ChatResponse Answer(string toolResult)
        {
            string standardOutput = ReadStandardOutput(toolResult);
            ChatMessage message = new ChatMessage(ChatRole.Assistant, "the program printed: " + standardOutput.Trim());
            return new ChatResponse(message);
        }

        private static string LastToolResultIn(List<ChatMessage> conversation)
        {
            ChatMessage tool = conversation.LastOrDefault(message => message.Role == ChatRole.Tool);
            if (tool == null)
            {
                return "";
            }
            FunctionResultContent result = tool.Contents.OfType<FunctionResultContent>().LastOrDefault();
            if (result == null)
            {
                return tool.Text ?? "";
            }
            return result.Result == null ? "" : result.Result.ToString();
        }

        private static string ReadStandardOutput(string toolResult)
        {
            using (JsonDocument document = JsonDocument.Parse(toolResult))
            {
                JsonElement run;
                if (document.RootElement.TryGetProperty("run", out run) == false)
                {
                    return "the submission did not compile";
                }
                if (run.ValueKind != JsonValueKind.Object)
                {
                    return "the submission did not compile";
                }
                JsonElement standardOutput = run.GetProperty("standardOutput");
                return standardOutput.GetString();
            }
        }
    }
}
