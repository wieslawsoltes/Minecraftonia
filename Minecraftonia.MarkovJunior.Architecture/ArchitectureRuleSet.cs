using System;
using System.Collections.Generic;
using Minecraftonia.MarkovJunior;
using Minecraftonia.WaveFunctionCollapse.Architecture;

namespace Minecraftonia.MarkovJunior.Architecture;

public static class ArchitectureRuleSet
{
    private const int StructureHeight = 5;
    private const string ClusterTag = "cluster";
    private const string EdgeTag = "cluster_edge";
    public const string MultiLevelTag = "multi_level";

    public static MarkovJuniorState GenerateLayout(ArchitectureModuleType moduleType, ArchitectureClusterContext context, int seed)
    {
        if (context.LayoutWidth <= 0 || context.LayoutDepth <= 0)
        {
            return new MarkovJuniorState(1, StructureHeight, 1, ArchitectureSymbolSet.Empty);
        }

        var state = new MarkovJuniorState(context.LayoutWidth, StructureHeight, context.LayoutDepth, ArchitectureSymbolSet.Empty);
        var random = new Random(seed);

        TagClusterCells(state, context);
        CreatePerimeterBoulevard(state, context);

        int axisWidth = ComputeAxisWidth(moduleType, context);
        if (axisWidth > 0)
        {
            CarvePrimaryAxes(state, context, axisWidth);
        }

        switch (moduleType)
        {
            case ArchitectureModuleType.Temple:
                GenerateTempleLayout(state, context, random);
                break;
            case ArchitectureModuleType.Market:
                GenerateMarketLayout(state, context, random);
                break;
            default:
                GenerateHousingLayout(state, context, random);
                break;
        }

        DecorateFacades(state, context, random);
        ScatterGardens(state, context, random);

        return state;
    }

    private static int ComputeAxisWidth(ArchitectureModuleType moduleType, ArchitectureClusterContext context)
    {
        int minTile = Math.Max(2, Math.Min(context.TileSizeX, context.TileSizeZ) / 2);
        int maxAxis = Math.Max(2, Math.Min(context.LayoutWidth, context.LayoutDepth) / 2);

        return moduleType switch
        {
            ArchitectureModuleType.Temple => Math.Min(Math.Max(minTile + 2, 4), maxAxis),
            ArchitectureModuleType.Market => Math.Min(Math.Max(minTile + 1, 3), maxAxis),
            _ => Math.Min(Math.Max(minTile, 2), maxAxis)
        };
    }

    private static void TagClusterCells(MarkovJuniorState state, ArchitectureClusterContext context)
    {
        for (int tileX = 0; tileX < context.TileCountX; tileX++)
        {
            for (int tileZ = 0; tileZ < context.TileCountZ; tileZ++)
            {
                if (!context.IsTileOccupied(tileX, tileZ))
                {
                    continue;
                }

                bool edgeTile =
                    !context.IsTileOccupied(tileX - 1, tileZ) ||
                    !context.IsTileOccupied(tileX + 1, tileZ) ||
                    !context.IsTileOccupied(tileX, tileZ - 1) ||
                    !context.IsTileOccupied(tileX, tileZ + 1);

                int startX = tileX * context.TileSizeX;
                int endX = Math.Min(startX + context.TileSizeX, context.LayoutWidth) - 1;
                int startZ = tileZ * context.TileSizeZ;
                int endZ = Math.Min(startZ + context.TileSizeZ, context.LayoutDepth) - 1;

                for (int x = startX; x <= endX; x++)
                {
                    for (int z = startZ; z <= endZ; z++)
                    {
                        state.AddCellTag(x, 0, z, ClusterTag);

                        if (edgeTile || x == startX || x == endX || z == startZ || z == endZ)
                        {
                            state.AddCellTag(x, 0, z, EdgeTag);
                        }
                    }
                }
            }
        }
    }

    private static void CreatePerimeterBoulevard(MarkovJuniorState state, ArchitectureClusterContext context)
    {
        int beltWidth = Math.Max(2, Math.Min(context.TileSizeX, context.TileSizeZ) / 3);

        for (int x = 0; x < context.LayoutWidth; x++)
        {
            for (int z = 0; z < context.LayoutDepth; z++)
            {
                if (!context.IsInsideCluster(x, z))
                {
                    continue;
                }

                if (IsNearClusterBoundary(context, x, z, beltWidth))
                {
                    state.SetSymbol(x, 0, z, ArchitectureSymbolSet.Street);
                }
            }
        }
    }

