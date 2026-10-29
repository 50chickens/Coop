namespace Coop.Library.Template
{
    public sealed class LanguageContract
    {
        public string Name { get; set; }

        public string LanguageVersion { get; set; }

        public string OutputKind { get; set; }

        public string TargetFramework { get; set; }

        public string AssemblyName { get; set; }

        public string OptimizationLevel { get; set; }

        public bool ImplicitUsings { get; set; }

        public string NullableContext { get; set; }
    }
}
