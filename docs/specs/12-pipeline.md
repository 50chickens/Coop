# 12 - Build, test, package and release

## The entry point and its stages

One script is the entry point. It takes a stage name, resolves configuration, checks its
preconditions, runs the stages in order, and exits on the first non-zero step.

| Stage | Does | Must not do |
| --- | --- | --- |
| `build` | Restores, builds the solution in the chosen configuration, then verifies formatting read-only. | Run tests. |
| `test` | Runs the tests for the solution or, when the caller asks for the incremental behaviour, runs the gate of 09-gate.md. | Build again. |
| `publish` | Publishes each application to its release output. | Modify source. |
| `container` | Builds the container image from published output. | Push the image. |
| `stage` | Writes release metadata and the release archive, and verifies the archive. | Publish again. |
| `all` | `build`, `test`, `container`, `stage`, in that order. | Skip a step because an earlier one passed earlier in the day. |

Rules for the entry point:

- Configuration comes from one document, resolved by one function. A script does not compute a path
  that configuration already holds.
- Every step writes its own progress through one logging function with a timestamp prefix. A step
  that is a no-op says so and why.
- The pipeline never invokes the build twice for one stage chain. `publish` and `stage` run against
  the output of the `build` stage.
- Container builds happen only through the container build command. Nothing constructs an image by
  copying files, and no image is created as a side effect of `publish`.
- The image tag is a single value used by the build and, when pruning, by the same script. A second
  literal tag is a defect.

## The local package feed

Components of the product consume each other as packages, not as project references. One folder in
the repository is the feed.

| Rule | Statement |
| --- | --- |
| Location | One folder at the repository root, its path from a single build property. Nothing writes a package anywhere else in the tree. |
| Sources | The repository's package source list contains exactly two entries: that folder, and the public NuGet feed. The folder is first. No third source exists. |
| Cache | The global packages folder is inside the repository, so a restore writes nothing into the user profile. |
| Granularity | One package per component. A component's package contains its library assembly and nothing else; test projects are never packaged. |
| Identity | The package id is the product name followed by the component's role, fixed at first publication and never renamed. A rename breaks every consumer at once. |
| Version | One version property in shared build configuration supplies every package's version, and the same value names the release. A component does not carry its own version. |
| Central pinning | Third-party package versions are declared once in central package management. A project file names a package and not a version. |
| Consumption | A component references another component by package id and the current version. The only project reference in a component's tree is from its test project to the component under test. |
| Order | Packages are produced in dependency order: a component's dependencies are packed before the component is built. |
| Replacement | A stage that packs writes the package into the feed for its version. A rebuild of the same version replaces the feed's copy, so the next restore sees the tree's own output. |

Consequences a reader must design for:

- A change to a component is invisible to its consumers until it is packed. A build that compiles a
  consumer straight after editing a dependency uses the packaged copy, not the edited source, and
  will appear to ignore the edit.
- Because the version does not change per build, a restore that already resolved that version into
  the local package cache will not re-read the replaced package. The pipeline clears the cache entry
  for a repacked component, or bumps the version, before building consumers.
- The feed is a plain directory of package files. It has no index, no retention and no signature. It
  is not a substitute for a feed server, and nothing in the product depends on one existing.

## The container image

The image exists because a submission must be executable on a host that is not Windows, where the
containment primitive the Windows host uses does not exist.

### Contents

| Layer | Rule |
| --- | --- |
| Base | The .NET runtime image for the pinned framework version, on the Alpine variant. The runtime, not the SDK: submissions are compiled by the server's embedded compiler and executed with the runtime. |
| Shell | PowerShell, copied from the PowerShell Alpine image as its own layer so the runtime layer is not rebuilt for it. The image's single entry point is a PowerShell script. |
| Application | The published, framework-dependent output of the server application, copied in as its own layer so a code change does not rebuild the base. |
| Entry point | Exactly one startup script, as the container's command. One process, no supervisor. |
| Port | One exposed port, the listener's. |
| Pinning | Every base image is pinned to an exact tag. No tag floats. |
| Context | The build context is the image's own directory. The published application is the only large content, and it is written by the publish step, not committed. |

### What replaces the Windows containment primitive

| Guarantee | On a Windows host | In the container |
| --- | --- | --- |
| The submission cannot outlive the server | A job object with kill-on-close, closed with the server | The server exits, the startup script returns, the container's single process exits, the container stops |
| The submission is killed at its deadline | Terminate the job, then kill the process tree | Kill the process tree |
| Per-process memory ceiling | A limit on the job object | The container's memory limit, 2 GiB |
| Process count and CPU | not imposed | The container's process limit, 256, and CPU limit, 2 |
| Processor time and peak working set reported | Read from the process handle | Not available; reported as null |

The containment primitive is used where the platform provides it. Where it does not, execution
relies on the container's limits plus a process-tree kill at the deadline, and reports processor
time and peak working set as null. There is no third mechanism: a host that provides neither the
primitive nor a container has no containment, and nothing in the outcome says so.

### What the image is not

It is not a hardened multi-tenant sandbox. It has no seccomp profile of its own, no user namespace
remapping, and no network policy. Its containment is the container's resource limits plus the
server's own deadline. A deployment that needs more must supply it around the container.

## Release staging

