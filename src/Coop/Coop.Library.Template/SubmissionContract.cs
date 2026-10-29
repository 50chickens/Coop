using System.Collections.Generic;

namespace Coop.Library.Template
{
    public sealed class SubmissionContract
    {
        public List<RequestField> Fields { get; set; }

        public EntryPointContract EntryPoint { get; set; }
    }

    public sealed class EntryPointContract
    {
        public string Form { get; set; }

        public string Type { get; set; }

        public string Accessibility { get; set; }

        public string ExitStatus { get; set; }

        public string Unit { get; set; }
    }
}
