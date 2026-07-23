using Windows.Storage;

namespace CodexDreamSkin.Services;

public sealed class ManagerSettingsService
{
    private const string AutoTakeoverKey = "AutoTakeoverStockCodex";
    private const string AppearanceKey = "ManagerAppearance";

    private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

    public bool AutoTakeoverEnabled
    {
        get => _settings.Values[AutoTakeoverKey] is bool enabled && enabled;
        set => _settings.Values[AutoTakeoverKey] = value;
    }

    public string Appearance
    {
        get => _settings.Values[AppearanceKey] as string ?? "system";
        set => _settings.Values[AppearanceKey] = value;
    }
}
