using System;
using System.Collections.Generic;
using System.Numerics;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.Game;

public sealed class MinecraftoniaVoxelWorld : VoxelWorld<BlockType>
{
    private readonly Random _random;

    public int WaterLevel { get; }
    public int Seed { get; }

    public MinecraftoniaVoxelWorld(
        int width,
        int height,
        int depth,
        int waterLevel = 8,
        int seed = 1337,
        ReadOnlySpan<BlockType> blockData = default)
        : base(width, height, depth)
    {
        WaterLevel = waterLevel;
        Seed = seed;
        _random = new Random(seed);

        if (!blockData.IsEmpty)
        {
            LoadBlocks(blockData);
        }
        else
        {
            GenerateTerrain();
        }
    }

    private void LoadBlocks(ReadOnlySpan<BlockType> blockData)
    {
        int expected = Width * Height * Depth;
        if (blockData.Length != expected)
        {
            throw new ArgumentException($"Block data must contain {expected} entries.", nameof(blockData));
        }

        int index = 0;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int z = 0; z < Depth; z++)
                {
                    Blocks[x, y, z] = blockData[index++];
                }
            }
        }
    }

    private void GenerateTerrain()
    {
        float scale1 = 0.12f;
        float scale2 = 0.04f;
        float hillStrength = 6f;
        float mountainStrength = 16f;
        float baseHeight = WaterLevel + 2f;

        for (int x = 0; x < Width; x++)
        {
            for (int z = 0; z < Depth; z++)
            {
                float nx = x * scale1;
                float nz = z * scale1;
                float nx2 = x * scale2;
                float nz2 = z * scale2;

                float undulation = MathF.Sin(nx) * MathF.Cos(nz) * hillStrength;
                float broad = MathF.Sin(nx2 * 0.7f + nz2 * 0.5f) * mountainStrength;
                float ridge = MathF.Cos((nx + nz) * 0.65f) * 4f;

                float heightF = baseHeight + undulation + broad + ridge;
                int columnHeight = Math.Clamp((int)MathF.Round(heightF), 2, Height - 2);

                int surfaceY = columnHeight;

                for (int y = 0; y <= columnHeight; y++)
                {
                    BlockType block = BlockType.Stone;
                    if (y == columnHeight)
                    {
                        block = BlockType.Grass;
                    }
                    else if (y >= columnHeight - 3)
                    {
                        block = BlockType.Dirt;
                    }

                    if (y <= WaterLevel - 2)
                    {
                        block = BlockType.Stone;
                    }

                    Blocks[x, y, z] = block;
                }

                // Fill below water level with water and sand edges.
                for (int y = WaterLevel; y >= 0; y--)
                {
                    if (Blocks[x, y, z] == BlockType.Air)
                    {
                        if (y <= WaterLevel)
                        {
                            Blocks[x, y, z] = BlockType.Water;
                        }
                    }
                }

                if (surfaceY <= WaterLevel + 1)
                {
                    Blocks[x, surfaceY, z] = BlockType.Sand;
                    for (int offset = 1; offset <= 2 && surfaceY - offset >= 0; offset++)
                    {
                        Blocks[x, surfaceY - offset, z] = BlockType.Sand;
                    }
                }
            }
        }

        CarveCaves();
        SprinkleSurfaceDetails();
        PlantTrees();
    }

    private void CarveCaves()
    {
        int tunnelCount = Width * Depth / 64;
        for (int i = 0; i < tunnelCount; i++)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float pitch = (float)(_random.NextDouble() * Math.PI / 6 - Math.PI / 12);
            Vector3 direction = new(MathF.Cos(angle) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Sin(angle) * MathF.Cos(pitch));
            Vector3 position = new(
                _random.Next(4, Width - 4),
                _random.Next(4, Math.Clamp(WaterLevel - 3, 3, Height - 4)),
                _random.Next(4, Depth - 4));

            float length = _random.Next(20, 45);
            float radius = _random.NextSingle() * 1.8f + 1.2f;

            for (int step = 0; step < length; step++)
            {
                position += direction;
                int px = (int)MathF.Round(position.X);
                int py = (int)MathF.Round(position.Y);
                int pz = (int)MathF.Round(position.Z);

                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        for (int dz = -2; dz <= 2; dz++)
                        {
                            var dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                            if (dist <= radius)
                            {
                                int x = px + dx;
                                int y = py + dy;
                                int z = pz + dz;
                                if (InBounds(x, y, z) && y > 1)
                                {
                                    Blocks[x, y, z] = BlockType.Air;
                                }
                            }
                        }
                    }
                }

                if (_random.NextDouble() < 0.12)
                {
                    angle += (float)(_random.NextDouble() * 0.6 - 0.3);
                    pitch += (float)(_random.NextDouble() * 0.3 - 0.15);
                    direction = Vector3.Normalize(new Vector3(MathF.Cos(angle) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Sin(angle) * MathF.Cos(pitch)));
                }
            }
        }
    }

    private void SprinkleSurfaceDetails()
    {
        for (int x = 2; x < Width - 2; x++)
        {
            for (int z = 2; z < Depth - 2; z++)
            {
                for (int y = Height - 2; y >= 1; y--)
                {
                    var block = Blocks[x, y, z];
                    if (block == BlockType.Grass)
                    {
                        if (_random.NextDouble() < 0.05)
                        {
                            Blocks[x, y + 1, z] = BlockType.Leaves;
                        }
                        break;
                    }
                    else if (block == BlockType.Sand)
                    {
                        if (_random.NextDouble() < 0.02)
                        {
                            Blocks[x, y + 1, z] = BlockType.Wood;
                        }
                        break;
                    }
                    else if (block != BlockType.Air)
                    {
                        break;
                    }
                }
            }
        }
    }

    private void PlantTrees()
    {
        int treeAttempts = Width * Depth / 48;
        for (int i = 0; i < treeAttempts; i++)
        {
            int x = _random.Next(2, Width - 2);
            int z = _random.Next(2, Depth - 2);

            int surfaceY = FindSurfaceY(x, z);
            if (surfaceY < 1 || surfaceY >= Height - 6)
            {
                continue;
            }

            var blockBelow = Blocks[x, surfaceY, z];
            if (blockBelow != BlockType.Grass)
            {
                continue;
            }

            if (_random.NextDouble() < 0.35)
            {
                CreateTree(x, surfaceY + 1, z);
            }
        }
    }

    private int FindSurfaceY(int x, int z)
    {
        for (int y = Height - 2; y >= 1; y--)
        {
            var block = Blocks[x, y, z];
            if (block == BlockType.Grass || block == BlockType.Sand)
            {
                return y;
            }
        }

        return -1;
    }

    private void CreateTree(int x, int y, int z)
    {
        int trunkHeight = _random.Next(4, 6);

        for (int i = 0; i < trunkHeight; i++)
        {
            if (InBounds(x, y + i, z))
            {
                Blocks[x, y + i, z] = BlockType.Wood;
            }
        }

        int canopyBase = y + trunkHeight - 1;
        int canopyRadius = 2;

        for (int dy = -canopyRadius; dy <= canopyRadius + 1; dy++)
        {
            float layerRadius = canopyRadius - MathF.Abs(dy) * 0.6f;
            int radiusInt = Math.Max(1, (int)MathF.Round(layerRadius));

            for (int dx = -radiusInt; dx <= radiusInt; dx++)
            {
                for (int dz = -radiusInt; dz <= radiusInt; dz++)
                {
                    float dist = MathF.Sqrt(dx * dx + dz * dz);
                    if (dist <= layerRadius + 0.3f)
                    {
                        int px = x + dx;
                        int py = canopyBase + dy;
                        int pz = z + dz;
                        if (InBounds(px, py, pz))
                        {
                            if (Blocks[px, py, pz] == BlockType.Air)
                            {
                                Blocks[px, py, pz] = BlockType.Leaves;
                            }
                        }
                    }
                }
            }
        }
    }
}
