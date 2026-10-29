# Content keying

Reuse is keyed at two levels. One key identifies a compiled artefact: this source, compiled by this compiler, for this
target framework and this runtime identifier. A second key identifies a result: that artefact, run with these
parameters. A single key cannot express the relationship: keyed on source alone it returns an answer for parameters that
were never used, and keyed on everything at once it recompiles whenever a parameter moves.

Both keys are SHA-256 over a canonical text encoding, written as 64 lowercase hexadecimal characters.

## The canonical form

Every field of every key is written the same way:

```
<field name>\n<character count>\n<value>\n
```

The character count is what makes the encoding injective. Without it, a value containing a newline could be read as the
following field, and two distinct inputs could produce one key. With it, `ab` followed by `c` and `a` followed by `bc`
produce different text even though the concatenation is identical.

A null value is written with the count `-1` and no value, so an absent field and an empty field are different keys. Both
keys carry a scheme version as their first field, so a change to the field set or to this encoding is a miss against
every stored key rather than a silent reinterpretation of one.

## The artefact key

The artefact key answers: what would the compiler emit for this input. Every field below is in the key because omitting
it produces a cache hit that is a wrong answer rather than a miss.

| Field | Value | What goes wrong without it |
| --- | --- | --- |
| scheme version | `2` | a changed field set or encoding silently reinterprets every stored key |
| source | the source text after normalisation | an edit reuses the assembly built from the older text, so a submission runs a program the caller never sent |
| assembly name | the fixed assembly name | the identity baked into metadata and loaded at run time is unverified |
| target framework | `net10.0` | a submission for another framework reuses an artefact whose runtime configuration and package asset selection belong to this one |
| runtime identifier | a value, or the explicit absence marker | RID-specific and RID-agnostic artefacts collide |
| compiler version | the version of the loaded C# compiler assembly | after a compiler upgrade the previous compiler's output is reused although the emitted IL and the diagnostics changed |
| language version | `Latest` | changing the accepted syntax and emitted code while the compiler version stays the same is invisible |
| output kind | console application | library and console output differ in the emitted entry point and metadata and share a key |
| optimisation level | `Debug` | optimised and unoptimised IL differ in code shape, attributes and every reported timing, and share a key |
| reference runtime version | the version of the runtime whose assemblies form the platform reference set | a different patch supplies a different API surface, and a submission compiled against the older one is reused |
| package count, then each package id and version | the whole resolved closure, not only the declared list | two closures whose assemblies differ become indistinguishable, so compilation resolves against the wrong reference |
| reference count, then each reference path, name and content hash | caller-supplied assemblies, full path plus hash of the bytes | a reference rebuilt in place keeps its path and name, which is the exact change a path-keyed or name-keyed design misses; a path that cannot be read is refused before a key is computed rather than hashed as absent |

Deliberately outside the artefact key, each a claim that the input cannot change what the compiler emits:

| Excluded | Reason |
| --- | --- |
| the source path | the compiler reads it only to label diagnostics, and a checkout elsewhere changes it |
| artefact directory and file names | they are where the artefact is kept, not what it contains |
| the machine, the clock, the culture | none is an input to the compiler, and including one would make an artefact unusable anywhere else |
| the parameter set | that is the whole point of the second level |
| the build's own version | it is composed at result level, described below |

### Source normalisation

Exactly three transformations are applied to the source before it is hashed:

1. every `\r\n` becomes `\n`
2. every remaining lone `\r` becomes `\n`
3. one leading byte-order mark is removed

Nothing else changes. Spaces, tabs, indentation, trailing whitespace and the contents of string literals are hashed as
written. The boundary is deliberate and testable in both directions: a checkout on another machine changes line endings
and must not change the key, while a whitespace change inside a literal changes what the program prints and must change
the key.

### Ordering inside the key

Ordering that belongs to the caller's convenience rather than to the request is normalised away before writing:

| Collection | Order |
| --- | --- |
| parameters | by name, ordinal |
| packages | by id, case-insensitive, then by version, ordinal |
| references | by full path, ordinal, then by content hash, ordinal |

## The result key

The result key answers: what did this artefact produce with these parameters.

| Field | Value | What goes wrong without it |
| --- | --- | --- |
| scheme version | `1` | a changed field set or encoding silently reinterprets existing keys |
| artefact key | the 64-character artefact key | a result produced by different code is returned, which is a plausible answer for a run that never happened |
| parameter count, then each parameter name and value | in ordinal name order | two runs that differ only in arguments share a result, which is the case the second level exists for |

Parameter values are hashed exactly as given. There is no normalisation of a value: a number formatted for another
culture, or a relative path that resolves elsewhere, is a different key for what a caller may think is one run, or one
key for two runs. Nothing can be normalised without knowing each parameter's type, so nothing is.

Resolved packages are in the result key through the artefact key rather than beside it. The package closure changes the
compiled artefact, so a change of package or version changes the artefact key, and the result key composed on top of it
changes with it. Naming packages a second time at result level would add no distinction and would let the two levels
disagree.

## The version of the thing that captured the result

A result records what a capture reported, and capture behaviour is part of the answer. The result store therefore keys
its documents one level further: over the result key and the building binary's informational version, and it treats a
stored document whose recorded version differs from the running one as absent. A capture change on a later build
invalidates earlier stored results without a constant anybody has to remember to bump.

This is a composition outside the two keys rather than a field inside the result key's scheme. It is safe, because it
can only cause a recompile, never return a stale answer.

## Limitations

1. The runtime identifier is an input to the artefact key but is never populated by the execution path, which starts
   every submission against a framework-dependent runtime configuration. Every artefact key therefore carries the
   absence marker. The day a submission is published self-contained or pinned to a runtime identifier, two artefacts
   that differ only by that identifier will share a key.
2. The platform reference set is represented by the runtime version string, not by hashing the assemblies. A patched
   runtime that keeps its version number is invisible to the key.
3. Parameter values are not interpreted, so a caller that formats the same value differently gets a miss, and a caller
   whose parameter means something different in a different working directory gets a hit.
4. The build version is composed at result level, outside the result key function. A key printed by one component and a
   document name used by the store are therefore not the same string, and a reader comparing them will find a
   difference that is not a defect.
5. An uncommitted local change to the binary does not change its informational version, so two different builds of one
   commit share a version and share stored results.
6. The artefact key is the identity of the artefact, so two callers that want the same source compiled into two
   different output names cannot have it: the assembly name is fixed and is an input.

## Acceptance criteria

1. Two computations of the artefact key from the same inputs in the same process produce the same 64 characters, and a
   computation in a second process produces the same 64 characters as the first.
2. Sources that differ only in line endings, or only in a leading byte-order mark, produce the same artefact key.
3. A source that differs only in whitespace inside a string literal produces a different artefact key.
4. A change to the source, to the declared target framework, to the compiler version, to the language version, to the
   output kind, to the optimisation level, to the reference runtime version, to a package version, or to a
   caller-supplied reference's bytes each produces a different artefact key.
5. A runtime identifier present, empty and absent produce three different artefact keys.
6. One submission run with one parameter set and then with a second differing in one value produces the same artefact
   key and two different result keys.
7. The same parameters inserted in a different order produce the same result key.
8. Two parameter sets whose concatenated names and values are identical but whose field boundary differs produce
   different result keys.
9. A package list resolved in a different order produces the same artefact key.
10. A reference assembly reached through a different directory but with identical bytes produces the same artefact key
    as the original.
11. A stored result document recorded under a different build version is treated as absent, and the request is compiled
    and executed again.
