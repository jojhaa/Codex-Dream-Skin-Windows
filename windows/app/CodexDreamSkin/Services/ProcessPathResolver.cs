using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace CodexDreamSkin.Services;

internal static class ProcessPathResolver
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ErrorInsufficientBuffer = 122;

    public static string? TryGetPath(int processId)
        => TryGetIdentity(processId)?.ExecutablePath;

    public static ProcessIdentity? TryGetIdentity(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var capacity = 32768;
            var buffer = new StringBuilder(capacity);
            var path = QueryFullProcessImageName(handle, 0, buffer, ref capacity) ? buffer.ToString() : null;
            return new(path, ReadPackageName(handle, GetPackageFullName), ReadPackageName(handle, GetPackageFamilyName));
        }
        finally { CloseHandle(handle); }
    }

    private static string? ReadPackageName(IntPtr process, PackageNameReader reader)
    {
        uint length = 0;
        if (reader(process, ref length, null) != ErrorInsufficientBuffer || length == 0) return null;
        var buffer = new StringBuilder(checked((int)length));
        return reader(process, ref length, buffer) == 0 ? buffer.ToString() : null;
    }

    internal sealed record ProcessIdentity(
        string? ExecutablePath,
        string? PackageFullName,
        string? PackageFamilyName);

    private delegate int PackageNameReader(IntPtr process, ref uint length, StringBuilder? packageName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder executableName, ref int size);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackageFullName(IntPtr process, ref uint packageFullNameLength, StringBuilder? packageFullName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackageFamilyName(IntPtr process, ref uint packageFamilyNameLength, StringBuilder? packageFamilyName);
}