| Artifact | Rule |
| --- | --- |
| Published applications | Each application is published framework-dependent, from the output the build stage produced, so what ships is what was tested. |
| Metadata document | One JSON document at the release root: `{ "version", "runtimeIdentifier", "configuration", "commit", "stagedAt" }`. `commit` is read from the repository read-only; a missing repository yields no commit rather than a fabricated one. |
| Archive | One archive named `<product>-<version>-<runtime identifier>`, built from the published applications and the metadata document. |
| Verification | The archive is opened after writing and asserted to contain each application's entry assembly. A stage that cannot verify its own archive fails. |
| Repeatability | A second `stage` run deletes and rewrites the stage output rather than skipping. |

The metadata document is the only place a build records which commit it came from. Nothing else
writes a version into a file under source control.

## What the pipeline gates

| Gate | When | Failure |
| --- | --- | --- |
| Formatting verification | After the build, before tests | Build stage fails; no file is written. |
| Unit and integration tests | Test stage | Non-zero exit from the test run fails the chain. |
| The incremental gate (09-gate.md) | When the caller asks for the incremental behaviour, instead of a blanket test run | Its verdict decides the stage. |
| Container assertions | Container stage, after the image is built and started | Any failed assertion fails the stage. |
| Archive verification | Stage step | Missing entry assembly fails the stage. |

The pipeline runs the test work once. A chain that both runs the incremental gate and then runs the
whole suite to be sure pays the cost the gate exists to remove.

## Refusals

| Condition | Behaviour |
| --- | --- |
| The configuration document is absent or unreadable | Precondition check fails before any stage runs; non-zero exit. |
| The solution named by configuration is absent | Precondition check fails before any stage runs; non-zero exit. |
| A stage step exits non-zero | The chain stops; later stages do not run; the process exits non-zero. |
| A component's package cannot be written to the feed | The pack step fails and the chain stops; consumers are not built against a stale copy without saying so. |
| A restore cannot find a component package | The build stage fails with the restore error naming the id and version. |
| Formatting verification finds a difference | The build stage fails and names the file and line. |
| The container build fails | The container stage fails; the archive stage does not run. |
| The archive does not contain an expected entry assembly | The stage step fails after writing. |
| The container runtime is unavailable | The container stage fails with the runtime's own error. The pipeline must not probe for the runtime first and must not report a skip as a pass. |

## Limitations

- The publish is framework-dependent and runs with the build's own output. A runtime-identifier
  specific publish of that output fails, because the build did not produce assets for the
  identifier. The identifier therefore appears in the archive name and the metadata document but is
  not passed to publish.
- There is no registry and no deployment. The deployment stages are no-ops with a stated reason, and
  the deployment test stage is a no-op because nothing was deployed. The container is exercised by
  the container assertions instead.
- The image carries the compiler and the package client, so it is large: the measured image is 496
  MB. Trimming is not attempted, because the compiler and its reference assemblies are the product.
- Inside the container, processor time and peak working set are null for every execution. This is a
  loss of measurement, not of containment, and it is visible in the outcome document.
- Containment is a property of the host, not of the product. A host that offers neither the job
  object nor a container runs submissions with a deadline and nothing else, and no field of the
  outcome distinguishes it from a contained run.
- The local package feed makes every cross-component reference a packaging step. A one-line change
  in a base component requires a pack before any consumer compiles, and the pack order must be
  maintained by hand or by the stage script. The benefit is that components have no compile-time
  knowledge of each other's layout; the cost is that a change is not built until it is packed.
- Versioning is by a single version value for the whole product. A consumer cannot take a newer
  version of one component and an older version of another without editing that value.
- The feed has no lock. Two pipeline runs packing the same version at once can interleave writes to
  the same package file.
- Formatting verification needs a restore to be current; a stale restore makes the check fail for a
  reason that has nothing to do with formatting.

## Acceptance criteria

1. The entry point with no stage argument runs the full chain and exits 0 on a green tree, writing
   one line of progress per step with a timestamp.
2. `-Stage build` builds the solution, verifies formatting without writing a file, runs no test, and
   exits 0 on a clean tree.
3. `-Stage test` runs the tests, builds nothing, and exits non-zero when any test fails.
4. A failing test fails the chain: no later stage runs, and the process exits non-zero.
5. The container image is produced only by the container build command, from published output, with
   every base image pinned to an exact tag.
6. The image builds from a context that contains only the image's own files and the published
   application, and the published application is not under source control.
7. The image starts through a single startup script and exposes one port.
8. A submission posted to the container is compiled and executed, and its outcome carries standard
   output and exit status with `processorMilliseconds` and `peakWorkingSetBytes` null.
9. A submission that overruns its deadline in the container is killed, and the container remains
   available for the next submission.
10. Each component is packed before any consumer is built, and the feed folder is the first package
    source in the repository's source list and the public feed is the second.
11. A restore of any component resolves every other component from the feed and no component from a
    project reference.
12. Packing replaces the feed's package for the current version, and a consumer built after the pack
    links the newly packed assembly.
13. The stage step writes a metadata document containing version, runtime identifier, configuration,
    commit and stagedAt, and an archive containing every application's entry assembly.
14. The stage step fails when the archive is missing an entry assembly, and fails when there was
    nothing to publish.
15. The full chain exits 0 and prints one completion line per stage in order, within fifteen minutes
    on a four-core host. A seven-test-project tree with 228 tests, seventeen container assertions
    and an image build measured 226.8 seconds, which is the figure to beat.
