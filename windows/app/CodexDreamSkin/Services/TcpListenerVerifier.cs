using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using CodexDreamSkin.Models;

namespace CodexDreamSkin.Services;

public sealed class TcpListenerVerifier
{
    private const int AddressFamilyInterNetwork = 2;
    private const uint ErrorInsufficientBuffer = 122;

    public int? GetTrustedLoopbackOwner(int port, CodexInstallation installation) =>
        Inspect(port, installation).TrustedProcessId;

    public ListenerInspection Inspect(int port, CodexInstallation installation)
    {
        var matches = ReadIpv4Listeners().Where(row => row.Port == port).ToArray();
        if (matches.Length == 0)
            return new(false, null, null, null, installation.ExecutablePath, "端口未被占用。");

        ListenerInspection? rejected = null;
        foreach (var row in matches)
        {
            if (!row.Address.Equals(IPAddress.Loopback))
            {
                rejected ??= new(true, null, row.ProcessId, row.Address.ToString(), installation.ExecutablePath, "监听地址不是 127.0.0.1。");
                continue;
            }

            try
            {
                var identity = ProcessPathResolver.TryGetIdentity(row.ProcessId);
                if (identity is null)
                {
                    rejected ??= new(true, null, row.ProcessId, null, installation.ExecutablePath, "无法读取监听进程身份。");
                }
                else if (identity.ExecutablePath is not null && string.Equals(
                    Path.GetFullPath(identity.ExecutablePath),
                    Path.GetFullPath(installation.ExecutablePath),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return new(true, row.ProcessId, row.ProcessId, identity.ExecutablePath, installation.ExecutablePath, "可信监听器（当前注册路径）。");
                }
                else if (string.Equals(
                    identity.PackageFullName,
                    installation.PackageFullName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return new(true, row.ProcessId, row.ProcessId, identity.ExecutablePath, installation.ExecutablePath, "可信监听器（当前动态 Store 包身份）。");
                }
                else rejected ??= new(
                    true,
                    null,
                    row.ProcessId,
                    identity.ExecutablePath,
                    installation.ExecutablePath,
                    $"监听进程属于 {identity.PackageFullName ?? identity.PackageFamilyName ?? "未知包"}，当前系统动态注册的是 {installation.PackageFullName}。");
            }
            catch (Exception error)
            {
                rejected ??= new(true, null, row.ProcessId, null, installation.ExecutablePath, $"监听进程验证失败：{error.Message}");
            }
        }

        return rejected ?? new(true, null, matches[0].ProcessId, null, installation.ExecutablePath, "未找到可信监听器。");
    }

    public bool IsOccupied(int port) => ReadIpv4Listeners().Any(row => row.Port == port);

    public IReadOnlyList<ManagedPortInspection> InspectManagedPorts(
        int firstPort,
        int lastPort,
        CodexInstallation installation)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(firstPort);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lastPort, 65535);
        if (lastPort < firstPort) throw new ArgumentOutOfRangeException(nameof(lastPort));

        var listeners = ReadIpv4Listeners()
            .Where(row => row.Port >= firstPort && row.Port <= lastPort)
            .GroupBy(row => row.Port)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var results = new List<ManagedPortInspection>();
        for (var port = firstPort; port <= lastPort; port++)
        {
            if (!listeners.TryGetValue(port, out var rows))
            {
                results.Add(new(port, null, "127.0.0.1", null, null, ManagedPortKind.Free, false));
                continue;
            }

            foreach (var row in rows.OrderByDescending(candidate => candidate.Address.Equals(IPAddress.Loopback)))
            {
                var identity = ProcessPathResolver.TryGetIdentity(row.ProcessId);
                var currentPath = identity?.ExecutablePath is not null && string.Equals(
                    Path.GetFullPath(identity.ExecutablePath),
                    Path.GetFullPath(installation.ExecutablePath),
                    StringComparison.OrdinalIgnoreCase);
                var currentPackage = string.Equals(
                    identity?.PackageFullName,
                    installation.PackageFullName,
                    StringComparison.OrdinalIgnoreCase);
                var codexFamily = string.Equals(
                    identity?.PackageFamilyName,
                    installation.PackageFamilyName,
                    StringComparison.OrdinalIgnoreCase);
                var kind = !row.Address.Equals(IPAddress.Loopback)
                    ? ManagedPortKind.NonLoopback
                    : currentPath || currentPackage
                        ? ManagedPortKind.CurrentCodex
                        : codexFamily
                            ? ManagedPortKind.PreviousCodex
                            : identity is null
                                ? ManagedPortKind.Unreadable
                                : ManagedPortKind.OtherProcess;
                results.Add(new(
                    port,
                    row.ProcessId,
                    row.Address.ToString(),
                    identity?.ExecutablePath,
                    identity?.PackageFullName,
                    kind,
                    kind is ManagedPortKind.CurrentCodex or ManagedPortKind.PreviousCodex));
            }
        }
        return results;
    }

