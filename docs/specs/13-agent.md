# 13 - Microsoft Agent Framework integration

## Scope

Coop adds one capability to Microsoft Agent Framework (MAF): an agent can compile and run C# as a
single step of its work. This document specifies how that capability is surfaced to an MAF agent,
and states plainly which of MAF's existing mechanisms Coop must leave alone.

Coop contributes a tool and a skill. It contributes no orchestrator, no conversation store, no
telemetry pipeline and no approval mechanism.

## The tool

| Property | Value |
| --- | --- |
| Name | `run_csharp` |
| Description | Compiles one C# submission and runs it in a contained child process under a deadline, returning JSON: compile metrics, diagnostics, standard output, standard error, exit status and cache status. |
| Arguments | `source` (string, required), `parameters` (map of string to string), `packages` (array of `{id, version}`), `timeoutSeconds` (integer). `name`, `description` and `category` are accepted with server-side defaults. |
| Result | One string of JSON. The fields are fixed: `executionId`, `status`, `reused`, `cache`, `compile`, `run`. |
| Approval | Required when the agent may submit code whose effects matter; see Approval below. |

The tool's argument list and the shape of its JSON are specified in 10-surface.md (The MCP tool,
Arguments and The JSON returned). They are the integration contract and do not vary by registration
route.

A tool result is a string, not a typed object. MAF delivers the function's return value to the model
as content; a structured result would have to be serialised anyway, and a string keeps the contract
in one place. The JSON is the contract; the fact that it arrives as text is an implementation
detail of the function-calling loop.

`run_csharp` reports failure in its result, not by throwing. A compile error, a non-zero exit and an
expired deadline are all outcomes the agent is expected to read and act on. The only failures that
throw are the ones where no result exists: an unreachable Coop endpoint, or a refusal (a malformed
request, or a package that does not exist), which is surfaced as an error result carrying `field`
and `reason`.

## Registering the tool

There are three routes. A deployment picks one; a deployment that registers the same capability
twice gives the model two identical tools and two different approval behaviours.

### Direct tool on the agent

Use `AIFunctionFactory.Create` to build an `AIFunction` over a delegate that takes the argument
types listed under The tool and returns the JSON string, then pass it to the agent's tool list when
the agent is created - `AsAIAgent` on a chat client, or `ChatClientAgentOptions.Tools` on
`ChatClientAgent`.

This route fits when the Coop endpoint, the default timeout and the package policy are fixed for the
agent's lifetime, which is the ordinary case. The factory derives the tool's JSON schema from the
delegate, so the schema and the code cannot drift.

### Provider-owned surface

Subclass `AIContextProvider` and return, from `ProvideAIContextAsync`, an `AIContext` whose `Tools`
contains the `run_csharp` function and whose `Instructions` contains the short guidance of The Agent
Skill, trimmed to the four lines that matter before a first call: one submission is one hypothesis,
parameters are a map rather than stitched source, a compile error is an answer, and the source text
is recorded.

This route fits when the surface must be shaped per invocation: when the tool set depends on the
tenant, when the default timeout or allowed package list differs per request, or when the model
needs the reasoning guidance in front of it every time.

Two rules make it safe:

- `StateKeys` returns one fixed value, so `ChatClientAgent`'s state-key uniqueness validation
  rejects a second registration of the same provider on one agent. A provider that lets the default
  state key be its own type name permits silent double registration.
- The provider reads its mutable configuration once per invocation and builds the tool from that
  snapshot. It does not mutate shared agent configuration while a run is in flight.

The provider owns its tool registry and exposes add, remove and list operations, mirroring the
pattern MAF's own code-execution providers use: a tool that should be reachable from inside Coop is
registered on Coop, and the agent's direct tool list is untouched.

### Approval

Wrap the function in `ApprovalRequiredAIFunction` when the agent may submit code the caller should
see before it runs - anything that touches the file system, the network, or a database.

Approval is bundled, not per call, for the same reason MAF's code-execution providers bundle it: a
submission can do anything its process can do after it starts, so approving the submission is
approving everything it will reach. There is no per-statement approval and no way to approve a
compilation and reject an execution, because compilation is not the risk.

A provider that wants approval unconditionally exposes that as its own configuration; the default is
to require approval if and only if the registered tool set requires it.

### Through an MCP client

When Coop runs as an MCP server (10-surface.md, The MCP tool), the agent needs no Coop package at
all:

