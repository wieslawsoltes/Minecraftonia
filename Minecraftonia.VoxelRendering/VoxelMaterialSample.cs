using System;
using System.Numerics;

namespace Minecraftonia.VoxelRendering;

public readonly struct VoxelMaterialSample
{
    private const float MinRoughness = 0.02f;

    public VoxelMaterialSample(
        Vector3 color,
        float opacity,
        float roughness = 0.5f,
        float metallic = 0f,
        float specular = 0.04f,
        Vector3 emission = default,
        Vector3 normal = default)
    {
        Color = Vector3.Clamp(color, Vector3.Zero, new Vector3(4f));
        Opacity = Math.Clamp(opacity, 0f, 1f);
        Roughness = Math.Clamp(roughness, MinRoughness, 1f);
        Metallic = Math.Clamp(metallic, 0f, 1f);
        Specular = Math.Clamp(specular, 0f, 1f);
        Emission = Vector3.Clamp(emission, Vector3.Zero, new Vector3(32f));

        if (normal.LengthSquared() > 1e-5f)
        {
            ShadingNormal = Vector3.Normalize(normal);
            HasShadingNormal = true;
        }
        else
        {
            ShadingNormal = Vector3.Zero;
            HasShadingNormal = false;
        }
    }

    public Vector3 Color { get; }
    public float Opacity { get; }
    public float Roughness { get; }
    public float Metallic { get; }
    public float Specular { get; }
    public Vector3 Emission { get; }
    public Vector3 ShadingNormal { get; }
    public bool HasShadingNormal { get; }

    public Vector3 Albedo => Color;
    public Vector3 Emissive => Emission;
}
