using System.Collections.Generic;

namespace Coop.Services.IssueWorkflow;

public sealed class ChangeScopeVerdict
{
    public bool Passed { get; set; }

    public IReadOnlyList<string> OffendingPaths { get; set; }
}
