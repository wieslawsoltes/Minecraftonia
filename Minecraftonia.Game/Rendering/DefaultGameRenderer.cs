using System;
using Minecraftonia.Core;
using Minecraftonia.VoxelRendering;

namespace Minecraftonia.Game.Rendering;

public sealed class DefaultGameRenderer : IGameRenderer
{
    private readonly IVoxelRenderer<BlockType> _voxelRenderer;
    private readonly IVoxelMaterialProvider<BlockType> _materials;

    public DefaultGameRenderer(IVoxelRenderer<BlockType> voxelRenderer, IVoxelMaterialProvider<BlockType> materials)
    {
        _voxelRenderer = voxelRenderer ?? throw new ArgumentNullException(nameof(voxelRenderer));
        _materials = materials ?? throw new ArgumentNullException(nameof(materials));
    }

    public GameRenderResult Render(MinecraftoniaGame game, IVoxelFrameBuffer? framebuffer)
    {
        if (game is null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        var result = _voxelRenderer.Render(game.World, game.Player, _materials, framebuffer);
        return new GameRenderResult(result.Framebuffer, result.Camera);
    }
}
