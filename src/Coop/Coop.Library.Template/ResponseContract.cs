using System.Collections.Generic;

namespace Coop.Library.Template
{
    public sealed class ResponseContract
    {
        public string UnmeasuredValue { get; set; }

        public string UnmeasuredDetail { get; set; }

        public List<ResponseField> Fields { get; set; }

        public ResponseShape Diagnostic { get; set; }

        public ResponseShape Reuse { get; set; }

        public ResponseShape Refusal { get; set; }
    }

    public sealed class ResponseShape
    {
        public string Detail { get; set; }

        public List<ResponseField> Fields { get; set; }
    }

    public sealed class ResponseField
    {
        public string Name { get; set; }

        public string Type { get; set; }

        public string WhenUnmeasured { get; set; }

        public string Description { get; set; }
    }
}
