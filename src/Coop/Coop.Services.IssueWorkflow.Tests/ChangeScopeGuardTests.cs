using System.Collections.Generic;
using NUnit.Framework;

namespace Coop.Services.IssueWorkflow.Tests;

[TestFixture]
public sealed class ChangeScopeGuardTests
{
    [Test]
    public void Test_That_We_Can_Fail_A_Change_That_Edits_A_Declared_File()
    {
        ChangeScopeVerdict verdict = Check(Owned("ci/gate.ps1"), File("ci/gate.ps1", ChangeKind.Modified));

        Assert.That(verdict.Passed, Is.False);
        Assert.That(verdict.OffendingPaths, Is.EqualTo(new[] { "ci/gate.ps1" }));
    }

    [Test]
    public void Test_That_We_Can_Fail_A_Change_That_Deletes_A_Declared_File()
    {
        ChangeScopeVerdict verdict = Check(Owned("ci/gate.ps1"), File("ci/gate.ps1", ChangeKind.Deleted));

        Assert.That(verdict.Passed, Is.False);
        Assert.That(verdict.OffendingPaths, Is.EqualTo(new[] { "ci/gate.ps1" }));
    }

    [Test]
    public void Test_That_We_Can_Fail_A_Change_That_Adds_A_Declared_File()
    {
        ChangeScopeVerdict verdict = Check(Owned("ci/gate.ps1"), File("ci/gate.ps1", ChangeKind.Added));

        Assert.That(verdict.Passed, Is.False);
        Assert.That(verdict.OffendingPaths, Is.EqualTo(new[] { "ci/gate.ps1" }));
    }

    [Test]
    public void Test_That_We_Can_Fail_A_Change_Beneath_A_Declared_Directory()
    {
        ChangeScopeVerdict verdict = Check(Owned("ci"), File("ci/nested/build.ps1", ChangeKind.Modified));

        Assert.That(verdict.Passed, Is.False);
        Assert.That(verdict.OffendingPaths, Is.EqualTo(new[] { "ci/nested/build.ps1" }));
    }

    [Test]
    public void Test_That_We_Can_Match_A_Declared_Directory_Written_With_A_Trailing_Slash()
    {
        ChangeScopeVerdict verdict = Check(Owned("ci/"), File("ci/build.ps1", ChangeKind.Modified));

        Assert.That(verdict.Passed, Is.False);
        Assert.That(verdict.OffendingPaths, Is.EqualTo(new[] { "ci/build.ps1" }));
    }

    [Test]
    public void Test_That_We_Can_Pass_A_File_That_Shares_A_Name_Prefix_With_A_Declared_Directory()
    {
        ChangeScopeVerdict verdict = Check(Owned("ci"), File("ci2/gate.ps1", ChangeKind.Modified));

        Assert.That(verdict.Passed, Is.True);
        Assert.That(verdict.OffendingPaths, Is.Empty);
    }

    [Test]
    public void Test_That_We_Can_Pass_A_Change_That_Touches_Nothing_Declared()
    {
        ChangeScopeVerdict verdict = Check(
            Owned("ci", ".github/workflows"),
            File("README.md", ChangeKind.Modified),
            File("src/Program.cs", ChangeKind.Added),
            File("docs/notes.md", ChangeKind.Deleted));

        Assert.That(verdict.Passed, Is.True);
        Assert.That(verdict.OffendingPaths, Is.Empty);
    }

    [Test]
    public void Test_That_We_Can_Report_Every_Offending_Path()
    {
        ChangeScopeVerdict verdict = Check(
            Owned("ci", ".github/workflows"),
            File("ci/gate.ps1", ChangeKind.Modified),
            File("README.md", ChangeKind.Modified),
            File("ci2/gate.ps1", ChangeKind.Added),
            File(".github/workflows/issue.yml", ChangeKind.Added),
            File("docs/notes.md", ChangeKind.Deleted));

        Assert.That(verdict.Passed, Is.False);
        Assert.That(verdict.OffendingPaths, Is.EqualTo(new[] { "ci/gate.ps1", ".github/workflows/issue.yml" }));
    }

    [Test]
    public void Test_That_We_Can_Treat_A_Case_Difference_As_The_Same_Path()
    {
        ChangeScopeVerdict declaredDirectory = Check(Owned("ci"), File("CI/gate.ps1", ChangeKind.Modified));
        ChangeScopeVerdict declaredFile = Check(Owned("ci/gate.ps1"), File("CI/Gate.PS1", ChangeKind.Modified));
        ChangeScopeVerdict declaredUpper = Check(Owned("CI"), File("ci/gate.ps1", ChangeKind.Modified));

        Assert.Multiple(() =>
        {
            Assert.That(declaredDirectory.Passed, Is.False);
            Assert.That(declaredDirectory.OffendingPaths, Is.EqualTo(new[] { "CI/gate.ps1" }));
            Assert.That(declaredFile.Passed, Is.False);
            Assert.That(declaredFile.OffendingPaths, Is.EqualTo(new[] { "CI/Gate.PS1" }));
            Assert.That(declaredUpper.Passed, Is.False);
            Assert.That(declaredUpper.OffendingPaths, Is.EqualTo(new[] { "ci/gate.ps1" }));
        });
    }

    [Test]
    public void Test_That_We_Can_Report_The_Changed_Path_When_The_Declaration_Differs_Only_In_Case()
    {
        ChangeScopeVerdict verdict = Check(Owned("CI/GATE.PS1"), File("ci/gate.ps1", ChangeKind.Modified));

        Assert.That(verdict.Passed, Is.False);
        Assert.That(verdict.OffendingPaths, Is.EqualTo(new[] { "ci/gate.ps1" }));
    }

    [Test]
    public void Test_That_We_Can_Match_A_Declared_Path_Written_With_A_Backslash()
    {
        ChangeScopeVerdict verdict = Check(Owned("ci\\gate.ps1"), File("ci/gate.ps1", ChangeKind.Modified));

        Assert.That(verdict.Passed, Is.False);
        Assert.That(verdict.OffendingPaths, Is.EqualTo(new[] { "ci/gate.ps1" }));
    }

    private static ChangeScopeVerdict Check(ChangeScopeConfiguration configuration, params ChangedFile[] changedFiles)
    {
        return new ChangeScopeGuard().Check(configuration, changedFiles);
    }

    private static ChangeScopeConfiguration Owned(params string[] ownedPaths)
    {
        ChangeScopeConfiguration configuration = new ChangeScopeConfiguration();
        configuration.OwnedPaths = ownedPaths;
        return configuration;
    }

    private static ChangedFile File(string path, ChangeKind changeKind)
    {
        ChangedFile changedFile = new ChangedFile();
        changedFile.Path = path;
        changedFile.ChangeKind = changeKind;
        return changedFile;
    }
}
