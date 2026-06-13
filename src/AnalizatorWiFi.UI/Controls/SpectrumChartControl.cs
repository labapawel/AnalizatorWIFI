using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AnalizatorWiFi.Core.Models;
using AnalizatorWiFi.UI.ViewModels;

namespace AnalizatorWiFi.UI.Controls;

public class SpectrumChartControl : Control
{
    public static readonly StyledProperty<ObservableCollection<SpectrumEntry>?> EntriesProperty =
        AvaloniaProperty.Register<SpectrumChartControl, ObservableCollection<SpectrumEntry>?>(nameof(Entries));

    public static readonly StyledProperty<string> BandProperty =
        AvaloniaProperty.Register<SpectrumChartControl, string>(nameof(Band), "2.4 GHz");

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

    static SpectrumChartControl()
    {
        AffectsRender<SpectrumChartControl>(EntriesProperty, BandProperty);
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
        const double margin = 48;
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

        // dBm scale on right
        DrawDbmScale(context, w - margin + 4, margin, chartH, dbmMin, dbmMax);
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
        double freqRange = maxFreq - minFreq;
        double step = freqRange < 100 ? 5 : freqRange < 500 ? 20 : 100;
        double start = Math.Ceiling(minFreq / step) * step;

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(40, 180, 180, 180)), 1);
        for (double f = start; f <= maxFreq; f += step)
        {
            double x = margin + (f - minFreq) / freqRange * chartW;
            ctx.DrawLine(pen, new Point(x, margin), new Point(x, margin + chartH));

            var ft = new FormattedText(
                $"{f:F0}",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"), 9, Brushes.Gray);
            ctx.DrawText(ft, new Point(x - ft.Width / 2, margin + chartH + 4));
        }
    }

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
