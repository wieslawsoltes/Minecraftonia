using System;
using System.IO;
using Minecraftonia.Content;
using Minecraftonia.Core;
using Minecraftonia.Rendering.Core;
using Minecraftonia.Rendering.Pipelines;
using Minecraftonia.VoxelEngine;
using Minecraftonia.Sample.Doom.Core;

namespace Minecraftonia.Sample.Doom;

internal static class Program
{
    private const string OutputFileName = "doom-frame.ppm";

    public static int Main()
    {
        var textures = new BlockTextures();
        var world = new DoomVoxelWorld();
        world.PreloadAllChunks();

        var player = new Player
        {
            Position = new System.Numerics.Vector3(DoomVoxelWorld.MapWidth / 2f, 1.0f, 5.5f),
            EyeHeight = 1.6f,
            Yaw = 0f,
            Pitch = -4f
        };

        var renderOptions = new VoxelRendererOptions<BlockType>(
            new VoxelSize(320, 200),
            65f,
            block => block.IsSolid(),
            block => block == BlockType.Air,
            SamplesPerPixel: 1,
            EnableFxaa: true,
            EnableSharpen: true,
            GlobalIllumination: GlobalIlluminationSettings.Default with { Enabled = false });

        var renderer = new VoxelRayTracerFactory<BlockType>().Create(renderOptions);
        var result = renderer.Render(world, player, textures);

        try
        {
            WriteFrameAsPpm(result.Framebuffer, OutputFileName);
        }
        finally
        {
            result.Framebuffer.Dispose();
        }

        Console.WriteLine($"Rendered Doom-inspired voxel hall to {Path.GetFullPath(OutputFileName)}");
        return 0;
    }

    private static void WriteFrameAsPpm(IVoxelFrameBuffer framebuffer, string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        int width = framebuffer.Size.Width;
        int height = framebuffer.Size.Height;
        writer.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));

        var span = framebuffer.ReadOnlySpan;
        for (int i = 0; i < span.Length; i += 4)
        {
            byte b = span[i];
            byte g = span[i + 1];
            byte r = span[i + 2];
            byte a = span[i + 3];

            if (a > 0 && a < 255)
            {
                float alpha = a / 255f;
                if (alpha > 0f)
                {
                    r = (byte)Math.Clamp((int)MathF.Round(r / alpha), 0, 255);
                    g = (byte)Math.Clamp((int)MathF.Round(g / alpha), 0, 255);
                    b = (byte)Math.Clamp((int)MathF.Round(b / alpha), 0, 255);
                }
            }

            writer.Write(r);
            writer.Write(g);
            writer.Write(b);
        }
    }
}
