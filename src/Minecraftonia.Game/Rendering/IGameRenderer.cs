using Minecraftonia.Rendering.Core;

namespace Minecraftonia.Game.Rendering;

public interface IGameRenderer
{
    GameRenderResult Render(MinecraftoniaGame game, IVoxelFrameBuffer? framebuffer);
}
