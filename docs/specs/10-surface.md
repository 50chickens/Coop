# 10 - Client surfaces

## Two surfaces, one operation

Coop exposes the same execution operation through two client surfaces:

- A tool on the Model Context Protocol, for an agent that is already talking to an MCP server.
  It takes raw C# source and returns one JSON document.
- An HTTP endpoint, for a caller that is not an MCP client and may not be on this host.

Both accept the same request fields, produce the same outcome, and are subject to the same
deadline, the same single-run lock and the same record store. Neither surface compiles or executes
anything itself; a difference between them is a defect.

The command-line surface of the same operation is not specified here. Where a field is shared it is
named identically on all three.

## The execution request

| Field | Type | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `name` | string | Yes | none | Name of the execution. |
| `description` | string | Yes | none | Purpose or type of the execution. |
| `category` | string | Yes | none | Category the execution is grouped under. |
| `timestamp` | string | Yes | none | ISO-8601 date-time with offset, supplied by the caller. |
| `submission` | object | Yes | none | The code to run; see below. |
| `shouldRunAsync` | boolean | No | `false` | When false the caller waits for the outcome. |
| `packages` | array | No | `[]` | Each entry `{ "id": string, "version": string }`. |
| `response` | object or null | No | `null` | Output only. A caller sends null; the server fills it. |

`submission`:

| Field | Type | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `sourceText` | string | No | none | Inline C# source. Supply `sourceText` or `sourcePath`. |
| `sourcePath` | string | No | none | Path to a source file the server can read. Supply `sourceText` or `sourcePath`. |
| `parameters` | object of string to string | Yes | none | Values the submission receives. Send `{}` when there are none. |

Execution contract:

- The submission is one source file, compiled as a console application, with no other source
  files.
- The entry point is a type containing `static int Main(string[] args)`. The type may be a class or
  a struct, static or not, and need not be public. The value `Main` returns is the exit status.
- Parameters arrive as the child process's command-line arguments, one `key=value` per parameter,
  sorted by key with an ordinal comparison. Everything after the first `=` is the value; the server
  does not quote, escape or encode it. An empty parameter map yields an empty argument list.
- The reference set is the .NET shared framework on the server, the assemblies of any resolved
  package, and the assemblies the caller names on the command line with `--reference` or in the
  `references` argument. Each named assembly must exist: a path that does not exist is refused by
  name before anything is compiled. An assembly outside those sources is not referenced, and using
  it is a compile error.
- Each named assembly is copied beside the compiled submission, and its full path and the hash of
  its bytes are part of the artefact key, so replacing the assembly recompiles rather than reusing a
  binary built against the older one.
- Packages are resolved from the public NuGet feed only, each pinned to one exact version, and
  cached by id and version so a repeat does not download again.
- The deadline defaults to 60 seconds and is clamped to a ceiling of 600 seconds. A value above the
  ceiling is clamped, not refused.

## The MCP tool

### Identity and annotations

| Property | Value |
| --- | --- |
| Name | `run_csharp` |
| Purpose (from the description) | Compiles one C# submission and runs it as a contained child process, returning JSON carrying compile metrics, standard output, standard error and cache status. |
| Input schema | The arguments listed under Arguments, from which the schema is derived. |
| Result | One content block, media type `application/json`, being the document under The JSON returned. |
| `readOnly` | false. A call writes an execution record and may write cache entries. |
| `destructive` | false. Nothing outside the server's own state is modified. |
| `idempotent` | false. A repeated identical call writes a new execution record with a new identifier, even when the outcome is reused from cache. |
| `openWorld` | false. The tool reaches no network resource on the caller's behalf, apart from the server resolving a declared package. |

The tool must not be advertised as read-only, because a caller that treats it as read-only will
call it speculatively and pay a compile each time.

### Arguments

| Argument | Type | Required | Default | Maps to |
| --- | --- | --- | --- | --- |
| `source` | string | Yes | none | `submission.sourceText` |
| `parameters` | object of string to string | No | `{}` | `submission.parameters` |
| `packages` | array of `{id, version}` | No | `[]` | `packages` |
| `references` | array of string | No | `[]` | `submission.references` |
| `timeoutSeconds` | integer | No | 60 | The deadline, clamped to 600 |
| `name` | string | No | `run_csharp` | `name` |
| `description` | string | No | the first 200 characters of `source` | `description` |
| `category` | string | No | `agent` | `category` |

