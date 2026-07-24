using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace CodexDreamSkin.Services;

public sealed record ReleaseCheckResult(
    string CurrentVersion,
    string LatestTag,
    bool IsUpdateAvailable,
    Uri ReleaseUri,
    DateTimeOffset? PublishedAt);

public sealed class ReleaseCheckService
{
    public const string LatestReleaseEndpoint =
        "https://api.github.com/repos/jojhaa/Codex-Dream-Skin-Windows/releases/latest";

    public const string ReleasesPage =
        "https://github.com/jojhaa/Codex-Dream-Skin-Windows/releases";

    private static readonly HttpClient Client = CreateClient();

    public static string CurrentVersionLabel
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
        }
    }

    public async Task<ReleaseCheckResult> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await Client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var latestTag = root.GetProperty("tag_name").GetString();
        if (string.IsNullOrWhiteSpace(latestTag))
        {
            throw new InvalidDataException("GitHub 最新发布缺少版本标签。");
        }

        var latestVersion = ParseVersion(latestTag);
        var currentVersion = ParseVersion(CurrentVersionLabel);
        var releaseUri = TryGetTrustedReleaseUri(root, out var trustedUri)
            ? trustedUri
            : new Uri(ReleasesPage);
        DateTimeOffset? publishedAt = null;
        if (root.TryGetProperty("published_at", out var publishedElement) &&
            publishedElement.ValueKind is JsonValueKind.String &&
            publishedElement.TryGetDateTimeOffset(out var parsedPublishedAt))
        {
            publishedAt = parsedPublishedAt;
        }

        return new ReleaseCheckResult(
            CurrentVersionLabel,
            latestTag.Trim(),
            latestVersion.CompareTo(currentVersion) > 0,
            releaseUri,
            publishedAt);
    }

    internal static Version ParseVersion(string value)
    {
        var normalized = value.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(normalized, out var parsed))
        {
            throw new InvalidDataException($"无法识别版本标签：{value}");
        }

        return new Version(
            parsed.Major,
            parsed.Minor,
            Math.Max(parsed.Build, 0),
            Math.Max(parsed.Revision, 0));
    }

    private static bool TryGetTrustedReleaseUri(JsonElement root, out Uri uri)
    {
        uri = null!;
        if (!root.TryGetProperty("html_url", out var urlElement) ||
            urlElement.ValueKind is not JsonValueKind.String ||
            !Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var candidate) ||
            candidate.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(candidate.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !candidate.AbsolutePath.StartsWith(
                "/jojhaa/Codex-Dream-Skin-Windows/releases/",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CodexDreamSkin/0.3.4");
        return client;
    }
}
