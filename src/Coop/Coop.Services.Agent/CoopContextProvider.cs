using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Coop.Services.Agent
{
    public sealed class CoopContextProvider : AIContextProvider
    {
        public const string FixedStateKey = "CoopContextProvider";

        public const string Instructions = "You can compile and run C# by calling the `run_csharp` tool: the source is compiled and executed in a contained child process under a deadline, and the JSON result carries what it printed. Reach for it when reading is not enough to settle what the code does. One submission is one hypothesis - keep it small and print what it observed. Pass changing values in `parameters` rather than stitching them into the source. A compile error is an answer: read `compile.diagnostics`, correct the source once, and do not loop. The source you submit is recorded verbatim, so never submit secrets, credentials or personal data.";

        private static readonly IReadOnlyList<string> s_stateKeys = new string[] { FixedStateKey };

        private readonly CoopExecutionOptions _options;

        public CoopContextProvider(CoopExecutionOptions options)
        {
            _options = options;
        }

        public override IReadOnlyList<string> StateKeys
        {
            get
            {
                return s_stateKeys;
            }
        }

        protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            AIContext aiContext = new AIContext();
            aiContext.Instructions = Instructions;
            aiContext.Tools = new AITool[] { CoopRunCSharp.CreateFunction(_options.Copy()) };
            return new ValueTask<AIContext>(aiContext);
        }
    }
}
