namespace Coop.Library.Records
{
    public sealed class RecordIndexRebuild
    {
        public RecordIndexDocument Index { get; set; }

        public int DocumentsRead { get; set; }

        public long ElapsedMilliseconds { get; set; }
    }
}
