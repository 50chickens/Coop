using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Coop.Services.Agent.Tests
{
    public sealed class CoopContextProviderTests
    {
        [Test]
        public void Test_That_We_Can_Get_The_Tool_And_Instructions_From_The_Context_Provider()
        {
            CoopContextProvider provider = new CoopContextProvider(TestOptions.Create());
            AIContextProvider.InvokingContext invoking = new AIContextProvider.InvokingContext(Agent(), null, new AIContext());

            AIContext context = provider.InvokingAsync(invoking, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            List<AITool> tools = context.Tools.ToList();

            Assert.That(tools, Has.Count.EqualTo(1));
            Assert.That(tools[0].Name, Is.EqualTo("run_csharp"));
            Assert.That(context.Instructions, Does.Contain("run_csharp"));
            Assert.That(context.Instructions, Does.Contain("parameters"));
        }

        [Test]
        public void Test_That_We_Can_Reject_A_Second_Context_Provider_On_One_Agent()
        {
            ChatClientAgentOptions options = new ChatClientAgentOptions();
            options.AIContextProviders = new AIContextProvider[]
            {
                new CoopContextProvider(TestOptions.Create()),
                new CoopContextProvider(TestOptions.Create()),
            };

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => new ChatClientAgent(new StubChatClient(), options));
            Assert.That(failure.Message, Does.Contain(CoopContextProvider.FixedStateKey));
        }

        private static AIAgent Agent()
        {
            return new ChatClientAgent(new StubChatClient(), new ChatClientAgentOptions());
        }
    }
}