    private static void CarvePrimaryAxes(MarkovJuniorState state, ArchitectureClusterContext context, int axisWidth)
    {
        int clampedWidth = Math.Max(1, Math.Min(axisWidth, Math.Min(context.LayoutWidth, context.LayoutDepth)));
        int halfWidth = clampedWidth / 2;
        int centerX = context.LayoutWidth / 2;
        int centerZ = context.LayoutDepth / 2;

        for (int x = centerX - halfWidth; x <= centerX + halfWidth; x++)
        {
            for (int z = 0; z < context.LayoutDepth; z++)
            {
                SetSymbol(state, context, x, z, ArchitectureSymbolSet.Street, overwrite: true);
            }
        }

        for (int z = centerZ - halfWidth; z <= centerZ + halfWidth; z++)
        {
            for (int x = 0; x < context.LayoutWidth; x++)
            {
                SetSymbol(state, context, x, z, ArchitectureSymbolSet.Street, overwrite: true);
            }
        }
    }

    private static void GenerateTempleLayout(MarkovJuniorState state, ArchitectureClusterContext context, Random random)
    {
        FillEmptyWithFloor(state, context);

        int centerX = context.LayoutWidth / 2;
        int centerZ = context.LayoutDepth / 2;
        int plazaSpanX = Math.Max(context.TileSizeX * 2, context.LayoutWidth / 2);
        int plazaSpanZ = Math.Max(context.TileSizeZ * 2, context.LayoutDepth / 2);

        int plazaMinX = Math.Max(0, centerX - plazaSpanX / 2);
        int plazaMaxX = Math.Min(context.LayoutWidth - 1, centerX + plazaSpanX / 2);
        int plazaMinZ = Math.Max(0, centerZ - plazaSpanZ / 2);
        int plazaMaxZ = Math.Min(context.LayoutDepth - 1, centerZ + plazaSpanZ / 2);

        FillRectangle(state, context, plazaMinX, plazaMaxX, plazaMinZ, plazaMaxZ, ArchitectureSymbolSet.Plaza, overwrite: true);

        int sanctumOffsetX = Math.Max(2, context.TileSizeX / 2);
        int sanctumOffsetZ = Math.Max(2, context.TileSizeZ / 2);

        int sanctumMinX = Math.Max(0, centerX - sanctumOffsetX);
        int sanctumMaxX = Math.Min(context.LayoutWidth - 1, centerX + sanctumOffsetX);
        int sanctumMinZ = Math.Max(0, centerZ - sanctumOffsetZ);
        int sanctumMaxZ = Math.Min(context.LayoutDepth - 1, centerZ + sanctumOffsetZ);

        FillRectangle(state, context, sanctumMinX, sanctumMaxX, sanctumMinZ, sanctumMaxZ, ArchitectureSymbolSet.Floor, overwrite: true);

        SetSymbol(state, context, centerX, centerZ, ArchitectureSymbolSet.Altar, overwrite: true);

        PlacePillarRing(state, context, sanctumMinX, sanctumMaxX, sanctumMinZ, sanctumMaxZ);
        PlaceRoofCanopy(state, context, sanctumMinX, sanctumMaxX, sanctumMinZ, sanctumMaxZ);

        PlaceGardenPatch(state, context, plazaMinX, plazaMinZ, random);
        PlaceGardenPatch(state, context, plazaMaxX - context.TileSizeX + 1, plazaMinZ, random);
        PlaceGardenPatch(state, context, plazaMinX, plazaMaxZ - context.TileSizeZ + 1, random);
        PlaceGardenPatch(state, context, plazaMaxX - context.TileSizeX + 1, plazaMaxZ - context.TileSizeZ + 1, random);
    }

