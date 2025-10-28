using System;
using System.Numerics;
using Minecraftonia.Rendering.Core;

namespace Minecraftonia.Rendering.Pipelines;

public sealed record VoxelRendererOptions<TBlock>(
    VoxelSize RenderSize,
    float FieldOfViewDegrees,
    Func<TBlock, bool> IsSolid,
    Func<TBlock, bool> IsEmpty,
    int SamplesPerPixel = 1,
    bool EnableFxaa = true,
    float FxaaContrastThreshold = 0.0312f,
    float FxaaRelativeThreshold = 0.125f,
    bool EnableSharpen = true,
    float SharpenAmount = 0.18f,
    float FogStart = 45f,
    float FogEnd = 90f,
    Vector3? FogColor = null,
    GlobalIlluminationSettings? GlobalIllumination = null);
