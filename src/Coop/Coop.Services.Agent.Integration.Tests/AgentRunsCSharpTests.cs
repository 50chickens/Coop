using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Coop.Services.Agent.Integration.Tests
{
    [TestFixture]
    public class AgentRunsCSharpTests
    {
        private List<string> _createdDirectories;

        [SetUp]
        public void SetUp()
        {
            _createdDirectories = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            _createdDirectories
                .Where(path => Directory.Exists(path))
                .ToList()
                .ForEach(DeleteWithRetry);
        }

        [Test]
        public async Task Test_That_We_Can_TurnAModelToolCallIntoARealExecutionAndAnAnswer()
        {
            ScriptedChatModel model = new ScriptedChatModel();
            IChatClient client = new FunctionInvokingChatClient(model);
            ChatOptions options = new ChatOptions();
            options.Tools = new List<AITool> { CoopRunCSharp.CreateFunction(Options()) };

            List<ChatMessage> conversation = new List<ChatMessage>();
            conversation.Add(new ChatMessage(ChatRole.User, "What does the project1 hello runner print?"));

            ChatResponse response = await client.GetResponseAsync(conversation, options);

            Assert.That(model.Requests.Count, Is.EqualTo(2), "the model must be asked again after the tool returns");
            Assert.That(model.LastToolResultText, Does.Contain("\"executionId\""));
            Assert.That(response.Text, Does.Contain("project1 hello runner"));

            using (JsonDocument toolResult = JsonDocument.Parse(model.LastToolResultText))
            {
                JsonElement root = toolResult.RootElement;
                Assert.That(root.GetProperty("status").GetString(), Is.EqualTo("completed"));
                Assert.That(root.GetProperty("run").GetProperty("exitStatus").GetInt32(), Is.EqualTo(0));
                Assert.That(root.GetProperty("run").GetProperty("standardOutput").GetString().Trim(), Is.EqualTo("project1 hello runner"));
                Assert.That(root.GetProperty("compile").GetProperty("success").GetBoolean(), Is.True);
            }
        }

        [Test]
        public async Task Test_That_We_Can_ReportACompileErrorToTheModelAsAnAnswer()
        {
            ScriptedChatModel model = new ScriptedChatModel();
            model.SubmitBrokenSource();
            IChatClient client = new FunctionInvokingChatClient(model);
            ChatOptions options = new ChatOptions();
            options.Tools = new List<AITool> { CoopRunCSharp.CreateFunction(Options()) };

            List<ChatMessage> conversation = new List<ChatMessage>();
            conversation.Add(new ChatMessage(ChatRole.User, "Does this compile?"));

            ChatResponse response = await client.GetResponseAsync(conversation, options);

            using (JsonDocument toolResult = JsonDocument.Parse(model.LastToolResultText))
            {
                JsonElement root = toolResult.RootElement;
                Assert.That(root.GetProperty("status").GetString(), Is.EqualTo("failed"));
                Assert.That(root.GetProperty("compile").GetProperty("success").GetBoolean(), Is.False);
                Assert.That(root.GetProperty("compile").GetProperty("diagnostics").GetArrayLength(), Is.GreaterThan(0));
            }
            Assert.That(response.Text, Is.Not.Null);
        }

        private CoopExecutionOptions Options()
        {
            string workingRoot = Path.Combine(TestContext.CurrentContext.WorkDirectory, "coop-agent-" + Guid.NewGuid().ToString("N"));
            _createdDirectories.Add(workingRoot);
            CoopExecutionOptions options = new CoopExecutionOptions();
            options.WorkingRoot = workingRoot;
            options.TimeoutSeconds = 60;
            return options;
        }

        private static void DeleteWithRetry(string path)
        {
            int attempt = 0;
            while (attempt < 40)
            {
                attempt++;
                try
                {
                    Directory.Delete(path, true);
                    return;
                }
                catch (IOException)
                {
                    if (attempt == 40)
                    {
                        TestContext.Out.WriteLine("left " + path + " in place");
                        return;
                    }
                    System.Threading.Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException)
                {
                    if (attempt == 40)
                    {
                        TestContext.Out.WriteLine("left " + path + " in place");
                        return;
                    }
                    System.Threading.Thread.Sleep(50);
                }
            }
        }
    }
}
