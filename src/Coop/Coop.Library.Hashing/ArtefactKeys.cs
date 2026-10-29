using System;
using System.Collections.Generic;
using System.Linq;
using Coop.Library.Common;
using Coop.Library.Execution;

namespace Coop.Library.Hashing
{
    public static class ArtefactKeys
    {
        public const string SchemeVersion = "2";

        public static KeyOutcome Of(ArtefactKeyRequest request)
        {
            if (request.SourceText == null)
            {
                return KeyOutcome.Refused(HashingRefusalNames.MissingSource, "the request supplies no source text");
            }
            if (request.RuntimeIdentifier == null)
            {
                return KeyOutcome.Refused(HashingRefusalNames.MissingRuntimeIdentifier, "the request supplies no runtime identifier");
            }
            ResolvedPackage unpinned = PackageClosure.Unpinned(request.Packages);
            if (unpinned != null)
            {
                return KeyOutcome.Refused(HashingRefusalNames.UnpinnedPackage, "the resolved package " + unpinned.Id + " carries no version");
            }
            List<ResolvedPackage> packages = PackageClosure.Sorted(request.Packages);
            List<KeyReference> references = request.References
                .OrderBy(reference => reference.Path, StringComparer.Ordinal)
                .ThenBy(reference => reference.ContentHash, StringComparer.Ordinal)
                .ToList();
            ContentKeyBuilder builder = new ContentKeyBuilder();
            builder.Add("scheme.version", SchemeVersion);
            builder.Add("source", SourceNormalisation.Apply(request.SourceText));
            builder.Add("assembly.name", SubmissionCompiler.AssemblyName);
            builder.Add("target.framework", request.TargetFramework);
            builder.Add("runtime.identifier", request.RuntimeIdentifier);
            builder.Add("compiler.version", request.CompilerVersion);
            builder.Add("language.version", request.LanguageVersion);
            builder.Add("output.kind", request.OutputKind);
            builder.Add("optimization.level", request.OptimizationLevel);
            builder.Add("reference.runtime.version", request.ReferenceRuntimeVersion);
            builder.Add("package.ids", PackageClosure.Ids(packages));
            builder.Add("package.versions", PackageClosure.Versions(packages));
            builder.Add("reference.paths", references.Select(reference => reference.Path).ToList());
            builder.Add("reference.names", references.Select(reference => reference.FileName).ToList());
            builder.Add("reference.hashes", references.Select(reference => reference.ContentHash).ToList());
            KeyOutcome outcome = new KeyOutcome();
            outcome.Key = builder.Finish();
            return outcome;
        }
    }
}
