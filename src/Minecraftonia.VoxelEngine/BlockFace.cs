namespace Minecraftonia.VoxelEngine;

public enum BlockFace
{
    NegativeX,
    PositiveX,
    NegativeY,
    PositiveY,
    NegativeZ,
    PositiveZ
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
