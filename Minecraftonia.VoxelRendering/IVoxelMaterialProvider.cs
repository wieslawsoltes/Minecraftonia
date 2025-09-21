using Minecraftonia.VoxelEngine;

namespace Minecraftonia.VoxelRendering;

public interface IVoxelMaterialProvider<TBlock>
{
    VoxelMaterialSample Sample(TBlock block, BlockFace face, float u, float v);
}
