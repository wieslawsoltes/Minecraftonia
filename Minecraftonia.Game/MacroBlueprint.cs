using System;
using System.Collections.Generic;
using Minecraftonia.Game.MarkovJunior.Architecture;

namespace Minecraftonia.Game;

internal sealed class MacroBlueprint
{
    private readonly MacroBlueprintCell[,] _cells;

    public MacroBlueprint(MacroBlueprintCell[,] cells, IReadOnlyList<SettlementCluster> clusters)
    {
        _cells = cells ?? throw new ArgumentNullException(nameof(cells));
        Width = cells.GetLength(0);
        Depth = cells.GetLength(1);
        Clusters = clusters ?? Array.Empty<SettlementCluster>();
    }

    public int Width { get; }
    public int Depth { get; }
    public IReadOnlyList<SettlementCluster> Clusters { get; }

    public IReadOnlyCollection<string> GetTags(int x, int z) => _cells[x, z].Tags;
    public double GetTargetElevation(int x, int z) => _cells[x, z].TargetElevation;
    public bool HasTag(int x, int z, string tag) => _cells[x, z].ContainsTag(tag);
    public SettlementCluster? GetCluster(int x, int z)
    {
        int id = _cells[x, z].ClusterId;
        if (id < 0 || id >= Clusters.Count)
        {
            return null;
        }

        return Clusters[id];
    }
    public ArchitectureModuleType? GetClusterModuleType(int x, int z)
    {
        int id = _cells[x, z].ClusterId;
        if (id < 0 || id >= Clusters.Count)
        {
            return null;
        }

        return Clusters[id].ModuleType;
    }

