using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.AI;

namespace Coop.Services.Agent.Tests
{
    public sealed class StubChatClient : IChatClient
    {
        public void Dispose()
        {
        }

        public object GetService(Type serviceType, object serviceKey = null)
        {
            return null;
        }

        public ChatClientMetadata GetMetadata()
        {
            return new ChatClientMetadata("stub");
        }

        public System.Threading.Tasks.Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("no model is available to this test");
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("no model is available to this test");
        }
    }
}
