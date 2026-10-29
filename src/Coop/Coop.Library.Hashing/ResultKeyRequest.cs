using System.Collections.Generic;

namespace Coop.Library.Hashing
{
    public sealed class ResultKeyRequest
    {
        public string ArtefactKey { get; set; }

        public string BuildVersion { get; set; }

        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

        public List<ResolvedPackage> Packages { get; set; } = new List<ResolvedPackage>();
    }
}
