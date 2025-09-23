namespace Minecraftonia.VoxelRendering;

public readonly struct VoxelRenderResult<TBlock>
{
    public VoxelRenderResult(VoxelFrameBuffer framebuffer, VoxelCamera camera)
    {
        Framebuffer = framebuffer;
        Camera = camera;
    }

    public VoxelFrameBuffer Framebuffer { get; }
    public VoxelCamera Camera { get; }
}