1. Connect an `McpClient` to the server's transport.
2. Call `ListAgentToolsWithTasksAsync`, which lists the server's tools and returns them as
   `IReadOnlyList<AIFunction>`, each a `TaskAwareMcpClientAIFunction`.
3. Pass the list to `AsAIAgent(tools: ...)` or `ChatClientAgentOptions.Tools`.

The task-aware function is what makes a slow execution safe: it opts into the MCP Tasks extension,
polls a task-backed call to completion, honours the server's polling interval within the bounds set
by `McpTaskOptions`, and propagates local cancellation as a best-effort remote cancel. Coop must not
build its own polling loop on top of it.

Mapping of concerns on this route:

| Concern | Owner |
| --- | --- |
| Poll interval and its bounds | `McpTaskOptions` |
| Cancellation propagation | `McpTaskOptions.CancelRemoteTaskOnLocalCancellation` |
| "Task stuck" detection | `McpTaskOptions.MaxConsecutiveStuckPolls` |
| Execution deadline | Coop, inside the submission |
| Delivering the JSON to the model | MAF |

A submission's deadline and an MCP task's lifetime are different clocks. Coop's deadline kills the
child; the task's lifetime bounds how long the client waits. A client timeout shorter than Coop's
deadline leaves the submission running with nobody reading its result, and the record is the only
place it can be found.

## What Coop must not reimplement

| MAF mechanism | What it already owns | What Coop must do instead |
| --- | --- | --- |
| Orchestration: `Microsoft.Agents.AI.Workflows`, `WorkflowBuilder`, `Executor`, `Workflow` | Sequencing agent and tool steps, fan-out, checkpointing, resumption, run events | Expose one function. A multi-step investigation is a workflow the host composes. Coop contributes no graph, no step state machine and no retry policy. |
| Memory: `ChatHistoryProvider`, `AIContext.Messages`, `AgentSession`, `AgentSessionStore` | Conversation history, per-session state, persistence of that state | Expose one function and, on the provider route, transient instructions. Coop keeps no conversation state and does not write into `AgentSession`. Its execution records are a separate store with a different purpose (what ran, and what it printed) and are never merged into chat history. |
| Telemetry: MAF's OpenTelemetry instrumentation (`UseOpenTelemetry` on the agent builder) | Spans for the agent run and for each tool invocation, with attributes and parentage | Do nothing but call the tool. `run_csharp` is traced as a normal tool invocation nested inside the surrounding run. Coop emits no span of its own for the tool call, no second activity source for the agent, and no duplicate metrics. Its own execution timings travel in the tool result, where they are data rather than telemetry. |
| Function invocation loop: `FunctionInvokingChatClient` | Deciding when to call a tool, marshalling `AIFunctionArguments`, feeding the result back, iterating | Do not wrap or replace the chat client. Coop supplies a function and nothing else. |
| Approval: `ApprovalRequiredAIFunction` | Surfacing a pending approval to the host and resuming on a decision | Wrap or do not wrap. Never implement a second approval prompt inside the tool. |
| Tool schema: `AIFunctionFactory`, `AIFunction.JsonSchema` | Deriving the JSON schema the model sees | Build the function with the factory and let it derive the schema. A hand-written schema is a second contract that drifts. |
| Skills: `AgentSkillsProvider` and the `load_skill`, `read_skill_resource`, `run_skill_script` tools | Progressive disclosure of skill content to the model | Contribute a skill through the skills provider. Coop must not add its own skill tools, its own skill index or its own prompt section. |

The rule behind the table: Coop is a capability, and MAF is the host. A capability that grows its
own orchestrator, its own memory or its own telemetry stops being composable, and the duplication is
what makes an agent's behaviour impossible to attribute.

## The Agent Skill

The tool tells the model what it can do. The skill tells it when doing it is the right move. Both are
needed: a model with only the tool will compile-and-run a question whose answer was in the source.

A skill is one directory containing a skill document with YAML frontmatter and a markdown body, plus
optional resource and script subdirectories. Coop's skill has no scripts: running a submission is
the tool's job, not a skill script's, and a skill script that shells out to the same tool duplicates
the approval surface.

### Frontmatter

| Key | Value |
| --- | --- |
| `name` | `compile-and-run-csharp` |
| `description` | One sentence naming the trigger: use when a question about C# or .NET is settled by executing a few lines rather than by reading a solution, and the answer must be exact. |
| `license`, `compatibility`, `allowed-tools`, `metadata` | Optional. `allowed-tools` names `run_csharp` when the host enforces it. |