`references` is a list of paths to assemblies the submission may use, so a caller whose own project
already builds an assembly does not build a probe project to reach its types.

`source` is raw C# source text. It is not a path, not a fragment to be wrapped, and not a
template: the server compiles it as the whole compilation unit. A submission that relies on an
implicit `using` the server does not supply fails to compile, and the diagnostics say so.

`timestamp` is not an argument. The server stamps the request at acceptance, so an agent cannot
backdate an execution and cannot be relied on to format a date.

### The JSON returned

One object, no envelope, no string-escaped JSON inside a JSON string:

```json
{
  "executionId": "41122878127a4658b652d85b3338b07c",
  "status": "completed",
  "reused": null,
  "cache": {
    "artefactKey": "<64 hex characters>",
    "resultKey": "<64 hex characters>",
    "artefactReused": false,
    "resultReused": false
  },
  "compile": {
    "success": true,
    "elapsedMilliseconds": 812,
    "diagnosticCount": 0,
    "diagnostics": []
  },
  "run": {
    "exitStatus": 0,
    "standardOutput": "hello\r\n",
    "standardError": "",
    "deadlineExpired": false,
    "elapsedMilliseconds": 721,
    "processorMilliseconds": 62,
    "peakWorkingSetBytes": 23154688
  }
}
```

| Field | Type | Rule |
| --- | --- | --- |
| `executionId` | string | 32 lowercase hexadecimal characters. Present on every outcome, including a compile failure and an expired deadline. |
| `status` | string | `pending`, `running`, `completed` or `failed`. `completed` means the compiled submission ran and exited zero. `failed` means it did not compile, exited non-zero, or was killed at its deadline. |
| `reused` | object or null | Null for a fresh execution. Non-null when the outcome was served from the result cache; see 3.4. |
| `cache.artefactKey` | string | Key of the compiled artefact: source plus compiler, target framework, runtime identifier, language and optimisation settings, and the package and reference set. |
| `cache.resultKey` | string | Key of the result: the artefact key plus the parameter map plus the server version. |
| `cache.artefactReused` | boolean | True when the compilation was not repeated because the artefact key was present. |
| `cache.resultReused` | boolean | True when neither compilation nor execution happened because the result key was present. |
| `compile.success` | boolean | False when the compiler reported at least one error. |
| `compile.elapsedMilliseconds` | integer | Wall time of the compiler invocation, excluding package resolution and process start. |
| `compile.diagnosticCount` | integer | Length of `compile.diagnostics`. |
| `compile.diagnostics` | array | Compiler errors, each `{ "id", "severity", "message", "filePath", "line", "column" }` with 1-based line and column. Empty when compilation succeeded. |
| `run` | object or null | Null when the submission did not compile. |
| `run.exitStatus` | integer or null | Null when the child never started or was killed at its deadline. |
| `run.standardOutput` | string | Everything written to standard output, including output written before a failure. |
| `run.standardError` | string | Everything written to standard error, including output written before a failure. |
| `run.deadlineExpired` | boolean | True only when the child was killed at its deadline. |
| `run.elapsedMilliseconds` | integer or null | Wall time of the child process. |
| `run.processorMilliseconds` | integer or null | Processor time of the child process; null where the host cannot read it. |
| `run.peakWorkingSetBytes` | integer or null | Peak working set of the child process; null where the host cannot read it. |

An unmeasured number is JSON `null`, never `0`. A consumer that needs to distinguish "no output"
from "not measured" reads `null` versus `""`.

### Cache status

- `status` `completed` with `cache.resultReused` true means the answer is a recorded outcome, not a
  new run. The timings in `run` belong to the execution that first produced it.
- `reused` carries the provenance of a reused answer:

```json
{
  "executionId": "<the execution that produced the outcome>",
  "recordedAt": "2026-10-23T10:40:39.7720305+00:00",
  "runnerVersion": "1.0.0+<commit>",
  "hint": "the timings in this response are those of execution <id> recorded at <time>, not of this run"
}
```

