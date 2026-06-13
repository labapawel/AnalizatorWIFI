using System.Net;
using System.Runtime.InteropServices;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Platform.Windows;

public sealed class WindowsTcpConnectionReader : ITcpConnectionReader
{
    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        nint pTcpTable, ref int pdwSize, bool bOrder,
        int ulAf, int TableClass, uint Reserved);

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    private const int AF_INET = 2;
    private const int TCP_TABLE_OWNER_PID_ALL = 5;

    private static readonly string[] StateNames =
    [
        "", "CLOSED", "LISTEN", "SYN_SENT", "SYN_RCVD",
        "ESTABLISHED", "FIN_WAIT1", "FIN_WAIT2", "CLOSE_WAIT",
        "CLOSING", "LAST_ACK", "TIME_WAIT", "DELETE_TCB"
    ];

    public Task<IReadOnlyList<TcpConnectionEntry>> GetConnectionsAsync(CancellationToken ct = default)
        => Task.FromResult(ReadTable());

    private static IReadOnlyList<TcpConnectionEntry> ReadTable()
    {
        int size = 0;
        GetExtendedTcpTable(nint.Zero, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
        if (size == 0) return [];

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buffer, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0) != 0)
                return [];

            int count = Marshal.ReadInt32(buffer);
            var results = new List<TcpConnectionEntry>(count);
            int rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            nint ptr = buffer + 4;

            for (int i = 0; i < count; i++, ptr += rowSize)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(ptr);
                uint s = row.State;
                string state = s < (uint)StateNames.Length ? StateNames[s] : $"ST{s}";

                results.Add(new TcpConnectionEntry
                {
                    Protocol = "TCP",
                    LocalAddress = ToIp(row.LocalAddr),
                    LocalPort = ToPort(row.LocalPort),
                    RemoteAddress = ToIp(row.RemoteAddr),
                    RemotePort = ToPort(row.RemotePort),
                    State = state,
                    ProcessId = (int)row.OwningPid,
                });
            }

            return results;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string ToIp(uint addr)
    {
        var b = new byte[4];
        b[0] = (byte)(addr & 0xFF);
        b[1] = (byte)((addr >> 8) & 0xFF);
        b[2] = (byte)((addr >> 16) & 0xFF);
        b[3] = (byte)((addr >> 24) & 0xFF);
        return new IPAddress(b).ToString();
    }

    private static int ToPort(uint port)
        => (int)(((port & 0xFF) << 8) | ((port >> 8) & 0xFF));
}
