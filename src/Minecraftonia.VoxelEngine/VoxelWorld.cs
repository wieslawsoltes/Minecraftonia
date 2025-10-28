using System;
using System.Collections.Generic;
using System.Numerics;

namespace Minecraftonia.VoxelEngine;

public abstract class VoxelWorld<TBlock> : IVoxelWorld<TBlock>
{
    private readonly Dictionary<ChunkCoordinate, VoxelChunk<TBlock>> _chunks = new();

    protected VoxelWorld(ChunkDimensions chunkSize, int chunkCountX, int chunkCountY, int chunkCountZ)
    {
        if (chunkCountX <= 0) throw new ArgumentOutOfRangeException(nameof(chunkCountX));
        if (chunkCountY <= 0) throw new ArgumentOutOfRangeException(nameof(chunkCountY));
        if (chunkCountZ <= 0) throw new ArgumentOutOfRangeException(nameof(chunkCountZ));

        ChunkSize = chunkSize;
        ChunkCountX = chunkCountX;
        ChunkCountY = chunkCountY;
        ChunkCountZ = chunkCountZ;
    }

    public ChunkDimensions ChunkSize { get; }
    public int ChunkCountX { get; }
    public int ChunkCountY { get; }
    public int ChunkCountZ { get; }

    public int Width => ChunkSize.SizeX * ChunkCountX;
    public int Height => ChunkSize.SizeY * ChunkCountY;
    public int Depth => ChunkSize.SizeZ * ChunkCountZ;

    public IReadOnlyDictionary<ChunkCoordinate, VoxelChunk<TBlock>> LoadedChunks => _chunks;

