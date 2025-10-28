using Minecraftonia.Rendering.Core;
using Minecraftonia.Rendering.Pipelines;
using Minecraftonia.Rendering.Avalonia.Presenters;

namespace Minecraftonia.Rendering.Avalonia;

public sealed record RenderingConfiguration<TBlock>(
    IVoxelRendererFactory<TBlock> RendererFactory,
    IVoxelFramePresenterFactory PresenterFactory,
    IVoxelMaterialProvider<TBlock> Materials)
    where TBlock : struct;
