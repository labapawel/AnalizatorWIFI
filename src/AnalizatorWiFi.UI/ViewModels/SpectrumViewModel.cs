using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnalizatorWiFi.Core.Models;
using AnalizatorWiFi.Core.Services;

namespace AnalizatorWiFi.UI.ViewModels;

public partial class SpectrumViewModel : ViewModelBase
{
    // ── Spectrum data ─────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<SpectrumEntry> _entries24 = [];
    [ObservableProperty] private ObservableCollection<SpectrumEntry> _entries5 = [];
    [ObservableProperty] private ObservableCollection<SpectrumEntry> _entries6 = [];
    [ObservableProperty] private string _selectedBand = "2.4 GHz";

    public ObservableCollection<string> Bands { get; } = ["2.4 GHz", "5 GHz", "6 GHz"];
    public ObservableCollection<SpectrumEntry> CurrentEntries =>
        SelectedBand switch { "5 GHz" => Entries5, "6 GHz" => Entries6, _ => Entries24 };

    // ── Channel / frequency selection ─────────────────────────────────────────
    public ObservableCollection<int> ChannelOptions { get; } = [];

    [ObservableProperty] private int? _selectedChannel;
    [ObservableProperty] private double _selectedFrequencyMhz;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private string _selectionInfo = string.Empty;
    [ObservableProperty] private bool _showFrequency;

    private bool _syncing;

    public SpectrumViewModel()
    {
        UpdateChannelOptions();
    }

    // ── Property change handlers ──────────────────────────────────────────────

    partial void OnSelectedBandChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentEntries));
        UpdateChannelOptions();
        ClearMarker();
    }

    partial void OnSelectedChannelChanged(int? value)
    {
        if (_syncing || value is null) return;
        _syncing = true;
        SelectedFrequencyMhz = ChannelCalculator.ChannelToFrequency(SelectedBand, value.Value);
        HasSelection = true;
        RefreshSelectionInfo();
        _syncing = false;
    }

    partial void OnSelectedFrequencyMhzChanged(double value)
    {
        if (_syncing) return;
        _syncing = true;
        if (value > 0)
        {
            int ch = ChannelCalculator.FrequencyToChannel(value);
            SelectedChannel = ChannelOptions.Contains(ch) ? ch : (int?)null;
            HasSelection = true;
            RefreshSelectionInfo();
        }
        else
        {
            SelectedChannel = null;
            HasSelection = false;
            SelectionInfo = string.Empty;
        }
        _syncing = false;
    }

    [RelayCommand]
    private void ClearMarker()
    {
        _syncing = true;
        SelectedChannel    = null;
        SelectedFrequencyMhz = 0;
        HasSelection       = false;
        SelectionInfo      = string.Empty;
        _syncing = false;
    }

    // ── Network data updates ──────────────────────────────────────────────────

    public void UpdateNetworks(IEnumerable<WifiNetwork> networks)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateNetworks(networks));
            return;
        }

        var allNetworks = networks.ToList();
        int colorIdx = 0;

        Entries24.Clear();
        Entries5.Clear();
        Entries6.Clear();

        foreach (var n in allNetworks.OrderBy(n => n.FrequencyMhz))
        {
            var entry = new SpectrumEntry(n, SpectrumColors[colorIdx % SpectrumColors.Length]);
            colorIdx++;
            switch (n.Band)
            {
                case WifiBand.GHz2_4: Entries24.Add(entry); break;
                case WifiBand.GHz5:   Entries5.Add(entry);  break;
                case WifiBand.GHz6:   Entries6.Add(entry);  break;
            }
        }

        OnPropertyChanged(nameof(CurrentEntries));

        if (HasSelection) RefreshSelectionInfo();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void RefreshSelectionInfo()
    {
        if (SelectedFrequencyMhz <= 0) { SelectionInfo = string.Empty; return; }

        int ch = ChannelCalculator.FrequencyToChannel(SelectedFrequencyMhz);
        var overlapping = CurrentEntries
            .Where(e => Math.Abs(e.CenterFreqMhz - SelectedFrequencyMhz) <= e.WidthMhz / 2.0 + 10)
            .Select(e => e.Ssid)
            .Take(4)
            .ToList();

        string chPart  = ch > 0 ? $"{L["Spectrum.Ch"]} {ch}  ·  " : "";
        string netPart = overlapping.Count > 0
            ? $"  ·  {L["Spectrum.Networks"]}: {string.Join(", ", overlapping)}"
            : "";
        SelectionInfo = $"{chPart}{SelectedFrequencyMhz:F0} MHz{netPart}";
    }

    private void UpdateChannelOptions()
    {
        ChannelOptions.Clear();
        int[] channels = SelectedBand switch
        {
            "5 GHz" => [36, 40, 44, 48, 52, 56, 60, 64, 100, 104, 108, 112, 116, 120, 124, 128, 132, 136, 140, 144, 149, 153, 157, 161, 165],
            "6 GHz" => Enumerable.Range(0, 24).Select(i => 1 + i * 4).ToArray(),
            _       => Enumerable.Range(1, 14).ToArray()
        };
        foreach (var ch in channels) ChannelOptions.Add(ch);
    }

    private static readonly string[] SpectrumColors =
    [
        "#3498db", "#e74c3c", "#2ecc71", "#f39c12", "#9b59b6",
        "#1abc9c", "#e67e22", "#34495e", "#e91e63", "#00bcd4"
    ];
}

public sealed class SpectrumEntry
{
    public WifiNetwork Network { get; }
    public string Color { get; }

    public string Ssid => string.IsNullOrEmpty(Network.Ssid) ? "<ukryta>" : Network.Ssid;
    public double CenterFreqMhz => Network.FrequencyMhz;
    public int WidthMhz => Network.ChannelWidthMhz > 0 ? Network.ChannelWidthMhz : 20;
    public int SignalDbm => Network.SignalDbm;
    public int Channel => Network.Channel;

    public double StartFreqMhz => CenterFreqMhz - WidthMhz / 2.0;
    public double EndFreqMhz   => CenterFreqMhz + WidthMhz / 2.0;

    public SpectrumEntry(WifiNetwork network, string color)
    {
        Network = network;
        Color   = color;
    }
}
