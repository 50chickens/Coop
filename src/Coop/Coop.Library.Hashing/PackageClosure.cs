using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Library.Hashing
{
    internal static class PackageClosure
    {
        internal static ResolvedPackage Unpinned(IReadOnlyList<ResolvedPackage> packages)
        {
            return packages.FirstOrDefault(package => package.Version == null);
        }

        internal static List<ResolvedPackage> Sorted(IReadOnlyList<ResolvedPackage> packages)
        {
            return packages
                .OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(package => package.Id, StringComparer.Ordinal)
                .ThenBy(package => package.Version, StringComparer.Ordinal)
                .ToList();
        }

        internal static List<string> Ids(IReadOnlyList<ResolvedPackage> sorted)
        {
            return sorted.Select(package => package.Id).ToList();
        }

        internal static List<string> Versions(IReadOnlyList<ResolvedPackage> sorted)
        {
            return sorted.Select(package => package.Version).ToList();
        }
    }
}
