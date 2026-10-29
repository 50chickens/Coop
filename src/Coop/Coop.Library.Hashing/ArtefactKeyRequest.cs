using System.Collections.Generic;

namespace Coop.Library.Hashing
{
    public sealed class ArtefactKeyRequest
    {
        public string SourceText { get; set; }

        public string TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string CompilerVersion { get; set; }

        public string LanguageVersion { get; set; }

        public string OutputKind { get; set; }

        public string OptimizationLevel { get; set; }

        public string ReferenceRuntimeVersion { get; set; }

        public List<ResolvedPackage> Packages { get; set; } = new List<ResolvedPackage>();

        public List<KeyReference> References { get; set; } = new List<KeyReference>();
    }
}
