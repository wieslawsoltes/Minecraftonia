using System.Numerics;

namespace Minecraftonia.App.Game;

public enum BlockType
{
    Air = 0,
    Grass,
    Dirt,
    Stone,
    Sand,
    Water,
    Wood,
    Leaves
}

public enum BlockFace
{
    NegativeX,
    PositiveX,
    NegativeY,
    PositiveY,
    NegativeZ,
    PositiveZ
}

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

public static class BlockTypeExtensions
{
    public static bool IsSolid(this BlockType type)
    {
        return type switch
        {
            BlockType.Air => false,
            BlockType.Water => false,
            BlockType.Leaves => false,
            _ => true
        };
    }

    public static bool IsTransparent(this BlockType type)
    {
        return type switch
        {
            BlockType.Air => true,
            BlockType.Water => true,
            BlockType.Leaves => true,
            _ => false
        };
    }
}

public static class BlockFaceExtensions
{
    public static Int3 ToOffset(this BlockFace face)
    {
        return face switch
        {
            BlockFace.NegativeX => new Int3(-1, 0, 0),
            BlockFace.PositiveX => new Int3(1, 0, 0),
            BlockFace.NegativeY => new Int3(0, -1, 0),
            BlockFace.PositiveY => new Int3(0, 1, 0),
            BlockFace.NegativeZ => new Int3(0, 0, -1),
            BlockFace.PositiveZ => new Int3(0, 0, 1),
            _ => Int3.Zero
        };
    }
}
