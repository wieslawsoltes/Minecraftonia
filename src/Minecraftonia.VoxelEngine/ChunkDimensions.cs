using System;

namespace Minecraftonia.VoxelEngine;

public readonly struct ChunkDimensions
{
    public ChunkDimensions(int sizeX, int sizeY, int sizeZ)
    {
        if (sizeX <= 0) throw new ArgumentOutOfRangeException(nameof(sizeX));
        if (sizeY <= 0) throw new ArgumentOutOfRangeException(nameof(sizeY));
        if (sizeZ <= 0) throw new ArgumentOutOfRangeException(nameof(sizeZ));

        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;
    }

    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }

    public int Volume => SizeX * SizeY * SizeZ;
}
