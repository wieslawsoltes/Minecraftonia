using System;
using System.Numerics;
using System.Runtime.Intrinsics;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.VoxelRendering.Lighting;

public sealed class GlobalIlluminationEngine<TBlock>
{
    private readonly Func<TBlock, bool> _isEmpty;
    private readonly Func<Vector3, Vector3> _sampleSky;

    private readonly int _sampleCount;
    private readonly int _bounceCount;
    private readonly float _maxDistance;
    private readonly float _strength;
    private readonly float _skyContribution;
    private readonly float _occlusionStrength;
    private readonly float _distanceAttenuation;
    private readonly float _shadowBias;
    private readonly float _sunShadowSoftness;
    private readonly float _sunMaxDistance;
    private readonly Vector3 _sunDirection;
    private readonly Vector3 _sunColor;
    private readonly float _sunIntensity;
    private readonly Vector3 _ambientLight;
    private readonly bool _useBentNormal;
    private readonly int _maxRaymarchSteps;

    public GlobalIlluminationEngine(
        GlobalIlluminationSettings settings,
        float maxRayDistance,
        Func<TBlock, bool> isEmpty,
        Func<Vector3, Vector3> sampleSky)
    {
        _isEmpty = isEmpty ?? throw new ArgumentNullException(nameof(isEmpty));
        _sampleSky = sampleSky ?? throw new ArgumentNullException(nameof(sampleSky));

        _sampleCount = Math.Clamp(settings.DiffuseSampleCount, 0, GlobalIlluminationSamples.HemisphereSamples128.Length);
        _bounceCount = Math.Max(0, settings.BounceCount);
        _maxDistance = Math.Clamp(settings.MaxDistance, 0.5f, maxRayDistance);
        _strength = MathF.Max(0f, settings.Strength);
        _skyContribution = MathF.Max(0f, settings.SkyContribution);
        _occlusionStrength = Math.Clamp(settings.OcclusionStrength, 0f, 1.5f);
        _distanceAttenuation = MathF.Max(0.0001f, settings.DistanceAttenuation);
        _shadowBias = MathF.Max(1e-4f, settings.ShadowBias);
        _sunShadowSoftness = Math.Clamp(settings.SunShadowSoftness, 0f, 1f);
        _sunMaxDistance = Math.Clamp(settings.SunMaxDistance, 0.5f, maxRayDistance);
        _sunDirection = Vector3.Normalize(settings.SunDirection.LengthSquared() > 0.0001f
            ? settings.SunDirection
            : new Vector3(-0.35f, 0.88f, 0.25f));
        _sunColor = Vector3.Clamp(settings.SunColor, Vector3.Zero, new Vector3(4f));
        _sunIntensity = MathF.Max(0f, settings.SunIntensity);
        _ambientLight = Vector3.Clamp(settings.AmbientLight, Vector3.Zero, new Vector3(4f));
        _useBentNormal = settings.UseBentNormalForAmbient;
        _maxRaymarchSteps = Math.Clamp(settings.MaxSecondarySteps, 16, 256);
    }

    public LightingResult ComputeLighting(
        VoxelWorld<TBlock> world,
        IVoxelMaterialProvider<TBlock> materials,
        Vector3 hitPoint,
        Vector3 normal,
        BlockFace face,
        VoxelMaterialSample material,
        int bounceDepth)
    {
        Vector3 direct = Vector3.Zero;
        if (_sunIntensity > 0f)
        {
            float ndotl = MathF.Max(0f, Vector3.Dot(normal, _sunDirection));
            if (ndotl > 0f)
            {
                Vector3 shadowOrigin = hitPoint + normal * _shadowBias;
                float visibility = ComputeSunVisibility(world, materials, shadowOrigin, _sunDirection);
                direct = _sunColor * (ndotl * _sunIntensity * visibility);
            }
        }

        float ambientOcclusion = 1f;
        Vector3 bentNormal = normal;
        Vector3 gi = Vector3.Zero;

        if (_sampleCount > 0 && bounceDepth < _bounceCount)
        {
            gi = SampleGlobalIllumination(
                world,
                materials,
                hitPoint,
                normal,
                bounceDepth,
                out ambientOcclusion,
                out bentNormal);
        }

        Vector3 ambientNormal = (_useBentNormal && bentNormal.LengthSquared() > 1e-4f) ? bentNormal : normal;
        float ambientWrap = Math.Clamp(Vector3.Dot(ambientNormal, _sunDirection) * 0.5f + 0.5f, 0.1f, 1f);
        Vector3 ambient = _ambientLight * ambientWrap * ambientOcclusion;
        Vector3 indirect = gi * _strength;

        return new LightingResult(ambient, direct, indirect, bentNormal, ambientOcclusion);
    }

