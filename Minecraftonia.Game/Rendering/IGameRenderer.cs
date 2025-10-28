using Minecraftonia.VoxelRendering;

namespace Minecraftonia.Game.Rendering;

public interface IGameRenderer
{
    GameRenderResult Render(MinecraftoniaGame game, IVoxelFrameBuffer? framebuffer);
}
