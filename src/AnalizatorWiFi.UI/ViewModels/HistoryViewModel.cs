using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.UI.Controls;

namespace AnalizatorWiFi.UI.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    private readonly IHistoryRepository _repository;

    private static readonly Color[] _palette =
    [
        Colors.DodgerBlue, Colors.OrangeRed, Colors.LimeGreen,
        Colors.Gold, Colors.MediumOrchid, Colors.DeepSkyBlue,
        Colors.Tomato, Colors.SpringGreen
    ];

    [ObservableProperty] private ObservableCollection<string> _trackedBssids = [];
    [ObservableProperty] private string? _selectedBssid;
    [ObservableProperty] private ObservableCollection<HistoryChartSeries> _chartSeries = [];
    [ObservableProperty] private string _selectedRange = "24h";
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<string> RangeOptions { get; } = ["1h", "6h", "24h", "7d", "30d"];

    public HistoryViewModel(IHistoryRepository repository)
    {
        _repository = repository;
    }

    public async Task LoadBssidsAsync()
    {
        var bssids = await _repository.GetTrackedBssidsAsync();
        TrackedBssids.Clear();
        foreach (var b in bssids) TrackedBssids.Add(b);
    }

    partial void OnSelectedBssidChanged(string? value) => _ = LoadChartAsync();
    partial void OnSelectedRangeChanged(string value) => _ = LoadChartAsync();

    [RelayCommand]
    private async Task LoadChartAsync()
    {
        if (string.IsNullOrEmpty(SelectedBssid)) return;
        IsLoading = true;

        try
        {
            var (from, to) = GetDateRange();
            var points = await _repository.GetHistoryAsync(SelectedBssid, from, to);

            var color = _palette[Math.Abs(SelectedBssid.GetHashCode()) % _palette.Length];
            var chartPoints = points
                .Select(p => (p.ScannedAt.DateTime, p.SignalDbm))
                .ToList();

            ChartSeries.Clear();
            ChartSeries.Add(new HistoryChartSeries
            {
                Name = SelectedBssid,
                Points = chartPoints,
                Color = color
            });
        }
        finally { IsLoading = false; }
    }

    private (DateTimeOffset from, DateTimeOffset to) GetDateRange()
    {
        var to = DateTimeOffset.Now;
        var from = SelectedRange switch
        {
            "1h"  => to.AddHours(-1),
            "6h"  => to.AddHours(-6),
            "7d"  => to.AddDays(-7),
            "30d" => to.AddDays(-30),
            _     => to.AddHours(-24)
        };
        return (from, to);
    }
}