- A reused outcome is never served for a submission that was killed at its deadline. An expired
  deadline is a failure of that run and is not cached.
- A cached result is bound to the server version. A result recorded by a different version is not
  reused, even at the same result key.

### Timeout behaviour

- The deadline is fixed at acceptance, not at the start of the run. Time spent waiting for the
  single-run lock is spent inside the deadline.
- At expiry the child process tree is killed. The outcome carries `deadlineExpired` true,
  `exitStatus` null, whatever the child wrote before it was killed, and `status` `failed`.
- A deadline expiring is an outcome, not a tool error. The tool returns `isError` false with the
  document under The JSON returned, because the caller asked what happened and the answer is "it was
  killed".
- A submission that overruns does not outlive the server: the child is in a job object closed with
  the server on a Windows host, and is killed with its process tree at the deadline on any host.

## The HTTP surface

### Operations

| Method and path | Purpose | Success | Failure |
| --- | --- | --- | --- |
| `GET /template` | The execution template document (see The template) | 200 | none |
| `POST /executions` | Submit one execution | 200 with the record, or 202 with `{ "executionId": "..." }` | 400 with `{ "field", "reason" }` |
| `GET /executions/{executionId}` | Read one execution record | 200 with the record | 404 with `{ "executionId", "reason" }` |
| `GET /health` | Liveness and store writability | 200 | none |
| `GET /openapi/v1.json` | The OpenAPI document for the four operations above | 200 | none |

The listener binds loopback by default.

### The template

`GET /template` returns the execution template document as `application/json`, byte for byte the
document the command-line surface prints. It is not assembled per request and it is versioned with
the server. A client reads it before submitting anything.

The template states, in one document: the request fields with their types, required flags and
defaults; the submission fields; the entry-point form; how parameters arrive; the reference and
package sources; the timeout default and ceiling; and the response fields with their
`whenUnmeasured` rule.

A client that hard-codes the field list instead of reading the template is outside the contract;
the template exists so the field list has one owner.

### Submitting

`POST /executions` with the request contract of The execution request as the body.

| Condition | Response |
| --- | --- |
| `shouldRunAsync` false, request accepted | 200, body is the execution record with `status` `completed` or `failed` and `response` filled |
| `shouldRunAsync` true, request accepted | 202, body is `{ "executionId": "..." }`; the record exists as `pending` before the response is written |
| Request refused | 400, body is `{ "status", "field", "reason" }` |

For the asynchronous case the record moves `pending` to `running` to `completed` or `failed` and is
readable at every step. An immediate poll after the 202 must not return 404 for an identifier the
service just returned; the `pending` record written before the 202 is what guarantees this.

`timeoutSeconds`, when present at the top level, is an integer number of seconds and is clamped to
the ceiling. A non-integer is refused by field name.

### Reading a record

`GET /executions/{executionId}` returns the stored record described under The record document. A
record whose run is still in flight has `response` null and `status` `pending` or `running`.

An identifier with no record is refused by name:

```
HTTP/1.1 404 Not Found

{"executionId":"doesnotexist","reason":"No execution record named 'doesnotexist' exists in <store root>"}
```

An unknown identifier is never answered with an empty result.

### Health

```json
{
  "status": "ok",
  "version": "1.0.0+<commit>",
  "storeRoot": "<absolute path>",
  "storeWritable": true
}
```

`storeWritable` is measured per request by opening and closing a probe file in the store root; it
is not a cached flag.

### Bind policy

The listener binds loopback only unless remote access is granted.

| Bind address | Grant | Behaviour |
| --- | --- | --- |
| `localhost` or any loopback address | not needed | Starts. |
| Any other address | absent | Refuses to start: `refusing to bind <address>: it is not a loopback address and this service compiles and runs the code it is sent, so remote access must be enabled on purpose with --allow-remote`, exit 7, nothing listening. |
| Any other address | present | Prints `warning: binding <address>:<port>, which is reachable from other machines, and this service has no authentication`, then starts. |

