using System;

namespace Minecraftonia.VoxelEngine;

public readonly struct ChunkCoordinate : IEquatable<ChunkCoordinate>
{
    public ChunkCoordinate(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    public bool Equals(ChunkCoordinate other)
    {
        return X == other.X && Y == other.Y && Z == other.Z;
    }

    public override bool Equals(object? obj)
    {
        return obj is ChunkCoordinate other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }

    public static bool operator ==(ChunkCoordinate left, ChunkCoordinate right) => left.Equals(right);
    public static bool operator !=(ChunkCoordinate left, ChunkCoordinate right) => !left.Equals(right);
}
