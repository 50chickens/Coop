using System.Collections.Generic;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coop.Library.Execution;

namespace Coop.Library.Template
{
    public static class CoopTemplate
    {
        public const int CurrentTemplateVersion = 1;

        public const string ToolVersionToken = "<version of the build that served this document>";

        private static readonly JsonSerializerOptions options = CreateOptions();

        public static JsonSerializerOptions Options
        {
            get
            {
                return options;
            }
        }

        public static TemplateDocument Create()
        {
            return Create(ToolVersionToken);
        }

        public static TemplateDocument Create(string toolVersion)
        {
            TemplateDocument document = new TemplateDocument();
            document.TemplateVersion = CurrentTemplateVersion;
            document.ToolVersion = toolVersion;
            document.Language = CreateLanguage();
            document.Request = CreateRequest();
            document.Submission = CreateSubmission();
            document.Parameters = CreateParameters();
            document.References = CreateReferences();
            document.Packages = CreatePackages();
            document.Timeout = CreateTimeout();
            document.Response = CreateResponse();
            document.ExampleRequest = CreateExampleRequest();
            return document;
        }

        public static string Serialise(string toolVersion)
        {
            return JsonSerializer.Serialize(Create(toolVersion), options);
        }

        private static JsonSerializerOptions CreateOptions()
        {
            JsonSerializerOptions created = new JsonSerializerOptions();
            created.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            created.WriteIndented = true;
            created.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            created.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            return created;
        }

        private static LanguageContract CreateLanguage()
        {
            LanguageContract language = new LanguageContract();
            language.Name = "C#";
            language.LanguageVersion = "Latest";
            language.OutputKind = "ConsoleApplication";
            language.TargetFramework = "net10.0";
            language.AssemblyName = "submission";
            language.OptimizationLevel = "Debug";
            language.ImplicitUsings = false;
            language.NullableContext = "disabled";
            return language;
        }

        private static RequestContract CreateRequest()
        {
            RequestContract request = new RequestContract();
            request.Format = "json";
            request.PropertyNames = "camelCase";
            request.Validation = "the required flags and the nested field list are read from this document, so the document and the validator cannot disagree about which fields are required";
            request.Fields = new List<RequestField>
            {
                Field("name", "string", true, null, "name of the execution"),
                Field("description", "string", true, null, "purpose or type of the execution"),
                Field("category", "string", true, null, "category the execution is grouped under"),
                Field("timestamp", "string", true, "0001-01-01T00:00:00+00:00", "ISO-8601 date-time with offset; an absent value becomes this default rather than a refusal"),
                Field("submission", "object", true, null, "the code to compile"),
                Field("timeoutSeconds", "integer", false, ExecutionDeadline.DefaultSeconds.ToString(CultureInfo.InvariantCulture), "deadline in seconds; an absent value means 60 and a value above 600 is clamped to 600 rather than refused"),
                Field("packages", "array", false, "[]", "packages to make available; each entry is an object with id and version"),
                Field("response", "object", false, "null", "output only; a client sends null and Coop fills it")
            };
            return request;
        }

        private static RequestField Field(string name, string type, bool required, string defaultValue, string description)
        {
            RequestField field = new RequestField();
            field.Name = name;
            field.Type = type;
            field.Required = required;
            field.Default = defaultValue;
            field.Description = description;
            return field;
        }

        private static SubmissionContract CreateSubmission()
        {
            SubmissionContract submission = new SubmissionContract();
            submission.Fields = new List<RequestField>
            {
                Field("sourceText", "string", false, "null", "inline C# source; supply sourceText or sourcePath"),
                Field("sourcePath", "string", false, "null", "path to a .cs file Coop can read"),
                Field("parameters", "object", true, null, "parameter values as a map of string to string; send {} when there are none; the map is read without a null check, so null is not the empty value")
            };
            EntryPointContract entryPoint = new EntryPointContract();
            entryPoint.Form = "a type containing static int Main(string[] args)";
            entryPoint.Type = "any class or struct, static or not";
            entryPoint.Accessibility = "any; the entry point does not have to be public";
            entryPoint.ExitStatus = "the value Main returns is reported as exitStatus";
            entryPoint.Unit = "one submission is one source file, compiled as a console application with no other source files";
            submission.EntryPoint = entryPoint;
            return submission;
        }

