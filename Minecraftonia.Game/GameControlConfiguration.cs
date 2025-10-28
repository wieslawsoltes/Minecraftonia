using System;
using Avalonia.Controls;
using Minecraftonia.Content;
using Minecraftonia.Core;
using Minecraftonia.Hosting.Avalonia;
using Minecraftonia.Rendering.Avalonia;
using Minecraftonia.Rendering.Avalonia.Presenters;
using Minecraftonia.Rendering.Core;
using Minecraftonia.Rendering.Pipelines;

namespace Minecraftonia.Game;

public sealed record GameControlConfiguration(
    RenderingConfiguration<BlockType> Rendering,
    BlockTextures Textures,
    MinecraftoniaWorldConfig WorldConfig,
    GlobalIlluminationSettings GlobalIllumination,
    VoxelSize RenderSize,
    FramePresentationMode InitialPresentationMode,
    GameInputConfiguration Input,
    IGameSaveService SaveService)
{
    public static GameControlConfiguration CreateDefault()
    {
        var textures = new BlockTextures();
        var worldConfig = MinecraftoniaWorldConfig.FromDimensions(
            96,
            48,
            96,
            waterLevel: 8,
            seed: 1337);

        var rendering = new RenderingConfiguration<BlockType>(
            new VoxelRayTracerFactory<BlockType>(),
            new DefaultVoxelFramePresenterFactory(),
            textures);

        var globalIllumination = GlobalIlluminationSettings.Default with
        {
            DiffuseSampleCount = 5,
            MaxDistance = 22f,
            Strength = 1.05f,
            AmbientLight = new System.Numerics.Vector3(0.18f, 0.21f, 0.26f),
            SunShadowSoftness = 0.58f,
            Enabled = false
        };

        var input = new GameInputConfiguration(
            topLevel => new KeyboardInputSource(topLevel),
            (topLevel, control) => new PointerInputSource(topLevel, control));

        var saveService = new FileGameSaveService();

        return new GameControlConfiguration(
            rendering,
            textures,
            worldConfig,
            globalIllumination,
            new VoxelSize(360, 202),
            FramePresentationMode.SkiaTexture,
            input,
            saveService);
    }
}
