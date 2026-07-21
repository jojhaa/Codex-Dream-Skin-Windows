using System.Text.Json;
using CodexDreamSkin.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace CodexDreamSkin.Services;

public sealed class ThemeCatalogService
{
    private const int MaximumImageBytes = 24 * 1024 * 1024;
    private const ulong MaximumPixels = 50_000_000;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };
    private static readonly HashSet<string> AllowedAppearances = new(StringComparer.Ordinal) { "auto", "light", "dark" };
    private static readonly HashSet<string> AllowedSafeAreas = new(StringComparer.Ordinal) { "auto", "left", "right", "center", "none" };
    private static readonly HashSet<string> AllowedTaskModes = new(StringComparer.Ordinal) { "auto", "ambient", "banner", "off" };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string BundledThemeDirectory => Path.Combine(AppContext.BaseDirectory, "Assets", "Theme");
    public string UserThemesDirectory => Path.Combine(ApplicationData.Current.LocalFolder.Path, "Themes");
    private string StatePath => Path.Combine(ApplicationData.Current.LocalFolder.Path, "theme-state.json");

    public async Task<IReadOnlyList<ThemeDefinition>> GetThemesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(UserThemesDirectory);
            var activeId = await ReadActiveIdCoreAsync(cancellationToken);
            var themes = new List<ThemeDefinition>
            {
                await ReadThemeCoreAsync(BundledThemeDirectory, true, cancellationToken)
            };

            foreach (var directory in Directory.EnumerateDirectories(UserThemesDirectory))
            {
                try { themes.Add(await ReadThemeCoreAsync(directory, false, cancellationToken)); }
                catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
            }

            if (!themes.Any(theme => theme.Id == activeId)) activeId = themes[0].Id;
            foreach (var theme in themes) theme.IsActive = theme.Id == activeId;
            return themes.OrderByDescending(theme => theme.IsActive).ThenByDescending(theme => theme.IsBundled).ThenBy(theme => theme.Name).ToArray();
        }
        finally { _gate.Release(); }
    }

    public async Task<string> GetActiveThemeDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var themes = await GetThemesAsync(cancellationToken);
        return themes.First(theme => theme.IsActive).DirectoryPath;
    }

    public async Task<ThemeDefinition> ImportAsync(StorageFile source, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(source.Name).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension)) throw new InvalidDataException("仅支持 PNG、JPEG 和 WebP 图片。");
        var properties = await source.GetBasicPropertiesAsync();
        if (properties.Size is < 1 or > MaximumImageBytes) throw new InvalidDataException("图片必须小于 24 MB。 ");

        using (var stream = await source.OpenReadAsync())
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var width = decoder.PixelWidth;
            var height = decoder.PixelHeight;
            if (width is 0 or > 16384 || height is 0 or > 16384 || (ulong)width * height > MaximumPixels)
                throw new InvalidDataException("图片尺寸必须小于 16384×16384 且不超过 5000 万像素。");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(UserThemesDirectory);
            var id = $"custom-{Guid.NewGuid():N}";
            var themesFolder = await StorageFolder.GetFolderFromPathAsync(UserThemesDirectory);
            var folder = await themesFolder.CreateFolderAsync(id, CreationCollisionOption.FailIfExists);
            var imageName = $"art{extension}";
            await source.CopyAsync(folder, imageName, NameCollisionOption.ReplaceExisting);
            var theme = new ThemeDefinition
            {
                Id = id,
                Name = Path.GetFileNameWithoutExtension(source.Name),
                DirectoryPath = folder.Path,
                ImageFileName = imageName,
                FocusX = 0.5,
                FocusY = 0.45,
                SafeArea = "left",
                TaskMode = "ambient"
            };
            await WriteThemeCoreAsync(theme, cancellationToken);
            await WriteActiveIdCoreAsync(theme.Id, cancellationToken);
            theme.IsActive = true;
            return theme;
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(ThemeDefinition theme, CancellationToken cancellationToken = default)
    {
        if (theme.IsBundled) throw new InvalidOperationException("内置主题是只读的。可先导入新图片创建用户主题。");
        Validate(theme);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureOwnedThemeDirectory(theme.DirectoryPath);
            await WriteThemeCoreAsync(theme, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task SelectAsync(ThemeDefinition theme, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!theme.IsBundled) EnsureOwnedThemeDirectory(theme.DirectoryPath);
            await WriteActiveIdCoreAsync(theme.Id, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteAsync(ThemeDefinition theme, CancellationToken cancellationToken = default)
    {
        if (theme.IsBundled) throw new InvalidOperationException("不能删除内置主题。");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureOwnedThemeDirectory(theme.DirectoryPath);
            Directory.Delete(theme.DirectoryPath, true);
            var activeId = await ReadActiveIdCoreAsync(cancellationToken);
            if (activeId == theme.Id) await WriteActiveIdCoreAsync("preset-kanna-hashimoto", cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private async Task<ThemeDefinition> ReadThemeCoreAsync(string directory, bool bundled, CancellationToken cancellationToken)
    {
        if (!bundled) EnsureOwnedThemeDirectory(directory);
        var themePath = Path.Combine(directory, "theme.json");
        await using var stream = File.OpenRead(themePath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var art = root.GetProperty("art");
        var palette = root.TryGetProperty("palette", out var paletteNode) ? paletteNode : default;
        var theme = new ThemeDefinition
        {
            Id = root.GetProperty("id").GetString() ?? throw new InvalidDataException("主题 ID 缺失。"),
            Name = root.GetProperty("name").GetString() ?? throw new InvalidDataException("主题名称缺失。"),
            DirectoryPath = Path.GetFullPath(directory),
            ImageFileName = root.GetProperty("image").GetString() ?? throw new InvalidDataException("主题图片缺失。"),
            Appearance = root.TryGetProperty("appearance", out var appearance) ? appearance.GetString() ?? "auto" : "auto",
            FocusX = art.TryGetProperty("focusX", out var focusX) && focusX.ValueKind == JsonValueKind.Number ? focusX.GetDouble() : 0.5,
            FocusY = art.TryGetProperty("focusY", out var focusY) && focusY.ValueKind == JsonValueKind.Number ? focusY.GetDouble() : 0.5,
            SafeArea = art.TryGetProperty("safeArea", out var safeArea) ? safeArea.GetString() ?? "auto" : "auto",
            TaskMode = art.TryGetProperty("taskMode", out var taskMode) ? taskMode.GetString() ?? "auto" : "auto",
            Accent = palette.ValueKind == JsonValueKind.Object && palette.TryGetProperty("accent", out var accent) ? accent.GetString() ?? "#1557b0" : "#1557b0",
            IsBundled = bundled
        };
        Validate(theme);
        var imagePath = Path.GetFullPath(theme.ImagePath);
        var trustedRoot = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!imagePath.StartsWith(trustedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(imagePath))
            throw new InvalidDataException("主题图片路径无效。");
        return theme;
    }

    private static void Validate(ThemeDefinition theme)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(theme.Id, "^(?:preset-kanna-hashimoto|custom-[0-9a-f]{32})$")) throw new InvalidDataException("主题 ID 无效。");
        if (string.IsNullOrWhiteSpace(theme.Name) || theme.Name.Length > 120 || theme.Name.Any(char.IsControl)) throw new InvalidDataException("主题名称必须为 1 到 120 个字符。");
        if (theme.FocusX is < 0 or > 1 || theme.FocusY is < 0 or > 1) throw new InvalidDataException("图片焦点必须位于 0 到 1 之间。");
        if (!AllowedAppearances.Contains(theme.Appearance) || !AllowedSafeAreas.Contains(theme.SafeArea) || !AllowedTaskModes.Contains(theme.TaskMode)) throw new InvalidDataException("主题选项无效。");
        if (!System.Text.RegularExpressions.Regex.IsMatch(theme.Accent, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")) throw new InvalidDataException("强调色必须为 #RRGGBB 或 #RRGGBBAA。");
        if (Path.IsPathRooted(theme.ImageFileName) || theme.ImageFileName.Contains("..", StringComparison.Ordinal)) throw new InvalidDataException("主题图片名称无效。");
    }

    private void EnsureOwnedThemeDirectory(string directory)
    {
        var root = Path.GetFullPath(UserThemesDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("用户主题目录超出应用数据范围。");
        var directoryInfo = new DirectoryInfo(candidate.TrimEnd(Path.DirectorySeparatorChar));
        if (directoryInfo.LinkTarget is not null) throw new InvalidOperationException("用户主题目录不能是链接或联接点。");
        var image = directoryInfo.EnumerateFiles("art.*", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (image?.LinkTarget is not null) throw new InvalidOperationException("用户主题图片不能是链接。");
    }

    private static async Task WriteThemeCoreAsync(ThemeDefinition theme, CancellationToken cancellationToken)
    {
        var value = new
        {
            schemaVersion = 1,
            id = theme.Id,
            name = theme.Name,
            image = theme.ImageFileName,
            appearance = theme.Appearance,
            art = new { focusX = theme.FocusX, focusY = theme.FocusY, safeArea = theme.SafeArea, taskMode = theme.TaskMode },
            palette = new { accent = theme.Accent }
        };
        await WriteJsonAtomicallyAsync(Path.Combine(theme.DirectoryPath, "theme.json"), value, cancellationToken);
    }

    private async Task<string> ReadActiveIdCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(StatePath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement.GetProperty("activeThemeId").GetString() ?? "preset-kanna-hashimoto";
        }
        catch (FileNotFoundException) { return "preset-kanna-hashimoto"; }
        catch (JsonException) { return "preset-kanna-hashimoto"; }
    }

    private Task WriteActiveIdCoreAsync(string id, CancellationToken cancellationToken) =>
        WriteJsonAtomicallyAsync(StatePath, new { schemaVersion = 1, activeThemeId = id }, cancellationToken);

    private static async Task WriteJsonAtomicallyAsync(string path, object value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(value, JsonOptions), cancellationToken);
        File.Move(temporaryPath, path, true);
    }
}
