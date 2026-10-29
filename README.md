# Coop

Coop is a local C# execution capability for Microsoft Agent Framework. An agent submits C# source; Coop compiles it,
runs it in a contained process under a deadline, caches the result by content, and returns structured JSON.

## Why it exists

Microsoft Agent Framework already ships code execution: `Microsoft.Agents.AI.LocalCodeAct` runs code in a child process
with resource limits, `Microsoft.Agents.AI.Hyperlight` runs it in a VM-isolated sandbox, and
`Microsoft.Agents.AI.Tools.Shell` runs commands in a hardened container. All three are Python-centric. None of them
compiles C# with reference resolution, keeps a content-addressed compile cache, or isolates NuGet packages per
execution.

Without that, an agent that wants to check whether a C# idea compiles has two options: guess from reading, or hand the
work to a full CI pipeline and wait minutes for an answer to a question about four lines of code. Coop exists to remove
that choice.

Coop supplies the execution gap and nothing else. Orchestration, memory, telemetry, provider selection and hosting
belong to the framework and stay there.

## Shape

Every component is its own solution in its own folder, with its own source and test projects. Components consume each
other as packages from the on-disk feed rather than by project reference, so a component can be built, versioned and
tested on its own.

```
50chickens
  Coop.Execution       submission contract, compilation, contained execution, deadline enforcement
  Coop.Locking         serialises concurrent invocations against shared build output
  Coop.Hashing         content keys for artefacts and results
  Coop.Packages        per-execution package resolution and isolation
  Coop.Records         one record per execution, plus the derived index
  Coop.Template        the self-describing contract the product serves
  Coop.Artefacts       content-addressed artefact and result storage with a reuse marker
  Coop.Plan            per-project input hashing and the skip plan
  Coop.Gate            acts on the skip plan
  Coop.Surface         MCP tool surface and HTTP endpoint
  Coop.Watch           submissions written to a watched folder
  Coop.Pipeline        build, test, package, release, container image
  Coop.Agent           Microsoft Agent Framework integration and Agent Skill
  Coop.IssueWorkflow   an issue as a prompt: trigger gate, engine handover, validation, route back to the host
  50chickens.library.common
  nuget_repo
  docs/specs
```

## Build order

The order is the dependency order, not a wish list. `Coop.Execution` defines the contract every other component keys on,
so it is first and a change to it is breaking.

1. `Coop.Execution`
2. `Coop.Locking`
3. `Coop.Hashing`
4. `Coop.Packages`
5. `Coop.Records`
6. `Coop.Template`
7. `Coop.Artefacts`
8. `Coop.Plan`
9. `Coop.Gate`
10. `Coop.Surface`
11. `Coop.Watch`
12. `Coop.Pipeline`
13. `Coop.Agent`
14. `Coop.IssueWorkflow`

`Coop.IssueWorkflow` is last and consumes the gate rather than defining it, so a deployment that already has a runner
and a gate inherits the capability as a sequence over existing components.

## Specifications

`docs/specs/` holds one specification per component. They describe behaviour, contract, refusal conditions and
acceptance criteria. They deliberately contain no file names, folder names or class names, so an implementation may
choose its own structure without contradicting them.

## Building

Each solution restores and builds on its own:

```
dotnet build Coop.Execution/Coop.Execution.sln
```

Shared build settings and package versions are declared once at this root. Packages publish to `nuget_repo`, which is
also a configured source in `NuGet.config`, so a component that depends on another resolves it as a package.

## Toolchain

.NET SDK 10.0.402, target framework net10.0. Microsoft Agent Framework 1.26.0 from nuget.org. Docker is required only for
the container image and for running submissions on a host that is not Windows.
