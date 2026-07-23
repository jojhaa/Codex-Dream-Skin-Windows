using System.Diagnostics;
using Windows.ApplicationModel;
using CodexDreamSkin.Models;
using Windows.Management.Deployment;

namespace CodexDreamSkin.Services;

public sealed class CodexPackageLocator
{
    public CodexInstallation? FindCurrent()
    {
        var package = new PackageManager()
            .FindPackagesForUser(string.Empty)
            .Where(candidate => string.Equals(candidate.Id.Name, "OpenAI.Codex", StringComparison.Ordinal))
            .Where(candidate => candidate.SignatureKind == PackageSignatureKind.Store)
            .OrderByDescending(candidate => candidate.Id.Version.Major)
            .ThenByDescending(candidate => candidate.Id.Version.Minor)
            .ThenByDescending(candidate => candidate.Id.Version.Build)
            .ThenByDescending(candidate => candidate.Id.Version.Revision)
            .FirstOrDefault();

        if (package is null)
        {
            return null;
        }

        var root = Path.GetFullPath(package.InstalledLocation.Path);
        var executable = Path.Combine(root, "app", "ChatGPT.exe");
        if (!File.Exists(executable))
        {
            return null;
        }

        var version = package.Id.Version;
        return new CodexInstallation(
            package.Id.FullName,
            package.Id.FamilyName,
            $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}",
            root,
            executable);
    }

    public static IReadOnlyList<Process> FindRunningProcesses(CodexInstallation installation)
    {
        var trustedExecutable = Path.GetFullPath(installation.ExecutablePath);
        var matches = new List<Process>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var identity = ProcessPathResolver.TryGetIdentity(process.Id);
                var pathMatches = identity?.ExecutablePath is not null &&
                    string.Equals(Path.GetFullPath(identity.ExecutablePath), trustedExecutable, StringComparison.OrdinalIgnoreCase);
                var currentPackageMatches = string.Equals(
                    identity?.PackageFullName,
                    installation.PackageFullName,
                    StringComparison.OrdinalIgnoreCase);
                if (pathMatches || currentPackageMatches)
                {
                    matches.Add(process);
                }
                else
                {
                    process.Dispose();
                }
            }
            catch
            {
                process.Dispose();
            }
        }

        return matches;
    }

}
