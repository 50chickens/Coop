namespace Coop.Library.Template
{
    public sealed class TemplateDocument
    {
        public int TemplateVersion { get; set; }

        public string ToolVersion { get; set; }

        public LanguageContract Language { get; set; }

        public RequestContract Request { get; set; }

        public SubmissionContract Submission { get; set; }

        public ParametersContract Parameters { get; set; }

        public ReferencesContract References { get; set; }

        public PackagesContract Packages { get; set; }

        public TimeoutContract Timeout { get; set; }

        public ResponseContract Response { get; set; }

        public ExampleRequest ExampleRequest { get; set; }
    }
}