    private static void GenerateMarketLayout(MarkovJuniorState state, ArchitectureClusterContext context, Random random)
    {
        FillEmptyWithFloor(state, context);

        int centerX = context.LayoutWidth / 2;
        int centerZ = context.LayoutDepth / 2;
        int plazaSpanX = Math.Max(context.TileSizeX, context.LayoutWidth / 3);
        int plazaSpanZ = Math.Max(context.TileSizeZ, context.LayoutDepth / 3);

        int plazaMinX = Math.Max(0, centerX - plazaSpanX / 2);
        int plazaMaxX = Math.Min(context.LayoutWidth - 1, centerX + plazaSpanX / 2);
        int plazaMinZ = Math.Max(0, centerZ - plazaSpanZ / 2);
        int plazaMaxZ = Math.Min(context.LayoutDepth - 1, centerZ + plazaSpanZ / 2);

        FillRectangle(state, context, plazaMinX, plazaMaxX, plazaMinZ, plazaMaxZ, ArchitectureSymbolSet.Plaza, overwrite: true);

        int aisleWidth = Math.Max(2, Math.Min(context.TileSizeX, context.TileSizeZ) / 3);
        int stallWidth = Math.Max(3, aisleWidth + 1);
        int strideX = stallWidth + aisleWidth;
        int strideZ = stallWidth + aisleWidth;

        for (int x = 0; x < context.LayoutWidth; x++)
        {
            for (int z = 0; z < context.LayoutDepth; z++)
            {
                if (!context.IsInsideCluster(x, z))
                {
                    continue;
                }

                var symbol = state.GetSymbol(x, 0, z);
                if (symbol == ArchitectureSymbolSet.Street || symbol == ArchitectureSymbolSet.Plaza)
                {
                    continue;
                }

                int moduloX = (x + stallWidth) % strideX;
                int moduloZ = (z + stallWidth) % strideZ;
                bool inAisle = moduloX < aisleWidth || moduloZ < aisleWidth;

                if (inAisle)
                {
                    SetSymbol(state, context, x, z, ArchitectureSymbolSet.Street, overwrite: true);
                }
                else
                {
                    SetSymbol(state, context, x, z, ArchitectureSymbolSet.Stall, overwrite: true);
                    if (random.NextDouble() < 0.18)
                    {
                        state.AddCellTag(x, 0, z, "market_canopy");
                    }
                }
            }
        }
    }

    private static void GenerateHousingLayout(MarkovJuniorState state, ArchitectureClusterContext context, Random random)
    {
        FillEmptyWithFloor(state, context);

        int streetWidth = Math.Max(2, Math.Min(context.TileSizeX, context.TileSizeZ) / 3);
        int blockStrideX = Math.Max(context.TileSizeX, streetWidth * 2);
        int blockStrideZ = Math.Max(context.TileSizeZ, streetWidth * 2);

        for (int x = streetWidth; x < context.LayoutWidth; x += blockStrideX)
        {
            for (int w = 0; w < streetWidth; w++)
            {
                int px = x + w;
                if (px >= context.LayoutWidth)
                {
                    break;
                }

                for (int z = 0; z < context.LayoutDepth; z++)
                {
                    SetSymbol(state, context, px, z, ArchitectureSymbolSet.Street, overwrite: true);
                }
            }
        }

        for (int z = streetWidth; z < context.LayoutDepth; z += blockStrideZ)
        {
            for (int w = 0; w < streetWidth; w++)
            {
                int pz = z + w;
                if (pz >= context.LayoutDepth)
                {
                    break;
                }

                for (int x = 0; x < context.LayoutWidth; x++)
                {
                    SetSymbol(state, context, x, pz, ArchitectureSymbolSet.Street, overwrite: true);
                }
            }
        }

        FillEmptyWithFloor(state, context);
        MarkMultiLevelDistricts(state, context, random);
    }

    private static void FillEmptyWithFloor(MarkovJuniorState state, ArchitectureClusterContext context)
    {
        for (int x = 0; x < context.LayoutWidth; x++)
        {
            for (int z = 0; z < context.LayoutDepth; z++)
            {
                if (!context.IsInsideCluster(x, z))
                {
                    continue;
                }

                if (state.GetSymbol(x, 0, z) == state.EmptySymbol)
                {
                    state.SetSymbol(x, 0, z, ArchitectureSymbolSet.Floor);
                }
            }
        }
    }

    private static void DecorateFacades(MarkovJuniorState state, ArchitectureClusterContext context, Random random)
    {
        for (int x = 0; x < context.LayoutWidth; x++)
        {
            for (int z = 0; z < context.LayoutDepth; z++)
            {
                if (!context.IsInsideCluster(x, z))
                {
                    continue;
                }

                var symbol = state.GetSymbol(x, 0, z);
                if (symbol != ArchitectureSymbolSet.Floor)
                {
                    continue;
                }

                bool nearStreet = HasNeighborSymbol(state, context, x, z, ArchitectureSymbolSet.Street);
                bool nearPlaza = HasNeighborSymbol(state, context, x, z, ArchitectureSymbolSet.Plaza);

                if (!nearStreet && !nearPlaza)
                {
                    continue;
                }

                double roll = random.NextDouble();
                if (roll < 0.22)
                {
                    state.SetSymbol(x, 0, z, ArchitectureSymbolSet.Doorway);
                }
                else if (roll < 0.55)
                {
                    state.SetSymbol(x, 0, z, ArchitectureSymbolSet.Window);
                }
            }
        }
    }

