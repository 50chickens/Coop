using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Coop.Library.Packages
{
    public sealed class PackageResolver
    {
        public const string FeedUrl = "https://api.nuget.org/v3/index.json";

        private const string CompletenessMarker = ".complete";

        private readonly string _cacheRoot;

        public PackageResolver(string cacheRoot)
        {
            _cacheRoot = cacheRoot;
        }

        public async Task<PackageResolution> ResolveAsync(IReadOnlyList<PackageRequest> requests)
        {
            List<ResolvedPackage> resolved = new List<ResolvedPackage>();
            foreach (PackageRequest request in requests)
            {
                PackageResolution refusal = await ValidateAsync(request);
                if (refusal != null)
                {
                    return refusal;
                }
                resolved.Add(await ResolveOneAsync(request));
            }

            PackageResolution resolution = new PackageResolution();
            resolution.Packages = resolved.ToArray();
            return resolution;
        }

        private static Task<PackageResolution> ValidateAsync(PackageRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
            {
                return Task.FromResult(Refuse(PackageRefusalNames.EmptyId, "a package entry has no id"));
            }

            NuGetVersion version;
            if (NuGetVersion.TryParse(request.Version, out version) == false)
            {
                return Task.FromResult(Refuse(PackageRefusalNames.UnpinnedVersion, $"package '{request.Id}' has no usable pinned version, found '{request.Version}'"));
            }

            return Task.FromResult<PackageResolution>(null);
        }

        private async Task<ResolvedPackage> ResolveOneAsync(PackageRequest request)
        {
            string folder = Path.Combine(_cacheRoot, request.Id.ToLowerInvariant(), request.Version);
            string marker = Path.Combine(folder, CompletenessMarker);

            ResolvedPackage package = new ResolvedPackage();
            package.Id = request.Id;
            package.Version = request.Version;
            package.PackageFolder = folder;

            if (File.Exists(marker))
            {
                package.FromCache = true;
                package.AssemblyPaths = ReadCachedAssemblies(folder);
                return package;
            }

            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
            Directory.CreateDirectory(folder);

            NuGetVersion version = NuGetVersion.Parse(request.Version);
            string nupkgPath = Path.Combine(folder, request.Id.ToLowerInvariant() + "." + request.Version + ".nupkg");

            SourceRepository repository = Repository.Factory.GetCoreV3(FeedUrl);
            FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();
            using (FileStream stream = new FileStream(nupkgPath, FileMode.Create, FileAccess.Write))
            {
                bool downloaded = await resource.CopyNupkgToStreamAsync(request.Id, version, stream, new SourceCacheContext(), NullLogger.Instance, CancellationToken.None);
                if (downloaded == false)
                {
                    throw new InvalidOperationException($"package {request.Id} {request.Version} was not found on {FeedUrl}");
                }
            }

            string libFolder = Path.Combine(folder, "lib");
            Directory.CreateDirectory(libFolder);
            List<string> written = ExtractAssemblies(nupkgPath, libFolder);
            if (written.Count == 0)
            {
                throw new InvalidOperationException($"package {request.Id} {request.Version} has no usable lib assemblies");
            }

            File.Delete(nupkgPath);
            File.WriteAllText(marker, written.Count.ToString());

            package.FromCache = false;
            package.AssemblyPaths = written.ToArray();
            return package;
        }

        private static List<string> ExtractAssemblies(string nupkgPath, string libFolder)
        {
            List<string> written = new List<string>();
            using (PackageArchiveReader reader = new PackageArchiveReader(nupkgPath))
            {
                FrameworkSpecificGroup group = SelectGroup(reader.GetLibItems());
                if (group == null)
                {
                    return written;
                }

                foreach (string entry in group.Items)
                {
                    if (entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) == false)
                    {
                        continue;
                    }
                    string destination = Path.Combine(libFolder, Path.GetFileName(entry));
                    using (Stream stream = reader.GetStream(entry))
                    using (FileStream file = new FileStream(destination, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(file);
                    }
                    written.Add(destination);
                }
            }
            return written;
        }

        private static FrameworkSpecificGroup SelectGroup(IEnumerable<FrameworkSpecificGroup> groups)
        {
            NuGetFramework target = NuGetFramework.Parse("net10.0");
            List<FrameworkSpecificGroup> usable = groups.Where(group => group.Items.Any()).ToList();
            if (usable.Count == 0)
            {
                return null;
            }

            FrameworkSpecificGroup exact = usable.FirstOrDefault(group => NuGetFramework.Parse(group.TargetFramework.GetShortFolderName()).Equals(target));
            if (exact != null)
            {
                return exact;
            }

            NuGetFramework netStandard = NuGetFramework.Parse("netstandard2.0");
            FrameworkSpecificGroup standard = usable.FirstOrDefault(group => NuGetFramework.Parse(group.TargetFramework.GetShortFolderName()).Equals(netStandard));
            if (standard != null)
            {
                return standard;
            }

            return usable.OrderByDescending(group => group.TargetFramework.Version).First();
        }

        private static string[] ReadCachedAssemblies(string folder)
        {
            return Directory.GetFiles(Path.Combine(folder, "lib"), "*.dll");
        }

        private static PackageResolution Refuse(string name, string message)
        {
            PackageResolution resolution = new PackageResolution();
            resolution.RefusalName = name;
            resolution.RefusalMessage = message;
            return resolution;
        }
    }
}
