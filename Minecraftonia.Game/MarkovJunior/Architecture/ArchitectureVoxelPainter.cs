using System;
using Minecraftonia.Game.MarkovJunior;
using Minecraftonia.WaveFunctionCollapse;
using Minecraftonia.WaveFunctionCollapse.Architecture;

namespace Minecraftonia.Game.MarkovJunior.Architecture;

internal static class ArchitectureVoxelPainter
{
    private const int BaseStructureHeight = 5;

    public static void Apply(BlockType[,,] blocks, int[,] heightMap, MacroBlueprint blueprint, int tileSizeX, int tileSizeZ, Random random, string? source = null)
    {
        if (blueprint is null)
        {
            return;
        }

        foreach (var cluster in blueprint.Clusters)
        {
            var context = new ArchitectureClusterContext(cluster, tileSizeX, tileSizeZ);
            if (context.LayoutWidth <= 0 || context.LayoutDepth <= 0)
            {
                continue;
            }

            var layout = ArchitectureRuleSet.GenerateLayout(cluster.ModuleType, context, random.Next());
            int originX = context.OriginGridX * tileSizeX;
            int originZ = context.OriginGridZ * tileSizeZ;

            ArchitectureDebugExporter.ExportCluster(blueprint, layout, cluster, context, originX, originZ, source);
            BakeCluster(blocks, heightMap, layout, context, originX, originZ, cluster.ModuleType, random);
        }
    }

    private static void BakeCluster(
        BlockType[,,] blocks,
        int[,] heightMap,
        MarkovJuniorState layout,
        ArchitectureClusterContext context,
        int originX,
        int originZ,
        ArchitectureModuleType moduleType,
        Random random)
    {
        int maxY = blocks.GetLength(1);

        for (int lx = 0; lx < layout.SizeX; lx++)
        {
            for (int lz = 0; lz < layout.SizeZ; lz++)
            {
                if (!context.IsInsideCluster(lx, lz))
                {
                    continue;
                }

                int worldX = originX + lx;
                int worldZ = originZ + lz;

                if (worldX < 0 || worldZ < 0 || worldX >= blocks.GetLength(0) || worldZ >= blocks.GetLength(2))
                {
                    continue;
                }

                int baseY = Math.Clamp(heightMap[worldX, worldZ], 1, maxY - 6);
                var symbol = layout.GetSymbol(lx, 0, lz);
                if (symbol == layout.EmptySymbol)
                {
                    continue;
                }

                bool multiLevel = layout.ContainsCellTag(lx, 0, lz, ArchitectureRuleSet.MultiLevelTag);
                bool canopy = layout.ContainsCellTag(lx, 0, lz, "market_canopy");

                int structureHeight = DetermineStructureHeight(moduleType, multiLevel, random);

                switch (symbol.Id)
                {
                    case "street":
                        PaintStreet(blocks, heightMap, worldX, baseY, worldZ);
                        break;
                    case "plaza":
                        PaintPlaza(blocks, heightMap, worldX, baseY, worldZ);
                        break;
                    case "garden":
                        PaintGarden(blocks, heightMap, worldX, baseY, worldZ, random);
                        break;
                    case "floor":
                        PaintFloor(blocks, heightMap, layout, lx, lz, worldX, baseY, worldZ, structureHeight, multiLevel, maxY);
                        break;
                    case "doorway":
                        PaintDoorway(blocks, heightMap, layout, lx, lz, worldX, baseY, worldZ, structureHeight, multiLevel, maxY);
                        break;
                    case "window":
                        PaintWindow(blocks, heightMap, layout, lx, lz, worldX, baseY, worldZ, structureHeight, multiLevel, maxY);
                        break;
                    case "pillar":
                        PaintPillar(blocks, heightMap, worldX, baseY, worldZ, structureHeight, maxY);
                        break;
                    case "stair":
                        PaintStair(blocks, heightMap, worldX, baseY, worldZ, maxY);
                        break;
                    case "stall":
                        PaintStall(blocks, heightMap, worldX, baseY, worldZ, canopy, maxY);
                        break;
                    case "roof":
                        PaintRoof(blocks, heightMap, layout, lx, lz, worldX, baseY, worldZ, structureHeight, maxY);
                        break;
                    case "altar":
                        PaintAltar(blocks, heightMap, worldX, baseY, worldZ, maxY);
                        break;
                }
            }
        }
    }

