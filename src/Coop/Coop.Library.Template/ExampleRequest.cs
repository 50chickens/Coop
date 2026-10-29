using System.Collections.Generic;

namespace Coop.Library.Template
{
    public sealed class ExampleRequest
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string Category { get; set; }

        public string Timestamp { get; set; }

        public ExampleSubmission Submission { get; set; }

        public List<ExamplePackage> Packages { get; set; }

        public object Response { get; set; }
    }

    public sealed class ExampleSubmission
    {
        public string SourceText { get; set; }

        public Dictionary<string, string> Parameters { get; set; }
    }

    public sealed class ExamplePackage
    {
        public string Id { get; set; }

        public string Version { get; set; }
    }
}
