# The two workflows Coop is built to serve

Coop exists to answer one question cheaply: does this C# do what I think it does? Two workflows use that answer. They
are different in shape and they exercise different components, so both are needed before Coop can be called working.

## Workflow A: an agent checks its own work

An agent is writing C#, or explaining C#, and reaches a step where reading is not enough. It compiles and runs a
snippet instead of guessing.

```
agent turn
  -> Coop.Surface  (MCP tool: execute)
       -> Coop.Locking      acquire
       -> Coop.Hashing      artefact key from source + compiler + framework + rid
       -> Coop.Artefacts    cache hit? -> return with the reuse marker
       -> Coop.Packages     resolve pinned packages, isolated per execution
       -> Coop.Execution    compile with Roslyn, run in a child process, deadline kill
       -> Coop.Hashing      result key from artefact key + parameters + packages
       -> Coop.Artefacts    store result
       -> Coop.Records      write one record, update the derived index
       -> Coop.Template     the contract the caller read before it called
```

What it buys, in the order it matters:

| Without Coop | With Coop |
| --- | --- |
| The agent asserts what the code does and is sometimes wrong | The agent runs it and reports what happened |
| A compile check costs minutes of CI | A compile check costs a cache lookup or a few hundred milliseconds |
| The same snippet compiled twice costs twice | The second is a cache hit with a marker saying so |
| An infinite loop hangs the agent | The deadline kills the tree and returns a timed-out outcome |

The last row is the one that changes behaviour: an agent with no execution cannot afford to try things, so it guesses.
An agent with a cheap, contained, *bounded* execution path can try, which is a different kind of correctness.

### Worked example

The tool contract Coop.Surface publishes is specified in [specs/10-surface.md](specs/10-surface.md). In one call:

```json
{
  "source": "using System; public static class P { public static void Main(string[] a) { foreach (var s in a) Console.WriteLine(s.ToUpperInvariant()); } }",
  "parameters": { "greeting": "hello world" },
  "timeoutSeconds": 10
}
```

and the outcome carries `executionId`, `status`, `run.standardOutput`, `run.exitStatus`, `run.elapsedMilliseconds` and
a `reuse` block that is absent on a fresh run and present on a reused one.

The agent does not need to know where the cache lives, what the artefact key contains, or how the deadline is enforced.
That separation is the reason the contract is published as a document rather than as a class.

## Workflow B: an issue as a prompt

A human opens an issue. A self-hosted runner treats the issue body as the prompt of an agent, which changes a working
copy. A gate validates the change and the result comes back as a branch, a pull request, a comment and a closed issue.

Full behaviour, trust boundaries and acceptance criteria: [specs/14-issue-workflow.md](specs/14-issue-workflow.md).

```
issue opened
  -> Coop.IssueWorkflow  gate the trigger          refusal 8
       -> prepare a clean working copy             refusal 10
       -> invoke the engine                        refusal 9
       -> Coop.Gate        validate the change     outcome, not a refusal
       -> declared-path guard                      fails validation
       -> commit, push, open pull request          refusal 11
       -> comment and close
```

The differences from workflow A that matter:

| | Workflow A | Workflow B |
| --- | --- | --- |
| Who waits | One agent turn | A CI run |
| Trust in the input | The agent's own text | Untrusted third-party text |
| Cost of a mistake | A wrong sentence | A bad pull request |
| What enforces correctness | The deadline and containment | The gate, and a human review |
| Component that carries the risk | `Coop.Surface` | `Coop.IssueWorkflow` |

Workflow B is the reason the two are not the same component. Workflow A's threat is a snippet that loops; workflow B's
threat is a person who can write anything into a text box that becomes the prompt of an engine holding a model
credential. The central rule of the issue workflow follows: the issue body is data, never instruction to the runner, and
the engine holds no version control credential.

## How the two meet

A run of workflow B often contains workflow A inside it. The engine is an agent; while it works it can compile and run
C# through Coop to check what it is writing, which is exactly what makes it able to produce a change worth reviewing
rather than a plausible-looking guess.

That composition is the point of the product. Workflow A alone is a developer convenience. Workflow B alone is a code
generator. Together they are an agent that can verify its own work before offering it.

```
Workflow B
  orchestrator
    -> engine (MAF agent)
         -> Coop surface  -> compile and run a check
         -> Coop surface  -> compile and run the test it just wrote
    -> Coop gate           the verdict that matters
```

The engine's own executions are diagnostics. The gate is the verdict. A workflow that treats the engine's successful
run as proof of anything has confused the two, which is why the issue workflow says in as many words that the verdict
is a build and not an exit code.

## What each workflow proves about Coop

A workflow is only worth having if passing it means something. These are the facts each one establishes.

| Fact | Established by |
| --- | --- |
| A submission compiles and runs, and the output is its own | Workflow A, one call |
| A repeat submission is served from cache and says so | Workflow A, same call twice |
| A runaway submission is killed and reported rather than hanging | Workflow A, an infinite loop with a short deadline |
| A package request resolves and is isolated per execution | Workflow A, two submissions requesting different versions |
| Records survive the process and are retrievable by id | Workflow A, then fetch by `executionId` |
| A change is validated by the same gate a human runs | Workflow B, a green and a red run |
| An untrusted trigger reaches nothing | Workflow B, an unpermitted author |
| A change that edits the gate cannot pass it | Workflow B, a declared-path change |
| The engine holds no version control credential | Workflow B, a run log naming the stripped variables |

The last four are the ones that distinguish a workflow from an unauthenticated code path.
