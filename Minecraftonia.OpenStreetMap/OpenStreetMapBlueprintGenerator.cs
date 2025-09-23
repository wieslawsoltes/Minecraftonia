using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Minecraftonia.WaveFunctionCollapse;

namespace Minecraftonia.OpenStreetMap;

public static class OpenStreetMapBlueprintGenerator
{
    private const string OverpassInterpreterUrl = "https://overpass-api.de/api/interpreter";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private const string OsmBuildingTag = "osm_building";
    private const string OsmRoadTag = "osm_road";

    private static readonly OpenStreetMapRegion[] Regions =
    {
        new("Paris, France", 48.8566, 2.3522, 0.18, 0.28),
        new("Tokyo, Japan", 35.6762, 139.6503, 0.20, 0.30),
        new("New York, USA", 40.7128, -74.0060, 0.20, 0.28),
        new("São Paulo, Brazil", -23.5505, -46.6333, 0.22, 0.30),
        new("Sydney, Australia", -33.8688, 151.2093, 0.16, 0.22),
        new("Johannesburg, South Africa", -26.2041, 28.0473, 0.18, 0.26),
        new("Berlin, Germany", 52.52, 13.4050, 0.18, 0.26),
        new("Seoul, South Korea", 37.5665, 126.9780, 0.16, 0.24),
        new("Mexico City, Mexico", 19.4326, -99.1332, 0.20, 0.28),
        new("Toronto, Canada", 43.6532, -79.3832, 0.18, 0.26),
        new("Mumbai, India", 19.0760, 72.8777, 0.20, 0.30),
        new("Istanbul, Türkiye", 41.0082, 28.9784, 0.22, 0.32)
    };

