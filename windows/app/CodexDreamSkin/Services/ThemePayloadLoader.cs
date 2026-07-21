using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CodexDreamSkin.Services;

public sealed record ThemePayload(string Expression, string Revision, string ThemeName);

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
        if (Path.IsPathRooted(imageName))
        {
            throw new InvalidDataException("Theme image must be relative.");
        }

        var imagePath = Path.GetFullPath(Path.Combine(themeRoot, imageName));
        var trustedRoot = Path.GetFullPath(themeRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!imagePath.StartsWith(trustedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Theme image escaped the bundled theme directory.");
        }

        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        var mime = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => throw new InvalidDataException("Unsupported theme image format.")
        };

        var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        if (imageBytes.Length is < 1 or > MaximumImageBytes)
        {
            throw new InvalidDataException("Theme image size is outside the safe range.");
        }

        var css = await File.ReadAllTextAsync(Path.Combine(engineAssetRoot, "dream-skin.css"), cancellationToken);
        var template = await File.ReadAllTextAsync(Path.Combine(engineAssetRoot, "renderer-inject.js"), cancellationToken);
        var artDataUrl = $"data:{mime};base64,{Convert.ToBase64String(imageBytes)}";
        var expression = template
            .Replace("__DREAM_CSS_JSON__", JsonSerializer.Serialize(css), StringComparison.Ordinal)
            .Replace("__DREAM_ART_JSON__", JsonSerializer.Serialize(artDataUrl), StringComparison.Ordinal)
            .Replace("__DREAM_THEME_JSON__", root.GetRawText(), StringComparison.Ordinal);

        if (expression.Contains("__DREAM_", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Theme payload still contains an unresolved placeholder.");
        }

        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expression))).ToLowerInvariant();
        var name = root.TryGetProperty("name", out var nameNode) ? nameNode.GetString() ?? "Codex Dream Skin" : "Codex Dream Skin";
        return new ThemePayload(expression, revision, name);
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
            const shell = document.querySelector('main.main-surface');
            const sidebar = document.querySelector('aside.app-shell-left-panel');
            if (!shell || !sidebar) return false;
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
