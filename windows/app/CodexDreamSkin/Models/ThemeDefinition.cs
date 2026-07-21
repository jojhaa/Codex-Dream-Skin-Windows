namespace CodexDreamSkin.Models;

public sealed record ThemeDefinition
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string DirectoryPath { get; init; }
    public required string ImageFileName { get; init; }
    public string Appearance { get; set; } = "auto";
    public double FocusX { get; set; } = 0.5;
    public double FocusY { get; set; } = 0.5;
    public string SafeArea { get; set; } = "left";
    public string TaskMode { get; set; } = "ambient";
    public string Accent { get; set; } = "#1557b0";
    public bool IsBundled { get; init; }
    public bool IsActive { get; set; }
    public string ImagePath => Path.Combine(DirectoryPath, ImageFileName);
    public override string ToString() => Name;
}
