using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Coop.Services.Agent;
using Microsoft.Extensions.AI;

namespace Coop.Demo.AgentTurn
{
    public static class Program
    {
        internal const string SubmittedSource = "public static class Program\n{\n    public static int Main(string[] args)\n    {\n        System.Console.WriteLine(\"project1 hello runner\");\n        return 0;\n    }\n}\n";

        public static async Task<int> Main(string[] args)
        {
            string outputRoot = args[0];
            string workingRoot = Path.Combine(outputRoot, "work");
            Directory.CreateDirectory(workingRoot);

            Recorder recorder = new Recorder();

            CoopExecutionOptions options = new CoopExecutionOptions();
            options.WorkingRoot = workingRoot;
            options.TimeoutSeconds = 60;

            ScriptedModel model = new ScriptedModel(recorder);
            IChatClient client = new FunctionInvokingChatClient(model);

            ChatOptions chatOptions = new ChatOptions();
            chatOptions.Tools = new List<AITool> { CoopRunCSharp.CreateFunction(options) };

            List<ChatMessage> conversation = new List<ChatMessage>();
            conversation.Add(new ChatMessage(ChatRole.User, "What does the project1 hello runner print?"));

            recorder.Add("turn", "conversation started", "user asked for one observable fact");

            Stopwatch total = Stopwatch.StartNew();
            ChatResponse response = await client.GetResponseAsync(conversation, chatOptions);
            total.Stop();

            recorder.Add("turn", "conversation finished", $"{total.ElapsedMilliseconds} ms, {model.Turns} model turns");

            Artefacts.Write(outputRoot, workingRoot, recorder, model, response);

            Console.WriteLine($"transcript:  {Path.Combine(outputRoot, "transcript.md")}");
            Console.WriteLine($"trace:       {Path.Combine(outputRoot, "trace.json")}");
            Console.WriteLine($"execution:   {Path.Combine(outputRoot, "execution.log")}");
            Console.WriteLine($"timeline:    {Path.Combine(outputRoot, "timeline.svg")}");
            Console.WriteLine($"viewer:      {Path.Combine(outputRoot, "index.html")}");
            Console.WriteLine($"answer:      {response.Text}");
            return 0;
        }
    }
}
