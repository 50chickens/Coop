namespace Coop.Services.IssueWorkflow;

public sealed class TriggerEvent
{
    public TriggerSubject Subject { get; set; }

    public bool IssueFieldPresent { get; set; }

    public string AuthorAssociation { get; set; }

    public bool FromAutomation { get; set; }
}
