using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace CodexDreamSkin.Services;

public sealed record ThemePayload(string Expression, string Revision, string ThemeName);
internal sealed record LoadedThemeImage(string DataUrl, uint Width, uint Height);

public sealed class ThemePayloadLoader
{
    private const int MaximumImageBytes = 24 * 1024 * 1024;

    public async Task<ThemePayload> LoadAsync(string themeDirectory, CancellationToken cancellationToken)
    {
        var themeRoot = Path.GetFullPath(themeDirectory);
        var engineAssetRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Theme");
        var themePath = Path.Combine(themeRoot, "theme.json");
        await using var themeStream = File.OpenRead(themePath);
        using var themeDocument = await JsonDocument.ParseAsync(themeStream, cancellationToken: cancellationToken);
        var root = themeDocument.RootElement;
        var imageName = root.GetProperty("image").GetString() ?? throw new InvalidDataException("Theme image is missing.");
        var trustedRoot = Path.GetFullPath(themeRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var images = root.TryGetProperty("images", out var imagesNode) && imagesNode.ValueKind == JsonValueKind.Object
            ? imagesNode
            : default;
        string ImageName(string name) => images.ValueKind == JsonValueKind.Object
            && images.TryGetProperty(name, out var node)
            && node.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(node.GetString())
                ? node.GetString()!
                : imageName;
        var composerName = ImageName("composer");
        var homeComposerName = images.ValueKind == JsonValueKind.Object
            && images.TryGetProperty("homeComposer", out var homeComposerNode)
            && homeComposerNode.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(homeComposerNode.GetString())
                ? homeComposerNode.GetString()!
                : composerName;
        var homeName = ImageName("home");
        var polaroidName = images.ValueKind == JsonValueKind.Object
            && images.TryGetProperty("polaroid", out var polaroidNode)
            && polaroidNode.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(polaroidNode.GetString())
                ? polaroidNode.GetString()!
                : homeName;
        var imageNames = new[] { imageName, ImageName("sidebar"), composerName, homeName, homeComposerName, polaroidName };
        var loadedImages = new Dictionary<string, LoadedThemeImage>(StringComparer.OrdinalIgnoreCase);
        foreach (var imageFileName in imageNames.Distinct(StringComparer.OrdinalIgnoreCase))
            loadedImages[imageFileName] = await LoadImageAsync(themeRoot, trustedRoot, imageFileName, cancellationToken);

        var payloadTheme = JsonNode.Parse(root.GetRawText())?.AsObject()
            ?? throw new InvalidDataException("Theme root was not an object.");
        payloadTheme["imageMetadata"] = new JsonObject
        {
            ["background"] = MetadataNode(loadedImages[imageNames[0]]),
            ["sidebar"] = MetadataNode(loadedImages[imageNames[1]]),
            ["composer"] = MetadataNode(loadedImages[imageNames[2]]),
            ["home"] = MetadataNode(loadedImages[imageNames[3]]),
            ["homeComposer"] = MetadataNode(loadedImages[imageNames[4]]),
            ["polaroid"] = MetadataNode(loadedImages[imageNames[5]])
        };

        var css = await File.ReadAllTextAsync(Path.Combine(engineAssetRoot, "dream-skin.css"), cancellationToken);
        var template = await File.ReadAllTextAsync(Path.Combine(engineAssetRoot, "renderer-inject.js"), cancellationToken);
        var expression = template
            .Replace("__DREAM_CSS_JSON__", JsonSerializer.Serialize(css), StringComparison.Ordinal)
            .Replace("__DREAM_ART_JSON__", JsonSerializer.Serialize(loadedImages[imageNames[0]].DataUrl), StringComparison.Ordinal)
            .Replace("__DREAM_SIDEBAR_ART_JSON__", JsonSerializer.Serialize<string?>(imageNames[1].Equals(imageNames[0], StringComparison.OrdinalIgnoreCase) ? null : loadedImages[imageNames[1]].DataUrl), StringComparison.Ordinal)
            .Replace("__DREAM_COMPOSER_ART_JSON__", JsonSerializer.Serialize<string?>(imageNames[2].Equals(imageNames[0], StringComparison.OrdinalIgnoreCase) ? null : loadedImages[imageNames[2]].DataUrl), StringComparison.Ordinal)
            .Replace("__DREAM_HOME_ART_JSON__", JsonSerializer.Serialize<string?>(imageNames[3].Equals(imageNames[0], StringComparison.OrdinalIgnoreCase) ? null : loadedImages[imageNames[3]].DataUrl), StringComparison.Ordinal)
            .Replace("__DREAM_HOME_COMPOSER_ART_JSON__", JsonSerializer.Serialize<string?>(imageNames[4].Equals(imageNames[2], StringComparison.OrdinalIgnoreCase) ? null : loadedImages[imageNames[4]].DataUrl), StringComparison.Ordinal)
            .Replace("__DREAM_POLAROID_ART_JSON__", JsonSerializer.Serialize<string?>(imageNames[5].Equals(imageNames[3], StringComparison.OrdinalIgnoreCase) ? null : loadedImages[imageNames[5]].DataUrl), StringComparison.Ordinal)
            .Replace("__DREAM_THEME_JSON__", payloadTheme.ToJsonString(), StringComparison.Ordinal);

        if (expression.Contains("__DREAM_", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Theme payload still contains an unresolved placeholder.");
        }

        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expression))).ToLowerInvariant();
        var name = root.TryGetProperty("name", out var nameNode) ? nameNode.GetString() ?? "Codex Dream Skin" : "Codex Dream Skin";
        return new ThemePayload(expression, revision, name);
    }

    private static JsonObject MetadataNode(LoadedThemeImage image) => new()
    {
        ["width"] = image.Width,
        ["height"] = image.Height
    };

    private static async Task<LoadedThemeImage> LoadImageAsync(
        string themeRoot,
        string trustedRoot,
        string imageName,
        CancellationToken cancellationToken)
    {
        if (Path.IsPathRooted(imageName) || !string.Equals(Path.GetFileName(imageName), imageName, StringComparison.Ordinal))
            throw new InvalidDataException("Theme image must be a top-level relative file.");
        var imagePath = Path.GetFullPath(Path.Combine(themeRoot, imageName));
        if (!imagePath.StartsWith(trustedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Theme image escaped the selected theme directory.");
        var mime = Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => throw new InvalidDataException("Unsupported theme image format.")
        };
        var bytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        if (bytes.Length is < 1 or > MaximumImageBytes)
            throw new InvalidDataException("Theme image size is outside the safe range.");
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        cancellationToken.ThrowIfCancellationRequested();
        if (decoder.PixelWidth is 0 or > 16384 || decoder.PixelHeight is 0 or > 16384 ||
            (ulong)decoder.PixelWidth * decoder.PixelHeight > 50_000_000)
            throw new InvalidDataException("Theme image dimensions are outside the safe range.");
        return new($"data:{mime};base64,{Convert.ToBase64String(bytes)}", decoder.PixelWidth, decoder.PixelHeight);
    }

    public static string BuildEarlyExpression(ThemePayload payload) => $$"""
        (() => {
          const generationKey = "__CODEX_DREAM_SKIN_EARLY_GENERATION__";
          const appliedKey = "__CODEX_DREAM_SKIN_EARLY_APPLIED__";
          const generation = {{JsonSerializer.Serialize(payload.Revision)}};
          window[generationKey] = generation;
          let observer = null;
          let timeout = null;
          const stop = () => {
            observer?.disconnect();
            observer = null;
            if (timeout) clearTimeout(timeout);
            timeout = null;
          };
          const install = () => {
            if (window[generationKey] !== generation) { stop(); return true; }
            if (!document.documentElement || !document.body) return false;
            const shell = document.querySelector('main.main-surface, main[data-app-shell-main-surface]');
            const content = document.querySelector('.composer-surface-chrome, [role="main"], header[data-app-shell-application-menu-bar]');
            if (!shell || !content) return false;
            stop();
            {{payload.Expression}};
            window[appliedKey] = generation;
            return true;
          };
          if (install()) return;
          if (typeof MutationObserver === "function" && document.documentElement) {
            observer = new MutationObserver(install);
            observer.observe(document.documentElement, { childList: true, subtree: true });
          }
          timeout = setTimeout(stop, 10000);
        })()
        """;
}
