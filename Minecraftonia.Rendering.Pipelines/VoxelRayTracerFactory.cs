using System;

namespace Minecraftonia.Rendering.Pipelines;

public sealed class VoxelRayTracerFactory<TBlock> : IVoxelRendererFactory<TBlock>
{
    public IVoxelRenderer<TBlock> Create(VoxelRendererOptions<TBlock> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.IsSolid);
        ArgumentNullException.ThrowIfNull(options.IsEmpty);

        return new VoxelRayTracer<TBlock>(
            options.RenderSize,
            options.FieldOfViewDegrees,
            options.IsSolid,
            options.IsEmpty,
            options.SamplesPerPixel,
            options.EnableFxaa,
            options.FxaaContrastThreshold,
            options.FxaaRelativeThreshold,
            options.EnableSharpen,
            options.SharpenAmount,
            options.FogStart,
            options.FogEnd,
            options.FogColor,
            options.GlobalIllumination);
    }
}
