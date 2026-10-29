using System.IO;

namespace Coop.Services.Agent
{
    public sealed class CoopExecutionOptions
    {
        public string WorkingRoot { get; set; } = Path.Combine(Path.GetTempPath(), "coop-agent");

        public string PackageCacheRoot { get; set; } = Path.Combine(Path.GetTempPath(), "coop-packages");

        public int? TimeoutSeconds { get; set; }

        public CoopExecutionOptions Copy()
        {
            CoopExecutionOptions copy = new CoopExecutionOptions();
            copy.WorkingRoot = WorkingRoot;
            copy.PackageCacheRoot = PackageCacheRoot;
            copy.TimeoutSeconds = TimeoutSeconds;
            return copy;
        }
    }
}
