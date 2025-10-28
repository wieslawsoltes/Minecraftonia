using System;

namespace Minecraftonia.VoxelRendering;

public readonly struct VoxelSize : IEquatable<VoxelSize>
{
    public VoxelSize(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }

    public bool Equals(VoxelSize other) => Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is VoxelSize other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Width, Height);

    public static bool operator ==(VoxelSize left, VoxelSize right) => left.Equals(right);
    public static bool operator !=(VoxelSize left, VoxelSize right) => !left.Equals(right);
}
