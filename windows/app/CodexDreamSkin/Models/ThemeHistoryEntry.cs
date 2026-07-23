namespace CodexDreamSkin.Models;

public sealed record ThemeHistoryEntry(string FilePath, DateTimeOffset SavedAt, string ThemeName)
{
    public string DisplayName => $"{SavedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} · {ThemeName}";
    public override string ToString() => DisplayName;
}
