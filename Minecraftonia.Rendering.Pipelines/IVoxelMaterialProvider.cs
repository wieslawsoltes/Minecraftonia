using Minecraftonia.Rendering.Core;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.Rendering.Pipelines;

public interface IVoxelMaterialProvider<TBlock>
{
    VoxelMaterialSample Sample(TBlock block, BlockFace face, float u, float v);
}
