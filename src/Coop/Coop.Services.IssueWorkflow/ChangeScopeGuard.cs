using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Services.IssueWorkflow;

public sealed class ChangeScopeGuard
{
    public ChangeScopeVerdict Check(ChangeScopeConfiguration configuration, IReadOnlyList<ChangedFile> changedFiles)
    {
        IReadOnlyList<string> ownedPaths = configuration.OwnedPaths
            .Select(ownedPath => Normalize(ownedPath))
            .Where(ownedPath => ownedPath.Length > 0)
            .ToList();

        IReadOnlyList<string> offendingPaths = changedFiles
            .Where(changedFile => ownedPaths.Any(ownedPath => IsWithin(Normalize(changedFile.Path), ownedPath)))
            .Select(changedFile => changedFile.Path)
            .ToList();

        ChangeScopeVerdict verdict = new ChangeScopeVerdict();
        verdict.Passed = offendingPaths.Count == 0;
        verdict.OffendingPaths = offendingPaths;
        return verdict;
    }

    private static bool IsWithin(string candidate, string ownedPath)
    {
        return string.Equals(candidate, ownedPath, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(ownedPath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        string normalized = path.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(2);
        }

        return normalized.Trim('/');
    }
}
