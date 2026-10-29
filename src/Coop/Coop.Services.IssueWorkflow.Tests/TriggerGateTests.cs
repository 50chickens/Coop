using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Coop.Services.IssueWorkflow.Tests;

[TestFixture]
public sealed class TriggerGateTests
{
    private static readonly string[] TrustedAssociations = { "OWNER", "MEMBER", "COLLABORATOR" };

    [Test]
    public void Test_That_We_Can_Allow_A_Permitted_Author()
    {
        TriggerDecision decision = new TriggerGate().Decide(IssueEvent("MEMBER", false), TrustedConfiguration());

        Assert.That(decision.Permitted, Is.True);
        Assert.That(decision.RefusalCode, Is.EqualTo(0));
        Assert.That(decision.Reason, Is.Empty);
    }

    [Test]
    public void Test_That_We_Can_Refuse_An_Unpermitted_Author()
    {
        TriggerDecision decision = new TriggerGate().Decide(IssueEvent("CONTRIBUTOR", false), TrustedConfiguration());

        Assert.That(decision.Permitted, Is.False);
        Assert.That(decision.RefusalCode, Is.EqualTo(RefusalCodes.TriggerRefused));
        Assert.That(decision.Reason, Does.Contain("author not permitted"));
        Assert.That(decision.Reason, Does.Contain("CONTRIBUTOR"));
    }

    [Test]
    public void Test_That_We_Can_Refuse_A_Pull_Request_Subject_That_Carries_An_Issue_Field()
    {
        TriggerEvent triggerEvent = new TriggerEvent();
        triggerEvent.Subject = TriggerSubject.PullRequest;
        triggerEvent.IssueFieldPresent = true;
        triggerEvent.AuthorAssociation = "OWNER";
        triggerEvent.FromAutomation = false;

        TriggerDecision decision = new TriggerGate().Decide(triggerEvent, TrustedConfiguration());

        Assert.That(decision.Permitted, Is.False);
        Assert.That(decision.RefusalCode, Is.EqualTo(RefusalCodes.TriggerRefused));
        Assert.That(decision.Reason, Does.Contain("pull request"));
    }

    [Test]
    public void Test_That_We_Can_Refuse_An_Automation_By_Default()
    {
        TriggerDecision decision = new TriggerGate().Decide(IssueEvent("OWNER", true), TrustedConfiguration());

        Assert.That(decision.Permitted, Is.False);
        Assert.That(decision.RefusalCode, Is.EqualTo(RefusalCodes.TriggerRefused));
        Assert.That(decision.Reason, Does.Contain("automation"));
    }

    [Test]
    public void Test_That_We_Can_Permit_An_Automation_When_Configured()
    {
        TriggerGateConfiguration configuration = TrustedConfiguration();
        configuration.AutomationMayTrigger = true;

        TriggerDecision decision = new TriggerGate().Decide(IssueEvent("OWNER", true), configuration);

        Assert.That(decision.Permitted, Is.True);
        Assert.That(decision.RefusalCode, Is.EqualTo(0));
    }

    [Test]
    public void Test_That_We_Can_Refuse_An_Absent_Association()
    {
        TriggerGate gate = new TriggerGate();
        TriggerDecision fromNull = gate.Decide(IssueEvent(null, false), TrustedConfiguration());
        TriggerDecision fromEmpty = gate.Decide(IssueEvent(string.Empty, false), TrustedConfiguration());
        TriggerDecision fromWhitespace = gate.Decide(IssueEvent("   ", false), TrustedConfiguration());

        Assert.Multiple(() =>
        {
            Assert.That(fromNull.Permitted, Is.False);
            Assert.That(fromNull.RefusalCode, Is.EqualTo(RefusalCodes.TriggerRefused));
            Assert.That(fromNull.Reason, Does.Contain("association absent"));
            Assert.That(fromEmpty.Permitted, Is.False);
            Assert.That(fromEmpty.Reason, Does.Contain("association absent"));
            Assert.That(fromWhitespace.Permitted, Is.False);
            Assert.That(fromWhitespace.Reason, Does.Contain("association absent"));
        });
    }

    [Test]
    public void Test_That_We_Can_Distinguish_Each_Refusal_Reason()
    {
        TriggerGate gate = new TriggerGate();
        TriggerGateConfiguration configuration = TrustedConfiguration();

        TriggerEvent pullRequest = new TriggerEvent();
        pullRequest.Subject = TriggerSubject.PullRequest;
        pullRequest.AuthorAssociation = "OWNER";

        TriggerEvent unknownSubject = new TriggerEvent();
        unknownSubject.Subject = TriggerSubject.Unknown;
        unknownSubject.AuthorAssociation = "OWNER";

        string[] reasons =
        {
            gate.Decide(IssueEvent("CONTRIBUTOR", false), configuration).Reason,
            gate.Decide(pullRequest, configuration).Reason,
            gate.Decide(unknownSubject, configuration).Reason,
            gate.Decide(IssueEvent("OWNER", true), configuration).Reason,
            gate.Decide(IssueEvent(null, false), configuration).Reason
        };

        Assert.That(reasons.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(reasons.Length));
    }

    [Test]
    public void Test_That_We_Can_Read_The_Permitted_Associations_Per_Call()
    {
        TriggerGate gate = new TriggerGate();
        TriggerGateConfiguration configuration = TrustedConfiguration();
        TriggerDecision beforeChange = gate.Decide(IssueEvent("MEMBER", false), configuration);

        configuration.PermittedAssociations = new[] { "OWNER" };
        TriggerDecision afterChange = gate.Decide(IssueEvent("MEMBER", false), configuration);

        Assert.Multiple(() =>
        {
            Assert.That(beforeChange.Permitted, Is.True);
            Assert.That(afterChange.Permitted, Is.False);
            Assert.That(afterChange.Reason, Does.Contain("author not permitted"));
        });
    }

    [Test]
    public void Test_That_We_Can_Refuse_Without_Writing_Anything()
    {
        string[] environmentBefore = ReadEnvironment();
        string[] filesBefore = ReadWorkingFiles();

        TriggerGate gate = new TriggerGate();
        TriggerDecision permitted = gate.Decide(IssueEvent("OWNER", false), TrustedConfiguration());
        TriggerDecision refused = gate.Decide(IssueEvent("CONTRIBUTOR", false), TrustedConfiguration());

        Assert.Multiple(() =>
        {
            Assert.That(permitted.Permitted, Is.True);
            Assert.That(refused.Permitted, Is.False);
            Assert.That(ReadEnvironment(), Is.EqualTo(environmentBefore));
            Assert.That(ReadWorkingFiles(), Is.EqualTo(filesBefore));
        });
    }

    private static TriggerEvent IssueEvent(string association, bool fromAutomation)
    {
        TriggerEvent triggerEvent = new TriggerEvent();
        triggerEvent.Subject = TriggerSubject.Issue;
        triggerEvent.IssueFieldPresent = true;
        triggerEvent.AuthorAssociation = association;
        triggerEvent.FromAutomation = fromAutomation;
        return triggerEvent;
    }

    private static TriggerGateConfiguration TrustedConfiguration()
    {
        TriggerGateConfiguration configuration = new TriggerGateConfiguration();
        configuration.PermittedAssociations = TrustedAssociations;
        configuration.AutomationMayTrigger = false;
        return configuration;
    }

    private static string[] ReadEnvironment()
    {
        IDictionary variables = Environment.GetEnvironmentVariables();
        return variables.Keys
            .Cast<object>()
            .Select(key => $"{key}={variables[key]}")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ReadWorkingFiles()
    {
        return Directory.GetFiles(AppContext.BaseDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }
}
