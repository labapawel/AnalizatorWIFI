using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Core.Services;

public sealed class GeoLocationService : IDisposable
{
    private sealed class ApiItem
    {
        [JsonPropertyName("status")]   public string Status      { get; set; } = "";
        [JsonPropertyName("query")]    public string? Query      { get; set; }
        [JsonPropertyName("country")]  public string? Country    { get; set; }
        [JsonPropertyName("countryCode")] public string? CountryCode { get; set; }
        [JsonPropertyName("lat")]      public double Lat         { get; set; }
        [JsonPropertyName("lon")]      public double Lon         { get; set; }
    }

    private readonly ConcurrentDictionary<string, GeoLocationResult?> _cache = new();
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public GeoLocationService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AnalizatorWiFi/1.0");
    }

    public GeoLocationResult? TryGet(string ip)
        => _cache.TryGetValue(ip, out var r) ? r : null;

    public async Task LookupAsync(IEnumerable<string> ips, CancellationToken ct = default)
    {
        var toFetch = ips
            .Where(ip => !IsPrivate(ip) && !_cache.ContainsKey(ip))
            .Distinct()
            .ToList();

        if (toFetch.Count == 0) return;

        for (int i = 0; i < toFetch.Count; i += 100)
        {
            var chunk = toFetch.Skip(i).Take(100).ToList();
            await FetchChunkAsync(chunk, ct);
        }
    }

    private async Task FetchChunkAsync(List<string> ips, CancellationToken ct)
    {
        try
        {
            var body = JsonSerializer.Serialize(ips.Select(ip => new { query = ip }));
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(
                "http://ip-api.com/batch?fields=status,country,countryCode,lat,lon,query",
                content, ct);
            if (!resp.IsSuccessStatusCode) return;

            var json = await resp.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<ApiItem[]>(json, _json);
            if (items is null) return;

            foreach (var item in items)
            {
                if (item.Status == "success" && item.Query != null)
                    _cache[item.Query] = new GeoLocationResult(
                        item.Country ?? "", item.CountryCode ?? "", item.Lat, item.Lon);
                else if (item.Query != null)
                    _cache[item.Query] = null;
            }
        }
        catch { /* network offline or rate-limited — silently skip */ }
    }

    public static bool IsPrivate(string ip)
    {
        if (!IPAddress.TryParse(ip, out var addr)) return true;
        var b = addr.GetAddressBytes();
        if (b.Length != 4) return false;
        return b[0] == 0 || b[0] == 10 || b[0] == 127 ||
               (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
               (b[0] == 192 && b[1] == 168) ||
               (b[0] == 169 && b[1] == 254); // APIPA
    }

    public void Dispose() => _http.Dispose();
}
