using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Coop.Library.Execution
{
    public static class ReferenceClosure
    {
        public static List<string> Resolve(IEnumerable<string> supplied)
        {
            List<string> resolved = new List<string>();
            if (supplied == null)
            {
                return resolved;
            }
            List<string> suppliedPaths = supplied.Select(path => Path.GetFullPath(path)).ToList();
            HashSet<string> platform = PlatformNames();
            HashSet<string> added = new HashSet<string>(suppliedPaths, StringComparer.OrdinalIgnoreCase);
            Queue<string> pending = new Queue<string>(suppliedPaths);
            while (pending.Count > 0)
            {
                string path = pending.Dequeue();
                resolved.Add(path);
                DependenciesOf(path)
                    .Where(name => platform.Contains(name) == false)
                    .Select(name => Path.Combine(Path.GetDirectoryName(path), name + ".dll"))
                    .Where(candidate => File.Exists(candidate))
                    .Select(candidate => Path.GetFullPath(candidate))
                    .Where(candidate => added.Add(candidate))
                    .ToList()
                    .ForEach(pending.Enqueue);
            }
            return resolved;
        }

        public static List<MissingSibling> Missing(IEnumerable<string> supplied)
        {
            List<MissingSibling> missing = new List<MissingSibling>();
            if (supplied == null)
            {
                return missing;
            }
            HashSet<string> platform = PlatformNames();
            supplied
                .ToList()
                .ForEach(path => AbsentBeside(path, platform, missing));
            return missing;
        }

        private static void AbsentBeside(string path, HashSet<string> platform, List<MissingSibling> missing)
        {
            string folder = Path.GetDirectoryName(path);
            DependenciesOf(path)
                .Where(name => platform.Contains(name) == false)
                .Where(name => File.Exists(Path.Combine(folder, name + ".dll")) == false)
                .Where(name => missing.Any(entry => entry.Name == name && entry.Folder == folder) == false)
                .ToList()
                .ForEach(name => missing.Add(SiblingOf(name, folder)));
        }

        private static MissingSibling SiblingOf(string name, string folder)
        {
            MissingSibling sibling = new MissingSibling();
            sibling.Name = name;
            sibling.Folder = folder;
            return sibling;
        }

        public static List<UndeclaredSibling> Undeclared(IEnumerable<string> supplied)
        {
            List<UndeclaredSibling> undeclared = new List<UndeclaredSibling>();
            if (supplied == null)
            {
                return undeclared;
            }
            List<string> paths = supplied.Select(path => Path.GetFullPath(path)).ToList();
            HashSet<string> platform = PlatformNames();
            HashSet<string> used = Resolve(paths)
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            paths.ForEach(path => UndeclaredBeside(path, platform, used, undeclared));
            return undeclared;
        }

        private static void UndeclaredBeside(string path, HashSet<string> platform, HashSet<string> used, List<UndeclaredSibling> undeclared)
        {
            string folder = Path.GetDirectoryName(path);
            Directory
                .GetFiles(folder, "*.dll")
                .Where(candidate => string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase) == false)
                .Select(candidate => Path.GetFileNameWithoutExtension(candidate))
                .Where(name => platform.Contains(name) == false)
                .Where(name => used.Contains(name) == false)
                .Where(name => undeclared.Any(entry => entry.Name == name && entry.Folder == folder) == false)
                .ToList()
                .ForEach(name => undeclared.Add(UndeclaredOf(name, folder)));
        }

        private static UndeclaredSibling UndeclaredOf(string name, string folder)
        {
            UndeclaredSibling sibling = new UndeclaredSibling();
            sibling.Name = name;
            sibling.Folder = folder;
            return sibling;
        }

        private static HashSet<string> PlatformNames()
        {
            return SubmissionCompiler.PlatformPaths()
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static List<string> DependenciesOf(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (PEReader reader = new PEReader(stream))
            {
                if (reader.HasMetadata == false)
                {
                    return new List<string>();
                }
                MetadataReader metadata = reader.GetMetadataReader();
                return metadata.AssemblyReferences
                    .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
                    .ToList();
            }
        }
    }
}
