using System.Collections.Generic;

namespace Coop.Services.IssueWorkflow;

public sealed class ChangeScopeConfiguration
{
    public IReadOnlyList<string> OwnedPaths { get; set; }
}
