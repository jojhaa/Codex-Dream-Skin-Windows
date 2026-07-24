using System.Text.Json;
using Windows.Storage;

namespace CodexDreamSkin.Services;

public sealed class ManagerSettingsService
{
    private const string AutoTakeoverKey = "AutoTakeoverStockCodex";
    private const string AppearanceKey = "ManagerAppearance";

    private readonly object _gate = new();
    private readonly ApplicationDataContainer? _settings;
    private readonly string _fallbackPath = Path.Combine(AppStoragePaths.LocalRoot, "manager-settings.json");
    private bool _fallbackAutoTakeover;
    private string _fallbackAppearance = "system";

    public ManagerSettingsService()
    {
        try
        {
            _settings = ApplicationData.Current.LocalSettings;
        }
        catch
        {
            LoadFallback();
        }
    }

    public bool AutoTakeoverEnabled
    {
        get
        {
            lock (_gate)
            {
                return _settings?.Values[AutoTakeoverKey] is bool enabled
                    ? enabled
                    : _fallbackAutoTakeover;
            }
        }
        set
        {
            lock (_gate)
            {
                if (_settings is not null)
                {
                    _settings.Values[AutoTakeoverKey] = value;
                    return;
                }

                _fallbackAutoTakeover = value;
                SaveFallback();
            }
        }
    }

    public string Appearance
    {
        get
        {
            lock (_gate)
            {
                return _settings?.Values[AppearanceKey] as string ?? _fallbackAppearance;
            }
        }
        set
        {
            lock (_gate)
            {
                if (_settings is not null)
                {
                    _settings.Values[AppearanceKey] = value;
                    return;
                }

                _fallbackAppearance = value;
                SaveFallback();
            }
        }
    }

    private void LoadFallback()
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_fallbackPath));
            var root = document.RootElement;
            _fallbackAutoTakeover = root.TryGetProperty("autoTakeoverEnabled", out var autoTakeover)
                && autoTakeover.ValueKind is JsonValueKind.True;
            _fallbackAppearance = root.TryGetProperty("appearance", out var appearance)
                && appearance.ValueKind is JsonValueKind.String
                ? appearance.GetString() ?? "system"
                : "system";
        }
        catch (FileNotFoundException)
        {
        }
        catch (JsonException)
        {
        }
    }

    private void SaveFallback()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_fallbackPath)!);
        var temporaryPath = _fallbackPath + ".tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                autoTakeoverEnabled = _fallbackAutoTakeover,
                appearance = _fallbackAppearance,
            }));
        File.Move(temporaryPath, _fallbackPath, true);
    }
}