    public double GetMultiplier(VoxelPattern3D pattern, int x, int z)
    {
        double multiplier = 1.0;
        var cell = _cells[x, z];
        var patternTags = pattern.BlueprintTags;

        if (cell.Tags.Count > 0)
        {
            if (patternTags.Count > 0)
            {
                bool intersects = false;
                foreach (var tag in patternTags)
                {
                    if (cell.Tags.Contains(tag))
                    {
                        intersects = true;
                        break;
                    }
                }

                bool cellIsWater = cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintWater) || cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintRiver);
                bool patternIsWater = PatternHasWaterAffinity(patternTags);

                if (intersects)
                {
                    multiplier *= 3.0;
                }
                else if (cellIsWater && !patternIsWater)
                {
                    multiplier *= 0.05;
                }
                else if (!cellIsWater && patternIsWater)
                {
                    multiplier *= 0.25;
                }
                else
                {
                    multiplier *= 0.35;
                }
            }
            else
            {
                multiplier *= 0.6;
            }
        }
        else if (patternTags.Count > 0)
        {
            multiplier *= 0.55;
        }

        double elevationDiff = Math.Abs(pattern.AverageSurfaceHeightNormalized - cell.TargetElevation);
        double elevationMultiplier = Math.Clamp(1.4 - elevationDiff * 2.2, 0.2, 1.6);

        return Math.Max(0.05, multiplier * elevationMultiplier);
    }

    private static bool PatternHasWaterAffinity(IReadOnlyCollection<string> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Equals(VoxelPatternLibraryFactory.BlueprintWater, StringComparison.OrdinalIgnoreCase) ||
                tag.Equals(VoxelPatternLibraryFactory.BlueprintRiver, StringComparison.OrdinalIgnoreCase) ||
                tag.Equals(VoxelPatternLibraryFactory.BlueprintWetland, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

internal static class MacroBlueprintGenerator
{
    private static readonly (int dx, int dz)[] ClusterNeighborOffsets =
    {
        (-1, 0),
        (1, 0),
        (0, -1),
        (0, 1),
        (-1, -1),
        (-1, 1),
        (1, -1),
        (1, 1)
    };

    public static MacroBlueprint Create(int width, int depth, int seed)
    {
        var cells = new MacroBlueprintCell[width, depth];
        var random = new Random(seed);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                cells[x, z] = new MacroBlueprintCell();
            }
        }

        GenerateBaseFields(cells, seed);
        LayRiver(cells, random, vertical: true);
        LayRiver(cells, random, vertical: false);
        var clusters = BuildSettlementClusters(cells, seed);
        return new MacroBlueprint(cells, clusters);
    }

    private static void GenerateBaseFields(MacroBlueprintCell[,] cells, int seed)
    {
        int width = cells.GetLength(0);
        int depth = cells.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                double nx = width > 1 ? x / (double)(width - 1) : 0.0;
                double nz = depth > 1 ? z / (double)(depth - 1) : 0.0;

                double continental = FractalNoise(nx * 2.3, nz * 2.3, seed ^ 0x4921, 3);
                double ridge = FractalNoise(nx * 5.1 + 12.4, nz * 5.1 - 6.3, seed ^ 0x8213, 2);
                double heightValue = Math.Clamp(0.55 * continental + 0.45 * ridge, 0.0, 1.0);

                double moisture = FractalNoise(nx * 3.1 + 5.7, nz * 3.1 + 2.2, seed ^ 0x3344, 3);

                var cell = cells[x, z];
                cell.TargetElevation = heightValue;

                if (heightValue < 0.20)
                {
                    cell.AddTag(VoxelPatternLibraryFactory.BlueprintWater);
                    cell.TargetElevation = 0.15;
                }
                else if (heightValue < 0.28)
                {
                    cell.AddTag(VoxelPatternLibraryFactory.BlueprintShore);
                    cell.TargetElevation = 0.25;
                }
                else if (heightValue > 0.78)
                {
                    cell.AddTag(VoxelPatternLibraryFactory.BlueprintMountain);
                    cell.TargetElevation = 0.85;
                }
                else if (heightValue > 0.62)
                {
                    cell.AddTag(VoxelPatternLibraryFactory.BlueprintHills);
                    cell.TargetElevation = 0.68;
                }
                else
                {
                    cell.AddTag(VoxelPatternLibraryFactory.BlueprintGrassland);
                    cell.TargetElevation = Math.Clamp(heightValue, 0.3, 0.55);
                }

                if (moisture > 0.7 && heightValue < 0.6)
                {
                    cell.AddTag(VoxelPatternLibraryFactory.BlueprintWetland);
                }
                else if (moisture < 0.28 && heightValue < 0.75)
                {
                    cell.AddTag(VoxelPatternLibraryFactory.BlueprintDesert);
                }

                bool suitableForSettlement =
                    !cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintWater) &&
                    !cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintWetland) &&
                    !cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintMountain) &&
                    !cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintDesert) &&
                    heightValue > 0.3 && heightValue < 0.62 && moisture > 0.32 && moisture < 0.68;

                if (suitableForSettlement)
                {
                    cell.AddTag(VoxelPatternLibraryFactory.BlueprintSettlement);
                }
            }
        }
    }

    private static void LayRiver(MacroBlueprintCell[,] cells, Random random, bool vertical)
    {
        int width = cells.GetLength(0);
        int depth = cells.GetLength(1);

        if (vertical)
        {
            int x = random.Next(width);
            for (int z = 0; z < depth; z++)
            {
                StampRiver(cells, x, z);
                x += random.Next(-1, 2);
                x = Math.Clamp(x, 1, width - 2);
            }
        }
        else
        {
            int z = random.Next(depth);
            for (int x = 0; x < width; x++)
            {
                StampRiver(cells, x, z);
                z += random.Next(-1, 2);
                z = Math.Clamp(z, 1, depth - 2);
            }
        }
    }

    private static void StampRiver(MacroBlueprintCell[,] cells, int x, int z)
    {
        var cell = cells[x, z];
        cell.AddTag(VoxelPatternLibraryFactory.BlueprintRiver);
        cell.AddTag(VoxelPatternLibraryFactory.BlueprintWater);
        cell.TargetElevation = Math.Min(cell.TargetElevation, 0.18);
        cell.RemoveTag(VoxelPatternLibraryFactory.BlueprintSettlement);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                int nx = x + dx;
                int nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= cells.GetLength(0) || nz >= cells.GetLength(1))
                {
                    continue;
                }

                if (dx == 0 && dz == 0)
                {
                    continue;
                }

                var neighbor = cells[nx, nz];
                if (!neighbor.ContainsTag(VoxelPatternLibraryFactory.BlueprintWater) && !neighbor.ContainsTag(VoxelPatternLibraryFactory.BlueprintRiver))
                {
                    neighbor.AddTag(VoxelPatternLibraryFactory.BlueprintShore);
                    neighbor.AddTag(VoxelPatternLibraryFactory.BlueprintGrassland);
                    neighbor.TargetElevation = Math.Min(neighbor.TargetElevation, 0.35);
                }

                neighbor.RemoveTag(VoxelPatternLibraryFactory.BlueprintSettlement);
            }
        }
    }

    private static IReadOnlyList<SettlementCluster> BuildSettlementClusters(MacroBlueprintCell[,] cells, int seed)
    {
        int width = cells.GetLength(0);
        int depth = cells.GetLength(1);
        var visited = new bool[width, depth];
        var clusters = new List<SettlementCluster>();
        int clusterId = 0;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                if (visited[x, z])
                {
                    continue;
                }

                if (!cells[x, z].ContainsTag(VoxelPatternLibraryFactory.BlueprintSettlement))
                {
                    continue;
                }

                var clusterCells = new List<(int X, int Z)>();
                var queue = new Queue<(int X, int Z)>();
                queue.Enqueue((x, z));
                visited[x, z] = true;

                while (queue.Count > 0)
                {
                    var (cx, cz) = queue.Dequeue();
                    clusterCells.Add((cx, cz));
                    cells[cx, cz].ClusterId = clusterId;

                    foreach (var (dx, dz) in ClusterNeighborOffsets)
                    {
                        int nx = cx + dx;
                        int nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
                        {
                            continue;
                        }

                        if (visited[nx, nz])
                        {
                            continue;
                        }

                        if (!cells[nx, nz].ContainsTag(VoxelPatternLibraryFactory.BlueprintSettlement))
                        {
                            continue;
                        }

                        visited[nx, nz] = true;
                        queue.Enqueue((nx, nz));
                    }
                }

                if (clusterCells.Count > 0)
                {
                    var cluster = new SettlementCluster(clusterId, ArchitectureModuleType.Housing, clusterCells);
                    clusters.Add(cluster);
                    clusterId++;
                }
            }
        }

        ExpandClusters(cells, clusters);
        AssignClusterTypes(clusters, seed);
        return clusters;
    }

    private static void ExpandClusters(MacroBlueprintCell[,] cells, List<SettlementCluster> clusters)
    {
        if (clusters.Count == 0)
        {
            return;
        }

        foreach (var cluster in clusters)
        {
            int steps = cluster.Area >= 20 ? 3 : cluster.Area >= 9 ? 2 : 1;
            ExpandCluster(cells, cluster, steps);
        }
    }

    private static void ExpandCluster(MacroBlueprintCell[,] cells, SettlementCluster cluster, int steps)
    {
        if (steps <= 0)
        {
            return;
        }

        int width = cells.GetLength(0);
        int depth = cells.GetLength(1);
        var frontier = new HashSet<(int X, int Z)>(cluster.Cells);

        for (int step = 0; step < steps; step++)
        {
            var additions = new List<(int X, int Z)>();

            foreach (var (cx, cz) in frontier)
            {
                foreach (var (dx, dz) in ClusterNeighborOffsets)
                {
                    int nx = cx + dx;
                    int nz = cz + dz;
                    if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
                    {
                        continue;
                    }

                    var cell = cells[nx, nz];
                    if (cell.ClusterId >= 0)
                    {
                        continue;
                    }

                    if (!IsEligibleForSettlementExpansion(cell))
                    {
                        continue;
                    }

                    cell.ClusterId = cluster.Id;
                    cell.AddTag(VoxelPatternLibraryFactory.BlueprintSettlement);
                    additions.Add((nx, nz));
                }
            }

            if (additions.Count == 0)
            {
                break;
            }

            foreach (var (ax, az) in additions)
            {
                cluster.AddCell(ax, az);
            }

            frontier = new HashSet<(int X, int Z)>(additions);
        }
    }

    private static bool IsEligibleForSettlementExpansion(MacroBlueprintCell cell)
    {
        if (cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintWater) ||
            cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintRiver) ||
            cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintWetland) ||
            cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintMountain))
        {
            return false;
        }

        if (cell.ContainsTag(VoxelPatternLibraryFactory.BlueprintDesert))
        {
            return false;
        }

        return true;
    }

    private static void AssignClusterTypes(List<SettlementCluster> clusters, int seed)
    {
        if (clusters.Count == 0)
        {
            return;
        }

        clusters.Sort((a, b) => b.Cells.Count.CompareTo(a.Cells.Count));
        var random = new Random(seed ^ 0x6f9a);

        if (clusters.Count > 0)
        {
            clusters[0].ModuleType = clusters[0].Area >= 6
                ? ArchitectureModuleType.Temple
                : ArchitectureModuleType.Market;
        }

        if (clusters.Count > 1)
        {
            clusters[1].ModuleType = clusters[1].Area >= 6
                ? ArchitectureModuleType.Market
                : ArchitectureModuleType.Housing;
        }

        for (int i = 2; i < clusters.Count; i++)
        {
            var candidate = clusters[i];
            double marketChance = candidate.Area >= 12 ? 0.5 : candidate.Area >= 6 ? 0.35 : 0.18;

            if (candidate.Area >= 18 && random.NextDouble() < 0.12)
            {
                candidate.ModuleType = ArchitectureModuleType.Temple;
                continue;
            }

            candidate.ModuleType = random.NextDouble() < marketChance
                ? ArchitectureModuleType.Market
                : ArchitectureModuleType.Housing;
        }

        clusters.Sort((a, b) => a.Id.CompareTo(b.Id));
    }

    private static double FractalNoise(double x, double z, int seed, int octaves)
    {
        double total = 0.0;
        double amplitude = 1.0;
        double frequency = 1.0;
        double sumAmplitude = 0.0;

        for (int i = 0; i < octaves; i++)
        {
            total += ValueNoise(x * frequency, z * frequency, seed + i * 9173) * amplitude;
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
}

internal sealed class MacroBlueprintCell
{
    private readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Tags => _tags;
    public double TargetElevation { get; set; }
    public int ClusterId { get; set; } = -1;

    public void AddTag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            _tags.Add(tag.ToLowerInvariant());
        }
    }

    public bool ContainsTag(string tag) => _tags.Contains(tag);

    public void RemoveTag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            _tags.Remove(tag.ToLowerInvariant());
        }
    }
}

