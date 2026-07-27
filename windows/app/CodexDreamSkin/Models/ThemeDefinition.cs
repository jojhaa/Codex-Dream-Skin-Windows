namespace CodexDreamSkin.Models;

public sealed record ThemeDefinition
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string DirectoryPath { get; init; }
    public required string ImageFileName { get; set; }
    public string SidebarImageFileName { get; set; } = string.Empty;
    public string ComposerImageFileName { get; set; } = string.Empty;
    public string HomeImageFileName { get; set; } = string.Empty;
    public string HomeComposerImageFileName { get; set; } = string.Empty;
    public string PolaroidImageFileName { get; set; } = string.Empty;
    public string Appearance { get; set; } = "auto";
    public double FocusX { get; set; } = 0.5;
    public double FocusY { get; set; } = 0.5;
    public ThemeComposition BackgroundComposition { get; set; } = ThemeComposition.Recommended(ThemeImageSlot.Background);
    public ThemeComposition SidebarComposition { get; set; } = ThemeComposition.Recommended(ThemeImageSlot.Sidebar);
    public ThemeComposition ComposerComposition { get; set; } = ThemeComposition.Recommended(ThemeImageSlot.Composer);
    public ThemeComposition HomeComposition { get; set; } = ThemeComposition.Recommended(ThemeImageSlot.Home);
    public ThemeComposition HomeComposerComposition { get; set; } = ThemeComposition.Recommended(ThemeImageSlot.HomeComposer);
    public ThemeComposition PolaroidComposition { get; set; } = ThemeComposition.Recommended(ThemeImageSlot.Polaroid);
    public string SafeArea { get; set; } = "left";
    public string TaskMode { get; set; } = "ambient";
    public string DecorationProfile { get; set; } = "minimal";
    public string SidebarBackgroundMode { get; set; } = "independent";
    public bool MatchWorkspaceTransparency { get; set; }
    public string Accent { get; set; } = "#1557b0";
    public double LightPageOpacity { get; set; } = 0.56;
    public double LightSidebarOpacity { get; set; } = 0.58;
    public double LightComposerOpacity { get; set; } = 0.48;
    public double LightCardOpacity { get; set; } = 0.18;
    public double DarkPageOpacity { get; set; } = 0.68;
    public double DarkSidebarOpacity { get; set; } = 0.74;
    public double DarkComposerOpacity { get; set; } = 0.62;
    public double DarkCardOpacity { get; set; } = 0.42;
    public ThemeComponentMaterials ComponentMaterials { get; set; } = ThemeComponentMaterials.Default;
    public bool IsBundled { get; init; }
    public bool IsActive { get; set; }
    public string ImagePath => Path.Combine(DirectoryPath, ImageFileName);
    public string SidebarImagePath => Path.Combine(DirectoryPath, EffectiveSidebarImageFileName);
    public string ComposerImagePath => Path.Combine(DirectoryPath, EffectiveComposerImageFileName);
    public string HomeImagePath => Path.Combine(DirectoryPath, EffectiveHomeImageFileName);
    public string HomeComposerImagePath => Path.Combine(DirectoryPath, EffectiveHomeComposerImageFileName);
    public string PolaroidImagePath => Path.Combine(DirectoryPath, EffectivePolaroidImageFileName);
    public string EffectiveSidebarImageFileName => string.IsNullOrWhiteSpace(SidebarImageFileName) ? ImageFileName : SidebarImageFileName;
    public string EffectiveComposerImageFileName => string.IsNullOrWhiteSpace(ComposerImageFileName) ? ImageFileName : ComposerImageFileName;
    public string EffectiveHomeImageFileName => string.IsNullOrWhiteSpace(HomeImageFileName) ? ImageFileName : HomeImageFileName;
    public string EffectiveHomeComposerImageFileName => string.IsNullOrWhiteSpace(HomeComposerImageFileName) ? EffectiveComposerImageFileName : HomeComposerImageFileName;
    public string EffectivePolaroidImageFileName => string.IsNullOrWhiteSpace(PolaroidImageFileName) ? EffectiveHomeImageFileName : PolaroidImageFileName;
    public IEnumerable<string> ImageFileNames => new[]
    {
        ImageFileName,
        EffectiveSidebarImageFileName,
        EffectiveComposerImageFileName,
        EffectiveHomeImageFileName,
        EffectiveHomeComposerImageFileName,
        EffectivePolaroidImageFileName
    }.Distinct(StringComparer.OrdinalIgnoreCase);
    public ThemeComposition GetComposition(ThemeImageSlot slot) => slot switch
    {
        ThemeImageSlot.Sidebar => SidebarComposition,
        ThemeImageSlot.Composer => ComposerComposition,
        ThemeImageSlot.Home => HomeComposition,
        ThemeImageSlot.HomeComposer => HomeComposerComposition,
        ThemeImageSlot.Polaroid => PolaroidComposition,
        _ => BackgroundComposition
    };
    public override string ToString() => Name;
}

