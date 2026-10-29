namespace Coop.Library.Records
{
    public sealed class RecordFile
    {
        public string FileName { get; set; }

        public string ExecutionId { get; set; }

        public long LastWriteTicks { get; set; }

        public long Length { get; set; }
    }
}
