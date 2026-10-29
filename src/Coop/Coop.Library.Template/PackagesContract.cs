using System.Collections.Generic;

namespace Coop.Library.Template
{
    public sealed class PackagesContract
    {
        public string Entry { get; set; }

        public string ExactVersion { get; set; }

        public string Feed { get; set; }

        public string Cache { get; set; }

        public List<RefusalDescription> Refusals { get; set; }

        public string RefusalEffect { get; set; }
    }

    public sealed class RefusalDescription
    {
        public string Condition { get; set; }

        public string MessageShape { get; set; }
    }
}
