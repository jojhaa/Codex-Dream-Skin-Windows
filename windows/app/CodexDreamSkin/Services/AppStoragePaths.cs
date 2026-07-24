using Windows.Storage;

namespace CodexDreamSkin.Services;

internal static class AppStoragePaths
{
    private static readonly Lazy<string> LocalRootValue = new(ResolveLocalRoot);
    private static readonly Lazy<string> TemporaryRootValue = new(ResolveTemporaryRoot);

    public static string LocalRoot => LocalRootValue.Value;

    public static string TemporaryRoot => TemporaryRootValue.Value;

    private static string ResolveLocalRoot()
    {
        try
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
        catch
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexDreamSkin",
                "Manager");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    private static string ResolveTemporaryRoot()
    {
        try
        {
            return ApplicationData.Current.TemporaryFolder.Path;
        }
        catch
        {
            var path = Path.Combine(Path.GetTempPath(), "CodexDreamSkin", "Manager");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
