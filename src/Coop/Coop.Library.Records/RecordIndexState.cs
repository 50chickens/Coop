namespace Coop.Library.Records
{
    public sealed class RecordIndexState
    {
        public bool Exists { get; set; }

        public bool Stale { get; set; }

        public int DocumentCount { get; set; }

        public int IndexedCount { get; set; }

        public long DocumentSignature { get; set; }

        public long IndexedSignature { get; set; }
    }
}
