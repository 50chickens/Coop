# Calling Coop from an agent

How a client compiles and runs C# through Coop. Two routes reach the same engine; pick by whether the client is an agent already running under Microsoft Agent Framework or an external process.

| Route | Use when | What you write |
| --- | --- | --- |
| In-process tool | Your agent is a `ChatClientAgent` in a .NET process you control | Attach `CoopContextProvider`; the model calls `run_csharp` |
| MCP over stdio | Your client is a separate process, or another agent framework entirely | Launch `Coop.Services.Surface` and call four MCP tools |

Both routes end in `Coop.Library.Execution`: Roslyn compilation, a contained child process, a deadline that kills the process tree.

## Route 1: attach the tool to an agent

Reference `Coop.Services.Agent`. It depends on `Coop.Library.Execution`, and neither reaches the network or a model provider — the model is whatever `IChatClient` you already have.

```csharp
CoopExecutionOptions options = new CoopExecutionOptions();
options.WorkingRoot = @"C:\work\coop-agent";
options.PackageCacheRoot = @"C:\work\coop-packages";
options.TimeoutSeconds = 60;

CoopContextProvider provider = new CoopContextProvider(options);

ChatClientAgentOptions agentOptions = new ChatClientAgentOptions();
agentOptions.AIContextProviders = new List<AIContextProvider> { provider };
```

The provider contributes two things on every invocation, and nothing else:

1. **One tool**, named `run_csharp`, taking C# source, an optional parameter map and an optional timeout.
2. **An instruction block** telling the agent when compiling and running settles a question that reading cannot, that one submission is one hypothesis, that parameters travel as arguments rather than being stitched into the source, and that a compile error is an answer to read rather than retry blindly.

Attach it once per agent. A second provider on the same agent is rejected through the framework's state-key validation rather than silently overriding the first.

If you wire tools manually instead of using the provider, build the same function yourself:

```csharp
AIFunction tool = CoopRunCSharp.CreateFunction(options);
chatOptions.Tools = new List<AITool> { tool };
```

### What the model sees and sends

The model sends one call:

```json
{
  "source": "public static class Program\n{\n    public static int Main(string[] args)\n    {\n        System.Console.WriteLine(\"hello\");\n        return 0;\n    }\n}\n",
  "parameters": { "greeting": "hello world" },
  "timeoutSeconds": 30
}
```

Rules that matter to the caller:

- The source is one file, compiled as a console application whose entry point is `static int Main(string[] args)`.
- Parameters arrive as command-line arguments, one per entry, `key=value`, sorted by key with an ordinal comparison. The value is everything after the first `=` and is never quoted or escaped. A parameter whose value contains `=` or a space works; one whose key is empty does not.
- The deadline is the caller's, clamped to a 600-second ceiling. It covers the run, not the compilation.

### What comes back

One JSON string. A compile failure, a non-zero exit and an expired deadline are values in this JSON, never exceptions:

```json
{
  "executionId": "5ccd178a0e09445d9c49b4136f8115a5",
  "status": "completed",
  "reused": null,
  "cache": null,
  "compile": { "success": true, "diagnosticCount": 0, "diagnostics": [] },
  "run": {
    "exitStatus": 0,
    "standardOutput": "hello world\n",
    "standardError": "",
    "deadlineExpired": false,
    "elapsedMilliseconds": 146,
    "processorMilliseconds": null,
    "peakWorkingSetBytes": null
  }
}
```

| Field | Meaning |
| --- | --- |
| `status` | `completed`, `failed`, or a refusal |
| `compile.diagnostics` | `id`, `severity`, `message`, `filePath`, `line`, `column` for each error |
| `run` | Absent when the submission did not compile |
| `run.exitStatus` | `null` when the child never started or was killed at its deadline — never a fabricated zero |
| `run.deadlineExpired` | The tree was killed at the deadline |
| `processorMilliseconds`, `peakWorkingSetBytes` | `null` when unmeasured. NULL, not `0` and not the string `"unavailable"` |
| `reused` | Present when the answer came from a store rather than this run; it names the execution that produced it |

A client must treat the two cost fields as nullable. Today they are always null on this route, because the process runner creates no job object and therefore holds no handle to measure — a limitation of the runner, not of the platform.

## Route 2: call the MCP server

`Coop.Services.Surface` speaks MCP over stdio. Publish it, run it, and connect with any MCP client.

```
dotnet publish src/Coop/Coop.Services.Surface/Coop.Services.Surface.csproj -c Release -o release/coop-surface
dotnet release/coop-surface/Coop.Services.Surface.dll
```

It writes its startup lines to **standard error** and nothing to standard output except the protocol:

```
coop-surface 0.1.0
data root: ...\coop-data
record store root: ...\coop-data\records
tools (4): execute, template, get, search
ready
```

`ready` is the line to wait for. A client that reads standard output for diagnostics will break the protocol — the payload owns standard output.

| Tool | Takes | Returns |
| --- | --- | --- |
| `execute` | C# source, optional parameters, optional timeout, optional paths to assemblies to reference | The same outcome JSON as route 1 |
| `template` | nothing | The published contract: request fields and defaults, entry point, parameter mechanics, references, timeout ceiling, response shape |
| `get` | an `executionId` | The stored record, or the named refusal for an unknown id |
| `search` | optional filters and a limit | Matching records, plus whether the index behind the answer was stale |

Read `template` first. It is generated from the code and a test fails the build if it drifts from the outcome type, so it is the contract rather than a copy of it.

## Choosing a timeout

The default is 60 seconds and the ceiling is 600. A request above the ceiling receives the ceiling rather than a refusal, because the caller asked for patience rather than made an error. Set it well below the ceiling for a snippet: a submission that has not finished in ten seconds is usually a loop, and the only thing a longer deadline buys is ten more seconds of a core.

## Failure shapes a client must handle

| What happened | What the client sees |
| --- | --- |
| Submission did not compile | `status: failed`, `compile.success: false`, diagnostics populated, `run` absent |
| Submission exited non-zero | `status: failed`, `run.exitStatus` non-zero, output preserved |
| Submission hung | `status: failed`, `run.deadlineExpired: true`, `run.exitStatus: null`, output up to the kill |
| Submission supplied neither source field | A named refusal, not an incidental read error |
| A source path or a reference path does not exist | A named refusal carrying `status` `refused`, the `path` and the reason, on standard output as valid JSON, with exit code 1. Never an exception, never a stack trace |
| Package support unavailable | A named refusal. Package resolution is not yet wired into this route |

## What Coop deliberately does not do

It does not choose a model, keep a conversation, orchestrate several agents, store memory or emit telemetry. Those belong to the framework. Coop compiles C#, runs it under a deadline and reports what happened.
