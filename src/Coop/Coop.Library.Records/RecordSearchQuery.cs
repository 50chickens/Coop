namespace Coop.Library.Records
{
    public sealed class RecordSearchQuery
    {
        public string ExecutionId { get; set; }

        public string Name { get; set; }

        public string Category { get; set; }

        public string Status { get; set; }

        public int Limit { get; set; } = 50;

        public bool RefreshWhenStale { get; set; } = true;
    }
}
