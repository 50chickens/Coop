namespace Coop.Library.Template
{
    public sealed class TimeoutContract
    {
        public int DefaultSeconds { get; set; }

        public int CeilingSeconds { get; set; }

        public bool Clamped { get; set; }

        public string Detail { get; set; }

        public string Expiry { get; set; }

        public string Guidance { get; set; }
    }
}
