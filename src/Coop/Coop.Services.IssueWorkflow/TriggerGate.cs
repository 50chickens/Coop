using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Services.IssueWorkflow;

public sealed class TriggerGate
{
    public TriggerDecision Decide(TriggerEvent triggerEvent, TriggerGateConfiguration configuration)
    {
        if (triggerEvent.Subject == TriggerSubject.PullRequest)
        {
            return TriggerDecision.Refuse("non-issue subject: the event subject is a pull request");
        }

        if (triggerEvent.Subject != TriggerSubject.Issue)
        {
            return TriggerDecision.Refuse("non-issue subject: the event subject is not an issue");
        }

        if (triggerEvent.FromAutomation && !configuration.AutomationMayTrigger)
        {
            return TriggerDecision.Refuse("automation origin: events authored by an automation are refused unless the deployment permits them");
        }

        string association = triggerEvent.AuthorAssociation;
        if (string.IsNullOrWhiteSpace(association))
        {
            return TriggerDecision.Refuse("association absent: the event carries no author association");
        }

        IReadOnlyList<string> permittedAssociations = configuration.PermittedAssociations;
        bool permitted = permittedAssociations
            .Any(entry => string.Equals(entry.Trim(), association.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!permitted)
        {
            return TriggerDecision.Refuse($"author not permitted: association '{association.Trim()}' is not in the configured list");
        }

        return TriggerDecision.Permit();
    }
}