    private float ComputeSunVisibility(
        VoxelWorld<TBlock> world,
        IVoxelMaterialProvider<TBlock> materials,
        Vector3 origin,
        Vector3 sunDirection)
    {
        if (_sunShadowSoftness <= 0.05f)
        {
            return TraceVisibility(world, materials, origin, sunDirection, _sunMaxDistance);
        }

        Vector3 tangent = BuildPerpendicular(sunDirection);
        Vector3 bitangent = Vector3.Normalize(Vector3.Cross(sunDirection, tangent));

        const int tapCount = 4;
        float radius = 0.35f * _sunShadowSoftness;
        float visibility = 0f;

        for (int i = 0; i < tapCount; i++)
        {
            float angle = (i / (float)tapCount) * MathF.PI * 2f;
            Vector3 offset = tangent * MathF.Cos(angle) + bitangent * MathF.Sin(angle);
            Vector3 sampleDir = Vector3.Normalize(sunDirection + offset * radius);
            visibility += TraceVisibility(world, materials, origin, sampleDir, _sunMaxDistance);
        }

        return visibility / tapCount;
    }

    private float TraceVisibility(
        VoxelWorld<TBlock> world,
        IVoxelMaterialProvider<TBlock> materials,
        Vector3 origin,
        Vector3 direction,
        float maxDistance)
    {
        var walker = new VoxelDdaWalker<TBlock>(world, origin, direction, maxDistance, _maxRaymarchSteps);
        float visibility = 1f;

        while (walker.TryStep(out var step))
        {
            if (step.Distance > maxDistance)
            {
                break;
            }

            if (step.Kind == VoxelDdaHitKind.Sky)
            {
                return visibility;
            }

            TBlock block = step.Block;
            if (_isEmpty(block))
            {
                continue;
            }

            Vector3 local = origin + direction * step.Distance;
            Vector3 voxelLocal = local - new Vector3(step.VoxelX, step.VoxelY, step.VoxelZ);
            Vector2 uv = VoxelLightingMath.ComputeFaceUv(step.Face, voxelLocal);
            VoxelMaterialSample sample = materials.Sample(block, step.Face, uv.X, uv.Y);
            float opacity = Math.Clamp(sample.Opacity, 0f, 1f);
            visibility *= MathF.Max(0f, 1f - opacity);
            if (visibility <= 0.01f)
            {
                return 0f;
            }
        }

        return visibility;
    }

    private Vector3 SampleGlobalIllumination(
        VoxelWorld<TBlock> world,
        IVoxelMaterialProvider<TBlock> materials,
        Vector3 hitPoint,
        Vector3 normal,
        int bounceDepth,
        out float ambientOcclusion,
        out Vector3 bentNormal)
    {
        Vector3 tangent = BuildPerpendicular(normal);
        Vector3 bitangent = Vector3.Normalize(Vector3.Cross(normal, tangent));

        uint hash = HashPosition(hitPoint);
        float rotation = (hash & 0xFFFFu) / 65535f * (MathF.PI * 2f);
        float sinR = MathF.Sin(rotation);
        float cosR = MathF.Cos(rotation);
        Vector3 rotatedTangent = tangent * cosR + bitangent * sinR;
        Vector3 rotatedBitangent = Vector3.Normalize(Vector3.Cross(normal, rotatedTangent));

        Vector3 accum = Vector3.Zero;
        Vector3 bentAccum = Vector3.Zero;
        float totalWeight = 0f;
        float blockedWeight = 0f;

        ReadOnlySpan<Vector3> samplePool = GlobalIlluminationSamples.HemisphereSamples128;
        int poolLength = samplePool.Length;
        int startIndex = (int)(hash % (uint)poolLength);
        const int stride = 17;

        float maxDistance = _maxDistance;

        for (int i = 0; i < _sampleCount; i++)
        {
            int index = (startIndex + i * stride) % poolLength;
            Vector3 local = samplePool[index];
            Vector3 dir = TransformHemisphereSample(rotatedTangent, rotatedBitangent, normal, local);
            dir = Vector3.Normalize(dir);

            float weight = MathF.Max(local.Y, 0.0001f);
            Vector3 sampleOrigin = hitPoint + dir * (_shadowBias * 2f);

            Vector3 sampleRadiance = Vector3.Zero;
            bool blocked = false;
            float opacityAccum = 0f;

            var walker = new VoxelDdaWalker<TBlock>(world, sampleOrigin, dir, maxDistance, _maxRaymarchSteps);
            while (walker.TryStep(out var step))
            {
                if (step.Kind == VoxelDdaHitKind.Sky)
                {
                    sampleRadiance = _sampleSky(dir) * _skyContribution;
                    break;
                }

                TBlock block = step.Block;
                if (_isEmpty(block))
                {
                    continue;
                }

                Vector3 bouncePoint = sampleOrigin + dir * step.Distance;
                Vector3 localHit = bouncePoint - new Vector3(step.VoxelX, step.VoxelY, step.VoxelZ);
                Vector2 uv = VoxelLightingMath.ComputeFaceUv(step.Face, localHit);
                VoxelMaterialSample bounceSample = materials.Sample(block, step.Face, uv.X, uv.Y);
                float opacity = Math.Clamp(bounceSample.Opacity, 0f, 1f);
                if (opacity <= 0.01f)
                {
                    continue;
                }

                Vector3 bounceNormal = VoxelLightingMath.FaceToNormal(step.Face);
                float lambert = MathF.Max(0.05f, Vector3.Dot(bounceNormal, -dir));
                float falloff = MathF.Exp(-step.Distance * _distanceAttenuation);
                Vector3 bounceColor = Vector3.Clamp(bounceSample.Color, Vector3.Zero, new Vector3(4f));
                Vector3 bounceLighting = ApproximateBounceLighting(step.Face, bounceNormal, bounceColor);
                sampleRadiance = bounceLighting * lambert * falloff;
                blocked = true;
                opacityAccum = opacity;
                break;
            }

            if (!blocked && sampleRadiance == Vector3.Zero)
            {
                sampleRadiance = _sampleSky(dir) * _skyContribution;
            }

            accum += sampleRadiance * weight;
            totalWeight += weight;

            if (blocked)
            {
                blockedWeight += weight * opacityAccum;
            }
            else
            {
                bentAccum += dir * weight;
            }
        }

        float invWeight = totalWeight > 0f ? 1f / totalWeight : 1f;
        ambientOcclusion = Math.Clamp(1f - blockedWeight * invWeight * _occlusionStrength, 0.1f, 1f);
        bentNormal = bentAccum.LengthSquared() > 1e-4f ? Vector3.Normalize(bentAccum) : normal;

        Vector3 gi = accum * invWeight;
        return Vector3.Clamp(gi, Vector3.Zero, new Vector3(4f));
    }