public sealed record ThemeComposition(
    double FocusX,
    double FocusY,
    double Zoom,
    string Fit,
    double OffsetX,
    double OffsetY)
{
    public static ThemeComposition Recommended(ThemeImageSlot slot, double focusX = 0.64, double focusY = 0.44) => slot switch
    {
        ThemeImageSlot.Sidebar => new(focusX, focusY, 1, "auto", 0, 0),
        ThemeImageSlot.Composer => new(focusX, focusY, 1, "auto", 0, 0),
        ThemeImageSlot.Home => new(focusX, focusY, 1, "auto", 0, 0),
        ThemeImageSlot.HomeComposer => new(focusX, focusY, 1, "auto", 0, 0),
        ThemeImageSlot.Polaroid => new(focusX, focusY, 1, "auto", 0, 0),
        _ => new(focusX, focusY, 1, "auto", 0, 0)
    };
}

public enum ThemeImageSlot
{
    Background,
    Sidebar,
    Composer,
    Home,
    HomeComposer,
    Polaroid
}

public sealed record ThemeComponentMaterial(
    string LightColor,
    double LightOpacity,
    string DarkColor,
    double DarkOpacity);

public sealed record ThemeComponentMaterials(
    ThemeComponentMaterial Messages,
    ThemeComponentMaterial Summaries,
    ThemeComponentMaterial Previews,
    ThemeComponentMaterial Menus,
    ThemeComponentMaterial Workspace,
    ThemeComponentMaterial Code,
    ThemeComponentMaterial Suggestions)
{
    public static ThemeComponentMaterials Default { get; } = new(
        new("#FDFFFF", 0.18, "#051423", 0.42),
        new("#FDFFFF", 0.18, "#051423", 0.42),
        new("#E0F1F7", 0.88, "#061728", 0.88),
        new("#F9FDFD", 0.26, "#051423", 0.42),
        new("#FDFFFF", 0.18, "#051423", 0.44),
        new("#FAFDFC", 0.12, "#071B2E", 0.24),
        new("#FFFFFF", 0.36, "#071A2D", 0.46));

    public ThemeComponentMaterial Get(ThemeComponentSlot slot) => slot switch
    {
        ThemeComponentSlot.Summaries => Summaries,
        ThemeComponentSlot.Previews => Previews,
        ThemeComponentSlot.Menus => Menus,
        ThemeComponentSlot.Workspace => Workspace,
        ThemeComponentSlot.Code => Code,
        ThemeComponentSlot.Suggestions => Suggestions,
        _ => Messages
    };

    public ThemeComponentMaterials Set(ThemeComponentSlot slot, ThemeComponentMaterial value) => slot switch
    {
        ThemeComponentSlot.Summaries => this with { Summaries = value },
        ThemeComponentSlot.Previews => this with { Previews = value },
        ThemeComponentSlot.Menus => this with { Menus = value },
        ThemeComponentSlot.Workspace => this with { Workspace = value },
        ThemeComponentSlot.Code => this with { Code = value },
        ThemeComponentSlot.Suggestions => this with { Suggestions = value },
        _ => this with { Messages = value }
    };

    public IEnumerable<ThemeComponentMaterial> All =>
        [Messages, Summaries, Previews, Menus, Workspace, Code, Suggestions];
}

public enum ThemeComponentSlot
{
    Messages,
    Summaries,
    Previews,
    Menus,
    Workspace,
    Code,
    Suggestions
}