        private static ParametersContract CreateParameters()
        {
            ParametersContract parameters = new ParametersContract();
            parameters.Arrival = "the submission receives its parameters as the command-line arguments of its own process";
            parameters.Form = "one argument per parameter, key=value, for example answer=42";
            parameters.Order = "not the caller's order: arguments are sorted by key with an ordinal comparison";
            parameters.Values = "everything after the first = is the value, so a value may contain =, spaces and punctuation; Coop does not quote, escape or encode it";
            parameters.Keys = "everything before the first =; must not be empty";
            parameters.Uniqueness = "a map keyed by name, so each key arrives once";
            parameters.Empty = "a submission with no parameters receives an empty argument list";
            parameters.Read = "read string[] args and split each argument on the first =";
            return parameters;
        }

        private static ReferencesContract CreateReferences()
        {
            ReferencesContract references = new ReferencesContract();
            references.Platform = "Microsoft.NETCore.App";
            references.PlatformDetail = "every Microsoft.NETCore.App assembly of the .NET 10.0 shared framework on the machine running the tool";
            references.AvailableByDefault = "the whole Microsoft.NETCore.App set, which is System.*, mscorlib, netstandard and the rest of the framework";
            references.SuppliedByCaller = "the command-line tool takes one --reference <assembly path> per assembly and the execute tool takes an optional references array of paths; each path must be an existing file, a path that does not exist is refused by name, and each path together with the hash of its bytes is part of the artefact key";
            references.Followed = "each supplied reference is followed into the assemblies it declares that sit in the same folder: those are referenced, their paths and bytes join the artefact key, and no other file in that folder is referenced";
            references.FolderRule = "a reference's own folder is part of the contract: the declared siblings beside the reference are picked up and nothing else in that folder is, and no other folder is searched, so point a reference at a folder that holds the whole closure, such as a publish output or an application bin, rather than a library's own output, which often holds only itself";
            references.SuppliedByPackages = "the assemblies of each resolved package are added to the reference set and copied beside the compiled submission";
            references.NothingElse = "an assembly outside those three sources is not referenced, so using it is a compile error";
            return references;
        }

        private static PackagesContract CreatePackages()
        {
            PackagesContract packages = new PackagesContract();
            packages.Entry = "each entry is an object with id and version, both required and both strings";
            packages.ExactVersion = "the version must be one exact version; a version containing [, ], (, ), , or * is a range and is refused before any network access, and a version that does not parse is refused";
            packages.Feed = "nuget.org v3 index is the only feed; no configuration file, additional feed, fallback folder or credential provider takes part";
            packages.Cache = "resolved packages are cached under the data root by lowercased id and normalised version; an entry counts as complete only when the installer metadata is present, which the installer writes last; a directory without that metadata is installed again rather than used; the cache is consulted before the feed";
            packages.Refusals = new List<RefusalDescription>
            {
                Refusal("id or version absent", "a package needs an id and a version; it was asked for as id <id> and version <version>"),
                Refusal("version is a range", "package <id> asks for version <version>, which is a range rather than one exact version"),
                Refusal("version is not a version", "package <id> asks for version <version>, which is not a version"),
                Refusal("pinned version absent from the feed", "package <id> version <version> does not exist on nuget.org"),
                Refusal("no published version satisfies a dependency range", "package <id> has no version matching <range> on nuget.org"),
                Refusal("one id required at two versions", "package <id> is needed at <version> and at <range> by the same request"),
                Refusal("no dependency group for the target framework", "package <id> version <version> carries no dependency group for net10.0; it offers <folders>"),
                Refusal("no lib folder", "package <id> version <version> offers no lib folder and so no assembly to compile against"),
                Refusal("no assembly for the target framework", "package <id> version <version> offers no assembly for net10.0; it offers <folders>"),
                Refusal("install failed", "package <id> version <version> could not be installed from nuget.org")
            };
            packages.RefusalEffect = "a refused package is reported before anything is compiled or run, so no outcome and no record exists for a refused request";
            return packages;
        }

        private static RefusalDescription Refusal(string condition, string messageShape)
        {
            RefusalDescription refusal = new RefusalDescription();
            refusal.Condition = condition;
            refusal.MessageShape = messageShape;
            return refusal;
        }

        private static TimeoutContract CreateTimeout()
        {
            TimeoutContract timeout = new TimeoutContract();
            timeout.DefaultSeconds = ExecutionDeadline.DefaultSeconds;
            timeout.CeilingSeconds = ExecutionDeadline.CeilingSeconds;
            timeout.Clamped = true;
            timeout.Detail = "the request field timeoutSeconds is an integer that defaults to 60; a value above 600 is clamped to 600 rather than refused";
            timeout.Expiry = "an expired deadline kills the submission and reports deadlineExpired true with exitStatus null";
            timeout.Guidance = "60 seconds is the default deadline and it is short for a long run: set timeoutSeconds explicitly for anything that sleeps, waits, or takes minutes, rather than relying on the default";
            return timeout;
        }

