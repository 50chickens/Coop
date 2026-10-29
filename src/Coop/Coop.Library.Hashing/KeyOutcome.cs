using Coop.Library.Execution;

namespace Coop.Library.Hashing
{
    public sealed class KeyOutcome
    {
        public string Key { get; set; }

        public ExecutionRefusal Refusal { get; set; }

        public static KeyOutcome Refused(string name, string message)
        {
            ExecutionRefusal refusal = new ExecutionRefusal();
            refusal.Name = name;
            refusal.Message = message;
            KeyOutcome outcome = new KeyOutcome();
            outcome.Refusal = refusal;
            return outcome;
        }
    }
}
