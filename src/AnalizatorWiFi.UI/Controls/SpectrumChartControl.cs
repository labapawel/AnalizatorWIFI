using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AnalizatorWiFi.Core.Models;
using AnalizatorWiFi.Core.Services;
using AnalizatorWiFi.UI.ViewModels;

namespace AnalizatorWiFi.UI.Controls;

public class SpectrumChartControl : Control
{
    private const double ChartMargin = 48.0;

    public static readonly StyledProperty<ObservableCollection<SpectrumEntry>?> EntriesProperty =
        AvaloniaProperty.Register<SpectrumChartControl, ObservableCollection<SpectrumEntry>?>(nameof(Entries));

    public static readonly StyledProperty<string> BandProperty =
        AvaloniaProperty.Register<SpectrumChartControl, string>(nameof(Band), "2.4 GHz");

    public static readonly StyledProperty<double> SelectedFrequencyMhzProperty =
        AvaloniaProperty.Register<SpectrumChartControl, double>(nameof(SelectedFrequencyMhz), 0.0);

    public static readonly StyledProperty<bool> ShowFrequencyProperty =
        AvaloniaProperty.Register<SpectrumChartControl, bool>(nameof(ShowFrequency), false);

    public ObservableCollection<SpectrumEntry>? Entries
    {
        get => GetValue(EntriesProperty);
        set => SetValue(EntriesProperty, value);
    }

    public string Band
    {
        get => GetValue(BandProperty);
        set => SetValue(BandProperty, value);
    }

    public double SelectedFrequencyMhz
    {
        get => GetValue(SelectedFrequencyMhzProperty);
        set => SetValue(SelectedFrequencyMhzProperty, value);
    }

    public bool ShowFrequency
    {
        get => GetValue(ShowFrequencyProperty);
        set => SetValue(ShowFrequencyProperty, value);
    }

