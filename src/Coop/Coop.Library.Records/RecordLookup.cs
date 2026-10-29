namespace Coop.Library.Records
{
    public sealed class RecordLookup
    {
        public ExecutionRecord Record { get; set; }

        public RecordRefusal Refusal { get; set; }

        public static RecordLookup Found(ExecutionRecord record)
        {
            RecordLookup lookup = new RecordLookup();
            lookup.Record = record;
            return lookup;
        }

        public static RecordLookup Refused(string name, string message, int code)
        {
            RecordRefusal refusal = new RecordRefusal();
            refusal.Name = name;
            refusal.Message = message;
            refusal.Code = code;
            RecordLookup lookup = new RecordLookup();
            lookup.Refusal = refusal;
            return lookup;
        }
    }
}
