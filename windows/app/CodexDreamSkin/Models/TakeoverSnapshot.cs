namespace CodexDreamSkin.Models;

public enum TakeoverState
{
    Disabled,
    Watching,
    TakingOver,
    Active,
    Faulted
}

public sealed record TakeoverSnapshot(
    TakeoverState State,
    string Summary,
    string Detail,
    DateTimeOffset UpdatedAt)
{
    public static TakeoverSnapshot Disabled { get; } = new(
        TakeoverState.Disabled,
        "普通启动托管已关闭",
        "启用后，管理器会在后台识别普通启动的官方 Codex，并自动重启为主题会话。",
        DateTimeOffset.UtcNow);
}
