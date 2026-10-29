using System;
using System.IO;
using System.Reflection;

namespace Coop.Services.Surface
{
    public sealed class SurfaceConfiguration
    {
        public const string DataRootVariable = "COOP_DATA_ROOT";

        public const string DefaultDataFolder = "coop-data";

        public const string RecordFolder = "records";

        public const string LockFileName = "coop.lock";

        public string Version { get; set; }

        public string DataRoot { get; set; }

        public string ArtefactStoreRoot { get; set; }

        public string RecordStoreRoot { get; set; }

        public string LockPath { get; set; }

        public static SurfaceConfiguration Resolve()
        {
            string configured = Environment.GetEnvironmentVariable(DataRootVariable);
            string chosen = configured;
            if (string.IsNullOrWhiteSpace(chosen))
            {
                chosen = Path.Combine(AppContext.BaseDirectory, DefaultDataFolder);
            }
            string dataRoot = Path.GetFullPath(chosen);
            Directory.CreateDirectory(dataRoot);
            SurfaceConfiguration configuration = new SurfaceConfiguration();
            configuration.Version = VersionOf();
            configuration.DataRoot = dataRoot;
            configuration.ArtefactStoreRoot = dataRoot;
            configuration.RecordStoreRoot = Path.Combine(dataRoot, RecordFolder);
            configuration.LockPath = Path.Combine(dataRoot, LockFileName);
            return configuration;
        }

        public static string VersionOf()
        {
            Assembly assembly = typeof(SurfaceConfiguration).Assembly;
            AssemblyInformationalVersionAttribute attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute != null)
            {
                return attribute.InformationalVersion;
            }
            return assembly.GetName().Version.ToString();
        }
    }
}
