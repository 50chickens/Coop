using System;
using System.Collections.Generic;
using System.Linq;
using Coop.Library.Common;

namespace Coop.Library.Hashing
{
    public static class ResultKeys
    {
        public const string SchemeVersion = "1";

        public static KeyOutcome Of(ResultKeyRequest request)
        {
            if (request.ArtefactKey == null)
            {
                return KeyOutcome.Refused(HashingRefusalNames.MissingArtefactKey, "the request supplies no artefact key");
            }
            if (request.BuildVersion == null)
            {
                return KeyOutcome.Refused(HashingRefusalNames.MissingBuildVersion, "the request supplies no build version");
            }
            ResolvedPackage unpinned = PackageClosure.Unpinned(request.Packages);
            if (unpinned != null)
            {
                return KeyOutcome.Refused(HashingRefusalNames.UnpinnedPackage, "the resolved package " + unpinned.Id + " carries no version");
            }
            List<ResolvedPackage> packages = PackageClosure.Sorted(request.Packages);
            List<KeyValuePair<string, string>> parameters = request.Parameters
                .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .ToList();
            ContentKeyBuilder builder = new ContentKeyBuilder();
            builder.Add("scheme.version", SchemeVersion);
            builder.Add("artefact.key", request.ArtefactKey);
            builder.Add("build.version", request.BuildVersion);
            builder.Add("package.ids", PackageClosure.Ids(packages));
            builder.Add("package.versions", PackageClosure.Versions(packages));
            builder.Add("parameter.names", parameters.Select(parameter => parameter.Key).ToList());
            builder.Add("parameter.values", parameters.Select(parameter => parameter.Value).ToList());
            KeyOutcome outcome = new KeyOutcome();
            outcome.Key = builder.Finish();
            return outcome;
        }
    }
}
