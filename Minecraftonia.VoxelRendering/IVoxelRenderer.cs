using Minecraftonia.VoxelEngine;

namespace Minecraftonia.VoxelRendering;

public interface IVoxelRenderer<TBlock>
{
    IVoxelRenderResult<TBlock> Render(
        IVoxelWorld<TBlock> world,
        Player player,
        IVoxelMaterialProvider<TBlock> materials,
        IVoxelFrameBuffer? framebuffer = null);
}
