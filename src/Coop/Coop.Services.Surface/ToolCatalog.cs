using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;

namespace Coop.Services.Surface
{
    public static class ToolCatalog
    {
        public const string ExecuteName = "execute";

        public const string TemplateName = "template";

        public const string GetName = "get";

        public const string SearchName = "search";

        private const string ExecuteSchema = "{\"type\":\"object\",\"properties\":{\"source\":{\"type\":\"string\",\"description\":\"Raw C# source text. It is the whole compilation unit: one console application with a type carrying static int Main(string[] args).\"},\"parameters\":{\"type\":\"object\",\"additionalProperties\":{\"type\":\"string\"},\"default\":{},\"description\":\"Parameter values, delivered as one key=value command-line argument each, sorted by key.\"},\"packages\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"},\"version\":{\"type\":\"string\"}},\"required\":[\"id\",\"version\"],\"additionalProperties\":false},\"default\":[],\"description\":\"Packages to make available. Refused by name: this build carries no package resolver.\"},\"references\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"default\":[],\"description\":\"Paths to assemblies added to the reference set so the submission can use their types. Each path must be an existing file and a path that does not exist is refused by name. Each path and the hash of its bytes are part of the artefact key, so replacing the assembly recompiles.\"},\"timeoutSeconds\":{\"type\":\"integer\",\"default\":60,\"description\":\"Deadline in seconds. An absent value means 60 and a value above 600 is clamped to 600.\"},\"name\":{\"type\":\"string\",\"default\":\"execute\",\"description\":\"Name recorded for this execution.\"},\"description\":{\"type\":\"string\",\"description\":\"Purpose of the execution. Defaults to the first 200 characters of source.\"},\"category\":{\"type\":\"string\",\"default\":\"agent\",\"description\":\"Category the execution is grouped under.\"}},\"required\":[\"source\"],\"additionalProperties\":false}";

        private const string TemplateSchema = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";

        private const string GetSchema = "{\"type\":\"object\",\"properties\":{\"executionId\":{\"type\":\"string\",\"description\":\"Identifier of the execution record to read.\"}},\"required\":[\"executionId\"],\"additionalProperties\":false}";

        private const string SearchSchema = "{\"type\":\"object\",\"properties\":{\"executionId\":{\"type\":\"string\",\"description\":\"Restrict the answer to one execution identifier.\"},\"name\":{\"type\":\"string\",\"description\":\"Substring of the execution name.\"},\"category\":{\"type\":\"string\",\"description\":\"Category to match, case-insensitively.\"},\"status\":{\"type\":\"string\",\"enum\":[\"pending\",\"running\",\"completed\",\"failed\"],\"description\":\"Status to match.\"},\"limit\":{\"type\":\"integer\",\"default\":50,\"description\":\"Maximum number of records returned, clamped to 200.\"}},\"additionalProperties\":false}";

        public static List<Tool> All()
        {
            List<Tool> tools = new List<Tool>();
            tools.Add(Describe(ExecuteName, "Compiles one C# submission and runs it as a contained child process, returning the execution outcome as JSON: the execution identifier and status, standard output, standard error, exit status, timings, compiler diagnostics, and a reuse marker when the answer was served from the result cache.", ExecuteSchema, false));
            tools.Add(Describe(TemplateName, "Returns the execution template document: the request fields, the submission fields, the entry point, how parameters arrive, the reference and package sources, the timeout default and ceiling, and the response fields. Read it before submitting anything.", TemplateSchema, true));
            tools.Add(Describe(GetName, "Returns one execution record by identifier, as written by execute. An identifier with no record is refused with the record refusal code 6.", GetSchema, true));
            tools.Add(Describe(SearchName, "Returns execution records matching the optional filters, newest first, together with the stale flag of the derived index the answer was read through.", SearchSchema, true));
            return tools;
        }

        public static List<string> Names()
        {
            return All().Select(tool => tool.Name).ToList();
        }

        private static Tool Describe(string name, string description, string schema, bool readOnly)
        {
            Tool tool = new Tool
            {
                Name = name,
                Description = description,
                InputSchema = JsonDocument.Parse(schema).RootElement.Clone()
            };
            ToolAnnotations annotations = new ToolAnnotations();
            annotations.Title = name;
            annotations.ReadOnlyHint = readOnly;
            annotations.DestructiveHint = false;
            annotations.IdempotentHint = false;
            annotations.OpenWorldHint = false;
            tool.Annotations = annotations;
            return tool;
        }
    }
}