        private static ResponseContract CreateResponse()
        {
            ResponseContract response = new ResponseContract();
            response.UnmeasuredValue = null;
            response.UnmeasuredDetail = "an unmeasured number is null in this document and null in the outcome, never 0 and never the string unavailable; a client must accept both a number and null in the same field";
            response.Fields = new List<ResponseField>
            {
                ResponseFieldOf("executionId", "string", null, "the id of the execution record this outcome was written to"),
                ResponseFieldOf("status", "string", null, "completed, failed when the deadline expired or the exit status is not zero, or refused when the request or a path it names is bad"),
                ResponseFieldOf("exitStatus", "integer", "null", "the submission's exit status; unmeasured when it never started, failed to compile, or its deadline expired"),
                ResponseFieldOf("standardOutput", "string", null, "everything written to standard output, including what was written before a failure"),
                ResponseFieldOf("standardError", "string", null, "everything written to standard error, including what was written before a failure"),
                ResponseFieldOf("deadlineExpired", "boolean", null, "true when the submission was killed at its deadline"),
                ResponseFieldOf("elapsedMilliseconds", "integer", "null", "wall clock time of the child process"),
                ResponseFieldOf("processorMilliseconds", "integer", "null", "processor time of the child process"),
                ResponseFieldOf("peakWorkingSetBytes", "integer", "null", "peak working set of the child process"),
                ResponseFieldOf("diagnostics", "array", null, "compiler errors when the submission did not compile"),
                ResponseFieldOf("reused", "object", null, "present only when the outcome was served from a stored execution; see reuse"),
                ResponseFieldOf("refusal", "object", null, "present only when a refusal is reported beside the outcome; see refusal")
            };
            response.Diagnostic = Shape("compiler errors when the submission did not compile, otherwise an empty array", new List<ResponseField>
            {
                ResponseFieldOf("id", "string", null, "compiler diagnostic id, for example CS0103"),
                ResponseFieldOf("severity", "string", null, "Error"),
                ResponseFieldOf("message", "string", null, "the compiler's message"),
                ResponseFieldOf("filePath", "string", null, "the submission's source path"),
                ResponseFieldOf("line", "integer", null, "1-based line"),
                ResponseFieldOf("column", "integer", null, "1-based column")
            });
            response.Reuse = Shape("an outcome served from a stored execution carries this block; it describes the earlier execution, so its timings belong to that execution rather than to this request", new List<ResponseField>
            {
                ResponseFieldOf("executionId", "string", null, "the id of the stored execution this outcome was served from"),
                ResponseFieldOf("recordedAt", "string", null, "when that execution was recorded"),
                ResponseFieldOf("runnerVersion", "string", null, "the version of the build that ran it"),
                ResponseFieldOf("hint", "string", null, "why the stored outcome was reused")
            });
            response.Refusal = Shape("a refusal reported beside an outcome carries these fields, so a caller can tell a refused request from a failed execution", new List<ResponseField>
            {
                ResponseFieldOf("name", "string", null, "the refusal's name"),
                ResponseFieldOf("message", "string", null, "the refusal's message")
            });
            return response;
        }

        private static ResponseShape Shape(string detail, List<ResponseField> fields)
        {
            ResponseShape shape = new ResponseShape();
            shape.Detail = detail;
            shape.Fields = fields;
            return shape;
        }

        private static ResponseField ResponseFieldOf(string name, string type, string whenUnmeasured, string description)
        {
            ResponseField field = new ResponseField();
            field.Name = name;
            field.Type = type;
            field.WhenUnmeasured = whenUnmeasured;
            field.Description = description;
            return field;
        }

        private static ExampleRequest CreateExampleRequest()
        {
            ExampleSubmission submission = new ExampleSubmission();
            string[] lines =
            {
                "using System;",
                "using System.Linq;",
                "",
                "public static class Program",
                "{",
                "    public static int Main(string[] args)",
                "    {",
                "        string[] words = args.Select(argument => argument.ToUpperInvariant()).ToArray();",
                "        Console.WriteLine(string.Join(\" \", words));",
                "        return 0;",
                "    }",
                "}"
            };
            submission.SourceText = string.Join("\n", lines);
            submission.Parameters = new Dictionary<string, string>();
            submission.Parameters["answer"] = "42";
            ExampleRequest example = new ExampleRequest();
            example.Name = "hello";
            example.Description = "print one line and exit";
            example.Category = "sample";
            example.Timestamp = "2026-01-01T00:00:00+00:00";
            example.Submission = submission;
            example.Packages = new List<ExamplePackage>();
            example.Response = null;
            return example;
        }
    }
}
