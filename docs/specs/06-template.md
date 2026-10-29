# The execution template

Coop serves one document that states the contract a client must satisfy. A client requests it before it submits
anything, so the contract is published once and versioned with the tool rather than restated in each client's own
words. The same document is served by every surface, byte for byte, with one substituted token.

## What the document is

The template is a single versioned document, not assembled at run time from the code. What a client receives can be
diffed against what the tool actually does, and a difference is a defect rather than a difference of phrasing.

| Property | Behaviour |
| --- | --- |
| identity | `templateVersion`, an integer, incremented when the shape of the document changes |
| build identity | `toolVersion`, the informational version of the build that served it, substituted into one token in the document |
| stability | byte-identical between requests, apart from that token |
| served by | every client surface, including the command surface, which writes it to standard output |

## Structure

```
{
  "templateVersion": 1,
  "toolVersion": "<version of the build that served this document>",
  "language": {
    "name": "C#", "languageVersion": "Latest", "outputKind": "ConsoleApplication",
    "targetFramework": "net10.0", "assemblyName": "submission",
    "optimizationLevel": "Debug", "implicitUsings": false, "nullableContext": "disabled"
  },
  "request": { "format": "json", "propertyNames": "camelCase", "fields": { ... } },
  "submission": { "fields": { ... }, "entryPoint": { ... } },
  "parameters": { ... },
  "references": { ... },
  "packages": { "entry": { ... }, "exactVersion": "...", "feed": "...", "cache": "...",
                "refusals": { ... }, "refusalEffect": "..." },
  "timeout": { "defaultSeconds": 60, "ceilingSeconds": 600, "clamped": true, "detail": "...", "expiry": "..." },
  "response": { "unmeasuredValue": "unavailable", "unmeasuredDetail": "...", "fields": { ... }, "diagnostic": { ... } },
  "exampleRequest": { ... }
}
```

Every field entry uses the same shape: `type`, `required`, `default`, `description`, and `whenUnmeasured` where the
field can be unmeasured.

## Language

| Member | Value |
| --- | --- |
| `name` | `C#` |
| `languageVersion` | `Latest` |
| `outputKind` | `ConsoleApplication` |
| `targetFramework` | `net10.0` |
| `assemblyName` | `submission` |
| `optimizationLevel` | `Debug` |
| `implicitUsings` | `false` |
| `nullableContext` | `disabled` |

The document states all eight because a client author needs to know that implicit usings are absent and nullable
analysis is off before writing a first submission, and because each is an input to the content key.

## Request fields and defaults

| Field | Type | Required | Default | Description as published |
| --- | --- | --- | --- | --- |
| `name` | string | yes | null | name of the execution |
| `description` | string | yes | null | purpose or type of the execution |
| `category` | string | yes | null | category the execution is grouped under |
| `timestamp` | string | yes | `0001-01-01T00:00:00+00:00` | ISO-8601 date-time with offset |
| `submission` | object | yes | null | the code to compile |
| `shouldRunAsync` | boolean | no | `false` | when false the caller waits for the outcome |
| `packages` | array | no | `[]` | packages to make available |
| `response` | object | no | null | output only; a client sends null |

The submission object declares three fields.

| Field | Type | Required | Default | Description as published |
| --- | --- | --- | --- | --- |
| `sourceText` | string | no | null | inline C# source; supply `sourceText` or `sourcePath` |
| `sourcePath` | string | no | null | path to a `.cs` file Coop can read |
| `parameters` | object | yes | null | parameter values as a map of string to string; send `{}` when there are none |

The published note on `parameters` is that the map is read without a null check, so `{}` is the correct empty value and
null is not.

## Entry point

| Property | Statement |
| --- | --- |
| form | a type containing `static int Main(string[] args)` |
| type | any class or struct, static or not |
| accessibility | any; the entry point does not have to be public |
| exit status | the value `Main` returns is reported as `exitStatus` |
| unit | one submission is one source file, compiled as a console application with no other source files |

The document declares one shape and states explicitly that the type and method accessibility are unrestricted, because
a client that assumed public accessibility would write needless code. The compiler accepts other forms than the declared
one; the declared form is the contract.

## Parameters

| Member | Statement |
| --- | --- |
| `arrival` | the submission receives its parameters as the command-line arguments of its own process |
| `form` | one argument per parameter, `key=value`, for example `answer=42` |
| `order` | not the caller's order: arguments are sorted by key with an ordinal comparison |
| `values` | everything after the first `=` is the value, so a value may contain `=`, spaces and punctuation; Coop does not quote, escape or encode it |
| `keys` | everything before the first `=`; must not be empty |
| `uniqueness` | a map keyed by name, so each key arrives once |
| `empty` | a submission with no parameters receives an empty argument list |
| `read` | read `string[] args` and split each argument on the first `=` |

## References

| Member | Statement |
| --- | --- |
| `platform` | `Microsoft.NETCore.App` |
| `platformDetail` | every `Microsoft.NETCore.App` assembly of the .NET 10.0 shared framework on the machine running the tool |
| `availableByDefault` | the whole `Microsoft.NETCore.App` set, which is `System.*`, `mscorlib`, `netstandard` and the rest of the framework |
| `suppliedByCaller` | the command surface takes one extra assembly per option; a request has no references field, so a request brings an extra assembly only through packages |
| `suppliedByPackages` | the assemblies of each resolved package are added to the reference set and copied beside the compiled submission |
| `nothingElse` | an assembly outside those three sources is not referenced, so using it is a compile error |

