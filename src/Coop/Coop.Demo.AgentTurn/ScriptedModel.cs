using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Coop.Services.Agent;
using Microsoft.Extensions.AI;

namespace Coop.Demo.AgentTurn
{
    public sealed class ScriptedModel : IChatClient
    {
        private readonly Recorder _recorder;

        private string _toolResult = "";

        public ScriptedModel(Recorder recorder)
        {
            _recorder = recorder;
        }

        public int Turns { get; private set; }

        public string SubmittedSource
        {
            get
            {
                return Program.SubmittedSource;
            }
        }

        public string ToolResult
        {
            get
            {
                return _toolResult;
            }
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options, CancellationToken cancellationToken = default)
        {
            Turns++;
            List<ChatMessage> conversation = messages.ToList();
            bool toolPresent = conversation.Any(message => message.Role == ChatRole.Tool);

            if (toolPresent == false)
            {
                _recorder.Add("model", $"model turn {Turns}", "model asked for run_csharp instead of guessing");
                return Task.FromResult(RequestExecution());
            }

            _toolResult = LastToolResult(conversation);
            _recorder.Add("tool", "tool result returned to the model", _toolResult.Length + " characters of JSON");
            string standardOutput = ReadStandardOutput(_toolResult);
            _recorder.Add("model", $"model turn {Turns}", "model answered from the observation: " + standardOutput.Trim());
            return Task.FromResult(Answer(standardOutput));
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
            arguments["source"] = Program.SubmittedSource;
            FunctionCallContent call = new FunctionCallContent("call-1", CoopRunCSharp.ToolName, arguments);
            List<AIContent> contents = new List<AIContent>();
            contents.Add(call);
            ChatMessage message = new ChatMessage(ChatRole.Assistant, contents);
            return new ChatResponse(message);
        }

        private static ChatResponse Answer(string standardOutput)
        {
            ChatMessage message = new ChatMessage(ChatRole.Assistant, "the program printed: " + standardOutput.Trim());
            return new ChatResponse(message);
        }

        private static string LastToolResult(List<ChatMessage> conversation)
        {
            ChatMessage tool = conversation.Last(message => message.Role == ChatRole.Tool);
            FunctionResultContent result = tool.Contents.OfType<FunctionResultContent>().Last();
            return result.Result.ToString();
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
                return run.GetProperty("standardOutput").GetString();
            }
        }
    }
}
