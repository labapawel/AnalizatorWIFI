using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO.Compression;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AnalizatorWiFi.UI.Services;
using AnalizatorWiFi.UI.ViewModels;

namespace AnalizatorWiFi.UI.Controls;

public class WorldMapControl : Control
{
    // ── Avalonia property ────────────────────────────────────────────────────
    public static readonly StyledProperty<ObservableCollection<MapPoint>?> PointsProperty =
        AvaloniaProperty.Register<WorldMapControl, ObservableCollection<MapPoint>?>(nameof(Points));

    public ObservableCollection<MapPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    static WorldMapControl()
    {
        AffectsRender<WorldMapControl>(PointsProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PointsProperty)
        {
            if (change.OldValue is ObservableCollection<MapPoint> old) old.CollectionChanged -= Refresh;
            if (change.NewValue is ObservableCollection<MapPoint> @new) @new.CollectionChanged += Refresh;
        }
    }

    private void Refresh(object? s, NotifyCollectionChangedEventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess()) InvalidateVisual();
        else Dispatcher.UIThread.Post(InvalidateVisual);
    }

    // ── Async map data loading ───────────────────────────────────────────────
    private static IReadOnlyList<IReadOnlyList<(double lat, double lon)>>? _polygons;
    private static Task? _loadTask;
    private static bool _usingBuiltin;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_polygons != null) return;
        if (_loadTask != null) return;

        _loadTask = Task.Run(async () =>
        {
            var data = await LoadAsync();
            _polygons  = data.polygons;
            _usingBuiltin = data.isBuiltin;
            Dispatcher.UIThread.Post(InvalidateVisual);
        });
    }

    private static async Task<(IReadOnlyList<IReadOnlyList<(double lat, double lon)>> polygons, bool isBuiltin)> LoadAsync()
    {
        // 1. Try local cache
        string cacheDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AnalizatorWiFi");
        string cachePath = Path.Combine(cacheDir, "worldmap_110m.gz");

        if (File.Exists(cachePath))
        {
            try
            {
                var polygons = await ReadCacheAsync(cachePath);
                if (polygons != null && polygons.Count > 0)
                    return (polygons, false);
            }
            catch { /* corrupt cache — re-fetch */ }
        }

        // 2. Fetch from CDN
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AnalizatorWiFi/1.0");
            var json     = await http.GetStringAsync("https://cdn.jsdelivr.net/npm/world-atlas@2/land-110m.json");
            var polygons = TopoJsonDecoder.Decode(json);

            // Save cache
            Directory.CreateDirectory(cacheDir);
            await WriteCacheAsync(cachePath, polygons);
            return (polygons, false);
        }
        catch { /* offline — use built-in */ }

        return (GetBuiltinPolygons(), true);
    }

    // ── Cache helpers ────────────────────────────────────────────────────────
    private static async Task<List<List<(double lat, double lon)>>?> ReadCacheAsync(string path)
    {
        await using var fs = File.OpenRead(path);
        await using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var sr       = new StreamReader(gz);
        var polygons       = new List<List<(double lat, double lon)>>();
        string? line;
        while ((line = await sr.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrEmpty(line)) continue;
            var parts   = line.Split(' ');
            var polygon = new List<(double lat, double lon)>(parts.Length / 2);
            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                if (double.TryParse(parts[i],     System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
                    double.TryParse(parts[i + 1], System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out double lon))
                    polygon.Add((lat, lon));
            }
            if (polygon.Count >= 3) polygons.Add(polygon);
        }
        return polygons.Count > 0 ? polygons : null;
    }

    private static async Task WriteCacheAsync(
        string path, IReadOnlyList<IReadOnlyList<(double lat, double lon)>> polygons)
    {
        await using var fs = File.Create(path);
        await using var gz = new GZipStream(fs, CompressionLevel.SmallestSize);
        await using var sw = new StreamWriter(gz);
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var polygon in polygons)
        {
            var sb = new System.Text.StringBuilder(polygon.Count * 16);
            foreach (var (lat, lon) in polygon)
                sb.Append(lat.ToString("F4", ic)).Append(' ').Append(lon.ToString("F4", ic)).Append(' ');
            await sw.WriteLineAsync(sb);
        }
    }

    // ── Render ───────────────────────────────────────────────────────────────
    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        double w = Bounds.Width, h = Bounds.Height;
        if (w < 10 || h < 10) return;

        // Ocean
        ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(12, 42, 78)), new Rect(0, 0, w, h));

        // Grid
        DrawGraticule(ctx, w, h);

        // Land polygons
        var polys = _polygons ?? GetBuiltinPolygons();
        var landFill   = new SolidColorBrush(Color.FromRgb(70, 106, 56));
        var landBorder = new Pen(new SolidColorBrush(Color.FromArgb(80, 110, 148, 86)), 0.4);

        foreach (var poly in polys)
        {
            if (poly.Count < 3) continue;
            var geo = new StreamGeometry();
            using (var sgc = geo.Open())
            {
                bool first = true;
                foreach (var (lat, lon) in poly)
                {
                    double x = (lon + 180.0) / 360.0 * w;
                    double y = (90.0 - lat) / 180.0 * h;
                    var p = new Point(x, y);
                    if (first) { sgc.BeginFigure(p, true); first = false; }
                    else sgc.LineTo(p);
                }
                sgc.EndFigure(true);
            }
            ctx.DrawGeometry(landFill, landBorder, geo);
        }

        // Loading banner
        if (_polygons == null)
            DrawBanner(ctx, w, h, "⏳ Pobieranie dokładniejszej mapy...");
        else if (_usingBuiltin)
            DrawBanner(ctx, w, h, "Mapa uproszczona (brak połączenia z CDN)");

        // Connection dots
        var pts = Points;
        if (pts is null || pts.Count == 0)
        {
            DrawHint(ctx, w, h);
            return;
        }

        DrawDots(ctx, pts, w, h);
        DrawLegend(ctx, w, h, pts.Count, pts.Sum(p => p.Count));
    }

    // ── Drawing helpers ──────────────────────────────────────────────────────
    private static void DrawGraticule(DrawingContext ctx, double w, double h)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(30, 100, 150, 220)), 0.5);
        foreach (int lat in new[] { -60, -30, 0, 30, 60 })
        {
            double y = (90.0 - lat) / 180.0 * h;
            ctx.DrawLine(pen, new Point(0, y), new Point(w, y));
        }
        foreach (int lon in new[] { -120, -60, 0, 60, 120 })
        {
            double x = (lon + 180.0) / 360.0 * w;
            ctx.DrawLine(pen, new Point(x, 0), new Point(x, h));
        }
    }

    private static void DrawDots(DrawingContext ctx, ObservableCollection<MapPoint> pts, double w, double h)
    {
        foreach (var pt in pts.OrderBy(p => p.Count))
        {
            double x = (pt.Lon + 180.0) / 360.0 * w;
            double y = (90.0 - pt.Lat) / 180.0 * h;
            var pos = new Point(x, y);
            double r = Math.Clamp(4.0 + Math.Sqrt(pt.Count) * 2.5, 4, 18);

            ctx.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(55, 255, 130, 20)), null, pos, r + 5, r + 5);
            ctx.DrawEllipse(
                new SolidColorBrush(Color.FromRgb(255, 100, 30)),
                new Pen(new SolidColorBrush(Color.FromRgb(255, 210, 80)), 1.0),
                pos, r, r);

            var ft = new FormattedText(
                $"{pt.CountryCode} {pt.Count}",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Inter"), 9,
                new SolidColorBrush(Colors.White));

            double tx = Math.Clamp(x - ft.Width / 2, 2, w - ft.Width - 2);
            double ty = Math.Clamp(y + r + 2, 2, h - ft.Height - 2);
            ctx.DrawText(ft, new Point(tx, ty));
        }
    }

    private static void DrawBanner(DrawingContext ctx, double w, double h, string text)
    {
        var ft = new FormattedText(text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Inter"), 11,
            new SolidColorBrush(Color.FromArgb(200, 180, 220, 255)));
        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
            new Rect(0, 0, w, 24));
        ctx.DrawText(ft, new Point(w / 2 - ft.Width / 2, 4));
    }

    private static void DrawHint(DrawingContext ctx, double w, double h)
    {
        var ft = new FormattedText("Brak danych — kliknij Odśwież",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Inter"), 13,
            new SolidColorBrush(Color.FromArgb(140, 200, 200, 200)));
        ctx.DrawText(ft, new Point(w / 2 - ft.Width / 2, h / 2 - ft.Height / 2));
    }

    private static void DrawLegend(DrawingContext ctx, double w, double h, int countries, int total)
    {
        string text = $"Kraje: {countries}  |  Połączeń: {total}";
        var ft = new FormattedText(text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Inter"), 11,
            new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)));
        double px = w - ft.Width - 8;
        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)),
            new Rect(px - 4, h - ft.Height - 8, ft.Width + 8, ft.Height + 6));
        ctx.DrawText(ft, new Point(px, h - ft.Height - 6));
    }

    // ── Improved built-in fallback polygons (~500 pts, equirectangular) ──────
    private static IReadOnlyList<IReadOnlyList<(double lat, double lon)>>? _builtin;
    private static IReadOnlyList<IReadOnlyList<(double lat, double lon)>> GetBuiltinPolygons()
        => _builtin ??= BuildBuiltinPolygons();

    private static IReadOnlyList<IReadOnlyList<(double lat, double lon)>> BuildBuiltinPolygons()
    {
        return new List<IReadOnlyList<(double lat, double lon)>>
        {
            // ── North America (clockwise ~72 pts) ─────────────────────────────
            P(
                (71.3,-157),(71,-148),(71,-141), // Arctic Alaska
                (69.5,-140),(60,-137),(58,-136),(57,-135),(55,-130), // SE Alaska/BC north
                (54,-131),(50,-128),(49,-124),(47,-124),(46,-124), // BC/Washington/Oregon
                (42,-124),(40,-124),(38,-123),(36,-122),(35,-121),(34,-120), // California
                (32,-117),(30,-116),(26,-110),(24,-110),(22.9,-109.9), // Mexico/Baja
                (22,-106),(20,-105),(18,-103),(16,-99),(15,-92), // W Mexico
                (14,-90),(13,-89),(11,-86),(10,-85),(9,-77), // C America Pacific
                (9,-80),(10,-84),(12,-84),(15,-88),(17,-88),(18,-88),(21,-87), // Caribbean C.Am.
                (21.5,-87.3),(18,-92),(18,-95),(20,-97),(22,-98), // Yucatan/Gulf Mexico
                (26,-97),(29,-93),(30,-89),(29,-89),(29,-88), // Texas/Louisiana/Delta
                (30,-86),(30,-84),(26,-82),(24.5,-82),(24.8,-81.7), // FL panhandle/W
                (25,-80),(27,-80),(30,-81), // FL keys/E coast
                (32,-81),(33,-79),(35,-76),(37,-76),(38,-75),(40,-74),(41,-72),(42,-70), // E coast
                (44,-68),(44,-64),(46,-60),(47,-53),(48,-53), // Maine/Maritime/Newfoundland
                (52,-56),(58,-64),(62,-65), // Labrador/Hudson Strait
                (62,-94),(60,-94),(55,-82),(57,-80),(62,-82),(65,-84), // Hudson Bay/Arctic
                (70,-110),(70,-131),(71,-141),(71.3,-157) // Arctic back
            ),
            // ── South America (clockwise ~36 pts) ────────────────────────────
            P(
                (12,-73),(11,-63),(8,-62),(4,-52),(2,-50),(0,-50), // N coast
                (-3,-41),(-8,-35),(-8,-34.5), // NE Brazil
                (-15,-39),(-23,-43),(-27,-48),(-30,-51),(-34,-53),(-35,-55), // SE Brazil/Uruguay
                (-38,-57),(-42,-63),(-47,-66),(-52,-68),(-55,-65),(-55,-63), // Argentina/Patagonia
                (-52,-73),(-47,-73),(-42,-73),(-39,-73),(-30,-71.5), // Chilean fjords
                (-18,-70),(-8,-80),(-3,-81),(2,-78),(5,-77),(8,-77),(12,-73) // W coast
            ),
            // ── Europe outer hull (clockwise ~48 pts) ────────────────────────
            P(
                (71,26),(70,20),(68,16),(65,14),(63,8),(60,5),(58,5),(57,8), // Norway
                (56,8),(55,8),(54,8),(54,11),(54,14),(54,18),(54,22),(56,22),(57,23),(58,24), // Germany/Baltic
                (55,22),(53,8),(51,3),(50,2),(49,0),(47,-2),(44,-2),(43,-9), // NW Europe/Biscay
                (41,-9),(39,-9),(36,-8),(36,-6),(36,3),(37,5),(38,10),(38,15), // Iberia/Med
                (40,18),(41,19),(43,16),(44,15),(45,14),(46,14), // Adriatic
                (41,19),(39,20),(37,22),(36,23),(37,27),(38,28), // Greece/Aegean
                (40,27),(41,29),(42,28),(44,29),(44,31),(46,30),(47,31),(47,37), // Black Sea
                (50,30),(55,22),(57,23),(59,24),(60,25),(65,25),(71,26) // Back north
            ),
            // ── Scandinavian Peninsula (clockwise ~20 pts) ───────────────────
            P(
                (56,10),(57,8),(58,5),(60,5),(62,5),(63,8),(65,14),(68,16),
                (70,20),(71,26),(70,28),(69,28),(68,26),(65,25),(60,27),(57,23),
                (55,14),(55,10),(56,10)
            ),
            // ── Africa (clockwise ~55 pts) ────────────────────────────────────
            P(
                (37,10),(36,5),(35,-1),(34,-7),(30,-10),(25,-17),(18,-16), // N/NW
                (14,-17),(11,-16),(8,-13),(5,-7),(4,-3),(2,3),(1,9),(4,7), // W Africa
                (3,8),(0,9),(-1,10),(-3,10),(-4,11),(-5,12),(-5,15),(-5,18), // Gulf Guinea
                (-8,12),(-10,13),(-12,14),(-15,12),(-17,11),(-22,14), // Congo/Angola
                (-28,17),(-34,25),(-35,20),(-34,27),(-31,30),(-29,32), // S Africa
                (-26,33),(-22,36),(-18,36),(-11,40),(-5,40),(-1,42),(2,41), // E Africa/Tanzania/Kenya
                (5,45),(11,50),(12,50),(12,43),(13,43),(15,42), // Somalia/Gulf of Aden
                (18,39),(20,38),(22,37),(24,36),(28,33),(30,32),(31,25), // Red Sea/Egypt
                (32,14),(32,12),(37,8),(37,10) // Libya/Tunisia/Morocco
            ),
            // ── Arabian Peninsula (~18 pts) ───────────────────────────────────
            P(
                (30,32),(22,38),(13,45),(12,45),(12,44),(12,43),(15,42),
                (18,39),(20,38),(22,39),(24,57),(22,59),(17,55),(12,44),
                (22,38),(30,32)
            ),
            // ── Asia main (clockwise ~78 pts) ────────────────────────────────
            P(
                (72,50),(55,37),(44,31),(38,28),(40,27),(38,26),(36,28), // Anatolia
                (36,36),(33,35),(31,35),(29,34),(24,37),(22,38),(13,45), // Levant/Arabia
                (12,44),(2,41),(0,41),(-2,42),(0,48),(0,62),(0,73), // E Africa/India Ocean
                (8,77),(8,78),(8,80),(8,77),(10,80),(13,80),(16,82), // India E coast
                (20,87),(22,88),(22,91),(24,91),(23,92),(22,92), // Bangladesh
                (22,94),(24,96),(26,96),(28,97),(29,96),(29,98), // Myanmar N
                (26,100),(22,100),(16,100),(10,99),(6,100),(3,101), // Malay Peninsula
                (1,104),(4,108),(10,107),(16,108),(18,107),(20,107),(21,108),(22,108), // Vietnam
                (22,104),(22,100),(26,100),(28,97), // Back through SE Asia
                (30,78),(31,76),(34,72),(30,64),(24,68),(22,68), // Pakistan/W India
                (25,62),(28,60),(28,55),(24,57),(22,59),(17,55), // Oman/Gulf
                (29,48),(30,48),(30,49),(30,50),(26,50),(22,59), // Kuwait/UAE
                (36,36),(38,42),(40,52),(44,50),(45,50),(50,51), // Caspian
                (50,51),(52,51),(55,60),(57,66),(58,67),(60,74),(62,78),(64,86),(68,95), // Kazakh/W Siberia
                (65,87),(70,72),(72,73),(73,68),(72,55),(72,50) // Arctic Russia
            ),
            // ── India subcontinent (~20 pts) ──────────────────────────────────
            P(
                (29,76),(28,72),(24,69),(22,68),(20,66),(15,73),(10,78),
                (8,77),(8,80),(10,80),(13,80),(16,82),(20,87),(22,88),
                (22,91),(24,91),(28,84),(30,78),(29,76)
            ),
            // ── SE Asia mainland + Malay Peninsula (~22 pts) ─────────────────
            P(
                (22,100),(18,98),(15,99),(10,99),(6,100),(3,101),(1,104),
                (4,108),(10,107),(16,108),(18,107),(20,107),(21,108),(22,108),
                (22,104),(22,100)
            ),
            // ── Borneo (~14 pts) ──────────────────────────────────────────────
            P(
                (4,108),(6,115),(5,119),(2,119),(1,109),(0,109),(1,111),(4,108)
            ),
            // ── Sumatra (~14 pts) ─────────────────────────────────────────────
            P(
                (5,95),(4,97),(2,99),(0,100),(-2,102),(-4,105),(-5,106),
                (-4,106),(-2,104),(0,103),(2,101),(4,98),(5,95)
            ),
            // ── Australia (clockwise ~30 pts) ─────────────────────────────────
            P(
                (-10,131),(-12,136),(-10,142),(-16,145),(-20,148),(-23,150),
                (-28,153),(-32,152),(-34,151),(-37,150),(-38,148),(-39,147),
                (-38,146),(-38,140),(-36,137),(-34,136),(-35,138),(-32,133),
                (-31,115),(-24,114),(-22,114),(-18,122),(-14,128),(-10,131)
            ),
            // ── New Guinea (~14 pts) ──────────────────────────────────────────
            P(
                (-1,131),(-1,136),(-3,140),(-5,141),(-6,145),(-8,148),
                (-8,147),(-6,143),(-4,140),(-2,134),(-1,131)
            ),
            // ── Greenland (clockwise ~16 pts) ─────────────────────────────────
            P(
                (60,-44),(62,-42),(65,-40),(68,-26),(71,-20),(73,-18),
                (76,-18),(83,-28),(83,-55),(76,-68),(72,-74),(68,-54),
                (65,-51),(63,-49),(60,-44)
            ),
            // ── Great Britain (~18 pts) ───────────────────────────────────────
            P(
                (50,-5),(50,0),(51,2),(52,2),(53,0),(54,-2),(55,-1),
                (56,-2),(57,-6),(58,-7),(58,-4),(56,-2),(55,-5),(53,-5),
                (51,-5),(50,-5)
            ),
            // ── Ireland (~10 pts) ─────────────────────────────────────────────
            P(
                (51,-10),(52,-10),(53,-10),(54,-8),(55,-7),(54,-6),
                (53,-6),(52,-7),(51,-8),(51,-10)
            ),
            // ── Honshu/Japan (~20 pts) ────────────────────────────────────────
            P(
                (31,131),(33,132),(34,133),(34,135),(35,137),(36,138),
                (37,140),(38,141),(39,141),(40,140),(40,140),(41,141),
                (42,141),(43,141),(44,144),(43,141),(40,139),(38,141),
                (37,137),(35,135),(34,134),(34,132),(31,131)
            ),
            // ── New Zealand North (~10 pts) ───────────────────────────────────
            P(
                (-34,172),(-36,174),(-37,176),(-39,177),(-41,175),
                (-41,172),(-38,174),(-36,173),(-34,172)
            ),
            // ── New Zealand South (~10 pts) ───────────────────────────────────
            P(
                (-41,174),(-42,173),(-43,172),(-44,168),(-46,168),
                (-44,169),(-43,172),(-41,174)
            ),
            // ── Iceland (~10 pts) ─────────────────────────────────────────────
            P(
                (63,-24),(64,-22),(66,-14),(66,-18),(65,-21),(64,-24),(63,-24)
            ),
            // ── Madagascar (~12 pts) ──────────────────────────────────────────
            P(
                (-12,49),(-14,50),(-18,48),(-22,44),(-25,44),(-25,45),
                (-23,48),(-18,49),(-15,50),(-12,49)
            ),
            // ── Cuba (~10 pts) ────────────────────────────────────────────────
            P(
                (23,-84),(23,-82),(22,-80),(20,-75),(20,-74),(20,-76),
                (22,-79),(22,-82),(23,-84)
            ),
            // ── Hispaniola (~8 pts) ───────────────────────────────────────────
            P(
                (20,-74),(20,-72),(18,-71),(18,-73),(19,-74),(20,-74)
            ),
            // ── Sulawesi (~10 pts) ────────────────────────────────────────────
            P(
                (0,120),(2,121),(2,123),(0,124),(-2,122),(-4,122),
                (-2,119),(0,120)
            ),
            // ── Philippines (Luzon, simplified ~10 pts) ───────────────────────
            P(
                (18,122),(16,121),(14,120),(12,122),(10,124),(9,125),
                (10,124),(14,122),(16,121),(18,122)
            ),
            // ── Sri Lanka (~8 pts) ────────────────────────────────────────────
            P(
                (10,80),(8,81),(6,81),(6,80),(8,79),(10,80)
            ),
            // ── Taiwan (~6 pts) ───────────────────────────────────────────────
            P(
                (25,121),(24,122),(22,120),(22,120),(24,121),(25,121)
            ),
        };
    }

    private static List<(double lat, double lon)> P(params (double lat, double lon)[] pts)
        => new(pts);
}
