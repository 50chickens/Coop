using System.Collections.Generic;

namespace Coop.Library.Template
{
    public sealed class RequestContract
    {
        public string Format { get; set; }

        public string PropertyNames { get; set; }

        public string Validation { get; set; }

        public List<RequestField> Fields { get; set; }
    }

    public sealed class RequestField
    {
        public string Name { get; set; }

        public string Type { get; set; }

        public bool Required { get; set; }

        public string Default { get; set; }

        public string Description { get; set; }
    }
}
