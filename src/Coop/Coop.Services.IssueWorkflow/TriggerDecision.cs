namespace Coop.Services.IssueWorkflow;

public sealed class TriggerDecision
{
    public bool Permitted { get; set; }

    public int RefusalCode { get; set; }

    public string Reason { get; set; }

    public static TriggerDecision Permit()
    {
        TriggerDecision decision = new TriggerDecision();
        decision.Permitted = true;
        decision.RefusalCode = 0;
        decision.Reason = string.Empty;
        return decision;
    }

    public static TriggerDecision Refuse(string reason)
    {
        TriggerDecision decision = new TriggerDecision();
        decision.Permitted = false;
        decision.RefusalCode = RefusalCodes.TriggerRefused;
        decision.Reason = reason;
        return decision;
    }
}