    private Vector3 ApproximateBounceLighting(BlockFace face, Vector3 normal, Vector3 albedo)
    {
        float faceLight = MathF.Max(0.35f, VoxelLightingMath.GetFaceLight(face));
        float sunTerm = MathF.Max(0f, Vector3.Dot(normal, _sunDirection));
        Vector3 direct = _sunColor * (_sunIntensity * 0.35f * sunTerm);
        Vector3 ambient = _ambientLight * (0.65f * faceLight);
        Vector3 lighting = ambient + direct;
        return Vector3.Clamp(albedo * lighting, Vector3.Zero, new Vector3(4f));
    }

    private static Vector3 TransformHemisphereSample(Vector3 tangent, Vector3 bitangent, Vector3 normal, Vector3 sample)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> weights = Vector128.Create(sample.X, sample.Z, sample.Y, 0f);
            Vector128<float> tx = Vector128.Create(tangent.X, bitangent.X, normal.X, 0f);
            Vector128<float> ty = Vector128.Create(tangent.Y, bitangent.Y, normal.Y, 0f);
            Vector128<float> tz = Vector128.Create(tangent.Z, bitangent.Z, normal.Z, 0f);

            Vector128<float> mx = tx * weights;
            Vector128<float> my = ty * weights;
            Vector128<float> mz = tz * weights;

            float x = mx.GetElement(0) + mx.GetElement(1) + mx.GetElement(2);
            float y = my.GetElement(0) + my.GetElement(1) + my.GetElement(2);
            float z = mz.GetElement(0) + mz.GetElement(1) + mz.GetElement(2);
            return new Vector3(x, y, z);
        }

        return tangent * sample.X + bitangent * sample.Z + normal * sample.Y;
    }

    private static Vector3 BuildPerpendicular(Vector3 direction)
    {
        Vector3 axis = MathF.Abs(direction.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 tangent = Vector3.Cross(direction, axis);
        float lenSq = tangent.LengthSquared();
        if (lenSq < 1e-6f)
        {
            tangent = Vector3.Cross(direction, Vector3.UnitZ);
            lenSq = tangent.LengthSquared();
        }

        return lenSq > 0f ? Vector3.Normalize(tangent) : Vector3.UnitX;
    }

    private static uint HashPosition(Vector3 position)
    {
        uint x = (uint)BitConverter.SingleToInt32Bits(position.X);
        uint y = (uint)BitConverter.SingleToInt32Bits(position.Y);
        uint z = (uint)BitConverter.SingleToInt32Bits(position.Z);

        uint hash = x * 73856093u ^ y * 19349663u ^ z * 83492791u;
        hash ^= hash >> 16;
        hash *= 0x7feb352du;
        hash ^= hash >> 15;
        hash *= 0x846ca68bu;
        hash ^= hash >> 16;
        return hash;
    }

}

public readonly struct LightingResult
{
    public LightingResult(
        Vector3 ambient,
        Vector3 direct,
        Vector3 indirect,
        Vector3 bentNormal,
        float ambientOcclusion)
    {
        Ambient = ambient;
        Direct = direct;
        Indirect = indirect;
        BentNormal = bentNormal;
        AmbientOcclusion = ambientOcclusion;
    }

    public Vector3 Ambient { get; }
    public Vector3 Direct { get; }
    public Vector3 Indirect { get; }
    public Vector3 BentNormal { get; }
    public float AmbientOcclusion { get; }

    public Vector3 CombinedLighting => Ambient + Direct + Indirect;
}