    public static bool TryCreate(
        int width,
        int depth,
        int seed,
        out MacroBlueprint? blueprint,
        out string? sourceDescription,
        out string? sourceLink)
    {
        blueprint = null;
        sourceDescription = null;
        sourceLink = null;

        var random = new Random(seed ^ 0x5f21a6dd);
        const int maxRegionSamples = 8;
        const int boundsPerRegion = 3;

        for (int sample = 0; sample < maxRegionSamples; sample++)
        {
            var region = Regions[random.Next(Regions.Length)];

            for (int boundsAttempt = 0; boundsAttempt < boundsPerRegion; boundsAttempt++)
            {
                var bounds = region.CreateBoundingBox(random);
                string query = BuildQuery(bounds);
                string? payload = FetchOverpassPayload(query);
                if (payload is null)
                {
                    continue;
                }

                if (!TryParseOverpass(payload, out var nodes, out var ways))
                {
                    continue;
                }

                if (nodes.Count == 0 || ways.Count == 0)
                {
                    continue;
                }

                if (!HasUsableContent(ways))
                {
                    continue;
                }

                int attemptSeed = seed
                    ^ unchecked(sample * (int)0x9e3779b9)
                    ^ unchecked(boundsAttempt * (int)0x6d2b79f5);
                var cells = InitializeCells(width, depth, attemptSeed);
                RasterizeLand(cells, bounds, ways, nodes, attemptSeed);
                ApplyWaterMargins(cells);
                EnsureDefaultTags(cells);
                SmoothSettlementMask(cells);
                SmoothSettlementMask(cells);

                var clusters = MacroBlueprintGenerator.BuildSettlementClusters(cells, attemptSeed);
                blueprint = new MacroBlueprint(cells, clusters);
                sourceDescription = region.Name;
                sourceLink = BuildOpenStreetMapLink(bounds);
                return true;
            }
        }

        return false;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.GZip
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(25)
        };
    }

    private static string BuildQuery(GeoBoundingBox bounds)
    {
        string rectangle = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2},{3}",
            bounds.South,
            bounds.West,
            bounds.North,
            bounds.East);

        var builder = new StringBuilder();
        builder.Append("[out:json][timeout:25];");
        builder.Append("(");
        builder.AppendFormat(CultureInfo.InvariantCulture, "way[\"building\"]({0});", rectangle);
        builder.AppendFormat(CultureInfo.InvariantCulture, "way[\"highway\"]({0});", rectangle);
        builder.AppendFormat(CultureInfo.InvariantCulture, "way[\"landuse\"]({0});", rectangle);
        builder.AppendFormat(CultureInfo.InvariantCulture, "way[\"natural\"=\"water\"]({0});", rectangle);
        builder.AppendFormat(CultureInfo.InvariantCulture, "way[\"waterway\"]({0});", rectangle);
        builder.Append(");");
        builder.Append("(._;>;);");
        builder.Append("out body;");

        return builder.ToString();
    }

    private static string? FetchOverpassPayload(string query)
    {
        try
        {
            using var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("data", query)
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, OverpassInterpreterUrl)
            {
                Content = content
            };

            using var response = HttpClient.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    private static bool TryParseOverpass(
        string payload,
        out Dictionary<long, GeoPoint> nodes,
        out List<OsmWay> ways)
    {
        nodes = new Dictionary<long, GeoPoint>();
        ways = new List<OsmWay>();

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("elements", out var elements))
            {
                return false;
            }

            foreach (var element in elements.EnumerateArray())
            {
                if (!element.TryGetProperty("type", out var typeProperty))
                {
                    continue;
                }

                var type = typeProperty.GetString();
                if (type is null)
                {
                    continue;
                }

                switch (type)
                {
                    case "node":
                        if (!element.TryGetProperty("id", out var nodeIdProperty))
                        {
                            continue;
                        }

                        long nodeId = nodeIdProperty.GetInt64();
                        double lat = element.GetProperty("lat").GetDouble();
                        double lon = element.GetProperty("lon").GetDouble();
                        nodes[nodeId] = new GeoPoint(lat, lon);
                        break;

                    case "way":
                        if (!element.TryGetProperty("id", out var wayIdProperty))
                        {
                            continue;
                        }

                        long wayId = wayIdProperty.GetInt64();
                        if (!element.TryGetProperty("nodes", out var nodeArray))
                        {
                            continue;
                        }

                        var nodeIds = new List<long>();
                        foreach (var nodeRef in nodeArray.EnumerateArray())
                        {
                            nodeIds.Add(nodeRef.GetInt64());
                        }

                        if (!element.TryGetProperty("tags", out var tagsProperty))
                        {
                            continue;
                        }

                        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var tag in tagsProperty.EnumerateObject())
                        {
                            tags[tag.Name] = tag.Value.GetString() ?? string.Empty;
                        }

                        ways.Add(new OsmWay(wayId, nodeIds, tags));
                        break;
                }
            }

            return true;
        }
        catch
        {
            nodes = new Dictionary<long, GeoPoint>();
            ways = new List<OsmWay>();
            return false;
        }
    }

    private static MacroBlueprintCell[,] InitializeCells(int width, int depth, int seed)
    {
        var cells = new MacroBlueprintCell[width, depth];
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                var cell = new MacroBlueprintCell
                {
                    TargetElevation = ComputeBaseElevation(x, z, width, depth, seed)
                };
                cell.AddTag(VoxelPatternLibraryFactory.BlueprintGrassland);
                if (cell.TargetElevation > 0.74)
                {
                    cell.AddTag(VoxelPatternLibraryFactory.BlueprintMountain);
                }
                else if (cell.TargetElevation > 0.64)
                {
                    cell.AddTag(VoxelPatternLibraryFactory.BlueprintHills);
                }
                else if (cell.TargetElevation < 0.32)
                {
                    cell.AddTag(VoxelPatternLibraryFactory.BlueprintWetland);
                }
                cells[x, z] = cell;
            }
        }

        return cells;
    }

    private static void RasterizeLand(
        MacroBlueprintCell[,] cells,
        GeoBoundingBox bounds,
        List<OsmWay> ways,
        Dictionary<long, GeoPoint> nodes,
        int seed)
    {
        var highways = ways.Where(w => w.Tags.ContainsKey("highway")).ToList();
        var buildings = ways.Where(w => w.Tags.ContainsKey("building")).ToList();
        var landuses = ways.Where(w => w.Tags.ContainsKey("landuse")).ToList();
        var water = ways.Where(w => w.Tags.TryGetValue("natural", out var natural) && natural.Equals("water", StringComparison.OrdinalIgnoreCase)).ToList();
        var waterways = ways.Where(w => w.Tags.ContainsKey("waterway")).ToList();

        foreach (var way in landuses)
        {
            if (!way.Tags.TryGetValue("landuse", out var value))
            {
                continue;
            }

            Action<MacroBlueprintCell> action = value.ToLowerInvariant() switch
            {
                "residential" or "commercial" or "industrial" or "retail" or "construction" or "military" => ProcessSettlementLanduse,
                "forest" or "wood" => ProcessForestCell,
                "meadow" or "grass" or "recreation_ground" or "village_green" or "cemetery" or "farmland" or "orchard" or "vineyard" => ProcessLanduseCell,
                "reservoir" or "basin" or "pond" or "salt_pond" or "water" => ProcessWaterCell,
                _ => ProcessLanduseCell
            };

            RasterizePolygon(cells, bounds, nodes, way, action);
        }

        foreach (var way in water)
        {
            RasterizePolygon(cells, bounds, nodes, way, ProcessWaterCell);
        }

        foreach (var way in waterways)
        {
            int thickness = GetWaterwayThickness(way);
            RasterizePolyline(cells, bounds, nodes, way, ProcessWaterCell, thickness);
        }

        foreach (var way in highways)
        {
            int thickness = GetHighwayThickness(way);
            RasterizePolyline(cells, bounds, nodes, way, ProcessHighwayCell, thickness);
        }

        foreach (var way in buildings)
        {
            RasterizePolygon(cells, bounds, nodes, way, ProcessBuildingCell);
        }
    }

    private static void EnsureDefaultTags(MacroBlueprintCell[,] cells)
    {
        int width = cells.GetLength(0);
        int depth = cells.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                var cell = cells[x, z];

                if (!cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintSettlement) &&
                    !cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintWater) &&
                    !cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintWetland) &&
                    !cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintDesert))
                {
                    cell.AddTag(VoxelPatternLibraryFactory.BlueprintGrassland);
                }
            }
        }
    }

    private static void ProcessLanduseCell(MacroBlueprintCell cell)
    {
        if (cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintWater))
        {
            return;
        }

        cell.AddTag(VoxelPatternLibraryFactory.BlueprintGrassland);
        cell.TargetElevation = Math.Clamp(cell.TargetElevation, 0.38, 0.52);
    }

    private static void ProcessForestCell(MacroBlueprintCell cell)
    {
        if (cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintWater))
        {
            return;
        }

        cell.AddTag(VoxelPatternLibraryFactory.BlueprintForest);
        cell.AddTag(VoxelPatternLibraryFactory.BlueprintGrassland);
        cell.TargetElevation = Math.Clamp(cell.TargetElevation, 0.40, 0.54);
    }

    private static void ProcessSettlementLanduse(MacroBlueprintCell cell)
    {
        if (cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintWater))
        {
            return;
        }

        cell.AddTag(VoxelPatternLibraryFactory.BlueprintSettlement);
        cell.TargetElevation = Math.Max(cell.TargetElevation, 0.46);
    }

    private static void ProcessWaterCell(MacroBlueprintCell cell)
    {
        cell.AddTag(VoxelPatternLibraryFactory.BlueprintWater);
        cell.RemoveTag(VoxelPatternLibraryFactory.BlueprintSettlement);
        cell.TargetElevation = 0.1;
    }

    private static void ProcessHighwayCell(MacroBlueprintCell cell)
    {
        if (cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintWater))
        {
            return;
        }

        cell.AddTag(VoxelPatternLibraryFactory.BlueprintSettlement);
        cell.AddTag(OsmRoadTag);
        cell.TargetElevation = Math.Max(cell.TargetElevation, 0.44);
    }

    private static void ProcessBuildingCell(MacroBlueprintCell cell)
    {
        if (cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintWater))
        {
            return;
        }

        cell.AddTag(VoxelPatternLibraryFactory.BlueprintSettlement);
        cell.AddTag(OsmBuildingTag);
        cell.TargetElevation = Math.Max(cell.TargetElevation, 0.5);
    }

    private static void RasterizePolygon(
        MacroBlueprintCell[,] cells,
        GeoBoundingBox bounds,
        Dictionary<long, GeoPoint> nodes,
        OsmWay way,
        Action<MacroBlueprintCell> callback)
    {
        var points = ResolveWayPoints(nodes, way);
        if (points.Count < 3)
        {
            return;
        }

        if (!IsClosed(points))
        {
            points.Add(points[0]);
        }

        var projected = ProjectPoints(points, bounds, cells.GetLength(0), cells.GetLength(1));
        FillPolygon(cells, projected, callback);
    }

    private static void RasterizePolyline(
        MacroBlueprintCell[,] cells,
        GeoBoundingBox bounds,
        Dictionary<long, GeoPoint> nodes,
        OsmWay way,
        Action<MacroBlueprintCell> callback,
        int thickness)
    {
        var points = ResolveWayPoints(nodes, way);
        if (points.Count < 2)
        {
            return;
        }

        var projected = ProjectPoints(points, bounds, cells.GetLength(0), cells.GetLength(1));
        DrawPolyline(cells, projected, thickness, callback);
    }

    private static List<GeoPoint> ResolveWayPoints(Dictionary<long, GeoPoint> nodes, OsmWay way)
    {
        var points = new List<GeoPoint>(way.NodeIds.Count);
        foreach (var id in way.NodeIds)
        {
            if (nodes.TryGetValue(id, out var point))
            {
                points.Add(point);
            }
        }

        return points;
    }

    private static bool IsClosed(List<GeoPoint> points)
    {
        if (points.Count < 3)
        {
            return false;
        }

        var first = points[0];
        var last = points[^1];
        return Math.Abs(first.Latitude - last.Latitude) < 1e-6 && Math.Abs(first.Longitude - last.Longitude) < 1e-6;
    }

    private static List<(double X, double Z)> ProjectPoints(
        List<GeoPoint> points,
        GeoBoundingBox bounds,
        int width,
        int depth)
    {
        var list = new List<(double X, double Z)>(points.Count);
        double latRange = Math.Max(bounds.North - bounds.South, 1e-6);
        double lonRange = Math.Max(bounds.East - bounds.West, 1e-6);

        foreach (var point in points)
        {
            double nx = (point.Longitude - bounds.West) / lonRange;
            double nz = (point.Latitude - bounds.South) / latRange;

            nx = Math.Clamp(nx, 0.0, 1.0);
            nz = Math.Clamp(nz, 0.0, 1.0);

            list.Add((nx * (width - 1), nz * (depth - 1)));
        }

        return list;
    }

    private static void FillPolygon(
        MacroBlueprintCell[,] cells,
        List<(double X, double Z)> vertices,
        Action<MacroBlueprintCell> callback)
    {
        if (vertices.Count < 3)
        {
            return;
        }

        double minX = vertices.Min(v => v.X);
        double maxX = vertices.Max(v => v.X);
        double minZ = vertices.Min(v => v.Z);
        double maxZ = vertices.Max(v => v.Z);

        int startX = Math.Max(0, (int)Math.Floor(minX));
        int endX = Math.Min(cells.GetLength(0) - 1, (int)Math.Ceiling(maxX));
        int startZ = Math.Max(0, (int)Math.Floor(minZ));
        int endZ = Math.Min(cells.GetLength(1) - 1, (int)Math.Ceiling(maxZ));

        for (int x = startX; x <= endX; x++)
        {
            for (int z = startZ; z <= endZ; z++)
            {
                double px = x + 0.5;
                double pz = z + 0.5;

                if (IsPointInPolygon(px, pz, vertices))
                {
                    callback(cells[x, z]);
                }
            }
        }
    }

    private static void DrawPolyline(
        MacroBlueprintCell[,] cells,
        List<(double X, double Z)> points,
        int thickness,
        Action<MacroBlueprintCell> callback)
    {
        int width = cells.GetLength(0);
        int depth = cells.GetLength(1);
        int radius = Math.Max(1, thickness);

        for (int i = 0; i < points.Count - 1; i++)
        {
            var (x0, z0) = points[i];
            var (x1, z1) = points[i + 1];

            int ix0 = (int)Math.Round(x0);
            int iz0 = (int)Math.Round(z0);
            int ix1 = (int)Math.Round(x1);
            int iz1 = (int)Math.Round(z1);

            PlotLine(ix0, iz0, ix1, iz1, (gx, gz) =>
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        int nx = gx + dx;
                        int nz = gz + dz;
                        if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
                        {
                            continue;
                        }

                        callback(cells[nx, nz]);
                    }
                }
            });
        }
    }

    private static bool IsPointInPolygon(double x, double z, List<(double X, double Z)> vertices)
    {
        bool inside = false;
        int count = vertices.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            var pi = vertices[i];
            var pj = vertices[j];

            bool intersect = ((pi.Z > z) != (pj.Z > z)) &&
                (x < (pj.X - pi.X) * (z - pi.Z) / (pj.Z - pi.Z + double.Epsilon) + pi.X);

            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static void PlotLine(int x0, int z0, int x1, int z1, Action<int, int> plot)
    {
        int dx = Math.Abs(x1 - x0);
        int dz = Math.Abs(z1 - z0);
        int sx = x0 < x1 ? 1 : -1;
        int sz = z0 < z1 ? 1 : -1;
        int err = dx - dz;

        while (true)
        {
            plot(x0, z0);
            if (x0 == x1 && z0 == z1)
            {
                break;
            }

            int e2 = 2 * err;
            if (e2 > -dz)
            {
                err -= dz;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                z0 += sz;
            }
        }
    }

    private static void ApplyWaterMargins(MacroBlueprintCell[,] cells)
    {
        int width = cells.GetLength(0);
        int depth = cells.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                if (!cells[x, z].ContainsTag(VoxelPatternLibraryFactory.BlueprintWater))
                {
                    continue;
                }

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0)
                        {
                            continue;
                        }

                        int nx = x + dx;
                        int nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
                        {
                            continue;
                        }

                        var neighbor = cells[nx, nz];
                        if (neighbor.ContainsTag(VoxelPatternLibraryFactory.BlueprintWater))
                        {
                            continue;
                        }

                        neighbor.AddTag(VoxelPatternLibraryFactory.BlueprintShore);
                        neighbor.RemoveTag(VoxelPatternLibraryFactory.BlueprintSettlement);
                        neighbor.TargetElevation = Math.Min(neighbor.TargetElevation, 0.36);
                    }
                }
            }
        }
    }

    private static void SmoothSettlementMask(MacroBlueprintCell[,] cells)
    {
        int width = cells.GetLength(0);
        int depth = cells.GetLength(1);

        var additions = new List<(int X, int Z)>();
        var removals = new List<(int X, int Z)>();

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                var cell = cells[x, z];
                bool isSettlement = cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintSettlement);
                int neighborCount = 0;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0)
                        {
                            continue;
                        }

                        int nx = x + dx;
                        int nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
                        {
                            continue;
                        }

                        if (cells[nx, nz].ContainsTag(VoxelPatternLibraryFactory.BlueprintSettlement))
                        {
                            neighborCount++;
                        }
                    }
                }

                if (!isSettlement && neighborCount >= 4 && !cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintWater))
                {
                    additions.Add((x, z));
                }
                else if (isSettlement && neighborCount <= 2 && !cell.ContainsTag(OsmBuildingTag) && !cell.ContainsTag(OsmRoadTag))
                {
                    removals.Add((x, z));
                }
            }
        }

        foreach (var (x, z) in additions)
        {
            var cell = cells[x, z];
            cell.AddTag(VoxelPatternLibraryFactory.BlueprintSettlement);
            cell.TargetElevation = Math.Max(cell.TargetElevation, 0.47);
        }

        foreach (var (x, z) in removals)
        {
            cells[x, z].RemoveTag(VoxelPatternLibraryFactory.BlueprintSettlement);
        }
    }

    private static double ComputeBaseElevation(int x, int z, int width, int depth, int seed)
    {
        double nx = width > 1 ? x / (double)(width - 1) : 0.0;
        double nz = depth > 1 ? z / (double)(depth - 1) : 0.0;

        double continental = FractalNoise(nx * 1.8, nz * 1.8, seed ^ 0x59a1, 3);
        double regional = FractalNoise(nx * 4.3 + 8.1, nz * 4.3 - 3.7, seed ^ 0x1aa3, 2);
        double ridge = FractalNoise(nx * 7.1 - 2.4, nz * 7.1 + 6.2, seed ^ 0x7f42, 2);
        double blended = 0.55 * continental + 0.3 * regional + 0.15 * ridge;

        return Math.Clamp(0.28 + blended * 0.5, 0.18, 0.86);
    }

    private static double FractalNoise(double x, double z, int seed, int octaves)
    {
        double total = 0.0;
        double amplitude = 1.0;
        double frequency = 1.0;
        double sumAmplitude = 0.0;

        for (int i = 0; i < octaves; i++)
        {
            total += ValueNoise(x * frequency, z * frequency, seed + i * 9187) * amplitude;
            sumAmplitude += amplitude;
            amplitude *= 0.5;
            frequency *= 2.0;
        }

        return sumAmplitude > 0 ? total / sumAmplitude : 0.0;
    }

    private static double ValueNoise(double x, double z, int seed)
    {
        int x0 = (int)Math.Floor(x);
        int z0 = (int)Math.Floor(z);
        int x1 = x0 + 1;
        int z1 = z0 + 1;

        double tx = x - x0;
        double tz = z - z0;

        double v00 = Hash(x0, z0, seed);
        double v10 = Hash(x1, z0, seed);
        double v01 = Hash(x0, z1, seed);
        double v11 = Hash(x1, z1, seed);

        double ix0 = Lerp(v00, v10, SmoothStep(tx));
        double ix1 = Lerp(v01, v11, SmoothStep(tx));
        return Lerp(ix0, ix1, SmoothStep(tz));
    }

    private static double Hash(int x, int z, int seed)
    {
        int h = seed;
        h = unchecked(h * 73856093) ^ x;
        h = unchecked(h * 19349663) ^ z;
        h ^= h >> 13;
        h ^= h << 7;
        h &= int.MaxValue;
        return h / (double)int.MaxValue;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double SmoothStep(double t) => t * t * (3 - 2 * t);

    private static int GetHighwayThickness(OsmWay way)
    {
        if (!way.Tags.TryGetValue("highway", out var type))
        {
            return 2;
        }

        return type.ToLowerInvariant() switch
        {
            "motorway" or "trunk" => 5,
            "primary" => 4,
            "secondary" => 4,
            "tertiary" or "unclassified" => 3,
            "residential" or "living_street" => 3,
            "service" or "track" => 2,
            "footway" or "path" or "cycleway" => 1,
            _ => 2
        };
    }

    private static int GetWaterwayThickness(OsmWay way)
    {
        if (!way.Tags.TryGetValue("waterway", out var type))
        {
            return 2;
        }

        return type.ToLowerInvariant() switch
        {
            "river" or "canal" => 5,
            "stream" => 3,
            "drain" or "ditch" => 2,
            _ => 2
        };
    }

    private static bool HasUsableContent(List<OsmWay> ways)
    {
        int buildingCount = 0;
        int roadCount = 0;
        int landuseCount = 0;
        int waterCount = 0;

        foreach (var way in ways)
        {
            if (way.Tags.ContainsKey("building"))
            {
                buildingCount++;
                continue;
            }

            if (way.Tags.ContainsKey("highway"))
            {
                roadCount++;
                continue;
            }

            if (way.Tags.ContainsKey("landuse"))
            {
                landuseCount++;
                continue;
            }

            if (way.Tags.ContainsKey("waterway") ||
                (way.Tags.TryGetValue("natural", out var natural) && natural.Equals("water", StringComparison.OrdinalIgnoreCase)))
            {
                waterCount++;
            }
        }

        if (buildingCount + roadCount >= 6)
        {
            return true;
        }

        if (roadCount >= 4 && landuseCount >= 2)
        {
            return true;
        }

        if (buildingCount >= 3 && landuseCount >= 3)
        {
            return true;
        }

        if (waterCount >= 3)
        {
            return true;
        }

        return false;
    }

    private readonly record struct GeoPoint(double Latitude, double Longitude);

    private readonly record struct GeoBoundingBox(double South, double West, double North, double East)
    {
        public double WidthDegrees => East - West;
        public double HeightDegrees => North - South;
        public double CenterLatitude => (South + North) * 0.5;
        public double CenterLongitude => (West + East) * 0.5;
    }

    private sealed record OsmWay(long Id, List<long> NodeIds, Dictionary<string, string> Tags);

    private sealed class OpenStreetMapRegion
    {
        private readonly double _latSpan;
        private readonly double _lonSpan;

        public OpenStreetMapRegion(string name, double latitude, double longitude, double latitudeSpanDegrees, double longitudeSpanDegrees)
        {
            Name = name;
            Latitude = latitude;
            Longitude = longitude;
            _latSpan = latitudeSpanDegrees;
            _lonSpan = longitudeSpanDegrees;
        }

        public string Name { get; }
        public double Latitude { get; }
        public double Longitude { get; }

        public GeoBoundingBox CreateBoundingBox(Random random)
        {
            double latSpan = _latSpan * (0.55 + random.NextDouble() * 0.45);
            double lonSpan = _lonSpan * (0.55 + random.NextDouble() * 0.45);

            double latOffset = (_latSpan - latSpan) * (random.NextDouble() - 0.5);
            double lonOffset = (_lonSpan - lonSpan) * (random.NextDouble() - 0.5);

            double south = Latitude - latSpan / 2 + latOffset;
            double north = south + latSpan;
            double west = Longitude - lonSpan / 2 + lonOffset;
            double east = west + lonSpan;

            south = Math.Clamp(south, -90.0, 90.0);
            north = Math.Clamp(north, -90.0, 90.0);
            west = Math.Clamp(west, -180.0, 180.0);
            east = Math.Clamp(east, -180.0, 180.0);

            if (north <= south)
            {
                north = Math.Min(90.0, south + 0.01);
            }

            if (east <= west)
            {
                east = Math.Min(180.0, west + 0.01);
            }

            return new GeoBoundingBox(south, west, north, east);
        }
    }

    private static string BuildOpenStreetMapLink(GeoBoundingBox bounds)
    {
        double centerLat = bounds.CenterLatitude;
        double centerLon = bounds.CenterLongitude;
        double spanDegrees = Math.Max(bounds.HeightDegrees, bounds.WidthDegrees);
        spanDegrees = Math.Max(spanDegrees, 1e-4);
        double zoomGuess = Math.Clamp(14 + Math.Log2(0.25 / spanDegrees), 8, 18);
        return string.Format(
            CultureInfo.InvariantCulture,
            "https://www.openstreetmap.org/#map={0:F0}/{1:F5}/{2:F5}",
            zoomGuess,
            centerLat,
            centerLon);
    }
}