    private static void ScatterGardens(MarkovJuniorState state, ArchitectureClusterContext context, Random random)
    {
        for (int x = 0; x < context.LayoutWidth; x++)
        {
            for (int z = 0; z < context.LayoutDepth; z++)
            {
                if (!context.IsInsideCluster(x, z))
                {
                    continue;
                }

                var symbol = state.GetSymbol(x, 0, z);
                if (symbol == ArchitectureSymbolSet.Plaza && random.NextDouble() < 0.04)
                {
                    state.SetSymbol(x, 0, z, ArchitectureSymbolSet.Garden);
                    continue;
                }

                if (symbol == ArchitectureSymbolSet.Floor && random.NextDouble() < 0.06 && HasNeighborSymbol(state, context, x, z, ArchitectureSymbolSet.Street))
                {
                    state.SetSymbol(x, 0, z, ArchitectureSymbolSet.Garden);
                }
            }
        }
    }

    private static void MarkMultiLevelDistricts(MarkovJuniorState state, ArchitectureClusterContext context, Random random)
    {
        var floorCells = new List<(int X, int Z)>();
        for (int x = 0; x < context.LayoutWidth; x++)
        {
            for (int z = 0; z < context.LayoutDepth; z++)
            {
                if (!context.IsInsideCluster(x, z))
                {
                    continue;
                }

                if (state.GetSymbol(x, 0, z) == ArchitectureSymbolSet.Floor)
                {
                    floorCells.Add((x, z));
                }
            }
        }

        if (floorCells.Count == 0)
        {
            return;
        }

        Shuffle(floorCells, random);

        int targetCells = Math.Max(1, floorCells.Count / 5);
        int radius = Math.Max(2, Math.Min(context.TileSizeX, context.TileSizeZ) / 3);

        int index = 0;
        while (targetCells > 0 && index < floorCells.Count)
        {
            var (x, z) = floorCells[index++];
            if (state.ContainsCellTag(x, 0, z, MultiLevelTag))
            {
                continue;
            }

            int claimed = FloodFillMultiLevel(state, context, x, z, radius);
            if (claimed > 0)
            {
                targetCells -= claimed;
                TryPlaceStair(state, context, x, z);
            }
        }
    }

    private static int FloodFillMultiLevel(MarkovJuniorState state, ArchitectureClusterContext context, int originX, int originZ, int radius)
    {
        var queue = new Queue<(int X, int Z)>();
        var visited = new HashSet<(int X, int Z)>();
        queue.Enqueue((originX, originZ));

        int claimed = 0;
        int maxCells = Math.Max(4, radius * radius);

        while (queue.Count > 0 && claimed < maxCells)
        {
            var (x, z) = queue.Dequeue();
            if (!visited.Add((x, z)))
            {
                continue;
            }

            if (!context.IsInsideCluster(x, z))
            {
                continue;
            }

            if (Math.Abs(x - originX) > radius || Math.Abs(z - originZ) > radius)
            {
                continue;
            }

            if (state.GetSymbol(x, 0, z) != ArchitectureSymbolSet.Floor)
            {
                continue;
            }

            if (state.ContainsCellTag(x, 0, z, MultiLevelTag))
            {
                continue;
            }

            state.AddCellTag(x, 0, z, MultiLevelTag);
            claimed++;

            queue.Enqueue((x + 1, z));
            queue.Enqueue((x - 1, z));
            queue.Enqueue((x, z + 1));
            queue.Enqueue((x, z - 1));
        }

        return claimed;
    }