Exit code 7 is this surface's own refusal, distinct from the product's shared codes 1 to 6. The
refusal is a bind-time check. It is the whole of the access control, and Limitations says so
plainly.

### Browser reference

The generated API reference page is served in the development environment only and returns 404 in
a production environment. It is a convenience, not part of the contract.

## The record document

Persisted once per execution, one document per identifier, named for the identifier, so two
concurrent executions never write the same document.

```json
{
  "executionId": "41122878127a4658b652d85b3338b07c",
  "status": "completed",
  "reused": null,
  "name": "api sync final",
  "description": "curl synchronous submit",
  "category": "adhoc",
  "timestamp": "2026-10-23T11:15:00+11:00",
  "submission": {
    "sourceText": "public static class Program { public static int Main() { return 0; } }",
    "sourcePath": null,
    "parameters": {}
  },
  "shouldRunAsync": false,
  "packages": [],
  "response": {
    "executionId": "41122878127a4658b652d85b3338b07c",
    "exitStatus": 0,
    "standardOutput": "",
    "standardError": "",
    "deadlineExpired": false,
    "elapsedMilliseconds": 721,
    "processorMilliseconds": 62,
    "peakWorkingSetBytes": 23154688,
    "diagnostics": []
  }
}
```

The record carries the request's eight fields plus `executionId`, `status` and `reused`. It is
written before the response is returned, so a caller that never receives a response can still find
what happened.

A record is written at three points: `pending` at acceptance for an asynchronous submission,
`running` before compilation, and terminal with `response` filled. A record is never deleted by the
server.

## Refusals

A refusal means the request was rejected before anything was compiled. A refusal never produces an
execution outcome. On the MCP surface it is returned as `isError` true with the same
`{ "status", "field", "reason" }` object as its content, carrying `field` where a field applies and
`status` `refused` so a caller can branch on `status` for every answer. A path that does not exist
also carries `path`, and its `field` is the argument or option that named it.

| Condition | `field` | `reason` |
| --- | --- | --- |
| Body is not JSON | `request` | `the request is not JSON: <parser message>` |
| Request is not an object | `request` | `the request is <kind> rather than a JSON object` |
| A required request field is absent or null | the field name | `<field> is required by the execution template and this request does not carry it` |
| `submission` is not an object | `submission` | `submission is <kind> rather than a JSON object` |
| A required submission field is absent | `submission.<field>` | `submission.<field> is required by the execution template and this request does not carry it` |
| A package has no id or no version | `packages` | `a package needs an id and a version; it was asked for as id '<id>' and version '<version>'` |
| A package version is a range | `packages` | `package '<id>' asks for version '<version>', which is a range rather than one exact version` |
| A package version is not a version | `packages` | `package '<id>' asks for version '<version>', which is not a version` |
| A package does not exist on the feed | `packages` | `package '<id>' version '<version>' does not exist on nuget.org` |
| A package offers no assembly for the target framework | `packages` | `package '<id>' version '<version>' offers no assembly for <framework>; it offers <folders>` |
| `timeoutSeconds` is not a whole number | `timeoutSeconds` | `timeoutSeconds is <kind> rather than a whole number of seconds` |
| The execution identifier is unknown | - | 404 with `executionId` and the store's message |
| The bind address is not loopback and remote access is not granted | - | Refusal to start, exit 7 |
| The single-run lock is not acquired within its budget | - | Not an execution: HTTP 503 with `{ "reason" }`, MCP error result with the same reason, no record written |

A compile error is not a refusal. A submission that does not compile is an execution with
`status` `failed`, `compile.success` false, the diagnostics, and `run` null.

## Concurrency and the single-run lock

- Every execution takes one file lock before package resolution and holds it until the child exits.
  The lock is the whole of the concurrency control: two executions never compile or run at once.
- A caller that cannot take the lock within 120 seconds is refused rather than queued. Nothing was
  compiled and nothing ran, so the refusal is not an execution outcome: no record is written, and
  the message names the lock location and the seconds waited.
- The refusal is distinguishable from a failed execution because the two demand opposite responses.
  A lock refusal says the request was fine and the machine was busy; the caller retries later. A
  failed outcome says the submission ran and did not succeed.