    private static int DetermineStructureHeight(ArchitectureModuleType moduleType, bool multiLevel, Random random)
    {
        int baseHeight = moduleType switch
        {
            ArchitectureModuleType.Temple => random.Next(6, 9),
            ArchitectureModuleType.Market => random.Next(4, 6),
            _ => random.Next(4, BaseStructureHeight + 3)
        };

        if (multiLevel)
        {
            baseHeight += 2;
        }

        return baseHeight;
    }

    private static void PaintStreet(BlockType[,,] blocks, int[,] heightMap, int x, int baseY, int z)
    {
        SetBlock(blocks, x, baseY, z, BlockType.Stone);
        for (int y = baseY + 1; y < baseY + 3 && y < blocks.GetLength(1); y++)
        {
            SetBlock(blocks, x, y, z, BlockType.Air);
        }
        heightMap[x, z] = baseY;
    }

    private static void PaintPlaza(BlockType[,,] blocks, int[,] heightMap, int x, int baseY, int z)
    {
        SetBlock(blocks, x, baseY, z, BlockType.Dirt);
        if (baseY + 1 < blocks.GetLength(1))
        {
            SetBlock(blocks, x, baseY + 1, z, BlockType.Air);
        }
        heightMap[x, z] = baseY;
    }

    private static void PaintGarden(BlockType[,,] blocks, int[,] heightMap, int x, int baseY, int z, Random random)
    {
        SetBlock(blocks, x, baseY, z, BlockType.Grass);
        if (random.NextDouble() < 0.6 && baseY + 1 < blocks.GetLength(1))
        {
            SetBlock(blocks, x, baseY + 1, z, BlockType.Leaves);
        }
        heightMap[x, z] = baseY;
    }

    private static int PaintFloor(
        BlockType[,,] blocks,
        int[,] heightMap,
        MarkovJuniorState layout,
        int lx,
        int lz,
        int worldX,
        int baseY,
        int worldZ,
        int structureHeight,
        bool multiLevel,
        int maxY)
    {
        SetBlock(blocks, worldX, baseY, worldZ, BlockType.Stone);
        if (baseY + 1 < maxY)
        {
            SetBlock(blocks, worldX, baseY + 1, worldZ, BlockType.Wood);
        }

        int wallTop = Math.Min(baseY + structureHeight, maxY - 2);
        bool perimeter = IsPerimeter(layout, lx, lz);

        if (perimeter)
        {
            for (int y = baseY + 2; y <= wallTop; y++)
            {
                SetBlock(blocks, worldX, y, worldZ, BlockType.Wood);
            }
        }
        else
        {
            for (int y = baseY + 2; y <= wallTop; y++)
            {
                SetBlock(blocks, worldX, y, worldZ, BlockType.Air);
            }
        }

        if (multiLevel)
        {
            int mezzanine = Math.Min(wallTop - 1, baseY + 3);
            if (mezzanine > baseY + 2)
            {
                SetBlock(blocks, worldX, mezzanine, worldZ, BlockType.Wood);
            }
            wallTop = Math.Min(wallTop + 1, maxY - 2);
        }

        heightMap[worldX, worldZ] = Math.Max(heightMap[worldX, worldZ], wallTop);
        return wallTop;
    }

    private static void PaintDoorway(
        BlockType[,,] blocks,
        int[,] heightMap,
        MarkovJuniorState layout,
        int lx,
        int lz,
        int worldX,
        int baseY,
        int worldZ,
        int structureHeight,
        bool multiLevel,
        int maxY)
    {
        int wallTop = PaintFloor(blocks, heightMap, layout, lx, lz, worldX, baseY, worldZ, structureHeight, multiLevel, maxY);
        for (int y = baseY + 2; y <= Math.Min(baseY + 3, wallTop); y++)
        {
            SetBlock(blocks, worldX, y, worldZ, BlockType.Air);
        }
    }

