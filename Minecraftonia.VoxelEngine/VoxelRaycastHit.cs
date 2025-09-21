using System.Numerics;

namespace Minecraftonia.VoxelEngine;

public readonly struct VoxelRaycastHit<TBlock>
{
    public Int3 Block { get; }
    public BlockFace Face { get; }
    public TBlock BlockType { get; }
    public Vector3 Point { get; }
    public float Distance { get; }

    public VoxelRaycastHit(Int3 block, BlockFace face, TBlock blockType, Vector3 point, float distance)
    {
        Block = block;
        Face = face;
        BlockType = blockType;
        Point = point;
        Distance = distance;
    }
}
