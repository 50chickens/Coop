# Packages

A submission may declare NuGet packages to compile against and to run with. Each entry names one package at one exact
version. Coop resolves the declared set and its transitive closure from nuget.org, caches the result by id and version,
and makes the closure available both to the compiler and to the child process. Resolution is per execution: two
executions never share a reference set, and one execution's packages cannot leak into another's compilation.

## Declaration

| Field | Type | Required | Contract |
| --- | --- | --- | --- |
| `id` | string | yes | package id |
| `version` | string | yes | one exact version |

The request carries an array of these entries under `packages`, defaulting to empty. An empty array means no package
resolution is attempted and no feed is contacted.

An exact version is required and enforced before any network access. A version string containing any of `[`, `]`, `(`,
`)`, `,` or `*` is a range and is refused. A version string that does not parse as a version is refused. A missing id or
version is refused.

## Source

There is exactly one source of packages: the nuget.org v3 index. It is the only feed a resolver instance ever
constructs. No configuration file, no additional feed, no fallback folder and no credential provider takes part, so the
answer to "where did this assembly come from" has one value.

## Resolution

Resolution is breadth-first over the declared entries and the dependencies they pull in.

1. Each declared entry is pinned to its exact version.
2. Each dependency is read from the nearest dependency group of the package's manifest for `net10.0`, compared across
   the groups the package offers.
3. Each dependency is satisfied by the lowest published version inside its declared range, prereleases excluded unless
   the range's own minimum is a prerelease. Because declared entries are exact, the closure is deterministic for a
   given feed state.
4. A package id already resolved is not resolved twice. If a second requirement for that id is not satisfied by the
   version already chosen, the whole request is refused, naming both versions.

Assembly selection from a package, in order, is:

| Order | Candidate |
| --- | --- |
| 1 | the `net10.0` folder under `lib`, when it holds at least one assembly |
| 2 | the highest `net5.0` to `net10.0` folder that holds at least one assembly |
| 3 | the `netstandard2.0` folder |
| 4 | assemblies sitting directly under `lib` |
| none | the request is refused, naming the folders the package does offer |

Only assemblies are taken. Nothing else a package can carry is consumed: no build targets, no content files, no
analysers, no native assets and no runtime-specific asset selection.

## Cache

Resolved packages are cached by id and version, in a layout keyed on the lowercased id and the normalised version. A
cached entry is complete when the installer's own metadata file is present, which the installer writes last, after the
manifest, the archive and every file. That metadata carries the package's content hash and the source it came from.

| Event | Behaviour |
| --- | --- |
| entry present with its metadata | served from the cache, no network access for that id and version |
| directory present without the metadata | not a hit: the package is installed again |
| entry absent | the metadata and content are fetched, then installed |

The cache is consulted before the feed, so a repeated execution of a resolved set does not download again. The failure
mode this closes is the half-extracted directory: an interrupted install leaves a directory without the metadata, and
the next resolution reinstalls rather than compiling against a partial package.

The HTTP staging area used while downloading is inside the data root as well, so no package artefact and no transfer
staging is written outside it. No shared machine-wide package folder is read or written.

## Availability at compile time and at run time

Every selected assembly of every resolved package enters the compiler's reference set for that execution, alongside the
platform reference set and any assembly the caller supplied.

The same assemblies are copied beside the compiled submission, so the child process resolves them from its own
directory without a dependency manifest. This is what makes run-time availability match compile-time availability: a
type that compiles is a type that loads, and nothing else is loadable.

## Isolation between executions

Isolation rests on three properties.

1. A reference set is built per execution from that execution's resolution. There is no ambient reference collection and
   no shared compilation object, so no execution inherits another's assemblies.
2. The resolved closure is an input to the artefact key. Two executions whose closures differ therefore never share a
   compiled artefact, even when the source is identical.
3. Package extraction writes only under the package cache, keyed by id and version, and every read is of a path inside
   that cache. Two executions resolving different versions of one id read different directories.

## Refusals

Each refusal is a value returned by resolution, not an exception, and each names the id and version it is about. A
refusal stops the invocation before anything is compiled or executed, so no outcome and no record exists for a refused
request.

