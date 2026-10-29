namespace Coop.Library.Locking
{
    public sealed class LockRefusal
    {
        public string Name { get; set; }

        public string Message { get; set; }

        public int ExitCode { get; set; }
    }
}
