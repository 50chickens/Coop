# End-to-end workflow and components

Two routes reach one engine. The diagram below is the whole system at the level a client cares about: what calls what, and which component owns which decision.

## The turn, end to end

```mermaid
flowchart TD
    client["Client<br/>agent under Microsoft Agent Framework,<br/>or any MCP client"]
    model["Model<br/>IChatClient, supplied by the caller<br/>Coop never owns this"]

    subgraph agent["Coop.Services.Agent - in-process route"]
        provider["CoopContextProvider<br/>contributes one tool + instructions"]
        tool["CoopRunCSharp<br/>run_csharp"]
    end

    subgraph surface["Coop.Services.Surface - MCP route"]
        mcpserver["MCP server over stdio<br/>execute, template, get, search"]
    end

    subgraph engine["Coop.Library.Execution - the engine"]
        resolver["SourceResolver<br/>one of source text or path,<br/>named refusal for neither"]
        compiler["SubmissionCompiler<br/>Roslyn, single file, console app"]
        deadline["ExecutionDeadline<br/>default 60 s, ceiling 600 s"]
        child["ChildProcess<br/>contained child, tree-kill at deadline"]
        outcome["ExecutionOutcome<br/>compile, run, timings, reuse"]
    end

    subgraph support["Supporting components"]
        locking["Coop.Library.Locking<br/>one run at a time"]
        hashing["Coop.Library.Hashing<br/>artefact key, result key"]
        artefacts["Coop.Library.Artefacts<br/>content cache + reuse marker"]
        records["Coop.Library.Records<br/>one record per execution"]
        packages["Coop.Library.Packages<br/>pinned version, local cache"]
        template["Coop.Library.Template<br/>the published contract"]
        common["Coop.Library.Common<br/>SHA, child process, atomic file,<br/>canonical key encoding"]
    end

    client -->|"asks a question<br/>only execution can settle"| model
    model -->|"tool call"| provider
    provider --> tool
    client -->|"tools/call"| mcpserver
    tool --> engine
    mcpserver --> engine

    resolver --> compiler
    compiler --> deadline
    deadline --> child
    child --> outcome
    outcome --> tool
    outcome --> mcpserver

    tool -.->|"surface route only"| locking
    mcpserver --> locking
    locking --> hashing
    hashing --> artefacts
    packages --> compiler
    outcome --> records
    mcpserver --> records
    mcpserver --> template
    engine --> common
    artefacts --> common
    hashing --> common
```

Read it as four boundaries:

| Boundary | What crosses | What does not |
| --- | --- | --- |
| Client to Coop | A question, and a tool call carrying C# source | A credential. Coop needs none |
| Model to tool | The decision to execute rather than read | Any control of compilation, containment or deadlines |
| Tool to engine | A submission and a deadline | The caller's parameters as source text — they travel as arguments |
| Engine to host | A child process that is killed at the deadline | Any child that outlives its deadline |

## What happens inside one execution

```mermaid
sequenceDiagram
    autonumber
    participant M as Model
    participant T as run_csharp
    participant E as Coop.Library.Execution
    participant R as Roslyn
    participant C as Child process

    M->>T: source, parameters, timeout
    T->>E: ExecutionRequest
    E->>E: SourceResolver: one source, or a named refusal
    E->>R: parse + compile, one syntax tree
    R-->>E: assembly, or diagnostics
    alt compile failed
        E-->>T: status failed, diagnostics, run absent
    else compile succeeded
        E->>C: dotnet exec submission.dll key=value...
        Note over C: deadline covers the run, not the compile
        alt finishes in time
            C-->>E: stdout, stderr, exit status
        else deadline expires
            E->>C: kill process tree
            E-->>E: deadlineExpired true, exitStatus null
        end
        E-->>T: status, run block, timings
    end
    T-->>M: one JSON string
    M->>M: answers from the observation
```

The second half is the part worth watching: the model is asked **again** after the tool returns. A tool call whose result never comes back is a tool that ran for nothing, and that round trip is the thing an integration test has to observe.

