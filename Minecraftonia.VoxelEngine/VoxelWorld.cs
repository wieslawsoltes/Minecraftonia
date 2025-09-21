using System;

namespace Minecraftonia.VoxelEngine;

public abstract class VoxelWorld<TBlock>
{
    private readonly TBlock[,,] _blocks;

    protected VoxelWorld(int width, int height, int depth)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (depth <= 0) throw new ArgumentOutOfRangeException(nameof(depth));

        Width = width;
        Height = height;
        Depth = depth;
        _blocks = new TBlock[width, height, depth];
    }

    public int Width { get; }
    public int Height { get; }
    public int Depth { get; }

    protected TBlock[,,] Blocks => _blocks;

    public bool InBounds(int x, int y, int z)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height && z >= 0 && z < Depth;
    }

    public TBlock GetBlock(int x, int y, int z)
    {
        return InBounds(x, y, z) ? _blocks[x, y, z] : default!;
    }

    public TBlock GetBlock(Int3 position) => GetBlock(position.X, position.Y, position.Z);

    public void SetBlock(int x, int y, int z, TBlock type)
    {
        if (InBounds(x, y, z))
        {
            _blocks[x, y, z] = type;
        }
    }

    public void SetBlock(Int3 position, TBlock type) => SetBlock(position.X, position.Y, position.Z, type);

    protected void Fill(TBlock value)
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int z = 0; z < Depth; z++)
                {
                    _blocks[x, y, z] = value;
                }
            }
        }
    }
}