    public async Task<bool> TerminateManagedListenerAsync(
        int port,
        int expectedProcessId,
        CodexInstallation installation,
        CancellationToken cancellationToken = default)
    {
        var current = InspectManagedPorts(port, port, installation)
            .FirstOrDefault(item => item.ProcessId == expectedProcessId);
        if (current is null || !current.CanTerminate)
            throw new InvalidOperationException("端口归属已变化，或该进程不是允许关闭的官方 Codex 进程。");

        try
        {
            using var process = Process.GetProcessById(expectedProcessId);
            if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
            {
                process.CloseMainWindow();
                await WaitForExitAsync(process, TimeSpan.FromSeconds(3), cancellationToken);
            }
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await WaitForExitAsync(process, TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
        catch (Exception error) when (
            error is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // Store-owned processes can remain queryable through package APIs while
            // System.Diagnostics cannot open them for termination. The elevated
            // fallback below still runs only after the exact port/PID/package recheck.
        }

        if (ReadIpv4Listeners().Any(row => row.Port == port && row.ProcessId == expectedProcessId))
            await TerminateWithElevationAsync(expectedProcessId, cancellationToken);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReadIpv4Listeners().Any(row => row.Port == port && row.ProcessId == expectedProcessId))
                return true;
            await Task.Delay(120, cancellationToken);
        }
        return false;
    }

    private static async Task TerminateWithElevationAsync(int processId, CancellationToken cancellationToken)
    {
        var taskkill = Path.Combine(Environment.SystemDirectory, "taskkill.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = taskkill,
            Arguments = $"/PID {processId} /T /F",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        using var elevated = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动管理员进程关闭程序。");
        await elevated.WaitForExitAsync(cancellationToken);
        if (elevated.ExitCode != 0)
            throw new Win32Exception(elevated.ExitCode, $"管理员进程关闭程序返回代码 {elevated.ExitCode}。");
    }

    private static async Task WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (process.HasExited) return;
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        try { await process.WaitForExitAsync(timeoutCancellation.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
    }

    private static IReadOnlyList<TcpRow> ReadIpv4Listeners()
    {
        uint size = 0;
        var first = GetExtendedTcpTable(IntPtr.Zero, ref size, false, AddressFamilyInterNetwork, TcpTableClass.OwnerPidListener, 0);
        if (first != ErrorInsufficientBuffer)
        {
            throw new Win32Exception((int)first);
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var result = GetExtendedTcpTable(buffer, ref size, false, AddressFamilyInterNetwork, TcpTableClass.OwnerPidListener, 0);
            if (result != 0)
            {
                throw new Win32Exception((int)result);
            }

            var count = Marshal.ReadInt32(buffer);
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            var rows = new List<TcpRow>(count);
            var cursor = IntPtr.Add(buffer, sizeof(uint));
            for (var index = 0; index < count; index++)
            {
                var native = Marshal.PtrToStructure<MibTcpRowOwnerPid>(cursor);
                // MIB_TCPROW_OWNER_PID exposes the IPv4 address as four network-order
                // bytes stored in a little-endian DWORD.  IPAddress(uint) interprets
                // that DWORD numerically and reverses 127.0.0.1 into 1.0.0.127.
                var address = new IPAddress(BitConverter.GetBytes(native.LocalAddress));
                var port = (int)(((native.LocalPort & 0xFF) << 8) | ((native.LocalPort >> 8) & 0xFF));
                rows.Add(new TcpRow(address, port, checked((int)native.OwningPid)));
                cursor = IntPtr.Add(cursor, rowSize);
            }

            return rows;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private sealed record TcpRow(IPAddress Address, int Port, int ProcessId);

    public sealed record ListenerInspection(
        bool IsOccupied,
        int? TrustedProcessId,
        int? ObservedProcessId,
        string? ObservedPath,
        string ExpectedPath,
        string Reason);

    public sealed record ManagedPortInspection(
        int Port,
        int? ProcessId,
        string Address,
        string? ExecutablePath,
        string? PackageFullName,
        ManagedPortKind Kind,
        bool CanTerminate);

    public enum ManagedPortKind
    {
        Free,
        CurrentCodex,
        PreviousCodex,
        OtherProcess,
        NonLoopback,
        Unreadable
    }

    private enum TcpTableClass
    {
        OwnerPidListener = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddress;
        public uint LocalPort;
        public uint RemoteAddress;
        public uint RemotePort;
        public uint OwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable,
        ref uint size,
        [MarshalAs(UnmanagedType.Bool)] bool order,
        int addressFamily,
        TcpTableClass tableClass,
        uint reserved);
}
