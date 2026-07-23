using Windows.Graphics.Imaging;
using Windows.Storage;

namespace CodexDreamSkin.Services;

public sealed record ImagePaletteAnalysis(
    string DominantHex,
    string AccentHex,
    double AverageLuminance,
    double SkinPixelRatio,
    bool UsedSkinAvoidance);

public sealed record ThemePaletteSuggestion(
    string Accent,
    double LightPage,
    double LightSidebar,
    double LightComposer,
    double LightCard,
    double DarkPage,
    double DarkSidebar,
    double DarkComposer,
    double DarkCard);

public sealed class ThemePaletteService
{
    public async Task<ImagePaletteAnalysis> AnalyzeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var scale = Math.Min(1d, 64d / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var transform = new BitmapTransform
        {
            ScaledWidth = Math.Max(1u, (uint)Math.Round(decoder.PixelWidth * scale)),
            ScaledHeight = Math.Max(1u, (uint)Math.Round(decoder.PixelHeight * scale))
        };
        var pixels = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = pixels.DetachPixelData();
        return await Task.Run(() => AnalyzePixels(bytes, cancellationToken), cancellationToken);
    }

    public static ThemePaletteSuggestion CreateSuggestion(ImagePaletteAnalysis analysis, string scheme)
    {
        var lightBoost = Math.Clamp((0.52 - analysis.AverageLuminance) * 0.28, -0.06, 0.12);
        var darkBoost = Math.Clamp((analysis.AverageLuminance - 0.42) * 0.30, -0.05, 0.14);
        ThemePaletteSuggestion suggestion = scheme switch
        {
            "midnight" => new ThemePaletteSuggestion(
                analysis.AccentHex,
                0.62 + lightBoost, 0.66 + lightBoost, 0.56 + lightBoost, 0.24 + lightBoost / 2,
                0.74 + darkBoost, 0.80 + darkBoost, 0.70 + darkBoost, 0.48 + darkBoost / 2),
            "lowfog" => new ThemePaletteSuggestion(
                analysis.AccentHex,
                0.38 + lightBoost, 0.42 + lightBoost, 0.34 + lightBoost, 0.12 + lightBoost / 2,
                0.50 + darkBoost, 0.56 + darkBoost, 0.46 + darkBoost, 0.24 + darkBoost / 2),
            _ => new ThemePaletteSuggestion(
                analysis.AccentHex,
                0.54 + lightBoost, 0.58 + lightBoost, 0.48 + lightBoost, 0.18 + lightBoost / 2,
                0.66 + darkBoost, 0.72 + darkBoost, 0.60 + darkBoost, 0.40 + darkBoost / 2)
        };
        return suggestion with
        {
            LightPage = ClampOpacity(scheme == "midnight" ? 0.62 + lightBoost : scheme == "lowfog" ? 0.38 + lightBoost : 0.54 + lightBoost),
            LightSidebar = ClampOpacity(scheme == "midnight" ? 0.66 + lightBoost : scheme == "lowfog" ? 0.42 + lightBoost : 0.58 + lightBoost),
            LightComposer = ClampOpacity(scheme == "midnight" ? 0.56 + lightBoost : scheme == "lowfog" ? 0.34 + lightBoost : 0.48 + lightBoost),
            LightCard = ClampOpacity(scheme == "midnight" ? 0.24 + lightBoost / 2 : scheme == "lowfog" ? 0.12 + lightBoost / 2 : 0.18 + lightBoost / 2),
            DarkPage = ClampOpacity(scheme == "midnight" ? 0.74 + darkBoost : scheme == "lowfog" ? 0.50 + darkBoost : 0.66 + darkBoost),
            DarkSidebar = ClampOpacity(scheme == "midnight" ? 0.80 + darkBoost : scheme == "lowfog" ? 0.56 + darkBoost : 0.72 + darkBoost),
            DarkComposer = ClampOpacity(scheme == "midnight" ? 0.70 + darkBoost : scheme == "lowfog" ? 0.46 + darkBoost : 0.60 + darkBoost),
            DarkCard = ClampOpacity(scheme == "midnight" ? 0.48 + darkBoost / 2 : scheme == "lowfog" ? 0.24 + darkBoost / 2 : 0.40 + darkBoost / 2)
        };
    }

