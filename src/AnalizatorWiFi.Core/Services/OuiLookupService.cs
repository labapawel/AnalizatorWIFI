using System.IO.Compression;
using AnalizatorWiFi.Core.Interfaces;

namespace AnalizatorWiFi.Core.Services;

public sealed class OuiLookupService : IOuiLookup
{
    private readonly Dictionary<string, string> _table = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public void LoadFromStream(Stream stream)
    {
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length < 8 || line[0] == '#') continue;
            // Format: XX:XX:XX\tVendor Name  OR  XX-XX-XX/28\tVendor
            string prefix = line[..8].Replace("-", ":").Replace("/", ":").ToUpperInvariant();
            if (prefix.Length >= 8)
            {
                string key = prefix[..8]; // XX:XX:XX
                string vendor = line.Length > 9 ? line[9..].Trim() : string.Empty;
                _table.TryAdd(key, vendor);
            }
        }
        _loaded = true;
    }

    public string GetVendor(string macAddress)
    {
        if (!_loaded || string.IsNullOrEmpty(macAddress)) return string.Empty;

        string normalized = macAddress.Replace("-", ":").ToUpperInvariant();
        if (normalized.Length < 8) return string.Empty;

        string key = normalized[..8];
        return _table.TryGetValue(key, out string? vendor) ? vendor : string.Empty;
    }
}
