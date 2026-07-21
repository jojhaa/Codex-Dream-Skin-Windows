namespace CodexDreamSkin.Models;

public sealed record CodexInstallation(
    string PackageFullName,
    string PackageFamilyName,
    string Version,
    string PackageRoot,
    string ExecutablePath);
