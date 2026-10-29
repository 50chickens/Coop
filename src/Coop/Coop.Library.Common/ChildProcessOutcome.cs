namespace Coop.Library.Common
{
    public sealed class ChildProcessOutcome
    {
        public bool Started { get; set; }

        public int? ExitStatus { get; set; }

        public string StandardOutput { get; set; }

        public string StandardError { get; set; }

        public long ElapsedMilliseconds { get; set; }

        public bool TimedOut { get; set; }
    }
}
