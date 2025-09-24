using System;
using System.Numerics;

namespace Minecraftonia.VoxelRendering.Lighting;

public static class GlobalIlluminationSamples
{
    public static ReadOnlySpan<Vector3> HemisphereSamples128 => _hemisphereSamples128;
    public static ReadOnlySpan<Vector3> HemisphereSamples512 => _hemisphereSamples512;
    public static ReadOnlySpan<Vector3> HemisphereSamples1024 => _hemisphereSamples1024;

    public static ReadOnlySpan<Vector3> GetHemisphereSamples(int requiredCount)
    {
        if (requiredCount <= _hemisphereSamples128.Length)
        {
            return _hemisphereSamples128;
        }

        if (requiredCount <= _hemisphereSamples512.Length)
        {
            return _hemisphereSamples512;
        }

        return _hemisphereSamples1024;
    }

    private static readonly Vector3[] _hemisphereSamples128 = CreateHemisphereSamples(128);
    private static readonly Vector3[] _hemisphereSamples512 = CreateHemisphereSamples(512);
    private static readonly Vector3[] _hemisphereSamples1024 = CreateHemisphereSamples(1024);

    private static Vector3[] CreateHemisphereSamples(int count)
    {
        var samples = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            Vector2 xi = Hammersley(i, count);
            float phi = xi.Y * MathF.PI * 2f;
            float cosTheta = MathF.Sqrt(1f - xi.X);
            float sinTheta = MathF.Sqrt(MathF.Max(0f, 1f - cosTheta * cosTheta));

            float x = MathF.Cos(phi) * sinTheta;
            float y = cosTheta;
            float z = MathF.Sin(phi) * sinTheta;
            samples[i] = new Vector3(x, y, z);
        }

        return samples;
    }

    private static Vector2 Hammersley(int index, int count)
    {
        float e1 = index / (float)count;
        float e2 = RadicalInverseVdC(index);
        return new Vector2(e1, e2);
    }

    private static float RadicalInverseVdC(int index)
    {
        uint bits = (uint)index;
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAau) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
        bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
        bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
        return bits * 2.3283064365386963e-10f;
    }
}
