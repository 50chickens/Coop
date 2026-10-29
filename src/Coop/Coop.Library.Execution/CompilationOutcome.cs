using System.Collections.Generic;

namespace Coop.Library.Execution
{
    public sealed class CompilationOutcome
    {
        public bool Success { get; set; }

        public string AssemblyPath { get; set; }

        public List<CompileDiagnostic> Diagnostics { get; set; }
    }
}
