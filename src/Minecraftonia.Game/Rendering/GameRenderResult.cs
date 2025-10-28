using Minecraftonia.Rendering.Core;

namespace Minecraftonia.Game.Rendering;

public readonly struct GameRenderResult
{
    public GameRenderResult(IVoxelFrameBuffer framebuffer, VoxelCamera camera)
    {
        Framebuffer = framebuffer;
        Camera = camera;
    }

    public IVoxelFrameBuffer Framebuffer { get; }
    public VoxelCamera Camera { get; }
}
