using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Coop.Library.Execution;

namespace Coop.Services.Surface
{
    public sealed class ExecuteArguments
    {
        public const string DefaultName = "execute";

        public const string DefaultCategory = "agent";

        public const int DescriptionLength = 200;

        public string Source { get; set; }

        public Dictionary<string, string> Parameters { get; set; }

        public List<string> PackageNames { get; set; }

        public List<string> References { get; set; }

        public int? TimeoutSeconds { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Category { get; set; }

        public JsonObject Refusal { get; set; }

        public static ExecuteArguments Parse(JsonObject arguments)
        {
            ExecuteArguments parsed = new ExecuteArguments();
            parsed.Parameters = new Dictionary<string, string>();
            parsed.PackageNames = new List<string>();
            parsed.References = new List<string>();
            parsed.Name = DefaultName;
            parsed.Category = DefaultCategory;
            parsed.ReadSource(arguments);
            if (parsed.Refusal != null)
            {
                return parsed;
            }
            parsed.ReadParameters(arguments);
            parsed.ReadPackages(arguments);
            parsed.ReadReferences(arguments);
            parsed.ReadTimeout(arguments);
            parsed.ReadText(arguments, "name", value => parsed.Name = value);
            parsed.ReadText(arguments, "description", value => parsed.Description = value);
            parsed.ReadText(arguments, "category", value => parsed.Category = value);
            if (parsed.Refusal != null)
            {
                return parsed;
            }
            if (parsed.Description == null)
            {
                parsed.Description = JsonKinds.Shortened(parsed.Source, DescriptionLength);
            }
            return parsed;
        }

        private void ReadSource(JsonObject arguments)
        {
            JsonNode source = JsonKinds.NodeOf(arguments, "source");
            if (source == null)
            {
                Refusal = Refusals.RequiredField("source");
                return;
            }
            if (source.GetValueKind() != JsonValueKind.String)
            {
                Refusal = Refusals.WrongKind("source", JsonKinds.Of(source), "a JSON string");
                return;
            }
            Source = source.GetValue<string>();
        }

        private void ReadParameters(JsonObject arguments)
        {
            JsonNode parameters = JsonKinds.NodeOf(arguments, "parameters");
            if (parameters == null)
            {
                return;
            }
            if (parameters.GetValueKind() != JsonValueKind.Object)
            {
                Refusal = Refusals.WrongKind("parameters", JsonKinds.Of(parameters), "a JSON object");
                return;
            }
            JsonObject map = parameters.AsObject();
            List<KeyValuePair<string, JsonNode>> entries = map.ToList();
            List<string> offending = entries
                .Where(entry => entry.Value == null || entry.Value.GetValueKind() != JsonValueKind.String)
                .Select(entry => entry.Key)
                .ToList();
            if (offending.Count > 0)
            {
                string key = offending[0];
                JsonNode value = map[key];
                Refusal = Refusals.WrongKind("parameters." + key, JsonKinds.Of(value), "a JSON string");
                return;
            }
            entries.ForEach(entry => Parameters[entry.Key] = entry.Value.GetValue<string>());
        }

        private void ReadPackages(JsonObject arguments)
        {
            JsonNode packages = JsonKinds.NodeOf(arguments, "packages");
            if (packages == null)
            {
                return;
            }
            if (packages.GetValueKind() != JsonValueKind.Array)
            {
                Refusal = Refusals.WrongKind("packages", JsonKinds.Of(packages), "a JSON array");
                return;
            }
            List<JsonNode> entries = packages.AsArray().ToList();
            if (entries.Count == 0)
            {
                return;
            }
            PackageNames = entries.Select(entry => Described(entry)).ToList();
            Refusal = Refusals.Field("packages", "package support is not available: this build of Coop carries no package resolver, so " + string.Join(", ", PackageNames) + " cannot be supplied");
        }

        private void ReadReferences(JsonObject arguments)
        {
            JsonNode references = JsonKinds.NodeOf(arguments, "references");
            if (references == null)
            {
                return;
            }
            if (references.GetValueKind() != JsonValueKind.Array)
            {
                Refusal = Refusals.WrongKind("references", JsonKinds.Of(references), "a JSON array of file paths");
                return;
            }
            List<JsonNode> entries = references.AsArray().ToList();
            List<JsonNode> offending = entries
                .Where(entry => entry == null || entry.GetValueKind() != JsonValueKind.String)
                .ToList();
            if (offending.Count > 0)
            {
                Refusal = Refusals.WrongKind("references", JsonKinds.Of(offending[0]), "a JSON string holding a file path");
                return;
            }
            List<string> paths = entries.Select(entry => Path.GetFullPath(entry.GetValue<string>())).ToList();
            List<string> missing = paths.Where(path => File.Exists(path) == false).ToList();
            if (missing.Count > 0)
            {
                Refusal = Refusals.MissingReference(missing[0]);
                return;
            }
            References = ReferenceClosure.Resolve(paths);
        }

        private void ReadTimeout(JsonObject arguments)
        {
            JsonNode timeout = JsonKinds.NodeOf(arguments, "timeoutSeconds");
            if (timeout == null)
            {
                return;
            }
            if (timeout.GetValueKind() != JsonValueKind.Number)
            {
                Refusal = Refusals.Field("timeoutSeconds", "timeoutSeconds is " + JsonKinds.Of(timeout) + " rather than a whole number of seconds");
                return;
            }
            int whole;
            if (timeout.AsValue().TryGetValue(out whole) == false)
            {
                Refusal = Refusals.Field("timeoutSeconds", "timeoutSeconds is " + JsonKinds.Of(timeout) + " rather than a whole number of seconds");
                return;
            }
            TimeoutSeconds = whole;
        }

        private void ReadText(JsonObject arguments, string name, System.Action<string> assign)
        {
            if (Refusal != null)
            {
                return;
            }
            JsonNode node = JsonKinds.NodeOf(arguments, name);
            if (node == null)
            {
                return;
            }
            if (node.GetValueKind() != JsonValueKind.String)
            {
                Refusal = Refusals.WrongKind(name, JsonKinds.Of(node), "a JSON string");
                return;
            }
            assign(node.GetValue<string>());
        }

        private static string Described(JsonNode entry)
        {
            if (entry == null || entry.GetValueKind() != JsonValueKind.Object)
            {
                return JsonKinds.Of(entry);
            }
            JsonObject package = entry.AsObject();
            return "package '" + Stated(package, "id") + "' version '" + Stated(package, "version") + "'";
        }

        private static string Stated(JsonObject package, string name)
        {
            string value = JsonKinds.TextOf(package, name);
            if (value == null)
            {
                return "unstated";
            }
            return value;
        }
    }
}
