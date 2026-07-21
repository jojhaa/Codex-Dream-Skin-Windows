namespace CodexDreamSkin.Models;

public enum EngineState
{
    Idle,
    CodexNotInstalled,
    RestartRequired,
    Connecting,
    Active,
    Faulted
}

public sealed record EngineSnapshot(
    EngineState State,
    string Summary,
    string Detail,
    string? PackageVersion = null,
    int? ListenerProcessId = null,
    int TargetCount = 0)
{
    public static EngineSnapshot Idle { get; } = new(
        EngineState.Idle,
        "尚未检查",
        "检查已安装的 Codex 与本机 CDP 会话。");
}
