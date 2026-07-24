namespace CodexDreamSkin.Models;

internal static class FreeSoftwareNotice
{
    public const string ProjectUrl = "https://github.com/jojhaa/Codex-Dream-Skin-Windows";

    public const string Chinese =
        "本软件永久免费、开源。若您通过付费渠道获得，请立即申请退款。请勿相信任何收费销售、捆绑下载或二次售卖。";

    public const string English =
        "This software is permanently free and open source. If you paid to obtain it, request a refund immediately. Do not trust paid sales, bundled downloads, or resellers.";

    public static string ForCurrentLanguage() =>
        System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith(
            "zh",
            StringComparison.OrdinalIgnoreCase)
            ? Chinese
            : English;

    public static bool IsCanonical(string? text) =>
        string.Equals(text, Chinese, StringComparison.Ordinal) ||
        string.Equals(text, English, StringComparison.Ordinal);
}