    static SpectrumChartControl()
    {
        AffectsRender<SpectrumChartControl>(EntriesProperty, BandProperty, SelectedFrequencyMhzProperty, ShowFrequencyProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EntriesProperty)
        {
            if (change.OldValue is ObservableCollection<SpectrumEntry> old)
                old.CollectionChanged -= OnCollectionChanged;
            if (change.NewValue is ObservableCollection<SpectrumEntry> @new)
                @new.CollectionChanged += OnCollectionChanged;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            InvalidateVisual();
        else
            Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var entries = Entries;
        if (entries == null || entries.Count == 0) { DrawEmpty(context); return; }

        double w = Bounds.Width;
        double h = Bounds.Height;
        const double margin = ChartMargin;
        double chartW = w - margin * 2;
        double chartH = h - margin * 2;

        var (minFreq, maxFreq) = GetBandRange();
        double freqRange = maxFreq - minFreq;
        if (freqRange <= 0) return;

        // Background
        context.FillRectangle(Brushes.Transparent, new Rect(0, 0, w, h));

        // Chart area border
        var pen = new Pen(Brushes.Gray, 1);
        context.DrawRectangle(null, pen, new Rect(margin, margin, chartW, chartH));

        // Grid lines & frequency labels
        DrawGrid(context, minFreq, maxFreq, margin, chartW, chartH);

        // RSSI scale (-30 to -100 dBm)
        const double dbmMax = -20;
        const double dbmMin = -100;
        double dbmRange = dbmMax - dbmMin;

        // Draw each network as a trapezoid/bell shape
        foreach (var entry in entries)
        {
            double centerX = margin + (entry.CenterFreqMhz - minFreq) / freqRange * chartW;
            double halfW = entry.WidthMhz / freqRange * chartW / 2.0;
            double dbmNorm = Math.Clamp((entry.SignalDbm - dbmMin) / dbmRange, 0, 1);
            double peakY = margin + chartH * (1 - dbmNorm);

            DrawNetworkShape(context, entry, centerX, halfW, peakY, margin + chartH);
            DrawLabel(context, entry.Ssid, centerX, peakY - 4);
        }

        // Selected frequency marker
        double selFreq = SelectedFrequencyMhz;
        if (selFreq > 0)
            DrawMarker(context, selFreq, minFreq, maxFreq, margin, chartW, chartH);

        // dBm scale on right
        DrawDbmScale(context, w - margin + 4, margin, chartH, dbmMin, dbmMax);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var pos    = e.GetPosition(this);
        double chartW = Bounds.Width  - ChartMargin * 2;
        double chartH = Bounds.Height - ChartMargin * 2;

        if (pos.X < ChartMargin || pos.X > ChartMargin + chartW ||
            pos.Y < ChartMargin || pos.Y > ChartMargin + chartH) return;

        var (minFreq, maxFreq) = GetBandRange();
        double freq = minFreq + (pos.X - ChartMargin) / chartW * (maxFreq - minFreq);
        SelectedFrequencyMhz = Math.Round(freq);
        e.Handled = true;
    }

    private void DrawMarker(DrawingContext ctx, double freq, double minFreq, double maxFreq,
        double margin, double chartW, double chartH)
    {
        double freqRange = maxFreq - minFreq;
        double x = margin + (freq - minFreq) / freqRange * chartW;
        if (x < margin - 1 || x > margin + chartW + 1) return;

        var markerBrush = new SolidColorBrush(Color.Parse("#FFD700"));
        var markerPen   = new Pen(markerBrush, 1.5, new DashStyle([5, 3], 0));
        ctx.DrawLine(markerPen, new Point(x, margin), new Point(x, margin + chartH));

        int ch = ChannelCalculator.FrequencyToChannel(freq);
        string label = ch > 0 ? $"Ch {ch} · {freq:F0} MHz" : $"{freq:F0} MHz";
        var ft = new FormattedText(
            label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"), 10,
            markerBrush);
        double labelX = Math.Clamp(x - ft.Width / 2, margin, margin + chartW - ft.Width);
        ctx.DrawText(ft, new Point(labelX, margin - ft.Height - 2));
    }

    private void DrawNetworkShape(DrawingContext ctx, SpectrumEntry entry, double cx, double halfW, double peakY, double baseY)
    {
        var color = ParseColor(entry.Color);
        var fill = new SolidColorBrush(new Color(100, color.R, color.G, color.B));
        var stroke = new SolidColorBrush(color);

        var geometry = new StreamGeometry();
        using (var sgc = geometry.Open())
        {
            sgc.BeginFigure(new Point(cx - halfW, baseY), true);
            sgc.LineTo(new Point(cx - halfW * 0.1, peakY + 2));
            sgc.QuadraticBezierTo(new Point(cx, peakY), new Point(cx + halfW * 0.1, peakY + 2));
            sgc.LineTo(new Point(cx + halfW, baseY));
            sgc.EndFigure(true);
        }

        ctx.DrawGeometry(fill, new Pen(stroke, 1.5), geometry);
    }

    private void DrawLabel(DrawingContext ctx, string text, double cx, double y)
    {
        var ft = new FormattedText(
            text.Length > 12 ? text[..12] + "…" : text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            10,
            Brushes.LightGray);

        ctx.DrawText(ft, new Point(cx - ft.Width / 2, y - ft.Height));
    }

    private void DrawGrid(DrawingContext ctx, double minFreq, double maxFreq, double margin, double chartW, double chartH)
    {
        if (ShowFrequency)
            DrawFrequencyGrid(ctx, minFreq, maxFreq, margin, chartW, chartH);
        else
            DrawChannelGrid(ctx, minFreq, maxFreq, margin, chartW, chartH);
    }

    private void DrawChannelGrid(DrawingContext ctx, double minFreq, double maxFreq, double margin, double chartW, double chartH)
    {
        double freqRange = maxFreq - minFreq;
        if (freqRange <= 0) return;

        var channels = GetChannelList();
        var gridPen  = new Pen(new SolidColorBrush(Color.FromArgb(35, 180, 180, 180)), 1);
        var tickPen  = new Pen(Brushes.DimGray, 1);

        // Compute minimum pixel gap to choose label density
        double minGapPx = chartW;
        for (int i = 1; i < channels.Count; i++)
        {
            double dx = (channels[i].freq - channels[i - 1].freq) / freqRange * chartW;
            if (dx < minGapPx) minGapPx = dx;
        }
        int labelEvery = minGapPx < 14 ? 4 : minGapPx < 22 ? 2 : 1;

        for (int i = 0; i < channels.Count; i++)
        {
            var (ch, freq) = channels[i];
            double x = margin + (freq - minFreq) / freqRange * chartW;
            if (x < margin - 1 || x > margin + chartW + 1) continue;

            ctx.DrawLine(gridPen, new Point(x, margin), new Point(x, margin + chartH));
            ctx.DrawLine(tickPen, new Point(x, margin + chartH), new Point(x, margin + chartH + 4));

            if (i % labelEvery == 0)
            {
                var ft = new FormattedText(
                    ch.ToString(),
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter"), 9, Brushes.Gray);
                ctx.DrawText(ft, new Point(x - ft.Width / 2, margin + chartH + 6));
            }
        }
    }

    private static void DrawFrequencyGrid(DrawingContext ctx, double minFreq, double maxFreq, double margin, double chartW, double chartH)
    {
        double freqRange = maxFreq - minFreq;
        if (freqRange <= 0) return;

        double step  = freqRange < 100 ? 5 : freqRange < 500 ? 20 : 100;
        double start = Math.Ceiling(minFreq / step) * step;

        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(35, 180, 180, 180)), 1);
        var tickPen = new Pen(Brushes.DimGray, 1);

        for (double f = start; f <= maxFreq; f += step)
        {
            double x = margin + (f - minFreq) / freqRange * chartW;
            ctx.DrawLine(gridPen, new Point(x, margin), new Point(x, margin + chartH));
            ctx.DrawLine(tickPen, new Point(x, margin + chartH), new Point(x, margin + chartH + 4));

            var ft = new FormattedText(
                $"{f:F0}",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"), 9, Brushes.Gray);
            ctx.DrawText(ft, new Point(x - ft.Width / 2, margin + chartH + 6));
        }
    }

    private List<(int ch, double freq)> GetChannelList() => Band switch
    {
        "5 GHz" => new[] { 36, 40, 44, 48, 52, 56, 60, 64, 100, 104, 108, 112, 116, 120, 124, 128, 132, 136, 140, 144, 149, 153, 157, 161, 165 }
                   .Select(ch => (ch, 5000.0 + ch * 5)).ToList(),
        "6 GHz" => Enumerable.Range(0, 24).Select(i => 1 + i * 4)
                   .Select(ch => (ch, 5950.0 + ch * 5)).ToList(),
        _       => Enumerable.Range(1, 13).Select(ch => (ch, 2407.0 + ch * 5))
                   .Append((14, 2484.0)).ToList()
    };

    private void DrawDbmScale(DrawingContext ctx, double x, double margin, double chartH, double dbmMin, double dbmMax)
    {
        for (int dbm = (int)dbmMin; dbm <= (int)dbmMax; dbm += 10)
        {
            double norm = (dbm - dbmMin) / (dbmMax - dbmMin);
            double y = margin + chartH * (1 - norm);
            var ft = new FormattedText(
                $"{dbm}",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"), 9, Brushes.Gray);
            ctx.DrawText(ft, new Point(x, y - ft.Height / 2));
        }
    }

    private void DrawEmpty(DrawingContext ctx)
    {
        var ft = new FormattedText(
            "Brak danych – wykonaj skanowanie",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"), 14, Brushes.Gray);
        ctx.DrawText(ft, new Point(Bounds.Width / 2 - ft.Width / 2, Bounds.Height / 2 - ft.Height / 2));
    }

    private (double min, double max) GetBandRange() => Band switch
    {
        "5 GHz" => (5170, 5830),
        "6 GHz" => (5945, 7125),
        _       => (2400, 2495)  // 2.4 GHz
    };

    private static Color ParseColor(string hex)
    {
        try { return Color.Parse(hex); }
        catch { return Colors.DodgerBlue; }
    }
}
