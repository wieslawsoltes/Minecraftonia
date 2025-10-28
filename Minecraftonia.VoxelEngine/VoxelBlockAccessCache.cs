namespace Minecraftonia.VoxelEngine;

public struct VoxelBlockAccessCache<TBlock>
{
    private ChunkCoordinate _coordinate;
    private VoxelChunk<TBlock>? _chunk;
    private TBlock[]? _blocks;

    internal ChunkCoordinate Coordinate => _coordinate;
    internal VoxelChunk<TBlock>? Chunk => _chunk;
    internal TBlock[]? Blocks => _blocks;

    public bool IsValid => _chunk is not null;

    internal void SetChunk(VoxelChunk<TBlock> chunk)
    {
        _chunk = chunk;
        _coordinate = chunk.Coordinate;
        _blocks = chunk.RawBlocks;
    }

    internal bool Matches(ChunkCoordinate coordinate)
    {
        return _chunk is not null && coordinate == _coordinate;
    }
}
