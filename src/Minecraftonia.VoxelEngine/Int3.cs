using System.Numerics;

namespace Minecraftonia.VoxelEngine;

public readonly struct Int3
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;

    public Int3(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static readonly Int3 Zero = new(0, 0, 0);

    public static Int3 operator +(Int3 a, Int3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Int3 operator -(Int3 a, Int3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public Vector3 ToVector3() => new(X, Y, Z);
}
