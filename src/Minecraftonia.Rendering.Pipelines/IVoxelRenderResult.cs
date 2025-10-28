using Minecraftonia.Rendering.Core;

namespace Minecraftonia.Rendering.Pipelines;

public interface IVoxelRenderResult<TBlock>
{
    IVoxelFrameBuffer Framebuffer { get; }
    VoxelCamera Camera { get; }
}
