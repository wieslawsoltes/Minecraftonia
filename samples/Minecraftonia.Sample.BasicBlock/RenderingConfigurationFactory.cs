using Minecraftonia.Content;
using Minecraftonia.Core;
using Minecraftonia.Hosting;
using Minecraftonia.Rendering.Avalonia;
using Minecraftonia.Rendering.Avalonia.Presenters;
using Minecraftonia.Rendering.Core;
using Minecraftonia.Rendering.Pipelines;

namespace Minecraftonia.Sample.BasicBlock;

internal static class RenderingConfigurationFactory
{
    public static RenderingConfiguration<BlockType> Create()
    {
        var rendererFactory = new VoxelRayTracerFactory<BlockType>();
        var presenterFactory = new DefaultVoxelFramePresenterFactory();
        var materials = new BlockTextures();

        return new RenderingConfiguration<BlockType>(rendererFactory, presenterFactory, materials);
    }
}
