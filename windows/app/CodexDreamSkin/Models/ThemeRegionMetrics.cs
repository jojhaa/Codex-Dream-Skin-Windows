namespace CodexDreamSkin.Models;

public sealed record ThemeRegionSize(double Width, double Height)
{
    public double Ratio => Height > 0 ? Width / Height : 0;
    public bool IsValid => Width > 0 && Height > 0;
}

public sealed record ThemeRegionMetrics(
    ThemeRegionSize Viewport,
    ThemeRegionSize Background,
    ThemeRegionSize Sidebar,
    ThemeRegionSize? Composer,
    ThemeRegionSize? Home,
    ThemeRegionSize? HomeComposer,
    ThemeRegionSize? Polaroid)
{
    public ThemeRegionSize? Get(ThemeImageSlot slot) => slot switch
    {
        ThemeImageSlot.Sidebar => Sidebar,
        ThemeImageSlot.Composer => Composer,
        ThemeImageSlot.Home => Home,
        ThemeImageSlot.HomeComposer => HomeComposer,
        ThemeImageSlot.Polaroid => Polaroid,
        _ => Background
    };
}
