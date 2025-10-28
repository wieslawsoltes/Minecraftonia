using System.Collections.Generic;
using System.Numerics;

namespace Minecraftonia.VoxelEngine;

public interface IVoxelWorld<TBlock>
{
    ChunkDimensions ChunkSize { get; }
    int ChunkCountX { get; }
    int ChunkCountY { get; }
    int ChunkCountZ { get; }
    int Width { get; }
    int Height { get; }
    int Depth { get; }

    IReadOnlyDictionary<ChunkCoordinate, VoxelChunk<TBlock>> LoadedChunks { get; }

    bool InBounds(int x, int y, int z);
    bool InBounds(Vector3 position);

    TBlock GetBlock(int x, int y, int z);
    TBlock GetBlock(Int3 position);
    TBlock GetBlock(int x, int y, int z, ref VoxelBlockAccessCache<TBlock> cache);
    TBlock GetBlockFast(
        int chunkX,
        int chunkY,
        int chunkZ,
        int localX,
        int localY,
        int localZ,
        ref VoxelBlockAccessCache<TBlock> cache);

    ChunkCoordinate GetChunkCoordinate(int x, int y, int z);
    ChunkCoordinate GetChunkCoordinate(Vector3 position);

    IEnumerable<ChunkCoordinate> EnumerateLoadedChunks();
    bool TryGetLoadedChunk(ChunkCoordinate coordinate, out VoxelChunk<TBlock> chunk);
}