## Components, and the one thing each owns

```mermaid
flowchart LR
    subgraph libraries["Libraries - consumed by other components"]
        common2["Common<br/>canonical encoding,<br/>child process, atomic file"]
        execution2["Execution<br/>compile and run"]
        hashing2["Hashing<br/>what makes two runs the same"]
        locking2["Locking<br/>serialise against shared output"]
        packages2["Packages<br/>pin, cache, isolate"]
        artefacts2["Artefacts<br/>store and reuse marker"]
        records2["Records<br/>persist and search"]
        template2["Template<br/>publish the contract"]
    end

    subgraph services["Services - entry points"]
        surface2["Surface<br/>MCP server"]
        agent2["Agent<br/>framework tool + skill"]
        watch2["Watch<br/>folder consumer"]
        gate2["Gate<br/>run what changed"]
        plan2["Plan<br/>per-project input hashes"]
        pipeline2["Pipeline<br/>build, test, package, release"]
        issue2["IssueWorkflow<br/>an issue as a prompt"]
    end

    common2 --> execution2
    execution2 --> hashing2
    hashing2 --> artefacts2
    execution2 --> records2
    execution2 --> packages2
    locking2 --> execution2
    template2 --> surface2
    records2 --> surface2
    artefacts2 --> surface2
    execution2 --> agent2
    plan2 --> gate2
    gate2 --> issue2
    surface2 --> agent2
```

| Component | Owns exactly one decision |
| --- | --- |
| `Coop.Library.Execution` | Whether a submission compiles, what it printed, and whether it outlived its deadline |
| `Coop.Library.Locking` | Which invocation holds the shared build output |
| `Coop.Library.Hashing` | What makes two runs the same run |
| `Coop.Library.Packages` | Which exact versions are present, and where they came from |
| `Coop.Library.Artefacts` | Whether an answer is fresh or reused, and which run produced it |
| `Coop.Library.Records` | What happened, retrievably, after the process is gone |
| `Coop.Library.Template` | What a client is allowed to assume |
| `Coop.Services.Surface` | How a client reaches all of that without linking an assembly |
| `Coop.Services.Agent` | When an agent should reach for execution at all |

## The pipeline that proves it

```mermaid
flowchart LR
    ci["ci.ps1"] --> bt["build-test<br/>restore, build with warnings as errors, test"]
    bt --> mock["integration-mock<br/>one scripted turn, five artefacts"]
    mock --> pk["packages<br/>every component to the local feed"]
    pk --> rel["release<br/>publish the server"]
    rel --> dk["docker<br/>image from the publish output"]
    dk --> dt["deployment-test<br/>start the deployed server, wait for ready"]
```

`build-test` carries a duration budget and throws when it is exceeded, so a pipeline that grows announces it. `integration-mock` runs after it, so a broken build never produces a picture of a working system.

## What the mock proves, and what it does not

```mermaid
flowchart TD
    scripted["Scripted IChatClient<br/>deterministic, no provider, no tokens"]
    loop["FunctionInvokingChatClient<br/>the framework's real tool loop"]
    real["Real compilation and a real child process"]
    answer["Answer built from the observation"]

    scripted --> loop --> real --> answer

    unproven["NOT proven: that a real model<br/>chooses the tool, or that the<br/>description and skill text are<br/>good enough"]
```

The offline mock is the gate: it fails the moment the wiring breaks. Whether a real model *chooses* to execute is a property of the model and its instructions, and only a live run observes it. That one question costs tokens and should be asked rarely.

## Where each artefact lives

| Artefact | Path | Produced by |
| --- | --- | --- |
| Messages between agents | `shared_tasks/queued` | either session; the durable record |
| Transcript, trace, log, timeline, page | `ci/host/integration-mock/output` | the `integration-mock` stage |
| Component packages | `nuget_repo` | the `packages` stage |
| Published server | `release/coop-surface` | the `release` stage |
| Container image | `coop-surface:0.1.0` | the `docker` stage |

All of them are generated and none is committed.
