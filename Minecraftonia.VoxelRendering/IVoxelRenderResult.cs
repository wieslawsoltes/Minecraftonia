namespace Minecraftonia.VoxelRendering;

public interface IVoxelRenderResult<TBlock>
{
    IVoxelFrameBuffer Framebuffer { get; }
    VoxelCamera Camera { get; }
}