The description is the only part of the skill the model sees before deciding to load it. It must
carry the trigger, not a summary of the body; a description that says "guidance about C#" is never
loaded for the question it was written for.

### Body

The body is short enough to be read in one pass. It states:

1. When to use the skill. Behaviour questions - does this compile, what does this print, what does
   this throw, what does this string operation return, what does this version of a library do -
   where running a few lines is cheaper and more certain than reading.
2. When not to use it. The answer is already in the source; the code needs credentials, a network
   service, a database or host state the execution does not have; the question needs a project
   reference or a full solution build, which a single submission cannot have; the work would exceed
   the deadline; or the caller needs a measurement with attribution, which a single run cannot give.
3. How to submit. One source file, a `static int Main(string[] args)` entry point, parameters as a
   map rather than stitched into the source, packages pinned to exact versions.
4. How to read the result. Compile diagnostics answer a question too - a submission that fails to
   compile has told you something. `deadlineExpired` is not a pass. `reused` means the timings
   belong to an earlier execution.
5. What is recorded. The source text is stored verbatim in the execution record. Secrets,
   credentials and personal data do not go into a submission.
6. The shape of a good submission. Small, one hypothesis, prints what it observed, returns a
   non-zero status only when the hypothesis failed rather than when the program errored.

The skill does not restate the field list. It names one resource that does, so the field list has one
copy.

### Registration

| Form | Registration |
| --- | --- |
| File-based skill directory | `AgentFileSkillsSource` over the skill directory, or `AgentSkillsProviderBuilder.UseFileSkill(<skill directory>)`. |
| Inline skill | `AgentInlineSkill` with the same name, description, body and resource, read through `AgentSkillsProviderBuilder.UseSkill` or an `AgentSkillsSource`. |

Either way the resulting provider - `AgentSkillsProvider`, from `AgentSkillsProviderBuilder.Build` -
is added to the agent's `ChatClientAgentOptions.AIContextProviders`. Duplicate names across sources
are deduplicated case-insensitively by the provider; Coop's skill name must therefore be unique in
the host's skill set.

The skill's resource is a document listing the `run_csharp` arguments, their defaults, the entry
point form, the parameter convention, the reference and package rules and the result fields. It is
read with the framework's `read_skill_resource` tool; Coop adds no tool for it.

### What the skill must not claim

- It must not claim the tool is free. A cold compile is hundreds of milliseconds and a cold package
  resolve is seconds; the skill says so, and says that repeated identical submissions are reused.
- It must not claim containment the tool does not give. Coop contains a runaway submission with a
  deadline and a process tree kill; it is not a security sandbox for hostile code, and the skill says
  the tool is for code the agent or its caller already trusts.
- It must not instruct the model to retry a failed compilation by editing the source repeatedly
  without limit. One corrective attempt per failure is the guidance; a loop of compiles is the
  behaviour the deadline exists to stop.

## Refusals

| Condition | Behaviour |
| --- | --- |
| The Coop endpoint is unreachable | The function throws; MAF reports a tool failure to the model. No result is fabricated. |
| The request is refused (`field`, `reason`) | The function throws with both values, or returns an error result carrying them; the choice is the host's and must be consistent. |
| A second `AIContextProvider` of Coop's type is registered on one agent | `ChatClientAgent` rejects the configuration at construction, because both providers return the same `StateKeys`. |
| A skill directory has no skill document, or its frontmatter lacks `name` or `description` | `AgentFileSkillsSource` fails to load it; the skill is absent rather than silently half-registered. |
| Two skills in one provider share Coop's name | The provider deduplicates case-insensitively, first wins; the duplicate is logged as a warning. |

## Limitations

- The tool is synchronous from the model's point of view. A submission that takes its full deadline
  holds the tool call for that long; there is no partial result, no streaming output and no progress
  event, so a caller cannot tell a slow submission from a hung one until the deadline fires.
- `AIContextProvider` is consulted before model invocation and cannot see the model's later tool
  calls. Coop's provider route can shape the tool set per invocation but cannot react to a
  submission's result mid-turn.
- The skill is guidance. Nothing enforces it: a model that ignores the skill still gets the tool. The
  only enforcement available is approval on the function and an allow-list on the tool set, and both
  are coarser than the skill's advice.
