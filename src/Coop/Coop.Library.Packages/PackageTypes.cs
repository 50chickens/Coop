namespace Coop.Library.Packages
{
    public sealed class PackageRequest
    {
        public string Id { get; set; }

        public string Version { get; set; }
    }

    public sealed class ResolvedPackage
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public bool FromCache { get; set; }

        public string PackageFolder { get; set; }

        public string[] AssemblyPaths { get; set; }
    }

    public sealed class PackageResolution
    {
        public ResolvedPackage[] Packages { get; set; }

        public string RefusalName { get; set; }

        public string RefusalMessage { get; set; }

        public bool Resolved
        {
            get
            {
                return RefusalName == null;
            }
        }
    }

    public static class PackageRefusalNames
    {
        public const string EmptyId = "package.emptyId";

        public const string UnpinnedVersion = "package.unpinnedVersion";

        public const string NotFound = "package.notFound";

        public const string NoAssemblies = "package.noAssemblies";
    }
}