internal sealed class SettlementCluster
{
    private readonly List<(int X, int Z)> _cells = new();

    public SettlementCluster(int id, ArchitectureModuleType moduleType, IEnumerable<(int X, int Z)> cells)
    {
        Id = id;
        ModuleType = moduleType;

        if (cells is not null)
        {
            foreach (var (x, z) in cells)
            {
                AddCell(x, z);
            }
        }
    }

    public int Id { get; }
    public ArchitectureModuleType ModuleType { get; set; }
    public IReadOnlyList<(int X, int Z)> Cells => _cells;
    public int Area => _cells.Count;
    public int MinX { get; private set; } = int.MaxValue;
    public int MaxX { get; private set; } = int.MinValue;
    public int MinZ { get; private set; } = int.MaxValue;
    public int MaxZ { get; private set; } = int.MinValue;
    public int Width => Area == 0 ? 0 : MaxX - MinX + 1;
    public int Depth => Area == 0 ? 0 : MaxZ - MinZ + 1;

    public void AddCell(int x, int z)
    {
        _cells.Add((x, z));

        if (Area == 1)
        {
            MinX = MaxX = x;
            MinZ = MaxZ = z;
            return;
        }

        if (x < MinX)
        {
            MinX = x;
        }

        if (x > MaxX)
        {
            MaxX = x;
        }

        if (z < MinZ)
        {
            MinZ = z;
        }

        if (z > MaxZ)
        {
            MaxZ = z;
        }
    }
}
