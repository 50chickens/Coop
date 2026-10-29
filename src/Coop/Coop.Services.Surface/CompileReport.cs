using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Coop.Library.Execution;

namespace Coop.Services.Surface
{
    public sealed class CompileReport
    {
        public const string MissingSiblingsDetail = "a reference's declared siblings are looked for only in the reference's own folder, so point a reference at a folder that holds the whole closure, such as a publish output or an application bin, rather than a library's own output, which often holds only itself";

        public const string UndeclaredSiblingsDetail = "these assemblies sit in a reference's folder but the reference does not declare them, so they are not used; pass each one explicitly as a reference to make it available";

        public bool Success { get; set; }

        public long ElapsedMilliseconds { get; set; }

        public List<CompileDiagnostic> Diagnostics { get; set; } = new List<CompileDiagnostic>();

        public List<MissingSibling> MissingSiblings { get; set; } = new List<MissingSibling>();

        public List<UndeclaredSibling> UndeclaredSiblings { get; set; } = new List<UndeclaredSibling>();

        public static CompileReport Reused(ExecutionOutcome outcome)
        {
            CompileReport report = new CompileReport();
            report.Diagnostics = outcome.Diagnostics;
            report.Success = outcome.Diagnostics.Count == 0;
            report.ElapsedMilliseconds = 0;
            return report;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject document = new JsonObject();
            document["success"] = Success;
            document["elapsedMilliseconds"] = ElapsedMilliseconds;
            document["diagnosticCount"] = Diagnostics.Count;
            document["missingSiblings"] = SiblingsOf();
            document["undeclaredSiblings"] = UndeclaredOf();
            if (MissingSiblings.Count > 0)
            {
                document["missingSiblingsDetail"] = MissingSiblingsDetail;
            }
            if (UndeclaredSiblings.Count > 0)
            {
                document["undeclaredSiblingsDetail"] = UndeclaredSiblingsDetail;
            }
            JsonArray diagnostics = new JsonArray();
            Diagnostics.ToList().ForEach(diagnostic => diagnostics.Add(OfDiagnostic(diagnostic)));
            document["diagnostics"] = diagnostics;
            return document;
        }

        private JsonArray SiblingsOf()
        {
            JsonArray siblings = new JsonArray();
            MissingSiblings.ToList().ForEach(sibling => siblings.Add(OfSibling(sibling)));
            return siblings;
        }

        private JsonArray UndeclaredOf()
        {
            JsonArray siblings = new JsonArray();
            UndeclaredSiblings
                .Select(sibling => SiblingDocumentOf(sibling.Name, sibling.Folder))
                .ToList()
                .ForEach(document => siblings.Add(document));
            return siblings;
        }

        private static JsonObject OfSibling(MissingSibling sibling)
        {
            return SiblingDocumentOf(sibling.Name, sibling.Folder);
        }

        private static JsonObject SiblingDocumentOf(string name, string folder)
        {
            JsonObject document = new JsonObject();
            document["name"] = name;
            document["folder"] = folder;
            return document;
        }

        private static JsonObject OfDiagnostic(CompileDiagnostic diagnostic)
        {
            JsonObject document = new JsonObject();
            document["id"] = diagnostic.Id;
            document["severity"] = diagnostic.Severity;
            document["message"] = diagnostic.Message;
            document["filePath"] = diagnostic.FilePath;
            document["line"] = diagnostic.Line;
            document["column"] = diagnostic.Column;
            return document;
        }
    }
}
