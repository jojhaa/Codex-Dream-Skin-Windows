using System.Text.Json;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using CodexDreamSkin.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace CodexDreamSkin.Services;

public sealed class ThemeCatalogService
{
    private const int MaximumImageBytes = 24 * 1024 * 1024;
    private const int MaximumPackageBytes = 32 * 1024 * 1024;
    private const long MaximumPackageExpandedBytes = 64L * 1024 * 1024;
    private const int MaximumMetadataBytes = 256 * 1024;
    private const int MaximumHistoryEntries = 20;
    private const ulong MaximumPixels = 50_000_000;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };
    private static readonly HashSet<string> AllowedAppearances = new(StringComparer.Ordinal) { "auto", "light", "dark" };
    private static readonly HashSet<string> AllowedSafeAreas = new(StringComparer.Ordinal) { "auto", "left", "right", "center", "none" };
    private static readonly HashSet<string> AllowedTaskModes = new(StringComparer.Ordinal) { "auto", "ambient", "banner", "off" };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _previewSourceSignature;

    public string BundledThemeDirectory => Path.Combine(AppContext.BaseDirectory, "Assets", "Theme");
    public string UserThemesDirectory => Path.Combine(AppStoragePaths.LocalRoot, "Themes");
    public string PreviewThemeDirectory => Path.Combine(AppStoragePaths.TemporaryRoot, "ThemePreview");
    private string StatePath => Path.Combine(AppStoragePaths.LocalRoot, "theme-state.json");

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
                SidebarImageFileName = imageName,
                ComposerImageFileName = imageName,
                HomeImageFileName = imageName,
                HomeComposerImageFileName = imageName,
                PolaroidImageFileName = imageName,
                FocusX = 0.5,
                FocusY = 0.45,
                BackgroundComposition = ThemeComposition.Recommended(ThemeImageSlot.Background, 0.5, 0.45),
                SidebarComposition = ThemeComposition.Recommended(ThemeImageSlot.Sidebar, 0.5, 0.45),
                ComposerComposition = ThemeComposition.Recommended(ThemeImageSlot.Composer, 0.5, 0.45),
                HomeComposition = ThemeComposition.Recommended(ThemeImageSlot.Home, 0.5, 0.45),
                HomeComposerComposition = ThemeComposition.Recommended(ThemeImageSlot.HomeComposer, 0.5, 0.45),
                PolaroidComposition = ThemeComposition.Recommended(ThemeImageSlot.Polaroid, 0.5, 0.45),
                SafeArea = "left",
                TaskMode = "ambient"
            };
            await WriteThemeCoreAsync(theme, cancellationToken);
            return theme;
        }
        finally { _gate.Release(); }
    }

    public async Task<string> StageImageAsync(
        ThemeDefinition theme,
        ThemeImageSlot slot,
        StorageFile source,
        CancellationToken cancellationToken = default)
    {
        if (theme.IsBundled) throw new InvalidOperationException("内置主题是只读的。请先创建副本。");
        var extension = Path.GetExtension(source.Name).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension)) throw new InvalidDataException("仅支持 PNG、JPEG 和 WebP 图片。");
        var properties = await source.GetBasicPropertiesAsync();
        if (properties.Size is < 1 or > MaximumImageBytes) throw new InvalidDataException("图片必须小于 24 MB。");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureOwnedThemeDirectory(theme.DirectoryPath);
            var prefix = slot.ToString().ToLowerInvariant();
            var imageName = $"{prefix}-{Guid.NewGuid():N}{extension}";
            var themesFolder = await StorageFolder.GetFolderFromPathAsync(theme.DirectoryPath);
            await source.CopyAsync(themesFolder, imageName, NameCollisionOption.FailIfExists);
            try
            {
                await ValidateImageFileAsync(Path.Combine(theme.DirectoryPath, imageName), cancellationToken);
                return imageName;
            }
            catch
            {
                File.Delete(Path.Combine(theme.DirectoryPath, imageName));
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<ThemeDefinition> ImportPackageAsync(StorageFile source, CancellationToken cancellationToken = default)
    {
        var properties = await source.GetBasicPropertiesAsync();
        if (properties.Size is < 1 or > MaximumPackageBytes) throw new InvalidDataException("主题包必须小于 32 MB。");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var packageStream = await source.OpenStreamForReadAsync();
            using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, false);
            if (archive.Entries.Count is < 3 or > 8) throw new InvalidDataException("主题包条目数量无效。");
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(entry.Name)
                    || !string.Equals(entry.Name, entry.FullName, StringComparison.Ordinal)
                    || !entries.TryAdd(entry.Name, entry))
                    throw new InvalidDataException("主题包包含目录、重复条目或不安全路径。");
            }

            var manifestEntry = GetRequiredEntry(entries, "manifest.json", MaximumMetadataBytes);
            var themeEntry = GetRequiredEntry(entries, "theme.json", MaximumMetadataBytes);
            var manifestText = await ReadTextEntryAsync(manifestEntry, MaximumMetadataBytes, cancellationToken);
            using (var manifest = JsonDocument.Parse(manifestText))
            {
                var root = manifest.RootElement;
                if (root.GetProperty("format").GetString() != "codex-dream-theme"
                    || root.GetProperty("formatVersion").GetInt32() != 1)
                    throw new InvalidDataException("不支持的主题包格式。");
            }

            var themeText = await ReadTextEntryAsync(themeEntry, MaximumMetadataBytes, cancellationToken);
            using var themeDocument = JsonDocument.Parse(themeText);
            var declaredImages = ReadImageDeclarations(themeDocument.RootElement).Values
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            long expandedBytes = manifestEntry.Length + themeEntry.Length;
            foreach (var declaredImage in declaredImages)
            {
                if (!AllowedExtensions.Contains(Path.GetExtension(declaredImage))
                    || !string.Equals(Path.GetFileName(declaredImage), declaredImage, StringComparison.Ordinal)
                    || !entries.TryGetValue(declaredImage, out var imageEntry)
                    || imageEntry.Length is < 1 or > MaximumImageBytes)
                    throw new InvalidDataException("主题包图片条目无效。");
                expandedBytes += imageEntry.Length;
            }
            if (expandedBytes > MaximumPackageExpandedBytes) throw new InvalidDataException("主题包解压后内容超过 64 MB。");
            if (entries.Keys.Any(name => name is not ("manifest.json" or "theme.json")
                    && !declaredImages.Contains(name, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidDataException("主题包包含不允许的附加文件。");

            Directory.CreateDirectory(UserThemesDirectory);
            var id = $"custom-{Guid.NewGuid():N}";
            var directory = Path.Combine(UserThemesDirectory, id);
            Directory.CreateDirectory(directory);
            try
            {
                foreach (var declaredImage in declaredImages)
                {
                    await using var sourceStream = entries[declaredImage].Open();
                    await using var destination = File.Create(Path.Combine(directory, declaredImage));
                    await sourceStream.CopyToAsync(destination, cancellationToken);
                }

                var importedSource = ParseThemeCore(themeDocument.RootElement, directory, false);
                var imported = CopyTheme(importedSource, id, directory);
                Validate(imported);
                foreach (var imagePath in imported.ImageFileNames.Select(name => Path.Combine(directory, name)))
                    await ValidateImageFileAsync(imagePath, cancellationToken);
                await WriteThemeCoreAsync(imported, cancellationToken);
                return imported;
            }
            catch
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task ExportPackageAsync(ThemeDefinition theme, StorageFile destination, CancellationToken cancellationToken = default)
    {
        Validate(theme);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!theme.IsBundled) EnsureOwnedThemeDirectory(theme.DirectoryPath);
            var themePath = Path.Combine(theme.DirectoryPath, "theme.json");
            if (new FileInfo(themePath).Length > MaximumMetadataBytes) throw new InvalidDataException("主题元数据过大。");
            foreach (var imagePath in theme.ImageFileNames.Select(name => Path.Combine(theme.DirectoryPath, name)))
                if (new FileInfo(imagePath).Length > MaximumImageBytes) throw new InvalidDataException("主题图片过大。");

            await using var output = await destination.OpenStreamForWriteAsync();
            output.SetLength(0);
            using var archive = new ZipArchive(output, ZipArchiveMode.Create, true);
            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            await using (var stream = manifestEntry.Open())
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: false))
                await writer.WriteAsync(JsonSerializer.Serialize(new
                {
                    format = "codex-dream-theme",
                    formatVersion = 1,
                    exportedAt = DateTimeOffset.UtcNow,
                    rendererVersion = "3.9.4"
                }, JsonOptions));

            await AddFileToArchiveAsync(archive, "theme.json", themePath, cancellationToken);
            foreach (var imageName in theme.ImageFileNames)
                await AddFileToArchiveAsync(archive, imageName, Path.Combine(theme.DirectoryPath, imageName), cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<ThemeHistoryEntry>> GetHistoryAsync(ThemeDefinition theme, CancellationToken cancellationToken = default)
    {
        if (theme.IsBundled) return [];
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureOwnedThemeDirectory(theme.DirectoryPath);
            var historyDirectory = Path.Combine(theme.DirectoryPath, "history");
            if (!Directory.Exists(historyDirectory)) return [];
            var entries = new List<ThemeHistoryEntry>();
            foreach (var file in Directory.EnumerateFiles(historyDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await using var stream = File.OpenRead(file);
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    var name = document.RootElement.GetProperty("name").GetString() ?? theme.Name;
                    entries.Add(new(file, File.GetLastWriteTimeUtc(file), name));
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
            }
            return entries.OrderByDescending(entry => entry.SavedAt).ToArray();
        }
        finally { _gate.Release(); }
    }

    public async Task<ThemeDefinition> RestoreHistoryAsync(ThemeDefinition theme, ThemeHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        if (theme.IsBundled) throw new InvalidOperationException("内置主题没有可恢复历史。");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureOwnedThemeDirectory(theme.DirectoryPath);
            var historyRoot = Path.GetFullPath(Path.Combine(theme.DirectoryPath, "history")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var historyPath = Path.GetFullPath(entry.FilePath);
            if (!historyPath.StartsWith(historyRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(historyPath))
                throw new InvalidOperationException("历史快照路径无效。");
            var historyText = await File.ReadAllTextAsync(historyPath, cancellationToken);
            if (Encoding.UTF8.GetByteCount(historyText) > MaximumMetadataBytes) throw new InvalidDataException("历史快照过大。");
            await CreateHistorySnapshotCoreAsync(theme.DirectoryPath, cancellationToken);
            using var document = JsonDocument.Parse(historyText);
            var restored = ParseThemeCore(document.RootElement, theme.DirectoryPath, false);
            Validate(restored);
            await WriteThemeCoreAsync(restored, cancellationToken);
            await CleanupUnreferencedImagesCoreAsync(restored.DirectoryPath, cancellationToken);
            return restored;
        }
        finally { _gate.Release(); }
    }

    public async Task<ThemeDefinition> DuplicateAsync(ThemeDefinition source, CancellationToken cancellationToken = default)
    {
        Validate(source);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!source.IsBundled) EnsureOwnedThemeDirectory(source.DirectoryPath);
            Directory.CreateDirectory(UserThemesDirectory);
            var id = $"custom-{Guid.NewGuid():N}";
            var directory = Path.Combine(UserThemesDirectory, id);
            Directory.CreateDirectory(directory);
            try
            {
                var copiedImages = CopyThemeImages(source, directory);
                var suffix = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? " 副本" : " Copy";
                var sourceName = source.Name[..Math.Min(source.Name.Length, 120 - suffix.Length)];
                var duplicate = new ThemeDefinition
                {
                    Id = id,
                    Name = sourceName + suffix,
                    DirectoryPath = directory,
                    ImageFileName = copiedImages[source.ImageFileName],
                    SidebarImageFileName = copiedImages[source.EffectiveSidebarImageFileName],
                    ComposerImageFileName = copiedImages[source.EffectiveComposerImageFileName],
                    HomeImageFileName = copiedImages[source.EffectiveHomeImageFileName],
                    HomeComposerImageFileName = copiedImages[source.EffectiveHomeComposerImageFileName],
                    PolaroidImageFileName = copiedImages[source.EffectivePolaroidImageFileName],
                    Appearance = source.Appearance,
                    FocusX = source.FocusX,
                    FocusY = source.FocusY,
                    BackgroundComposition = source.BackgroundComposition,
                    SidebarComposition = source.SidebarComposition,
                    ComposerComposition = source.ComposerComposition,
                    HomeComposition = source.HomeComposition,
                    HomeComposerComposition = source.HomeComposerComposition,
                    PolaroidComposition = source.PolaroidComposition,
                    SafeArea = source.SafeArea,
                    TaskMode = source.TaskMode,
                    Accent = source.Accent,
                    LightPageOpacity = source.LightPageOpacity,
                    LightSidebarOpacity = source.LightSidebarOpacity,
                    LightComposerOpacity = source.LightComposerOpacity,
                    LightCardOpacity = source.LightCardOpacity,
                    DarkPageOpacity = source.DarkPageOpacity,
                    DarkSidebarOpacity = source.DarkSidebarOpacity,
                    DarkComposerOpacity = source.DarkComposerOpacity,
                    DarkCardOpacity = source.DarkCardOpacity,
                    ComponentMaterials = source.ComponentMaterials
                };
                await WriteThemeCoreAsync(duplicate, cancellationToken);
                return duplicate;
            }
            catch
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<string> PreparePreviewAsync(ThemeDefinition draft, CancellationToken cancellationToken = default)
    {
        Validate(draft);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!draft.IsBundled) EnsureOwnedThemeDirectory(draft.DirectoryPath);
            var sourceSignature = GetPreviewSourceSignature(draft);
            var copiedImages = MapThemeImages(draft);
            var canReuseImages = string.Equals(_previewSourceSignature, sourceSignature, StringComparison.Ordinal)
                && Directory.Exists(PreviewThemeDirectory)
                && copiedImages.Values.All(name => File.Exists(Path.Combine(PreviewThemeDirectory, name)));
            if (!canReuseImages)
            {
                if (Directory.Exists(PreviewThemeDirectory)) Directory.Delete(PreviewThemeDirectory, true);
                Directory.CreateDirectory(PreviewThemeDirectory);
                copiedImages = CopyThemeImages(draft, PreviewThemeDirectory);
                _previewSourceSignature = sourceSignature;
            }
            var preview = new ThemeDefinition
            {
                Id = draft.Id,
                Name = draft.Name,
                DirectoryPath = PreviewThemeDirectory,
                ImageFileName = copiedImages[draft.ImageFileName],
                SidebarImageFileName = copiedImages[draft.EffectiveSidebarImageFileName],
                ComposerImageFileName = copiedImages[draft.EffectiveComposerImageFileName],
                HomeImageFileName = copiedImages[draft.EffectiveHomeImageFileName],
                HomeComposerImageFileName = copiedImages[draft.EffectiveHomeComposerImageFileName],
                PolaroidImageFileName = copiedImages[draft.EffectivePolaroidImageFileName],
                Appearance = draft.Appearance,
                FocusX = draft.FocusX,
                FocusY = draft.FocusY,
                BackgroundComposition = draft.BackgroundComposition,
                SidebarComposition = draft.SidebarComposition,
                ComposerComposition = draft.ComposerComposition,
                HomeComposition = draft.HomeComposition,
                HomeComposerComposition = draft.HomeComposerComposition,
                PolaroidComposition = draft.PolaroidComposition,
                SafeArea = draft.SafeArea,
                TaskMode = draft.TaskMode,
                Accent = draft.Accent,
                LightPageOpacity = draft.LightPageOpacity,
                LightSidebarOpacity = draft.LightSidebarOpacity,
                LightComposerOpacity = draft.LightComposerOpacity,
                LightCardOpacity = draft.LightCardOpacity,
                DarkPageOpacity = draft.DarkPageOpacity,
                DarkSidebarOpacity = draft.DarkSidebarOpacity,
                DarkComposerOpacity = draft.DarkComposerOpacity,
                DarkCardOpacity = draft.DarkCardOpacity,
                ComponentMaterials = draft.ComponentMaterials
            };
            await WriteThemeCoreAsync(preview, cancellationToken);
            return PreviewThemeDirectory;
        }
        finally { _gate.Release(); }
    }

    public async Task CleanupPreviewAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (Directory.Exists(PreviewThemeDirectory)) Directory.Delete(PreviewThemeDirectory, true);
            _previewSourceSignature = null;
        }
        finally { _gate.Release(); }
    }

    public async Task CleanupAbandonedImagesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(UserThemesDirectory);
            foreach (var directory in Directory.EnumerateDirectories(UserThemesDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    EnsureOwnedThemeDirectory(directory);
                    await CleanupUnreferencedImagesCoreAsync(directory, cancellationToken);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // Fail closed for one damaged theme without blocking the rest of the library.
                }
            }
        }
        finally { _gate.Release(); }
    }

    public async Task CleanupUnreferencedImagesAsync(ThemeDefinition theme, CancellationToken cancellationToken = default)
    {
        if (theme.IsBundled) return;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureOwnedThemeDirectory(theme.DirectoryPath);
            await CleanupUnreferencedImagesCoreAsync(theme.DirectoryPath, cancellationToken);
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
            await CreateHistorySnapshotCoreAsync(theme.DirectoryPath, cancellationToken);
            await WriteThemeCoreAsync(theme, cancellationToken);
            await CleanupUnreferencedImagesCoreAsync(theme.DirectoryPath, cancellationToken);
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
        return ParseThemeCore(document.RootElement, directory, bundled);
    }

    private ThemeDefinition ParseThemeCore(JsonElement root, string directory, bool bundled)
    {
        var schemaVersion = root.TryGetProperty("schemaVersion", out var schemaNode) && schemaNode.ValueKind == JsonValueKind.Number
            ? schemaNode.GetInt32()
            : 1;
        if (schemaVersion is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8)) throw new InvalidDataException("不支持的主题数据版本。");
        var imageNames = ReadImageDeclarations(root);
        var art = root.GetProperty("art");
        var palette = root.TryGetProperty("palette", out var paletteNode) ? paletteNode : default;
        var materials = root.TryGetProperty("materials", out var materialsNode) ? materialsNode : default;
        var lightMaterials = materials.ValueKind == JsonValueKind.Object && materials.TryGetProperty("light", out var lightNode) ? lightNode : default;
        var darkMaterials = materials.ValueKind == JsonValueKind.Object && materials.TryGetProperty("dark", out var darkNode) ? darkNode : default;
        var legacyFocusX = art.TryGetProperty("focusX", out var focusX) && focusX.ValueKind == JsonValueKind.Number ? focusX.GetDouble() : 0.5;
        var legacyFocusY = art.TryGetProperty("focusY", out var focusY) && focusY.ValueKind == JsonValueKind.Number ? focusY.GetDouble() : 0.5;
        var compositions = root.TryGetProperty("compositions", out var compositionsNode) ? compositionsNode : default;
        var theme = new ThemeDefinition
        {
            Id = root.GetProperty("id").GetString() ?? throw new InvalidDataException("主题 ID 缺失。"),
            Name = root.GetProperty("name").GetString() ?? throw new InvalidDataException("主题名称缺失。"),
            DirectoryPath = Path.GetFullPath(directory),
            ImageFileName = imageNames[ThemeImageSlot.Background],
            SidebarImageFileName = imageNames[ThemeImageSlot.Sidebar],
            ComposerImageFileName = imageNames[ThemeImageSlot.Composer],
            HomeImageFileName = imageNames[ThemeImageSlot.Home],
            HomeComposerImageFileName = imageNames[ThemeImageSlot.HomeComposer],
            PolaroidImageFileName = imageNames[ThemeImageSlot.Polaroid],
            Appearance = root.TryGetProperty("appearance", out var appearance) ? appearance.GetString() ?? "auto" : "auto",
            FocusX = legacyFocusX,
            FocusY = legacyFocusY,
            BackgroundComposition = ReadComposition(compositions, "background", ThemeImageSlot.Background, legacyFocusX, legacyFocusY),
            SidebarComposition = ReadComposition(compositions, "sidebar", ThemeImageSlot.Sidebar, legacyFocusX, legacyFocusY),
            ComposerComposition = ReadComposition(compositions, "composer", ThemeImageSlot.Composer, legacyFocusX, legacyFocusY),
            HomeComposition = ReadComposition(compositions, "home", ThemeImageSlot.Home, legacyFocusX, legacyFocusY),
            HomeComposerComposition = ReadComposition(compositions, "homeComposer", ThemeImageSlot.HomeComposer, legacyFocusX, legacyFocusY),
            PolaroidComposition = ReadComposition(compositions, "polaroid", ThemeImageSlot.Polaroid, legacyFocusX, legacyFocusY),
            SafeArea = art.TryGetProperty("safeArea", out var safeArea) ? safeArea.GetString() ?? "auto" : "auto",
            TaskMode = art.TryGetProperty("taskMode", out var taskMode) ? taskMode.GetString() ?? "auto" : "auto",
            Accent = palette.ValueKind == JsonValueKind.Object && palette.TryGetProperty("accent", out var accent) ? accent.GetString() ?? "#1557b0" : "#1557b0",
            LightPageOpacity = ReadOpacity(lightMaterials, "page", 0.56),
            LightSidebarOpacity = ReadOpacity(lightMaterials, "sidebar", 0.58),
            LightComposerOpacity = ReadOpacity(lightMaterials, "composer", 0.48),
            LightCardOpacity = ReadOpacity(lightMaterials, "card", 0.18),
            DarkPageOpacity = ReadOpacity(darkMaterials, "page", 0.68),
            DarkSidebarOpacity = ReadOpacity(darkMaterials, "sidebar", 0.74),
            DarkComposerOpacity = ReadOpacity(darkMaterials, "composer", 0.62),
            DarkCardOpacity = ReadOpacity(darkMaterials, "card", 0.42),
            ComponentMaterials = ReadComponentMaterials(materials),
            IsBundled = bundled
        };
        theme.FocusX = theme.BackgroundComposition.FocusX;
        theme.FocusY = theme.BackgroundComposition.FocusY;
        Validate(theme);
        var trustedRoot = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var imageName in theme.ImageFileNames)
        {
            var imagePath = Path.GetFullPath(Path.Combine(theme.DirectoryPath, imageName));
            if (!imagePath.StartsWith(trustedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(imagePath))
                throw new InvalidDataException("主题图片路径无效。");
        }
        return theme;
    }

    private static IReadOnlyDictionary<ThemeImageSlot, string> ReadImageDeclarations(JsonElement root)
    {
        var background = root.GetProperty("image").GetString()
            ?? throw new InvalidDataException("主题图片缺失。");
        var images = root.TryGetProperty("images", out var imagesNode) && imagesNode.ValueKind == JsonValueKind.Object
            ? imagesNode
            : default;
        string Read(string name) => images.ValueKind == JsonValueKind.Object
            && images.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!
                : background;
        return new Dictionary<ThemeImageSlot, string>
        {
            [ThemeImageSlot.Background] = background,
            [ThemeImageSlot.Sidebar] = Read("sidebar"),
            [ThemeImageSlot.Composer] = Read("composer"),
            [ThemeImageSlot.Home] = Read("home"),
            [ThemeImageSlot.HomeComposer] = images.ValueKind == JsonValueKind.Object && images.TryGetProperty("homeComposer", out _)
                ? Read("homeComposer") : Read("composer"),
            [ThemeImageSlot.Polaroid] = images.ValueKind == JsonValueKind.Object && images.TryGetProperty("polaroid", out _)
                ? Read("polaroid") : Read("home")
        };
    }

    private static Dictionary<string, string> MapThemeImages(ThemeDefinition source)
    {
        var images = new[]
        {
            (ThemeImageSlot.Background, source.ImageFileName),
            (ThemeImageSlot.Sidebar, source.EffectiveSidebarImageFileName),
            (ThemeImageSlot.Composer, source.EffectiveComposerImageFileName),
            (ThemeImageSlot.Home, source.EffectiveHomeImageFileName),
            (ThemeImageSlot.HomeComposer, source.EffectiveHomeComposerImageFileName),
            (ThemeImageSlot.Polaroid, source.EffectivePolaroidImageFileName)
        };
        var copied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slot, sourceName) in images)
        {
            if (copied.ContainsKey(sourceName)) continue;
            var extension = Path.GetExtension(sourceName).ToLowerInvariant();
            var destinationName = $"{slot.ToString().ToLowerInvariant()}{extension}";
            copied[sourceName] = destinationName;
        }
        return copied;
    }

    private static Dictionary<string, string> CopyThemeImages(ThemeDefinition source, string destinationDirectory)
    {
        var copied = MapThemeImages(source);
        foreach (var (sourceName, destinationName) in copied)
            File.Copy(Path.Combine(source.DirectoryPath, sourceName), Path.Combine(destinationDirectory, destinationName), false);
        return copied;
    }

    private static string GetPreviewSourceSignature(ThemeDefinition source) => string.Join("\n",
        MapThemeImages(source)
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item =>
            {
                var file = new FileInfo(Path.Combine(source.DirectoryPath, item.Key));
                return $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}|{item.Value}";
            }));

    private static ThemeDefinition CopyTheme(ThemeDefinition source, string id, string directory) => new()
    {
        Id = id,
        Name = source.Name,
        DirectoryPath = directory,
        ImageFileName = source.ImageFileName,
        SidebarImageFileName = source.EffectiveSidebarImageFileName,
        ComposerImageFileName = source.EffectiveComposerImageFileName,
        HomeImageFileName = source.EffectiveHomeImageFileName,
        HomeComposerImageFileName = source.EffectiveHomeComposerImageFileName,
        PolaroidImageFileName = source.EffectivePolaroidImageFileName,
        Appearance = source.Appearance,
        FocusX = source.FocusX,
        FocusY = source.FocusY,
        BackgroundComposition = source.BackgroundComposition,
        SidebarComposition = source.SidebarComposition,
        ComposerComposition = source.ComposerComposition,
        HomeComposition = source.HomeComposition,
        HomeComposerComposition = source.HomeComposerComposition,
        PolaroidComposition = source.PolaroidComposition,
        SafeArea = source.SafeArea,
        TaskMode = source.TaskMode,
        Accent = source.Accent,
        LightPageOpacity = source.LightPageOpacity,
        LightSidebarOpacity = source.LightSidebarOpacity,
        LightComposerOpacity = source.LightComposerOpacity,
        LightCardOpacity = source.LightCardOpacity,
        DarkPageOpacity = source.DarkPageOpacity,
        DarkSidebarOpacity = source.DarkSidebarOpacity,
        DarkComposerOpacity = source.DarkComposerOpacity,
        DarkCardOpacity = source.DarkCardOpacity,
        ComponentMaterials = source.ComponentMaterials
    };

    private static ZipArchiveEntry GetRequiredEntry(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        string name,
        int maximumBytes)
    {
        if (!entries.TryGetValue(name, out var entry) || entry.Length is < 1 || entry.Length > maximumBytes)
            throw new InvalidDataException($"主题包缺少有效的 {name}。");
        return entry;
    }

    private static async Task<string> ReadTextEntryAsync(ZipArchiveEntry entry, int maximumBytes, CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, false);
        var buffer = new char[4096];
        var builder = new StringBuilder();
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0) break;
            builder.Append(buffer, 0, count);
            if (Encoding.UTF8.GetByteCount(builder.ToString()) > maximumBytes)
                throw new InvalidDataException("主题包文本条目过大。");
        }
        return builder.ToString();
    }

    private static async Task AddFileToArchiveAsync(
        ZipArchive archive,
        string entryName,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var source = File.OpenRead(sourcePath);
        await using var destination = entry.Open();
        await source.CopyToAsync(destination, cancellationToken);
    }

    private static async Task ValidateImageFileAsync(string path, CancellationToken cancellationToken)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        var properties = await file.GetBasicPropertiesAsync();
        if (properties.Size is < 1 or > MaximumImageBytes) throw new InvalidDataException("主题图片大小无效。");
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        cancellationToken.ThrowIfCancellationRequested();
        var expectedCodec = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => BitmapDecoder.PngDecoderId,
            ".jpg" or ".jpeg" => BitmapDecoder.JpegDecoderId,
            ".webp" => BitmapDecoder.WebpDecoderId,
            _ => Guid.Empty
        };
        if (expectedCodec == Guid.Empty || decoder.DecoderInformation.CodecId != expectedCodec)
            throw new InvalidDataException("主题图片内容与文件扩展名不一致。");
        var width = decoder.PixelWidth;
        var height = decoder.PixelHeight;
        if (width is 0 or > 16384 || height is 0 or > 16384 || (ulong)width * height > MaximumPixels)
            throw new InvalidDataException("主题图片尺寸无效。");
    }

    private static Task CreateHistorySnapshotCoreAsync(string directory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var themePath = Path.Combine(directory, "theme.json");
        if (!File.Exists(themePath)) return Task.CompletedTask;
        if (new FileInfo(themePath).Length > MaximumMetadataBytes) throw new InvalidDataException("主题元数据过大。");
        var historyDirectory = Path.Combine(directory, "history");
        Directory.CreateDirectory(historyDirectory);
        var snapshotPath = Path.Combine(historyDirectory, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fffffff}.json");
        File.Copy(themePath, snapshotPath, false);
        foreach (var stale in Directory.EnumerateFiles(historyDirectory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Skip(MaximumHistoryEntries))
            File.Delete(stale);
        return Task.CompletedTask;
    }

    private static async Task CleanupUnreferencedImagesCoreAsync(string directory, CancellationToken cancellationToken)
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentThemePath = Path.Combine(directory, "theme.json");
        await using (var stream = File.OpenRead(currentThemePath))
        {
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            foreach (var imageName in ReadImageDeclarations(document.RootElement).Values)
                referenced.Add(imageName);
        }

        var historyDirectory = Path.Combine(directory, "history");
        foreach (var metadataPath in Directory.Exists(historyDirectory)
                     ? Directory.EnumerateFiles(historyDirectory, "*.json", SearchOption.TopDirectoryOnly)
                     : [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = File.OpenRead(metadataPath);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                foreach (var imageName in ReadImageDeclarations(document.RootElement).Values)
                    referenced.Add(imageName);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // A damaged history item must not prevent saving the valid current theme.
            }
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (AllowedExtensions.Contains(Path.GetExtension(file)) && !referenced.Contains(Path.GetFileName(file)))
                File.Delete(file);
        }
    }

    private static void Validate(ThemeDefinition theme)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(theme.Id, "^(?:preset-kanna-hashimoto|custom-[0-9a-f]{32})$")) throw new InvalidDataException("主题 ID 无效。");
        if (string.IsNullOrWhiteSpace(theme.Name) || theme.Name.Length > 120 || theme.Name.Any(char.IsControl)) throw new InvalidDataException("主题名称必须为 1 到 120 个字符。");
        if (theme.FocusX is < 0 or > 1 || theme.FocusY is < 0 or > 1) throw new InvalidDataException("图片焦点必须位于 0 到 1 之间。");
        if (!AllowedAppearances.Contains(theme.Appearance) || !AllowedSafeAreas.Contains(theme.SafeArea) || !AllowedTaskModes.Contains(theme.TaskMode)) throw new InvalidDataException("主题选项无效。");
        if (!System.Text.RegularExpressions.Regex.IsMatch(theme.Accent, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")) throw new InvalidDataException("强调色必须为 #RRGGBB 或 #RRGGBBAA。");
        var opacityValues = new[]
        {
            theme.LightPageOpacity, theme.LightSidebarOpacity, theme.LightComposerOpacity, theme.LightCardOpacity,
            theme.DarkPageOpacity, theme.DarkSidebarOpacity, theme.DarkComposerOpacity, theme.DarkCardOpacity
        };
        if (opacityValues.Any(value => !double.IsFinite(value) || value is < 0.04 or > 0.92))
            throw new InvalidDataException("材质透明度必须位于 4% 到 92% 之间。");
        foreach (var component in theme.ComponentMaterials.All)
        {
            if (!IsHexColor(component.LightColor) || !IsHexColor(component.DarkColor))
                throw new InvalidDataException("组件颜色必须为 #RRGGBB。");
            if (!double.IsFinite(component.LightOpacity) || component.LightOpacity is < 0.04 or > 0.92
                || !double.IsFinite(component.DarkOpacity) || component.DarkOpacity is < 0.04 or > 0.92)
                throw new InvalidDataException("组件透明度必须位于 4% 到 92% 之间。");
        }
        foreach (var imageName in theme.ImageFileNames)
        {
            if (Path.IsPathRooted(imageName)
                || imageName.Contains("..", StringComparison.Ordinal)
                || !string.Equals(Path.GetFileName(imageName), imageName, StringComparison.Ordinal)
                || !AllowedExtensions.Contains(Path.GetExtension(imageName)))
                throw new InvalidDataException("主题图片名称无效。");
        }
        foreach (var composition in new[]
                 {
                     theme.BackgroundComposition,
                     theme.SidebarComposition,
                     theme.ComposerComposition,
                     theme.HomeComposition,
                     theme.HomeComposerComposition,
                     theme.PolaroidComposition
                 })
        {
            if (!double.IsFinite(composition.FocusX) || composition.FocusX is < 0 or > 1
                || !double.IsFinite(composition.FocusY) || composition.FocusY is < 0 or > 1
                || !double.IsFinite(composition.Zoom) || composition.Zoom is < 0.5 or > 3
                || !double.IsFinite(composition.OffsetX) || composition.OffsetX is < -1 or > 1
                || !double.IsFinite(composition.OffsetY) || composition.OffsetY is < -1 or > 1
                || composition.Fit is not ("auto" or "cover" or "contain" or "fill"))
                throw new InvalidDataException("区域构图参数无效。");
        }
    }

    private static double ReadOpacity(JsonElement group, string name, double fallback) =>
        group.ValueKind == JsonValueKind.Object
        && group.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
            ? number
            : fallback;

    private static bool IsHexColor(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$");

    private static ThemeComponentMaterials ReadComponentMaterials(JsonElement materials)
    {
        var fallback = ThemeComponentMaterials.Default;
        var components = materials.ValueKind == JsonValueKind.Object
            && materials.TryGetProperty("components", out var node)
            && node.ValueKind == JsonValueKind.Object ? node : default;
        ThemeComponentMaterial Read(string name, ThemeComponentMaterial defaultValue)
        {
            if (components.ValueKind != JsonValueKind.Object
                || !components.TryGetProperty(name, out var group)
                || group.ValueKind != JsonValueKind.Object) return defaultValue;
            var light = group.TryGetProperty("light", out var lightNode) ? lightNode : default;
            var dark = group.TryGetProperty("dark", out var darkNode) ? darkNode : default;
            static string Color(JsonElement value, string defaultColor) => value.ValueKind == JsonValueKind.Object
                && value.TryGetProperty("color", out var color)
                && color.ValueKind == JsonValueKind.String ? color.GetString() ?? defaultColor : defaultColor;
            return new ThemeComponentMaterial(
                Color(light, defaultValue.LightColor),
                ReadOpacity(light, "opacity", defaultValue.LightOpacity),
                Color(dark, defaultValue.DarkColor),
                ReadOpacity(dark, "opacity", defaultValue.DarkOpacity));
        }
        return new ThemeComponentMaterials(
            Read("messages", fallback.Messages),
            Read("summaries", fallback.Summaries),
            Read("previews", fallback.Previews),
            Read("menus", fallback.Menus),
            Read("workspace", fallback.Workspace),
            Read("code", fallback.Code),
            Read("suggestions", fallback.Suggestions));
    }

    private static ThemeComposition ReadComposition(
        JsonElement compositions,
        string name,
        ThemeImageSlot slot,
        double legacyFocusX,
        double legacyFocusY)
    {
        var fallback = ThemeComposition.Recommended(slot, legacyFocusX, legacyFocusY);
        if (compositions.ValueKind != JsonValueKind.Object
            || !compositions.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Object) return fallback;
        static double Number(JsonElement group, string key, double defaultValue) =>
            group.TryGetProperty(key, out var node) && node.ValueKind == JsonValueKind.Number && node.TryGetDouble(out var number)
                ? number
                : defaultValue;
        var fit = value.TryGetProperty("fit", out var fitNode) && fitNode.ValueKind == JsonValueKind.String
            ? fitNode.GetString() ?? fallback.Fit
            : fallback.Fit;
        return new ThemeComposition(
            Number(value, "focusX", fallback.FocusX),
            Number(value, "focusY", fallback.FocusY),
            Number(value, "zoom", fallback.Zoom),
            fit,
            Number(value, "offsetX", fallback.OffsetX),
            Number(value, "offsetY", fallback.OffsetY));
    }

    private void EnsureOwnedThemeDirectory(string directory)
    {
        var root = Path.GetFullPath(UserThemesDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("用户主题目录超出应用数据范围。");
        var directoryInfo = new DirectoryInfo(candidate.TrimEnd(Path.DirectorySeparatorChar));
        if (directoryInfo.LinkTarget is not null) throw new InvalidOperationException("用户主题目录不能是链接或联接点。");
        if (directoryInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Any(file => AllowedExtensions.Contains(file.Extension) && file.LinkTarget is not null))
            throw new InvalidOperationException("用户主题图片不能是链接。");
    }

    private static async Task WriteThemeCoreAsync(ThemeDefinition theme, CancellationToken cancellationToken)
    {
        var value = new
        {
            schemaVersion = 8,
            id = theme.Id,
            name = theme.Name,
            image = theme.ImageFileName,
            images = new
            {
                sidebar = theme.EffectiveSidebarImageFileName,
                composer = theme.EffectiveComposerImageFileName,
                home = theme.EffectiveHomeImageFileName,
                homeComposer = theme.EffectiveHomeComposerImageFileName,
                polaroid = theme.EffectivePolaroidImageFileName
            },
            appearance = theme.Appearance,
            art = new
            {
                focusX = theme.BackgroundComposition.FocusX,
                focusY = theme.BackgroundComposition.FocusY,
                safeArea = theme.SafeArea,
                taskMode = theme.TaskMode
            },
            compositions = new
            {
                background = CompositionValue(theme.BackgroundComposition),
                sidebar = CompositionValue(theme.SidebarComposition),
                composer = CompositionValue(theme.ComposerComposition),
                home = CompositionValue(theme.HomeComposition),
                homeComposer = CompositionValue(theme.HomeComposerComposition),
                polaroid = CompositionValue(theme.PolaroidComposition)
            },
            palette = new { accent = theme.Accent },
            materials = new
            {
                light = new
                {
                    page = theme.LightPageOpacity,
                    sidebar = theme.LightSidebarOpacity,
                    composer = theme.LightComposerOpacity,
                    card = theme.LightCardOpacity
                },
                dark = new
                {
                    page = theme.DarkPageOpacity,
                    sidebar = theme.DarkSidebarOpacity,
                    composer = theme.DarkComposerOpacity,
                    card = theme.DarkCardOpacity
                },
                components = new
                {
                    messages = ComponentMaterialValue(theme.ComponentMaterials.Messages),
                    summaries = ComponentMaterialValue(theme.ComponentMaterials.Summaries),
                    previews = ComponentMaterialValue(theme.ComponentMaterials.Previews),
                    menus = ComponentMaterialValue(theme.ComponentMaterials.Menus),
                    workspace = ComponentMaterialValue(theme.ComponentMaterials.Workspace),
                    code = ComponentMaterialValue(theme.ComponentMaterials.Code),
                    suggestions = ComponentMaterialValue(theme.ComponentMaterials.Suggestions)
                }
            }
        };
        await WriteJsonAtomicallyAsync(Path.Combine(theme.DirectoryPath, "theme.json"), value, cancellationToken);
    }

    private static object CompositionValue(ThemeComposition value) => new
    {
        focusX = value.FocusX,
        focusY = value.FocusY,
        zoom = value.Zoom,
        fit = value.Fit,
        offsetX = value.OffsetX,
        offsetY = value.OffsetY
    };

    private static object ComponentMaterialValue(ThemeComponentMaterial value) => new
    {
        light = new { color = value.LightColor, opacity = value.LightOpacity },
        dark = new { color = value.DarkColor, opacity = value.DarkOpacity }
    };

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