- Skill loading costs a tool round trip. A host that never loads the skill sees only the tool's
  description, so the description must be good enough on its own; the skill is an improvement, not a
  requirement.
- The MCP route exposes the execution to any client that can reach the server, subject to the
  server's bind policy, and gives the agent no way to distinguish a reused result from a fresh one
  other than reading `reused` in the JSON.
- Coop cannot report a submission's compile duration as telemetry, because it emits none; the number
  exists only inside the tool result.
- A host that registers both the direct tool and the MCP route presents the model with two tools of
  the same name and different approval behaviour. Nothing in MAF prevents it.
- Nothing in this document has been exercised against a running MAF agent. The types named below are
  MAF's public surface and the integration is specified, not measured; a host that finds a named
  member absent from its MAF version has a version mismatch, not an ambiguity in this contract.

## MAF public types this specification depends on

| Type | Why it is named |
| --- | --- |
| `AIFunction` | The tool abstraction the capability is delivered as. |
| `AIFunctionFactory` | Creates the function and derives its JSON schema. |
| `AIFunctionArguments` | Carries the model's arguments into the function. |
| `AIContext` | Carries injected `Tools` and `Instructions` for one invocation. |
| `AIContextProvider` | The per-invocation extension point for the provider route. |
| `AIAgent` | The agent the tool is attached to. |
| `ChatClientAgent` | The agent implementation whose construction validates provider state keys. |
| `ChatClientAgentOptions` | Where `Tools` and `AIContextProviders` are supplied. |
| `ApprovalRequiredAIFunction` | The framework's approval wrapper; Coop must use it rather than its own. |
| `McpClient` | The MCP client that reaches Coop as a server. |
| `ListAgentToolsWithTasksAsync` | The extension that returns MCP tools as task-aware `AIFunction`s. |
| `TaskAwareMcpClientAIFunction` | The function type that polls MCP tasks to completion. |
| `McpTaskOptions` | The bounds on that polling and on cancellation. |
| `AgentSkill`, `AgentSkillFrontmatter` | The skill model and its metadata. |
| `AgentSkillsProvider` | The provider that turns skills into `load_skill` and its siblings. |
| `AgentSkillsProviderBuilder` | Composes sources into that provider. |
| `AgentFileSkillsSource` | Loads a skill from a directory. |
| `AgentInlineSkill` | Defines a skill in code instead. |
| `ChatHistoryProvider` | Conversation memory Coop must not duplicate. |
| `AgentSession`, `AgentSessionStore` | Per-session state and its persistence, which Coop must not duplicate. |
| `FunctionInvokingChatClient` | The tool loop Coop must not replace. |
| `WorkflowBuilder`, `Executor`, `Workflow` | Orchestration Coop must not reimplement. |

## Acceptance criteria

1. An agent created with the `run_csharp` function can compile and run a submission and receives the
   JSON of The tool as the tool result, in one tool call, with no second tool needed to read it.
2. The tool's advertised schema has `source` required and `parameters`, `packages` and
   `timeoutSeconds` optional, and matches the argument list in 10-surface.md.
3. A submission that fails to compile returns a result with `status` `failed`, non-empty
   `compile.diagnostics` and `run` null; the tool does not throw.
4. A submission that exceeds its deadline returns a result with `deadlineExpired` true and
   `exitStatus` null; the tool does not throw.
5. Registering the provider route twice on one agent fails at agent construction, and the failure
   names the conflicting state key.
6. With approval required, the tool call is surfaced to the host before anything is compiled, and a
   rejected approval compiles and runs nothing.
7. An agent connected to Coop through `McpClient` and `ListAgentToolsWithTasksAsync` gets the same
   tool name, argument list and result JSON as the direct route.
8. A cancelled tool call on the MCP route issues a best-effort remote cancel, and a submission whose
   client stopped waiting is still killed at its own deadline and recorded.
9. The skill loads through the skills provider, is listed by name and description before loading, and
   its body names `run_csharp` and the resource that carries the field list.
10. The skill's description contains the trigger condition, not a topic summary.
11. No Coop-supplied tool other than `run_csharp` appears in the agent's tool list, and no Coop
    `AIContextProvider` returns `load_skill`, `read_skill_resource` or `run_skill_script`.
12. An agent using Coop emits exactly the telemetry MAF emits for an ordinary tool invocation: one
    tool span per call, no additional activity source, no duplicate metrics.