- The lock is shared by every surface, so a watcher and an HTTP submission never build at once.

## Limitations

- The HTTP surface has no authentication and no per-caller authorization. Remote access is a single
  on/off grant with a warning; anyone who can reach the port can execute code as the service
  account. That is why the grant is not the default.
- The service has no concurrency cap and no queue. N callers serialize on the run lock, and the
  N+1th caller waits up to 120 seconds and is then refused. There is no fairness and no ordering.
- A lock refusal leaves no record, so a client that must distinguish "never accepted" from
  "accepted and still running" has only the response status to go on.
- `timeoutSeconds` is accepted at the top level and is not one of the template's declared request
  fields. A client that validates its body against the template will not know about it. It is
  documented here and in the template's timeout section instead.
- The template document declares that an unmeasured number is the string `unavailable`, while the
  outcome documents emit `null` for the same condition. The two disagree. This specification
  adopts `null` and the template wording is a defect to be corrected in the template, not in the
  outcome.
- Compile metrics (`compile` under The JSON returned) are returned by the tool and are not stored in
  the record. A record read back by identifier carries no compile duration and no artefact reuse
  flag.
- `compile.elapsedMilliseconds` measures the compiler invocation only. It excludes package download
  and resolution, artefact cache read and copy, and child process start, so it is not the wall time
  a caller waited.
- The record has no similarity or full-text query operation on either surface. Executions can be
  read by identifier only.
- A record's `submission.sourceText` is stored verbatim, so a large submission is a large document.
  There is no retention policy and no pruning.
- The listener is a single process with an in-memory web host. A restart loses nothing that was
  recorded, but an asynchronous execution in flight is left `running` forever, because no
  background reconciler marks it failed.
- The template marks `timestamp` required and both surfaces refuse a request without it, while the
  request type's own default would read an absent value as `0001-01-01T00:00:00+00:00`. The refusal
  is the rule; the default exists only so a document read back from the record store deserialises.
- Only the command-line and HTTP surfaces were exercised against a running implementation. The MCP
  tool is specified from the same execution contract and shares its validation, deadline, lock and
  record semantics, but it was not measured. Its three argument defaults - `name`, `description`
  and `category` - are specification decisions, not observed behaviour, and a host that relies on
  them should read the tool's own description instead.

## Acceptance criteria

1. `GET /template` returns `application/json` and a body byte-identical to the template printed by
   the command-line surface.
2. A synchronous `POST /executions` returns 200 with a record whose `executionId` then resolves
   through `GET /executions/{executionId}` to the same record.
3. An asynchronous `POST /executions` returns 202 with only `executionId`, and that identifier
   resolves immediately afterwards with `status` `pending` or `running`, never 404.
4. A refused request returns 400 with `field` and `reason`; omitting `name` yields `field` `name`
   and omitting `submission.parameters` yields `field` `submission.parameters`.
5. `GET /executions/{unknown}` returns 404 with the identifier echoed and a reason naming it.
6. `GET /health` returns `storeWritable` true when the store root is writable and false when it is
   not, measured per request.
7. The OpenAPI document lists all four operations.
8. Starting with a non-loopback bind address and no grant exits 7, prints the refusal text, and
   leaves nothing listening on the port.
9. Starting with a non-loopback bind address and a grant prints the exposure warning before
   serving.
10. `timeoutSeconds` of 0 against a submission that sleeps produces `deadlineExpired` true,
    `exitStatus` null and `status` `failed`, within the timeout rather than after the submission's
    own duration.
11. A submission that fails to compile produces `status` `failed`, at least one diagnostic with an
    identifier, a message and a 1-based line, and `run` null.
12. A second identical submission after a successful one returns `cache.resultReused` true and a
    non-null `reused` object naming the first execution, and its `run` timings equal the first
    execution's.
13. The MCP tool declares `run_csharp` with the arguments listed under Arguments, returns one
    `application/json` content block, and is annotated `readOnly` false and `openWorld` false.
14. A package version given as a range is refused with `field` `packages` and the text naming the
    id and the range, before any compilation starts.
15. Output written to standard output before a non-zero exit is present in `run.standardOutput`
    together with `run.exitStatus` non-zero.