| Condition | Message shape | Why it must be distinguishable |
| --- | --- | --- |
| id or version absent | a package needs an id and a version; it was asked for as id `<id>` and version `<version>` | the caller's request is incomplete, and retrying cannot help |
| version is a range | package `<id>` asks for version `<version>`, which is a range rather than one exact version | a range cannot be reproduced later, so no result recorded against it would be a record of anything. The caller must pin |
| version is not a version | package `<id>` asks for version `<version>`, which is not a version | a typographical error, refused before any network access, so the caller learns it without waiting for a feed |
| pinned version absent from the feed | package `<id>` version `<version>` does not exist on nuget.org | the caller named something that has never existed; distinct from a dependency the feed cannot satisfy |
| no published version satisfies a dependency range | package `<id>` has no version matching `<range>` on nuget.org | the caller's own entries were satisfiable and a dependency was not, so the caller cannot fix it by changing its own pin alone |
| one id required at two versions | package `<id>` is needed at `<version>` and at `<range>` by the same request | the request contradicts itself; no choice of version satisfies it, so it must be refused rather than silently resolved |
| no dependency group for the target framework | package `<id>` version `<version>` carries no dependency group for net10.0; it offers `<folders>` | the package exists but was not built for this target; the caller needs a different package, not a different version |
| no `lib` folder | package `<id>` version `<version>` offers no lib folder and so no assembly to compile against | the package exists but supplies nothing to compile against; a version change will not help |
| no assembly for the target framework | package `<id>` version `<version>` offers no assembly for net10.0; it offers `<folders>` | the offered folders tell the caller which frameworks the package does support, which is the information needed to choose |
| install failed | package `<id>` version `<version>` could not be installed from nuget.org | a transfer or extraction failure, which is worth retrying, unlike every condition above |

The command surface reports a package refusal with exit code 5 and one line naming the reason. A surface that validates
before acknowledging reports it as a refusal of the `packages` field, so it is distinguishable from a refusal of the
submission and from a failed execution.

## Limitations

1. Transitive dependency versions are chosen from what is already cached before the feed is consulted, so that a
   repeated execution does not depend on the network. The cost is real: pruning the cache can select a different
   transitive version than a cold resolution would, and two executions of the same request can compile against
   different dependency assemblies depending on cache state. Declared entries are always exact, so what the caller asked
   for is unchanged.
2. The resolved closure is not recorded with the execution. A record carries the declared packages, so a reader can
   reproduce the request but cannot read back which transitive versions were used. The closure is recoverable only from
   the cache state and the artefact key.
3. The transitive version conflict refusal is implemented and names both versions, but no test reaches it against a
   real conflict, so its behaviour on a real manifest is unverified.
4. A feed that cannot be reached raises an unhandled error rather than returning a refusal. There is no offline
   behaviour and no retry: a cold resolution needs the feed.
5. Only assemblies are consumed. A package that works through build targets, content files, analysers or native assets
   will appear to resolve and then fail, or silently lose behaviour the caller expected.
6. Runtime-specific assets are never selected, because no runtime identifier is set. A package that supplies its
   implementation only under a runtime-specific folder has no usable assembly and is refused.
7. Only one feed, fixed at build time. A private feed, a mirrored feed or an offline folder cannot be configured.
8. Package entries are declared per request and never inherited, so a caller that needs the same five packages in every
   submission declares them every time.
9. Nothing verifies a package signature and no licence metadata is read or recorded, so a resolved closure is a
   reproducibility claim and not a provenance claim.

## Acceptance criteria

1. A declared package at an exact version that exists is resolved, its assembly is available at compile time, and a
   submission that uses one of its types compiles and runs.
2. A second execution of the same declared package contacts no feed for that id and version.
3. A version containing `[`, `]`, `(`, `)`, `,` or `*` is refused with the range message, and no network request is
   made.
4. A version string that is not a version is refused with the not-a-version message, and no network request is made.
5. A version of a known package that does not exist is refused naming both the id and the version asked for.
6. A package id that does not exist is refused naming both the id and the version asked for.
7. A package whose only assemblies are for another framework is refused with a message listing the folders it does
   offer.
8. A refused package causes nothing to be compiled, no child process to start, and no record to be written, and the
   command surface exits 5.
9. Two executions with different package versions of one id never share a compiled artefact.
10. An execution's reference set contains no assembly that belongs to another execution's closure.
11. A package directory without the installer's metadata is not treated as a cache hit.
12. Two declared entries for one id at incompatible exact versions are refused with a message naming both.
13. Nothing is written outside the data root during resolution, including the HTTP staging area.
