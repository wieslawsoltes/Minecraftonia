using Minecraftonia.Rendering.Core;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.Rendering.Pipelines;

public interface IVoxelRenderer<TBlock>
{
    IVoxelRenderResult<TBlock> Render(
        IVoxelWorld<TBlock> world,
        Player player,
        IVoxelMaterialProvider<TBlock> materials,
        IVoxelFrameBuffer? framebuffer = null);
}
