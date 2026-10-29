namespace Coop.Services.IssueWorkflow;

public sealed class ChangedFile
{
    public string Path { get; set; }

    public ChangeKind ChangeKind { get; set; }
}
