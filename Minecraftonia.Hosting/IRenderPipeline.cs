using Minecraftonia.Rendering.Core;
using Minecraftonia.Rendering.Pipelines;

namespace Minecraftonia.Hosting;

public interface IRenderPipeline<TBlock>
    where TBlock : struct
{
    IVoxelRenderResult<TBlock> Render(IGameSession<TBlock> session, IVoxelFrameBuffer? framebuffer = null);
}
