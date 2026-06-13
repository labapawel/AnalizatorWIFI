using System.Text.Json;

namespace AnalizatorWiFi.UI.Services;

/// <summary>
/// Minimal decoder for Natural Earth 110m TopoJSON land outline.
/// Converts delta-encoded integer arcs to geographic polygons.
/// </summary>
internal static class TopoJsonDecoder
{
    public static IReadOnlyList<IReadOnlyList<(double lat, double lon)>> Decode(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Read scale/translate transform
        var xform    = root.GetProperty("transform");
        var scaleEl  = xform.GetProperty("scale");
        var transEl  = xform.GetProperty("translate");
        double sx = scaleEl[0].GetDouble(), sy = scaleEl[1].GetDouble();
        double tx = transEl[0].GetDouble(), ty = transEl[1].GetDouble();

        // Decode delta-encoded arcs → (lon, lat) pairs
        var arcsEl = root.GetProperty("arcs");
        var arcs   = new List<List<(double lon, double lat)>>();

        foreach (var arcEl in arcsEl.EnumerateArray())
        {
            var arc = new List<(double lon, double lat)>();
            long ax = 0, ay = 0;
            foreach (var ptEl in arcEl.EnumerateArray())
            {
                ax += ptEl[0].GetInt64();
                ay += ptEl[1].GetInt64();
                arc.Add((ax * sx + tx, ay * sy + ty));
            }
            arcs.Add(arc);
        }

        // Collect land polygons
        var polygons = new List<IReadOnlyList<(double lat, double lon)>>();
        ProcessGeometry(root.GetProperty("objects").GetProperty("land"), arcs, polygons);
        return polygons;
    }

    private static void ProcessGeometry(
        JsonElement geo,
        List<List<(double lon, double lat)>> arcs,
        List<IReadOnlyList<(double lat, double lon)>> result)
    {
        string type = geo.GetProperty("type").GetString()!;
        switch (type)
        {
            case "GeometryCollection":
                foreach (var g in geo.GetProperty("geometries").EnumerateArray())
                    ProcessGeometry(g, arcs, result);
                break;
            case "Polygon":
                AddRing(geo.GetProperty("arcs")[0], arcs, result);
                break;
            case "MultiPolygon":
                foreach (var poly in geo.GetProperty("arcs").EnumerateArray())
                    AddRing(poly[0], arcs, result);
                break;
        }
    }

    private static void AddRing(
        JsonElement ring,
        List<List<(double lon, double lat)>> arcs,
        List<IReadOnlyList<(double lat, double lon)>> result)
    {
        var polygon = new List<(double lat, double lon)>();
        foreach (var refEl in ring.EnumerateArray())
        {
            int idx      = refEl.GetInt32();
            bool reverse = idx < 0;
            if (reverse) idx = ~idx;
            var arc = arcs[idx];
            if (reverse)
                for (int i = arc.Count - 1; i >= 0; i--)
                    polygon.Add((arc[i].lat, arc[i].lon));
            else
                foreach (var (lon, lat) in arc)
                    polygon.Add((lat, lon));
        }
        if (polygon.Count >= 3)
            result.Add(polygon);
    }
}
