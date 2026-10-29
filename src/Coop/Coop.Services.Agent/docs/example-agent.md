# Wiring Coop into an MAF agent

One page, two routes. The agent, the model client and the run loop are the host's; Coop contributes
a tool, a context provider and a skill.

## The provider route

The provider injects `run_csharp` plus the short guidance block on every invocation, so the model
sees the tool and the reasoning together.

```csharp
using System.Collections.Generic;
using Coop.Agent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

CoopExecutionOptions coopOptions = new CoopExecutionOptions();
coopOptions.WorkingRoot = @"C:\coop\runs";
coopOptions.TimeoutSeconds = 30;

AgentSkillsProvider skills = new AgentSkillsProviderBuilder()
    .UseCoopSkill()
    .Build();

ChatClientAgentOptions agentOptions = new ChatClientAgentOptions();
agentOptions.Name = "coop-engine";
agentOptions.ChatOptions = new ChatOptions()
{
    Instructions = "You are a C# engineer. Check what the code does before you describe it.",
};
agentOptions.AIContextProviders = new AIContextProvider[]
{
    skills,
    new CoopContextProvider(coopOptions),
};

AIAgent agent = new ChatClientAgent(chatClient, agentOptions);

AgentResponse response = await agent.RunAsync("Does DateTime.Parse(\"2024-02-30\") throw, or return a value?");
```

`chatClient` is the `IChatClient` the host already holds. Nothing in Coop constructs a model client,
runs a chat loop, or owns a session.

Output of the call the model makes:

```json
{"executionId":"41122878127a4658b652d85b3338b07c","status":"completed","reused":null,"cache":null,"compile":{"success":true,"diagnosticCount":0,"diagnostics":[]},"run":{"exitStatus":0,"standardOutput":"FormatException\r\n","standardError":"","deadlineExpired":false,"elapsedMilliseconds":721,"processorMilliseconds":62,"peakWorkingSetBytes":23154688}}
```

## The direct tool route

When one fixed working root and timeout will do for the agent's lifetime, skip the provider and put
the function on the agent's tool list. Add the skill provider if the model should also get the
written guidance.

```csharp
ChatClientAgentOptions agentOptions = new ChatClientAgentOptions();
agentOptions.Tools = new List<AITool>() { CoopRunCSharp.CreateFunction(coopOptions) };
agentOptions.AIContextProviders = new AIContextProvider[]
{
    new AgentSkillsProviderBuilder().UseCoopSkill().Build(),
};
```

Register one route, not both: two `run_csharp` tools on one agent give the model the same capability
twice, with two approval behaviours.

## When the submission should be approved first

```csharp
agentOptions.Tools = new List<AITool>()
{
    new ApprovalRequiredAIFunction(CoopRunCSharp.CreateFunction(coopOptions)),
};
```

Approval is per submission, not per statement: a submission can do anything its process can do once
it starts, so approving it approves everything it reaches. MAF surfaces the pending call and resumes
on the decision; Coop implements no approval prompt of its own.

## What is not here

No orchestrator, no workflow, no memory, no session store, no telemetry pipeline, no provider
selection, no hosting. `run_csharp` is traced as an ordinary tool invocation inside the host's run.
