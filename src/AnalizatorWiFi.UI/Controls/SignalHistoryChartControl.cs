using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AnalizatorWiFi.UI.Controls;

public sealed class HistoryChartSeries
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<(DateTime Time, int Dbm)> Points { get; init; } = [];
    public Color Color { get; init; } = Colors.DodgerBlue;
}

public class SignalHistoryChartControl : Control
{
    public static readonly StyledProperty<ObservableCollection<HistoryChartSeries>?> SeriesProperty =
        AvaloniaProperty.Register<SignalHistoryChartControl, ObservableCollection<HistoryChartSeries>?>(nameof(Series));

    public ObservableCollection<HistoryChartSeries>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    static SignalHistoryChartControl()
    {
        AffectsRender<SignalHistoryChartControl>(SeriesProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SeriesProperty)
        {
            if (change.OldValue is ObservableCollection<HistoryChartSeries> old)
                old.CollectionChanged -= OnCollectionChanged;
            if (change.NewValue is ObservableCollection<HistoryChartSeries> @new)
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

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        var series = Series;

        double w = Bounds.Width;
        double h = Bounds.Height;
        const double leftMargin = 55;
        const double rightMargin = 16;
        const double topMargin = 12;
        const double bottomMargin = 38;
        double chartW = w - leftMargin - rightMargin;
        double chartH = h - topMargin - bottomMargin;

        if (chartW <= 0 || chartH <= 0) return;

        var chartRect = new Rect(leftMargin, topMargin, chartW, chartH);
        const double dbmMin = -100;
        const double dbmMax = -20;
        const double dbmRange = dbmMax - dbmMin;

        // Background
        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)), chartRect);

        // Horizontal grid lines at each 10 dBm
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(50, 180, 180, 180)), 1);
        for (int dbm = (int)dbmMin; dbm <= (int)dbmMax; dbm += 10)
        {
            double norm = (dbm - dbmMin) / dbmRange;
            double y = topMargin + chartH * (1 - norm);

            ctx.DrawLine(gridPen, new Point(leftMargin, y), new Point(leftMargin + chartW, y));

            var label = new FormattedText(
                $"{dbm}",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"), 10, Brushes.Gray);
            ctx.DrawText(label, new Point(leftMargin - label.Width - 4, y - label.Height / 2));
        }

        // No data
        if (series == null || series.Count == 0 || series.All(s => s.Points.Count == 0))
        {
            var noData = new FormattedText(
                "Brak danych — wybierz BSSID",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"), 14, Brushes.Gray);
            ctx.DrawText(noData, new Point(leftMargin + chartW / 2 - noData.Width / 2, topMargin + chartH / 2 - noData.Height / 2));
            DrawAxes(ctx, chartRect);
            return;
        }

        // Determine time range from all series combined
        var allTimes = series.SelectMany(s => s.Points.Select(p => p.Time)).ToList();
        if (allTimes.Count == 0) { DrawAxes(ctx, chartRect); return; }

        DateTime tMin = allTimes.Min();
        DateTime tMax = allTimes.Max();
        double tRange = (tMax - tMin).TotalSeconds;
        if (tRange <= 0) tRange = 60;

        // Draw each series
        foreach (var s in series)
        {
            if (s.Points.Count == 0) continue;

            var stroke = new Pen(new SolidColorBrush(s.Color), 2);
            Point? prev = null;

            foreach (var (time, dbm) in s.Points.OrderBy(p => p.Time))
            {
                double xNorm = (time - tMin).TotalSeconds / tRange;
                double yNorm = Math.Clamp((dbm - dbmMin) / dbmRange, 0, 1);

                double px = leftMargin + xNorm * chartW;
                double py = topMargin + chartH * (1 - yNorm);
                var cur = new Point(px, py);

                if (prev.HasValue)
                    ctx.DrawLine(stroke, prev.Value, cur);

                ctx.FillRectangle(new SolidColorBrush(s.Color), new Rect(px - 2, py - 2, 4, 4));
                prev = cur;
            }
        }

        // X-axis time labels
        DrawTimeLabels(ctx, tMin, tMax, leftMargin, chartW, topMargin + chartH);

        // Legend
        DrawLegend(ctx, series, leftMargin, topMargin);

        DrawAxes(ctx, chartRect);
    }

    private static void DrawAxes(DrawingContext ctx, Rect chartRect)
    {
        var pen = new Pen(Brushes.Gray, 1);
        ctx.DrawRectangle(null, pen, chartRect);
    }

    private static void DrawTimeLabels(DrawingContext ctx, DateTime tMin, DateTime tMax, double leftMargin, double chartW, double baselineY)
    {
        double totalSeconds = (tMax - tMin).TotalSeconds;
        int steps = 6;
        for (int i = 0; i <= steps; i++)
        {
            double norm = (double)i / steps;
            double x = leftMargin + norm * chartW;
            var t = tMin.AddSeconds(totalSeconds * norm);
            string fmt = totalSeconds < 7200 ? t.ToString("HH:mm") : t.ToString("dd.MM HH:mm");
            var label = new FormattedText(fmt, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Inter"), 9, Brushes.Gray);
            ctx.DrawText(label, new Point(x - label.Width / 2, baselineY + 4));
        }
    }

    private static void DrawLegend(DrawingContext ctx, ObservableCollection<HistoryChartSeries> series, double leftMargin, double topMargin)
    {
        double lx = leftMargin + 8;
        double ly = topMargin + 8;
        foreach (var s in series)
        {
            ctx.FillRectangle(new SolidColorBrush(s.Color), new Rect(lx, ly, 14, 3));
            var label = new FormattedText(s.Name, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Inter"), 10, Brushes.LightGray);
            ctx.DrawText(label, new Point(lx + 18, ly - label.Height / 2 + 1));
            ly += label.Height + 4;
        }
    }
}
