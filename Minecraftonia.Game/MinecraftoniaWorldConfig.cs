using System;

namespace Minecraftonia.Game;

public sealed class MinecraftoniaWorldConfig
{
    public int ChunkSizeX { get; init; } = 16;
    public int ChunkSizeY { get; init; } = 16;
    public int ChunkSizeZ { get; init; } = 16;

    public int ChunkCountX { get; init; } = 6;
    public int ChunkCountY { get; init; } = 3;
    public int ChunkCountZ { get; init; } = 6;

    public int WaterLevel { get; init; } = 8;
    public int Seed { get; init; } = 1337;
    public TerrainGenerationMode GenerationMode { get; init; } = TerrainGenerationMode.Legacy;
    public bool UseOpenStreetMap { get; init; } = true;
    public bool RequireOpenStreetMap { get; init; } = true;

    public int Width => ChunkSizeX * ChunkCountX;
    public int Height => ChunkSizeY * ChunkCountY;
    public int Depth => ChunkSizeZ * ChunkCountZ;

    public static MinecraftoniaWorldConfig FromDimensions(
        int width,
        int height,
        int depth,
        int waterLevel,
        int seed,
        int chunkSizeX = 16,
        int chunkSizeY = 16,
        int chunkSizeZ = 16,
        bool useOpenStreetMap = true,
        bool requireOpenStreetMap = true)
    {
        if (width % chunkSizeX != 0)
        {
            throw new ArgumentException($"Width {width} must be divisible by chunk size X {chunkSizeX}.", nameof(width));
        }

        if (height % chunkSizeY != 0)
        {
            throw new ArgumentException($"Height {height} must be divisible by chunk size Y {chunkSizeY}.", nameof(height));
        }

        if (depth % chunkSizeZ != 0)
        {
            throw new ArgumentException($"Depth {depth} must be divisible by chunk size Z {chunkSizeZ}.", nameof(depth));
        }

        return new MinecraftoniaWorldConfig
        {
            ChunkSizeX = chunkSizeX,
            ChunkSizeY = chunkSizeY,
            ChunkSizeZ = chunkSizeZ,
            ChunkCountX = width / chunkSizeX,
            ChunkCountY = height / chunkSizeY,
            ChunkCountZ = depth / chunkSizeZ,
            WaterLevel = waterLevel,
            Seed = seed,
            GenerationMode = TerrainGenerationMode.Legacy,
            UseOpenStreetMap = useOpenStreetMap,
            RequireOpenStreetMap = requireOpenStreetMap
        };
    }
}