    private static ImagePaletteAnalysis AnalyzePixels(byte[] pixels, CancellationToken cancellationToken)
    {
        var bins = new Dictionary<int, (double Weight, long Red, long Green, long Blue, int Count)>();
        var skinPixels = 0;
        var visiblePixels = 0;
        var luminanceTotal = 0d;
        for (var index = 0; index + 3 < pixels.Length; index += 4)
        {
            if ((index & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
            var blue = pixels[index];
            var green = pixels[index + 1];
            var red = pixels[index + 2];
            var alpha = pixels[index + 3];
            if (alpha < 32) continue;
            visiblePixels++;
            var luminance = RelativeLuminance(red, green, blue);
            luminanceTotal += luminance;
            if (LooksLikeSkin(red, green, blue))
            {
                skinPixels++;
                continue;
            }
            var maximum = Math.Max(red, Math.Max(green, blue));
            var minimum = Math.Min(red, Math.Min(green, blue));
            var saturation = maximum == 0 ? 0 : (maximum - minimum) / (double)maximum;
            var weight = 1 + saturation * 1.8 + (luminance is > 0.12 and < 0.84 ? 0.35 : 0);
            var key = (red >> 4) << 8 | (green >> 4) << 4 | (blue >> 4);
            bins.TryGetValue(key, out var bin);
            bins[key] = (bin.Weight + weight, bin.Red + red, bin.Green + green, bin.Blue + blue, bin.Count + 1);
        }

        (double Weight, long Red, long Green, long Blue, int Count) fallback = (1d, 21L, 87L, 176L, 1);
        var dominant = bins.Count == 0 ? fallback : bins.Values.OrderByDescending(bin => bin.Weight).First();
        var accent = bins.Count == 0
            ? fallback
            : bins.Values.OrderByDescending(bin => bin.Weight * (1 + Saturation(bin) * 2.4) * ContrastFitness(bin)).First();
        var dominantColor = Average(dominant);
        var accentColor = EnsureCoolAccent(Average(accent));
        return new ImagePaletteAnalysis(
            ToHex(dominantColor),
            ToHex(accentColor),
            visiblePixels == 0 ? 0.5 : luminanceTotal / visiblePixels,
            visiblePixels == 0 ? 0 : skinPixels / (double)visiblePixels,
            skinPixels > 0);
    }

    private static (byte Red, byte Green, byte Blue) Average((double Weight, long Red, long Green, long Blue, int Count) value) =>
        ((byte)(value.Red / Math.Max(1, value.Count)), (byte)(value.Green / Math.Max(1, value.Count)), (byte)(value.Blue / Math.Max(1, value.Count)));

    private static double Saturation((double Weight, long Red, long Green, long Blue, int Count) value)
    {
        var color = Average(value);
        var maximum = Math.Max(color.Red, Math.Max(color.Green, color.Blue));
        var minimum = Math.Min(color.Red, Math.Min(color.Green, color.Blue));
        return maximum == 0 ? 0 : (maximum - minimum) / (double)maximum;
    }

    private static double ContrastFitness((double Weight, long Red, long Green, long Blue, int Count) value)
    {
        var color = Average(value);
        var luminance = RelativeLuminance(color.Red, color.Green, color.Blue);
        return 0.45 + Math.Abs(luminance - 0.5);
    }

    private static bool LooksLikeSkin(byte red, byte green, byte blue)
    {
        var maximum = Math.Max(red, Math.Max(green, blue));
        var minimum = Math.Min(red, Math.Min(green, blue));
        return red > 92 && green > 38 && blue > 18 && maximum - minimum > 15
            && Math.Abs(red - green) > 12 && red > green && red > blue;
    }

    private static (byte Red, byte Green, byte Blue) EnsureCoolAccent((byte Red, byte Green, byte Blue) color)
    {
        if (color.Red > color.Blue * 1.12 && color.Red > color.Green * 1.08)
            return ((byte)Math.Min((int)color.Red, 54), (byte)Math.Max((int)color.Green, 118), (byte)Math.Max((int)color.Blue, 188));
        return color;
    }

    private static double RelativeLuminance(byte red, byte green, byte blue)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(red) + 0.7152 * Linear(green) + 0.0722 * Linear(blue);
    }

    private static string ToHex((byte Red, byte Green, byte Blue) color) => $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    private static double ClampOpacity(double value) => Math.Clamp(value, 0.04, 0.92);
}