    private static void TryPlaceStair(MarkovJuniorState state, ArchitectureClusterContext context, int originX, int originZ)
    {
        for (int radius = 1; radius <= 3; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Abs(dx) + Math.Abs(dz) != radius)
                    {
                        continue;
                    }

                    int nx = originX + dx;
                    int nz = originZ + dz;

                    if (!context.IsInsideCluster(nx, nz))
                    {
                        continue;
                    }

                    if (state.GetSymbol(nx, 0, nz) == ArchitectureSymbolSet.Street)
                    {
                        state.SetSymbol(nx, 0, nz, ArchitectureSymbolSet.Stair);
                        return;
                    }
                }
            }
        }
    }

    private static void PlacePillarRing(MarkovJuniorState state, ArchitectureClusterContext context, int minX, int maxX, int minZ, int maxZ)
    {
        var positions = new (int X, int Z)[]
        {
            (minX, minZ),
            (minX, maxZ),
            (maxX, minZ),
            (maxX, maxZ),
            ((minX + maxX) / 2, minZ),
            ((minX + maxX) / 2, maxZ),
        };

        foreach (var (x, z) in positions)
        {
            SetSymbol(state, context, x, z, ArchitectureSymbolSet.Pillar, overwrite: true);
        }
    }

    private static void PlaceRoofCanopy(MarkovJuniorState state, ArchitectureClusterContext context, int minX, int maxX, int minZ, int maxZ)
    {
        int canopyPadding = 1;
        int canopyMinX = Math.Max(0, minX - canopyPadding);
        int canopyMaxX = Math.Min(context.LayoutWidth - 1, maxX + canopyPadding);
        int canopyMinZ = Math.Max(0, minZ - canopyPadding);
        int canopyMaxZ = Math.Min(context.LayoutDepth - 1, maxZ + canopyPadding);

        for (int x = canopyMinX; x <= canopyMaxX; x++)
        {
            for (int z = canopyMinZ; z <= canopyMaxZ; z++)
            {
                if (!context.IsInsideCluster(x, z))
                {
                    continue;
                }

                if (state.GetSymbol(x, 0, z) == ArchitectureSymbolSet.Plaza)
                {
                    continue;
                }

                if (x >= minX && x <= maxX && z >= minZ && z <= maxZ)
                {
                    SetSymbol(state, context, x, z, ArchitectureSymbolSet.Roof, overwrite: false);
                }
            }
        }
    }

    private static void SetSymbol(MarkovJuniorState state, ArchitectureClusterContext context, int x, int z, MarkovSymbol symbol, bool overwrite)
    {
        if (!context.IsInsideCluster(x, z))
        {
            return;
        }

        var current = state.GetSymbol(x, 0, z);
        if (!overwrite && current != state.EmptySymbol && current != ArchitectureSymbolSet.Floor)
        {
            return;
        }

        state.SetSymbol(x, 0, z, symbol);
    }

    private static void FillRectangle(MarkovJuniorState state, ArchitectureClusterContext context, int minX, int maxX, int minZ, int maxZ, MarkovSymbol symbol, bool overwrite)
    {
        if (minX > maxX || minZ > maxZ)
        {
            return;
        }

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                SetSymbol(state, context, x, z, symbol, overwrite);
            }
        }
    }

    private static void PlaceGardenPatch(MarkovJuniorState state, ArchitectureClusterContext context, int originX, int originZ, Random random)
    {
        int sizeX = Math.Max(3, context.TileSizeX / 2);
        int sizeZ = Math.Max(3, context.TileSizeZ / 2);

        for (int x = originX; x < originX + sizeX; x++)
        {
            for (int z = originZ; z < originZ + sizeZ; z++)
            {
                if (!context.IsInsideCluster(x, z))
                {
                    continue;
                }

                if (random.NextDouble() < 0.65)
                {
                    state.SetSymbol(x, 0, z, ArchitectureSymbolSet.Garden);
                }
            }
        }
    }

    private static bool IsNearClusterBoundary(ArchitectureClusterContext context, int x, int z, int distance)
    {
        for (int dx = -distance; dx <= distance; dx++)
        {
            for (int dz = -distance; dz <= distance; dz++)
            {
                if (Math.Abs(dx) + Math.Abs(dz) > distance)
                {
                    continue;
                }

                int nx = x + dx;
                int nz = z + dz;
                if (!context.IsInsideCluster(nx, nz))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasNeighborSymbol(MarkovJuniorState state, ArchitectureClusterContext context, int x, int z, MarkovSymbol symbol)
    {
        var offsets = new (int dx, int dz)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        foreach (var (dx, dz) in offsets)
        {
            int nx = x + dx;
            int nz = z + dz;
            if (!context.IsInsideCluster(nx, nz))
            {
                continue;
            }

            if (state.GetSymbol(nx, 0, nz) == symbol)
            {
                return true;
            }
        }

        return false;
    }

    private static void Shuffle<T>(IList<T> list, Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
