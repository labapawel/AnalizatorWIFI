using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.UI.ViewModels;

public partial class SpectrumViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<SpectrumEntry> _entries24 = [];
    [ObservableProperty] private ObservableCollection<SpectrumEntry> _entries5 = [];
    [ObservableProperty] private ObservableCollection<SpectrumEntry> _entries6 = [];
    [ObservableProperty] private string _selectedBand = "2.4 GHz";

    public ObservableCollection<string> Bands { get; } = ["2.4 GHz", "5 GHz", "6 GHz"];
    public ObservableCollection<SpectrumEntry> CurrentEntries =>
        SelectedBand switch { "5 GHz" => Entries5, "6 GHz" => Entries6, _ => Entries24 };

    partial void OnSelectedBandChanged(string value) => OnPropertyChanged(nameof(CurrentEntries));

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

    // Canvas layout helpers — normalized 0..1 within band range
    public double StartFreqMhz => CenterFreqMhz - WidthMhz / 2.0;
    public double EndFreqMhz => CenterFreqMhz + WidthMhz / 2.0;

    public SpectrumEntry(WifiNetwork network, string color)
    {
        Network = network;
        Color = color;
    }
}
