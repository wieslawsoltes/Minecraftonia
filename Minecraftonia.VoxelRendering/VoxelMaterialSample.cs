using System.Numerics;

namespace Minecraftonia.VoxelRendering;

public readonly struct VoxelMaterialSample
{
    public VoxelMaterialSample(Vector3 color, float opacity)
    {
        Color = color;
        Opacity = opacity;
    }

    public Vector3 Color { get; }
    public float Opacity { get; }
}
