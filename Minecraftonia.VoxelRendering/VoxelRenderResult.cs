using System;

namespace Minecraftonia.VoxelRendering;

public readonly struct VoxelRenderResult<TBlock> : IVoxelRenderResult<TBlock>
{
    public VoxelRenderResult(IVoxelFrameBuffer framebuffer, VoxelCamera camera)
    {
        Framebuffer = framebuffer ?? throw new ArgumentNullException(nameof(framebuffer));
        Camera = camera;
    }

    public IVoxelFrameBuffer Framebuffer { get; }
    public VoxelCamera Camera { get; }
}
