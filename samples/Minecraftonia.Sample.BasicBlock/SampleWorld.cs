using System;
using Minecraftonia.Core;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.Sample.BasicBlock;

internal sealed class SampleWorld : VoxelWorld<BlockType>
{
    private readonly BlockType[] _blocks;

    public SampleWorld()
        : base(new ChunkDimensions(16, 16, 16), 1, 1, 1)
    {
        _blocks = new BlockType[ChunkSize.Volume];
        PopulateArray();
    }

    private void PopulateArray()
    {
        Array.Fill(_blocks, BlockType.Air);

        for (int z = 0; z < ChunkSize.SizeZ; z++)
        {
            for (int x = 0; x < ChunkSize.SizeX; x++)
            {
                _blocks[Index(x, 0, z)] = BlockType.Stone;
            }
        }

        int pillarX = ChunkSize.SizeX / 2;
        int pillarZ = ChunkSize.SizeZ / 2;

        _blocks[Index(pillarX, 1, pillarZ)] = BlockType.Wood;
        _blocks[Index(pillarX, 2, pillarZ)] = BlockType.Leaves;
    }

    protected override void PopulateChunk(VoxelChunk<BlockType> chunk)
    {
        var data = chunk.DataSpan;
        for (int y = 0; y < ChunkSize.SizeY; y++)
        {
            for (int z = 0; z < ChunkSize.SizeZ; z++)
            {
                for (int x = 0; x < ChunkSize.SizeX; x++)
                {
                    data[(y * ChunkSize.SizeZ + z) * ChunkSize.SizeX + x] = _blocks[Index(x, y, z)];
                }
            }
        }
    }

    private int Index(int x, int y, int z)
    {
        return (y * ChunkSize.SizeZ + z) * ChunkSize.SizeX + x;
    }
}
