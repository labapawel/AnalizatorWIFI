using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.UI.Services;

/// <summary>
/// Singleton localization service.
/// XAML binding: {Binding [key], Source={x:Static loc:LocalizationService.Instance}}
/// Language switch fires PropertyChanged("Item[]") — all indexer bindings refresh automatically.
/// </summary>
public sealed partial class LocalizationService : ObservableObject
{
    // ── Singleton ────────────────────────────────────────────────────────────
    public static LocalizationService Instance { get; } = new();

    // ── State ─────────────────────────────────────────────────────────────────
    private Dictionary<string, string> _strings = new(StringComparer.Ordinal);

    public LanguageInfo Current { get; private set; } = new("pl", "Polski");
    public IReadOnlyList<LanguageInfo> Available { get; private set; } = [];

    public event Action? LanguageChanged;

    // ── Indexer — used by XAML bindings ──────────────────────────────────────
    public string this[string key] =>
        _strings.TryGetValue(key, out var v) ? v : key;

    // ── Initialisation ────────────────────────────────────────────────────────
    /// <summary>Scans langDir for *.json files and builds the Available list.</summary>
    public void Discover(string langDir)
    {
        if (!Directory.Exists(langDir)) return;

        var langs = new List<LanguageInfo>();
        foreach (var file in Directory.GetFiles(langDir, "*.json").OrderBy(f => f))
        {
            string code = Path.GetFileNameWithoutExtension(file);
            string nativeName = ResolveNativeName(code, file);
            langs.Add(new LanguageInfo(code, nativeName));
        }
        Available = langs;
    }

    /// <summary>Loads a language by code.  Falls back silently if the file is missing.</summary>
    public void Load(string code)
    {
        string langDir  = Path.Combine(AppContext.BaseDirectory, "lang");
        string filePath = Path.Combine(langDir, $"{code}.json");

        if (!File.Exists(filePath))
        {
            // Try to discover first (in case Discover wasn't called yet)
            if (!Available.Any())
                Discover(langDir);
            if (!File.Exists(filePath)) return;
        }

        LoadFile(filePath, code);
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private void LoadFile(string path, string code)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            if (dict is null) return;

            _strings = new Dictionary<string, string>(dict, StringComparer.Ordinal);

            string nativeName = dict.TryGetValue("_nativeName", out var n) ? n : code;
            Current = new LanguageInfo(code, nativeName);

            // Notify all indexer bindings in XAML to re-evaluate
            OnPropertyChanged("Item[]");
            LanguageChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Loc] Failed to load {path}: {ex.Message}");
        }
    }

    private static string ResolveNativeName(string code, string filePath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            if (doc.RootElement.TryGetProperty("_nativeName", out var el))
                return el.GetString() ?? code;
        }
        catch { }

        return code switch
        {
            "pl" => "Polski",
            "en" => "English",
            "de" => "Deutsch",
            "fr" => "Français",
            "es" => "Español",
            "it" => "Italiano",
            "ru" => "Русский",
            "cs" => "Čeština",
            "uk" => "Українська",
            _    => code
        };
    }
}
