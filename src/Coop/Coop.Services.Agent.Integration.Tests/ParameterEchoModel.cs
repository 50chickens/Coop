using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Coop.Services.Agent.Integration.Tests
{
    public sealed class ParameterEchoModel : IChatClient
    {
        public const string EchoSource = "public static class Program\n{\n    public static int Main(string[] args)\n    {\n        for (int index = 0; index < args.Length; index++)\n        {\n            System.Console.WriteLine(args[index]);\n        }\n        return 0;\n    }\n}\n";

        private readonly Barrier _barrier;

        private readonly string _who;

        private readonly List<string> _toolResults = new List<string>();

        public ParameterEchoModel(Barrier barrier, string who)
        {
            _barrier = barrier;
            _who = who;
        }

        public string Who
        {
            get
            {
                return _who;
            }
        }

        public string ToolResult
        {
            get
            {
                lock (_toolResults)
                {
                    return _toolResults.LastOrDefault();
                }
            }
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options, CancellationToken cancellationToken = default)
        {
            List<ChatMessage> conversation = messages.ToList();
            bool toolPresent = conversation.Any(message => message.Role == ChatRole.Tool);

            if (toolPresent == false)
            {
                return Task.FromResult(RequestExecution());
            }

            string toolResult = LastToolResult(conversation);
            lock (_toolResults)
            {
                _toolResults.Add(toolResult);
            }
            return Task.FromResult(Answer(toolResult));
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
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters["who"] = _who;

            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments["source"] = EchoSource;
            arguments["parameters"] = parameters;
            arguments["timeoutSeconds"] = 60;

            FunctionCallContent call = new FunctionCallContent("call-" + _who, "run_csharp", arguments);
            List<AIContent> contents = new List<AIContent>();
            contents.Add(call);
            ChatMessage message = new ChatMessage(ChatRole.Assistant, contents);

            _barrier.SignalAndWait(TimeSpan.FromSeconds(120));

            return new ChatResponse(message);
        }

        private ChatResponse Answer(string toolResult)
        {
            using (JsonDocument document = JsonDocument.Parse(toolResult))
            {
                JsonElement run = document.RootElement.GetProperty("run");
                string output = run.GetProperty("standardOutput").GetString().Trim();
                ChatMessage message = new ChatMessage(ChatRole.Assistant, "observed: " + output);
                return new ChatResponse(message);
            }
        }

        private static string LastToolResult(List<ChatMessage> conversation)
        {
            ChatMessage tool = conversation.Last(message => message.Role == ChatRole.Tool);
            return tool.Contents.OfType<FunctionResultContent>().Last().Result.ToString();
        }
    }
}
