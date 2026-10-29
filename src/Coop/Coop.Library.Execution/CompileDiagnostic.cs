namespace Coop.Library.Execution
{
    public sealed class CompileDiagnostic
    {
        public string Id { get; set; }

        public string Severity { get; set; }

        public string Message { get; set; }

        public string FilePath { get; set; }

        public int Line { get; set; }

        public int Column { get; set; }
    }
}
