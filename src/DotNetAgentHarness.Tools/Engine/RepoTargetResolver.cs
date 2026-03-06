using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DotNetAgentHarness.Tools.Engine;

public static class RepoTargetResolver
{
    public static RepoTargetSelection Resolve(string repoRoot, RepositoryProfile profile, string? requestedTarget)
    {
        if (!string.IsNullOrWhiteSpace(requestedTarget))
        {
            var fullPath = Path.IsPathRooted(requestedTarget)
                ? requestedTarget
                : Path.GetFullPath(Path.Combine(repoRoot, requestedTarget));

            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                return new RepoTargetSelection
                {
                    DisplayPath = requestedTarget,
                    IsExplicit = true,
                    IsAmbiguous = false,
                    Resolution = "Requested target was not found."
                };
            }

            return new RepoTargetSelection
            {
                TargetPath = fullPath,
                DisplayPath = Path.GetRelativePath(repoRoot, fullPath),
                IsExplicit = true,
                Resolution = "Explicit target provided."
            };
        }

        if (profile.Solutions.Count == 1)
        {
            var target = Path.Combine(repoRoot, profile.Solutions[0]);
            return new RepoTargetSelection
            {
                TargetPath = target,
                DisplayPath = profile.Solutions[0],
                Resolution = "Single solution detected."
            };
        }

        var nonTestProjects = profile.Projects.Where(project => !project.IsTestProject).ToList();
        if (profile.Solutions.Count == 0 && nonTestProjects.Count == 1)
        {
            return new RepoTargetSelection
            {
                TargetPath = Path.Combine(repoRoot, nonTestProjects[0].RelativePath),
                DisplayPath = nonTestProjects[0].RelativePath,
                Resolution = "Single non-test project detected."
            };
        }

        if (profile.Solutions.Count == 0 && profile.Projects.Count == 1)
        {
            return new RepoTargetSelection
            {
                TargetPath = Path.Combine(repoRoot, profile.Projects[0].RelativePath),
                DisplayPath = profile.Projects[0].RelativePath,
                Resolution = "Single project detected."
            };
        }

        var candidates = profile.Solutions.Count > 0
            ? profile.Solutions
            : (nonTestProjects.Count > 0
                ? nonTestProjects.Select(project => project.RelativePath).ToList()
                : profile.Projects.Select(project => project.RelativePath).ToList());

        return new RepoTargetSelection
        {
            IsAmbiguous = candidates.Count > 1,
            Candidates = candidates,
            Resolution = candidates.Count == 0
                ? "No solution or project target could be resolved."
                : "Multiple repository targets are available."
        };
    }
}
