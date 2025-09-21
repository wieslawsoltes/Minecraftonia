using System.Numerics;

namespace Minecraftonia.Game;

public readonly struct RaycastHit
{
    public readonly Int3 Block;
    public readonly BlockFace Face;
    public readonly BlockType BlockType;
    public readonly Vector3 Point;
    public readonly float Distance;

    public RaycastHit(Int3 block, BlockFace face, BlockType blockType, Vector3 point, float distance)
    {
        Block = block;
        Face = face;
        BlockType = blockType;
        Point = point;
        Distance = distance;
    }
}
