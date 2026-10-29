namespace Coop.Library.Locking
{
    public sealed class LockAcquisition
    {
        public LockHandle Handle { get; set; }

        public LockRefusal Refusal { get; set; }
    }
}
