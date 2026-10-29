using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Coop.Services.Agent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Coop.Services.Agent.Integration.Tests
{
    [TestFixture]
    public class ConcurrentExecutionTests
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
        public async Task Test_That_We_Can_GiveTwoAgentsTheirOwnAnswerWhenBothRunAtOnce()
        {
            using (Barrier barrier = new Barrier(2))
            {
                ParameterEchoModel first = new ParameterEchoModel(barrier, "alpha");
                ParameterEchoModel second = new ParameterEchoModel(barrier, "beta");

                Task<AgentResponse> firstRun = RunAgentAsync(first);
                Task<AgentResponse> secondRun = RunAgentAsync(second);

                await Task.WhenAll(firstRun, secondRun).ConfigureAwait(false);

                string firstAnswer = firstRun.Result.Text;
                string secondAnswer = secondRun.Result.Text;

                Assert.That(firstAnswer, Does.Contain("alpha"));
                Assert.That(firstAnswer, Does.Not.Contain("beta"));
                Assert.That(secondAnswer, Does.Contain("beta"));
                Assert.That(secondAnswer, Does.Not.Contain("alpha"));

                using (JsonDocument firstResult = JsonDocument.Parse(first.ToolResult))
                using (JsonDocument secondResult = JsonDocument.Parse(second.ToolResult))
                {
                    string firstId = firstResult.RootElement.GetProperty("executionId").GetString();
                    string secondId = secondResult.RootElement.GetProperty("executionId").GetString();

                    Assert.That(firstId, Is.Not.EqualTo(secondId), "two concurrent executions must not share an identifier");
                    Assert.That(firstResult.RootElement.GetProperty("status").GetString(), Is.EqualTo("completed"));
                    Assert.That(secondResult.RootElement.GetProperty("status").GetString(), Is.EqualTo("completed"));
                }
            }
        }

        [Test]
        public async Task Test_That_We_Can_RunEightAgentsAtOnceWithoutAnyOfThemLosingItsAnswer()
        {
            int agents = 8;
            using (Barrier barrier = new Barrier(agents))
            {
                List<ParameterEchoModel> models = Enumerable
                    .Range(0, agents)
                    .Select(index => new ParameterEchoModel(barrier, "worker" + index))
                    .ToList();

                Task<AgentResponse>[] runs = models.Select(model => RunAgentAsync(model)).ToArray();
                await Task.WhenAll(runs).ConfigureAwait(false);

                List<string> identities = models
                    .Select(model => JsonDocument.Parse(model.ToolResult).RootElement.GetProperty("executionId").GetString())
                    .ToList();

                Assert.That(identities.Distinct().Count(), Is.EqualTo(agents), "every concurrent execution needs its own identifier");

                for (int index = 0; index < agents; index++)
                {
                    string expected = "worker" + index;
                    Assert.That(runs[index].Result.Text, Does.Contain(expected));
                    Assert.That(
                        models.Where(model => model.Who != expected).Any(model => runs[models.IndexOf(model)].Result.Text.Contains(expected)),
                        Is.False,
                        "no agent may report another agent's output");
                }
            }
        }

        private Task<AgentResponse> RunAgentAsync(ParameterEchoModel model)
        {
            return Task.Run(() =>
            {
                CoopExecutionOptions options = new CoopExecutionOptions();
                options.WorkingRoot = NewWorkingRoot();
                options.TimeoutSeconds = 60;

                ChatClientAgentOptions agentOptions = new ChatClientAgentOptions();
                agentOptions.Name = model.Who;
                agentOptions.ChatOptions = new ChatOptions();
                agentOptions.AIContextProviders = new AIContextProvider[]
                {
                    new CoopContextProvider(options),
                };

                AIAgent agent = new ChatClientAgent(new FunctionInvokingChatClient(model), agentOptions);
                return agent.RunAsync("Report which worker you are.");
            });
        }

        private string NewWorkingRoot()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "concurrent-" + Guid.NewGuid().ToString("N"));
            _createdDirectories.Add(path);
            return path;
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
