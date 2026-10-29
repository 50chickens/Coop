namespace Coop.Library.Records
{
    public sealed class RecordWrite
    {
        public string DocumentPath { get; set; }

        public RecordRefusal Refusal { get; set; }

        public static RecordWrite Written(string documentPath)
        {
            RecordWrite write = new RecordWrite();
            write.DocumentPath = documentPath;
            return write;
        }

        public static RecordWrite Refused(string name, string message, int code)
        {
            RecordRefusal refusal = new RecordRefusal();
            refusal.Name = name;
            refusal.Message = message;
            refusal.Code = code;
            RecordWrite write = new RecordWrite();
            write.Refusal = refusal;
            return write;
        }
    }
}
