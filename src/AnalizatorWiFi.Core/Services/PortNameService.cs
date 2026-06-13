namespace AnalizatorWiFi.Core.Services;

public static class PortNameService
{
    private static readonly Dictionary<int, string> _ports = new()
    {
        [20] = "FTP-Data", [21] = "FTP", [22] = "SSH", [23] = "Telnet",
        [25] = "SMTP", [53] = "DNS", [67] = "DHCP", [80] = "HTTP",
        [110] = "POP3", [119] = "NNTP", [123] = "NTP", [143] = "IMAP",
        [194] = "IRC", [389] = "LDAP", [443] = "HTTPS", [445] = "SMB",
        [465] = "SMTPS", [500] = "IKE", [587] = "SMTP-Sub", [636] = "LDAPS",
        [993] = "IMAPS", [995] = "POP3S", [1194] = "OpenVPN", [1433] = "MSSQL",
        [1723] = "PPTP", [3306] = "MySQL", [3389] = "RDP", [5201] = "iperf3",
        [5222] = "XMPP", [5432] = "PostgreSQL", [6379] = "Redis",
        [6881] = "BitTorrent", [8080] = "HTTP-Alt", [8443] = "HTTPS-Alt",
        [9200] = "Elasticsearch", [27017] = "MongoDB", [51820] = "WireGuard",
    };

    public static string GetName(int port)
        => _ports.TryGetValue(port, out var name) ? name : string.Empty;
}
