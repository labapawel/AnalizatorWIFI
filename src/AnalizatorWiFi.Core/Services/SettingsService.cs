using System.Text.Json;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _filePath;

    public AppSettings Current { get; private set; } = new();

    public SettingsService(string filePath)
    {
        _filePath = filePath;
    }

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath)) { SetDefaults(); return; }
            await using var stream = File.OpenRead(_filePath);
            Current = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOpts) ?? new AppSettings();
            SetDefaults();
        }
        catch { SetDefaults(); }
    }

    public async Task SaveAsync()
    {
        string dir = Path.GetDirectoryName(_filePath)!;
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, Current, JsonOpts);
    }

    private void SetDefaults()
    {
        if (string.IsNullOrEmpty(Current.HistoryFilePath))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            Current.HistoryFilePath = Path.Combine(appData, "AnalizatorWiFi", "history.db");
        }
    }
}