    public bool InBounds(int x, int y, int z)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height && z >= 0 && z < Depth;
    }

    public bool InBounds(Vector3 position)
    {
        int x = (int)MathF.Floor(position.X);
        int y = (int)MathF.Floor(position.Y);
        int z = (int)MathF.Floor(position.Z);
        return InBounds(x, y, z);
    }

    public TBlock GetBlock(int x, int y, int z)
    {
        if (!InBounds(x, y, z))
        {
            return default!;
        }

        var chunkCoord = GetChunkCoordinate(x, y, z);
        var chunk = GetOrCreateChunk(chunkCoord);
        var (localX, localY, localZ) = GetLocalCoordinates(chunkCoord, x, y, z);
        return chunk.GetBlock(localX, localY, localZ);
    }

    public TBlock GetBlock(int x, int y, int z, ref VoxelBlockAccessCache<TBlock> cache)
    {
        if (!InBounds(x, y, z))
        {
            return default!;
        }

        var dims = ChunkSize;
        int chunkX = Math.DivRem(x, dims.SizeX, out int localX);
        int chunkY = Math.DivRem(y, dims.SizeY, out int localY);
        int chunkZ = Math.DivRem(z, dims.SizeZ, out int localZ);
        var chunkCoord = new ChunkCoordinate(chunkX, chunkY, chunkZ);

        if (!cache.Matches(chunkCoord))
        {
            var chunk = GetOrCreateChunk(chunkCoord);
            cache.SetChunk(chunk);
        }

        var blocks = cache.Blocks!;
        int index = (localY * dims.SizeZ + localZ) * dims.SizeX + localX;
        return blocks[index];
    }

    public TBlock GetBlock(Int3 position) => GetBlock(position.X, position.Y, position.Z);

    public void SetBlock(int x, int y, int z, TBlock block)
    {
        if (!InBounds(x, y, z))
        {
            return;
        }

        var chunkCoord = GetChunkCoordinate(x, y, z);
        var chunk = GetOrCreateChunk(chunkCoord);
        var (localX, localY, localZ) = GetLocalCoordinates(chunkCoord, x, y, z);
        chunk.SetBlock(localX, localY, localZ, block, markDirty: true);
    }

    public void SetBlock(Int3 position, TBlock block) => SetBlock(position.X, position.Y, position.Z, block);

    public TBlock GetBlockFast(
        int chunkX,
        int chunkY,
        int chunkZ,
        int localX,
        int localY,
        int localZ,
        ref VoxelBlockAccessCache<TBlock> cache)
    {
        if (chunkX < 0 || chunkX >= ChunkCountX ||
            chunkY < 0 || chunkY >= ChunkCountY ||
            chunkZ < 0 || chunkZ >= ChunkCountZ)
        {
            return default!;
        }

        var coordinate = new ChunkCoordinate(chunkX, chunkY, chunkZ);
        if (!cache.Matches(coordinate))
        {
            var chunk = GetOrCreateChunk(coordinate);
            cache.SetChunk(chunk);
        }

        var blocks = cache.Blocks!;
        int index = (localY * ChunkSize.SizeZ + localZ) * ChunkSize.SizeX + localX;
        return blocks[index];
    }

    public ChunkCoordinate GetChunkCoordinate(int x, int y, int z)
    {
        return new ChunkCoordinate(
            x / ChunkSize.SizeX,
            y / ChunkSize.SizeY,
            z / ChunkSize.SizeZ);
    }

    public ChunkCoordinate GetChunkCoordinate(Vector3 position)
    {
        int clampedX = Math.Clamp((int)MathF.Floor(position.X), 0, Math.Max(Width - 1, 0));
        int clampedY = Math.Clamp((int)MathF.Floor(position.Y), 0, Math.Max(Height - 1, 0));
        int clampedZ = Math.Clamp((int)MathF.Floor(position.Z), 0, Math.Max(Depth - 1, 0));
        return GetChunkCoordinate(clampedX, clampedY, clampedZ);
    }

    public (int localX, int localY, int localZ) GetLocalCoordinates(ChunkCoordinate chunkCoord, int x, int y, int z)
    {
        int localX = x - chunkCoord.X * ChunkSize.SizeX;
        int localY = y - chunkCoord.Y * ChunkSize.SizeY;
        int localZ = z - chunkCoord.Z * ChunkSize.SizeZ;
        return (localX, localY, localZ);
    }

    public IEnumerable<ChunkCoordinate> EnumerateLoadedChunks()
    {
        return _chunks.Keys;
    }

    public bool TryGetLoadedChunk(ChunkCoordinate coordinate, out VoxelChunk<TBlock> chunk)
    {
        return _chunks.TryGetValue(coordinate, out chunk!);
    }

    public void LoadChunk(ChunkCoordinate coordinate, ReadOnlySpan<TBlock> data, bool markDirty)
    {
        if (data.Length != ChunkSize.Volume)
        {
            throw new ArgumentException($"Chunk data length {data.Length} does not match expected volume {ChunkSize.Volume}.", nameof(data));
        }

        var chunk = new VoxelChunk<TBlock>(coordinate, ChunkSize);
        data.CopyTo(chunk.DataSpan);
        chunk.RecalculateOccupancy();
        chunk.MarkPopulated();
        if (markDirty)
        {
            chunk.MarkDirty();
        }
        else
        {
            chunk.ClearDirty();
        }

        _chunks[coordinate] = chunk;
    }

    public void EnsureChunksInRange(ChunkCoordinate center, int radius)
    {
        var required = new HashSet<ChunkCoordinate>();
        for (int dx = -radius; dx <= radius; dx++)
        {
            int chunkX = center.X + dx;
            if (chunkX < 0 || chunkX >= ChunkCountX)
            {
                continue;
            }

            for (int dz = -radius; dz <= radius; dz++)
            {
                int chunkZ = center.Z + dz;
                if (chunkZ < 0 || chunkZ >= ChunkCountZ)
                {
                    continue;
                }

                for (int dy = -radius; dy <= radius; dy++)
                {
                    int chunkY = center.Y + dy;
                    if (chunkY < 0 || chunkY >= ChunkCountY)
                    {
                        continue;
                    }

                    var coord = new ChunkCoordinate(chunkX, chunkY, chunkZ);
                    required.Add(coord);
                    GetOrCreateChunk(coord);
                }
            }
        }

        var toRemove = new List<ChunkCoordinate>();
        foreach (var kvp in _chunks)
        {
            if (required.Contains(kvp.Key))
            {
                continue;
            }

            if (kvp.Value.IsDirty)
            {
                continue;
            }

            toRemove.Add(kvp.Key);
        }

        foreach (var remove in toRemove)
        {
            _chunks.Remove(remove);
        }
    }

    protected abstract void PopulateChunk(VoxelChunk<TBlock> chunk);

    private VoxelChunk<TBlock> GetOrCreateChunk(ChunkCoordinate coordinate)
    {
        if (!_chunks.TryGetValue(coordinate, out var chunk))
        {
            if (!IsChunkInBounds(coordinate))
            {
                throw new ArgumentOutOfRangeException(nameof(coordinate), coordinate, "Chunk coordinate is outside of world bounds.");
            }

            chunk = new VoxelChunk<TBlock>(coordinate, ChunkSize);
            _chunks[coordinate] = chunk;
            PopulateChunk(chunk);
            chunk.MarkPopulated();
        }

        return chunk;
    }

    private bool IsChunkInBounds(ChunkCoordinate coordinate)
    {
        return coordinate.X >= 0 && coordinate.X < ChunkCountX
               && coordinate.Y >= 0 && coordinate.Y < ChunkCountY
               && coordinate.Z >= 0 && coordinate.Z < ChunkCountZ;
    }
}
