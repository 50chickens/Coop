using System;
using System.IO;
using Coop.Library.Common;
using Coop.Library.Execution;

namespace Coop.Library.Artefacts
{
    public sealed class ArtefactStore
    {
        public const string AssemblyFileName = SubmissionCompiler.AssemblyName + ".dll";

        public const string RuntimeConfigurationFileName = SubmissionCompiler.AssemblyName + ".runtimeconfig.json";

        private readonly string root;

        public ArtefactStore(string root)
        {
            this.root = root;
        }

        public string Root
        {
            get
            {
                return root;
            }
        }

        public string DirectoryPathOf(string artefactKey)
        {
            return Path.Combine(Path.Combine(root, "artefacts"), artefactKey);
        }

        public string NewStagingDirectory()
        {
            string path = Path.Combine(Path.Combine(root, "staging"), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public ArtefactLookup Read(string artefactKey)
        {
            string directoryPath = DirectoryPathOf(artefactKey);
            string assemblyPath = Path.Combine(directoryPath, AssemblyFileName);
            ArtefactLookup lookup = new ArtefactLookup();
            lookup.ArtefactKey = artefactKey;
            lookup.Found = File.Exists(assemblyPath) && File.Exists(Path.Combine(directoryPath, RuntimeConfigurationFileName));
            if (lookup.Found)
            {
                lookup.DirectoryPath = directoryPath;
                lookup.AssemblyPath = assemblyPath;
            }
            return lookup;
        }

        public ArtefactLookup Claim(string artefactKey, string stagingDirectory)
        {
            string destination = DirectoryPathOf(artefactKey);
            Directory.CreateDirectory(Path.Combine(root, "artefacts"));
            MoveIntoPlace(stagingDirectory, destination);
            return Read(artefactKey);
        }

        private static void MoveIntoPlace(string stagingDirectory, string destination)
        {
            try
            {
                AtomicFile.MoveDirectory(stagingDirectory, destination);
            }
            catch (IOException)
            {
                if (!Directory.Exists(destination))
                {
                    throw;
                }
                Directory.Delete(stagingDirectory, true);
            }
        }
    }
}
