using System.Collections.Generic;

namespace Coop.Services.IssueWorkflow;

public sealed class TriggerGateConfiguration
{
    public IReadOnlyList<string> PermittedAssociations { get; set; }

    public bool AutomationMayTrigger { get; set; }
}