    private static void PaintWindow(
        BlockType[,,] blocks,
        int[,] heightMap,
        MarkovJuniorState layout,
        int lx,
        int lz,
        int worldX,
        int baseY,
        int worldZ,
        int structureHeight,
        bool multiLevel,
        int maxY)
    {
        int wallTop = PaintFloor(blocks, heightMap, layout, lx, lz, worldX, baseY, worldZ, structureHeight, multiLevel, maxY);
        if (baseY + 3 <= wallTop)
        {
            SetBlock(blocks, worldX, baseY + 3, worldZ, BlockType.Leaves);
        }
    }

    private static void PaintPillar(BlockType[,,] blocks, int[,] heightMap, int x, int baseY, int z, int structureHeight, int maxY)
    {
        int top = Math.Min(baseY + structureHeight + 1, maxY - 1);
        for (int y = baseY; y <= top; y++)
        {
            SetBlock(blocks, x, y, z, BlockType.Stone);
        }
        heightMap[x, z] = Math.Max(heightMap[x, z], top);
    }

    private static void PaintStair(BlockType[,,] blocks, int[,] heightMap, int x, int baseY, int z, int maxY)
    {
        SetBlock(blocks, x, baseY, z, BlockType.Stone);
        if (baseY + 1 < maxY)
        {
            SetBlock(blocks, x, baseY + 1, z, BlockType.Wood);
        }
        heightMap[x, z] = Math.Max(heightMap[x, z], baseY + 1);
    }

    private static void PaintStall(BlockType[,,] blocks, int[,] heightMap, int x, int baseY, int z, bool canopy, int maxY)
    {
        SetBlock(blocks, x, baseY, z, BlockType.Stone);
        if (baseY + 1 < maxY)
        {
            SetBlock(blocks, x, baseY + 1, z, BlockType.Wood);
        }

        if (canopy && baseY + 2 < maxY)
        {
            SetBlock(blocks, x, baseY + 2, z, BlockType.Leaves);
            heightMap[x, z] = baseY + 2;
        }
        else
        {
            heightMap[x, z] = Math.Max(heightMap[x, z], baseY + 1);
        }
    }

    private static void PaintRoof(
        BlockType[,,] blocks,
        int[,] heightMap,
        MarkovJuniorState layout,
        int lx,
        int lz,
        int worldX,
        int baseY,
        int worldZ,
        int structureHeight,
        int maxY)
    {
        int top = PaintFloor(blocks, heightMap, layout, lx, lz, worldX, baseY, worldZ, structureHeight, multiLevel: false, maxY);
        if (top + 1 < maxY)
        {
            SetBlock(blocks, worldX, top + 1, worldZ, BlockType.Wood);
            heightMap[worldX, worldZ] = top + 1;
        }
    }

    private static void PaintAltar(BlockType[,,] blocks, int[,] heightMap, int x, int baseY, int z, int maxY)
    {
        SetBlock(blocks, x, baseY, z, BlockType.Stone);
        if (baseY + 1 < maxY)
        {
            SetBlock(blocks, x, baseY + 1, z, BlockType.Stone);
        }
        if (baseY + 2 < maxY)
        {
            SetBlock(blocks, x, baseY + 2, z, BlockType.Leaves);
        }
        heightMap[x, z] = Math.Max(heightMap[x, z], Math.Min(baseY + 2, maxY - 1));
    }

    private static bool IsPerimeter(MarkovJuniorState state, int x, int z)
    {
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
                if (!state.InBounds(nx, 0, nz))
                {
                    return true;
                }

                var neighbor = state.GetSymbol(nx, 0, nz);
                if (neighbor == state.EmptySymbol || neighbor == ArchitectureSymbolSet.Street || neighbor == ArchitectureSymbolSet.Plaza)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void SetBlock(BlockType[,,] blocks, int x, int y, int z, BlockType block)
    {
        if (x < 0 || z < 0 || x >= blocks.GetLength(0) || z >= blocks.GetLength(2))
        {
            return;
        }

        if (y < 0 || y >= blocks.GetLength(1))
        {
            return;
        }

        blocks[x, y, z] = block;
    }
}
