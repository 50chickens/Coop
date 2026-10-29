using System.Collections.Generic;

namespace Coop.Library.Execution
{
    public sealed class Submission
    {
        public string SourceText { get; set; }

        public string SourcePath { get; set; }

        public Dictionary<string, string> Parameters { get; set; }
    }
}
