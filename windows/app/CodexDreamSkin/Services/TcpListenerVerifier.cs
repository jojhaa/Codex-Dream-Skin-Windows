using System.ComponentModel;
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
                var path = ProcessPathResolver.TryGetPath(row.ProcessId);
                if (path is null)
                {
                    rejected ??= new(true, null, row.ProcessId, null, installation.ExecutablePath, "无法读取监听进程的完整路径。");
                }
                else if (string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(installation.ExecutablePath),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return new(true, row.ProcessId, row.ProcessId, path, installation.ExecutablePath, "可信监听器。");
                }
                else rejected ??= new(true, null, row.ProcessId, path, installation.ExecutablePath, "监听进程路径与已注册 Codex 不一致。");
            }
            catch (Exception error)
            {
                rejected ??= new(true, null, row.ProcessId, null, installation.ExecutablePath, $"监听进程验证失败：{error.Message}");
            }
        }

        return rejected ?? new(true, null, matches[0].ProcessId, null, installation.ExecutablePath, "未找到可信监听器。");
    }

    public bool IsOccupied(int port) => ReadIpv4Listeners().Any(row => row.Port == port);

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