## Packages and timeout

The package section states the entry shape, that the version must be exact and that a range is refused, the single
feed, the cache behaviour by id and version, the refusal conditions with their message shapes, and that a refused
package is reported before anything is compiled or run. It is a restatement of the package specification in the form a
client reads first.

The timeout section states:

| Member | Value |
| --- | --- |
| `defaultSeconds` | 60 |
| `ceilingSeconds` | 600 |
| `clamped` | true; a value above the ceiling is clamped to it rather than refused |
| `detail` | the request has no timeout field, so only the command surface's option varies it |
| `expiry` | an expired deadline kills the submission and reports `deadlineExpired` true with no `exitStatus` |

## Response shape

| Field | Type | `whenUnmeasured` | Description as published |
| --- | --- | --- | --- |
| `executionId` | string | none | the id of the execution record this response was written to |
| `exitStatus` | integer | `unavailable` | the submission's exit status; unmeasured when it never started, failed to compile, or its deadline expired |
| `standardOutput` | string | none | everything written to standard output, including what was written before a failure |
| `standardError` | string | none | everything written to standard error, including what was written before a failure |
| `deadlineExpired` | boolean | none | true when the submission was killed at its deadline |
| `elapsedMilliseconds` | integer | `unavailable` | wall clock time of the child process |
| `processorMilliseconds` | integer | `unavailable` | processor time of the child process |
| `peakWorkingSetBytes` | integer | `unavailable` | peak working set of the child process |
| `diagnostics` | array | none | compiler errors when the submission did not compile |

The unmeasured representation is stated twice, once as `unmeasuredValue` and once as `unmeasuredDetail`, because it is
the contract's most surprising detail: an unmeasured number is the string `unavailable`, never `0`.

| Diagnostic field | Statement |
| --- | --- |
| `id` | compiler diagnostic id, for example `CS0103` |
| `severity` | `Error` |
| `message` | the compiler's message |
| `filePath` | the submission's source path |
| `line` | 1-based line |
| `column` | 1-based column |

## The example request

The document ends with one complete request: a name, a description, a category, a timestamp, a submission with inline
source and one parameter, `shouldRunAsync` false, an empty package list, and a null response.

The example is the submission the acceptance proof executes unchanged. An example that cannot be executed rots
silently; one that is executed turns a documentation error into a failing test.

## Refusals

Request validation reads the required flags and the nested field list from the template itself, in one implementation
shared by every surface. There is no second copy of the field list, so the document and the validator cannot disagree
about which fields are required.

| Condition | Reason it must be distinguishable |
| --- | --- |
| the request is not a JSON object | there is no field to name, so the refusal names the request itself; a caller must learn that its document is malformed rather than that a field is missing |
| a required field is absent or null | the refusal names the field path, such as `name` or `submission.parameters`, so the caller can fix the exact field rather than guess |
| the submission is not an object | the refusal names `submission`; a caller is told the nested document is the wrong kind rather than that every one of its fields is missing |
| a package is refused | the refusal names the `packages` field and carries the package reason, so a caller can tell a malformed request from an unreachable package |

A refusal is not an execution failure. A refusal means nothing was accepted, so there is no execution identifier and
nothing to poll for; an execution failure has an identifier and an outcome. A caller that treats a refusal as a failed
execution will poll an identifier that was never issued, and a caller that treats a failed execution as a refusal will
resubmit a request that was already accepted.

## Limitations

1. The published response section does not describe the reuse block that the execution surfaces return beside the
   outcome. A client that reads only the template will not learn that an outcome can be a stored one, nor that its
   timings belong to an earlier execution.
2. The document states that a request has no timeout field, yet one surface accepts a top-level `timeoutSeconds` that
   is absent from the documented field list. The document and that surface disagree, and the surface is the one that
   behaves as documented nowhere.
3. The document states that `shouldRunAsync` false means the caller waits, and implies true means it does not. No
   surface defers execution on the strength of the flag; a surface either waits always, or acknowledges first and runs
   in the background regardless of the flag's value.
4. Validation checks presence and JSON kind for required fields only. It does not reject unknown fields, does not check
   that exactly one of `sourceText` and `sourcePath` is present, and does not check that `timestamp` parses.
5. The template is served as JSON with no schema document beside it, so a client that wants generated types has to
   write them from the prose.
6. `toolVersion` is the build's informational version, which includes build metadata. Two builds of one commit report
   different strings, and a client comparing versions should treat the metadata as meaningful.

## Acceptance criteria

1. Every surface serves the same document, character for character, when asked for the template.
2. The served document differs from the checked-in document in exactly one substring: the tool version token.
3. `templateVersion` is a positive integer and `toolVersion` is non-empty in every served copy.
4. The example request, taken from the document and submitted unchanged, produces an outcome with exit status 0.
5. A request missing `name` is refused with a message naming `name`.
6. A request missing `submission.parameters` is refused with a message naming `submission.parameters`.
7. A request whose `submission` is not an object is refused with a message naming `submission`.
8. Every field the response can carry is described in the response section, and every response field's unmeasured case
   is described as `unavailable`.
9. The timeout section states the default, the ceiling, that the ceiling clamps rather than refuses, and the effect of
   expiry.
10. The references section states all three sources of assemblies and that nothing else is referenced.
11. The parameters section states the arrival form, the ordinal ordering, the split on the first `=`, and the empty
    case.
