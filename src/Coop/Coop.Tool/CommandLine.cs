using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Coop.Services.Surface;

namespace Coop.Tool
{
    public static class CommandLine
    {
        private const string Command = "exec";

        public const string StandardInputPath = "-";

        public static ParsedCommandLine Parse(string[] args)
        {
            ParsedCommandLine parsed = new ParsedCommandLine();
            List<string> tokens = args.ToList();
            if (tokens.Count == 0)
            {
                parsed.Refusal = Refusals.Field("command", "the first argument is the command and the command line carries none; the command is '" + Command + "'");
                return parsed;
            }
            if (tokens[0] != Command)
            {
                parsed.Refusal = Refusals.Field("command", "the command is '" + tokens[0] + "' rather than '" + Command + "'");
                return parsed;
            }
            SubmissionOptions options = new SubmissionOptions();
            options.Parameters = new Dictionary<string, string>();
            options.Texts = new Dictionary<string, string>();
            options.References = new List<string>();
            int index = 1;
            while (index < tokens.Count)
            {
                if (index + 1 == tokens.Count)
                {
                    parsed.Refusal = Refusals.Field(tokens[index], tokens[index] + " requires a value");
                    return parsed;
                }
                if (options.Accept(tokens[index], tokens[index + 1]) == false)
                {
                    parsed.Refusal = Refusals.Field(tokens[index], tokens[index] + " is not an option this command accepts");
                    return parsed;
                }
                index = index + 2;
            }
            if (options.FilePath == null)
            {
                parsed.Refusal = Refusals.Field("--file", "--file is required and this command carries no other source");
                return parsed;
            }
            parsed.Arguments = SubmissionOf(options, parsed);
            return parsed;
        }

        private static JsonObject SubmissionOf(SubmissionOptions options, ParsedCommandLine parsed)
        {
            string source = SourceOf(options.FilePath, parsed);
            if (parsed.Refusal != null)
            {
                return null;
            }
            List<string> references = options.References.Select(path => Path.GetFullPath(path)).ToList();
            List<string> missing = references.Where(path => File.Exists(path) == false).ToList();
            if (missing.Count > 0)
            {
                parsed.Refusal = Refusals.MissingReference(missing[0]);
                return null;
            }
            JsonObject parameters = new JsonObject();
            options.Parameters.ToList().ForEach(entry => parameters[entry.Key] = entry.Value);
            JsonObject submission = new JsonObject();
            submission["source"] = source;
            submission["parameters"] = parameters;
            options.Texts.ToList().ForEach(entry => submission[entry.Key] = entry.Value);
            if (references.Count > 0)
            {
                JsonArray declared = new JsonArray();
                references.ForEach(reference => declared.Add(reference));
                submission["references"] = declared;
            }
            if (options.TimeoutSeconds != null)
            {
                submission["timeoutSeconds"] = options.TimeoutSeconds.Value;
            }
            return submission;
        }

        private static string SourceOf(string path, ParsedCommandLine parsed)
        {
            if (path == StandardInputPath)
            {
                return Console.In.ReadToEnd();
            }
            string fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath) == false)
            {
                parsed.Refusal = Refusals.MissingPath("--file", fullPath, "--file names a path that does not exist");
                return null;
            }
            try
            {
                return File.ReadAllText(fullPath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                parsed.Refusal = Refusals.MissingPath("--file", fullPath, "--file names a path that cannot be read: " + exception.Message);
                return null;
            }
        }

        private sealed class SubmissionOptions
        {
            public string FilePath { get; set; }

            public int? TimeoutSeconds { get; set; }

            public Dictionary<string, string> Parameters { get; set; }

            public Dictionary<string, string> Texts { get; set; }

            public List<string> References { get; set; }

            public bool Accept(string name, string value)
            {
                if (name == "--file")
                {
                    FilePath = value;
                    return true;
                }
                if (name == "--timeout")
                {
                    TimeoutSeconds = int.Parse(value, CultureInfo.InvariantCulture);
                    return true;
                }
                if (name == "--reference")
                {
                    References.Add(value);
                    return true;
                }
                if (name == "--param")
                {
                    return AcceptParameter(value);
                }
                if (name == "--name" || name == "--description" || name == "--category")
                {
                    Texts[name.Substring(2)] = value;
                    return true;
                }
                return false;
            }

            private bool AcceptParameter(string value)
            {
                int separator = value.IndexOf('=');
                if (separator < 0)
                {
                    return false;
                }
                Parameters[value.Substring(0, separator)] = value.Substring(separator + 1);
                return true;
            }
        }
    }
}
