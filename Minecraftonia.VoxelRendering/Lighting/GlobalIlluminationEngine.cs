using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Threading;
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
    private readonly bool _sunVisibilityCacheEnabled;
    private readonly ThreadLocal<Dictionary<SunVisibilityCacheKey, float>>? _sunVisibilityCache;
    private readonly bool _irradianceCacheEnabled;
    private readonly float _temporalBlendFactor;
    private readonly ConcurrentDictionary<IrradianceCacheKey, IrradianceCacheEntry>? _currentIrradianceCache;
    private Dictionary<IrradianceCacheKey, IrradianceCacheEntry> _previousIrradianceCache = new();
    private readonly int _adaptiveMinSamples;
    private readonly float _adaptiveStartDistance;
    private readonly float _adaptiveEndDistance;
    private readonly float _adaptiveInvRange;
    private readonly ThreadLocal<VoxelWorld<TBlock>.BlockAccessCache> _walkerCache;

    public GlobalIlluminationEngine(
        GlobalIlluminationSettings settings,
        float maxRayDistance,
        Func<TBlock, bool> isEmpty,
        Func<Vector3, Vector3> sampleSky)
    {
        _isEmpty = isEmpty ?? throw new ArgumentNullException(nameof(isEmpty));
        _sampleSky = sampleSky ?? throw new ArgumentNullException(nameof(sampleSky));

        _sampleCount = Math.Max(0, settings.DiffuseSampleCount);
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
        _sunVisibilityCacheEnabled = settings.EnableSunVisibilityCache;
        _irradianceCacheEnabled = settings.EnableIrradianceCache;
        _temporalBlendFactor = Math.Clamp(settings.TemporalBlendFactor, 0f, 1f);
        _adaptiveMinSamples = _sampleCount > 0
            ? Math.Clamp(Math.Max(1, settings.AdaptiveMinSamples), 1, _sampleCount)
            : 0;
        _adaptiveStartDistance = MathF.Max(0f, settings.AdaptiveStartDistance);
        _adaptiveEndDistance = MathF.Max(_adaptiveStartDistance + 0.0001f, settings.AdaptiveEndDistance);
        _adaptiveInvRange = 1f / MathF.Max(_adaptiveEndDistance - _adaptiveStartDistance, 0.0001f);

        if (_sunVisibilityCacheEnabled)
        {
            _sunVisibilityCache = new ThreadLocal<Dictionary<SunVisibilityCacheKey, float>>(
                () => new Dictionary<SunVisibilityCacheKey, float>(capacity: 128),
                trackAllValues: true);
        }

        if (_irradianceCacheEnabled)
        {
            _currentIrradianceCache = new ConcurrentDictionary<IrradianceCacheKey, IrradianceCacheEntry>();
        }

        _walkerCache = new ThreadLocal<VoxelWorld<TBlock>.BlockAccessCache>(() => new VoxelWorld<TBlock>.BlockAccessCache());
    }

    internal Vector3 PrimaryLightDirection => _sunDirection;

    internal void BeginFrame()
    {
        if (_sunVisibilityCache is { } caches)
        {
            foreach (var cache in caches.Values)
            {
                cache.Clear();
            }
        }

        if (_irradianceCacheEnabled && _currentIrradianceCache is { } current)
        {
            if (current.Count > 0)
            {
                var snapshot = current.ToArray();
                var previous = _previousIrradianceCache;
                previous.Clear();
                foreach (var pair in snapshot)
                {
                    previous[pair.Key] = pair.Value;
                }
            }

            current.Clear();
        }
    }

    internal LightingResult ComputeLighting(
        VoxelWorld<TBlock> world,
        IVoxelMaterialProvider<TBlock> materials,
        in VoxelDdaHit<TBlock> hit,
        Vector3 hitPoint,
        Vector2 uv,
        float viewDistance,
        VoxelMaterialSample material,
        int bounceDepth)
    {
        BlockFace face = hit.Face;
        Vector3 normal = VoxelLightingMath.FaceToNormal(face);

        Vector3 direct = Vector3.Zero;
        if (_sunIntensity > 0f)
        {
            float ndotl = MathF.Max(0f, Vector3.Dot(normal, _sunDirection));
            if (ndotl > 0f)
            {
                Vector3 shadowOrigin = hitPoint + normal * _shadowBias;
                float visibility = ComputeSunVisibility(world, materials, shadowOrigin, _sunDirection, in hit, uv);
                direct = _sunColor * (ndotl * _sunIntensity * visibility);
            }
        }

        float ambientOcclusion = 1f;
        Vector3 bentNormal = normal;
        Vector3 gi = Vector3.Zero;

        bool canSampleGi = _sampleCount > 0 && bounceDepth < _bounceCount;

        if (canSampleGi)
        {
            if (_irradianceCacheEnabled && _currentIrradianceCache is { } currentCache)
            {
                int uBucket = QuantizeIrradianceUv(uv.X);
                int vBucket = QuantizeIrradianceUv(uv.Y);
                var cacheKey = new IrradianceCacheKey(hit.VoxelX, hit.VoxelY, hit.VoxelZ, face, uBucket, vBucket);

                if (currentCache.TryGetValue(cacheKey, out var cachedEntry))
                {
                    ambientOcclusion = cachedEntry.AmbientOcclusion;
                    bentNormal = cachedEntry.BentNormal;
                    gi = cachedEntry.Irradiance;
                }
                else
                {
                    IrradianceCacheEntry? previousEntry = null;
                    if (_previousIrradianceCache.TryGetValue(cacheKey, out var prevEntry))
                    {
                        previousEntry = prevEntry;
                    }

                    int sampleBudget = GetSampleBudget(viewDistance, previousEntry, bounceDepth);

                    if (sampleBudget <= 0)
                    {
                        if (previousEntry.HasValue)
                        {
                            var prev = previousEntry.Value;
                            ambientOcclusion = prev.AmbientOcclusion;
                            bentNormal = prev.BentNormal;
                            gi = prev.Irradiance;
                            currentCache.TryAdd(cacheKey, prev);
                        }
                    }
                    else
                    {
                        Vector3 sampledGi = SampleGlobalIllumination(
                            world,
                            materials,
                            hitPoint,
                            normal,
                            bounceDepth,
                            sampleBudget,
                            out float sampledAo,
                            out Vector3 sampledBentNormal,
                            out int samplesTaken);

                        float blendedAo = sampledAo;
                        Vector3 blendedBentNormal = sampledBentNormal;
                        Vector3 blendedGi = sampledGi;
                        int totalSamples = Math.Min(_sampleCount, Math.Max(samplesTaken, _adaptiveMinSamples));

                        if (previousEntry.HasValue)
                        {
                            var previous = previousEntry.Value;
                            float previousSamples = Math.Clamp(previous.SampleCount, 0, _sampleCount);
                            float newSamples = Math.Max(1, samplesTaken);

                            blendedGi = BlendIrradiance(previous.Irradiance, previousSamples, sampledGi, newSamples, _temporalBlendFactor);
                            blendedAo = BlendScalar(previous.AmbientOcclusion, previousSamples, sampledAo, newSamples, _temporalBlendFactor);
                            blendedBentNormal = BlendNormalWeighted(previous.BentNormal, previousSamples, sampledBentNormal, newSamples, _temporalBlendFactor);

                            totalSamples = Math.Min(_sampleCount, (int)MathF.Round(previousSamples + newSamples));
                        }

                        var entry = new IrradianceCacheEntry(blendedGi, blendedBentNormal, blendedAo, totalSamples);
                        currentCache.AddOrUpdate(cacheKey, entry, (_, _) => entry);

                        ambientOcclusion = blendedAo;
                        bentNormal = blendedBentNormal;
                        gi = blendedGi;
                    }
                }
            }
            else
            {
                int sampleBudget = GetSampleBudget(viewDistance, null, bounceDepth);
                if (sampleBudget > 0)
                {
                    gi = SampleGlobalIllumination(
                        world,
                        materials,
                        hitPoint,
                        normal,
                        bounceDepth,
                        sampleBudget,
                        out ambientOcclusion,
                        out bentNormal,
                        out _);
                }
            }
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
        Vector3 sunDirection,
        in VoxelDdaHit<TBlock> hit,
        Vector2 uv)
    {
        var key = new SunVisibilityCacheKey(
            hit.VoxelX,
            hit.VoxelY,
            hit.VoxelZ,
            hit.Face,
            QuantizeUv(uv.X),
            QuantizeUv(uv.Y));

        if (!_sunVisibilityCacheEnabled)
        {
            return ComputeSunVisibilityInternal(world, materials, origin, sunDirection);
        }

        var cache = _sunVisibilityCache?.Value;
        if (cache is null)
        {
            return ComputeSunVisibilityInternal(world, materials, origin, sunDirection);
        }

        if (cache.TryGetValue(key, out float visibility))
        {
            return visibility;
        }

        visibility = ComputeSunVisibilityInternal(world, materials, origin, sunDirection);
        cache[key] = visibility;
        return visibility;
    }

    private float ComputeSunVisibilityInternal(
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

    private static int QuantizeUv(float value)
    {
        if (float.IsNaN(value))
        {
            return 0;
        }

        int bucket = (int)MathF.Floor(value * 8f);
        return Math.Clamp(bucket, 0, 7);
    }

    private static int QuantizeIrradianceUv(float value)
    {
        if (float.IsNaN(value))
        {
            return 0;
        }

        float clamped = Math.Clamp(value, 0f, 0.999f);
        return (int)(clamped * 8f);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private static Vector3 BlendIrradiance(
        Vector3 previous,
        float previousSamples,
        Vector3 current,
        float newSamples,
        float blendFactor)
    {
        float previousWeight = previousSamples * Math.Clamp(1f - blendFactor, 0f, 1f);
        float newWeight = newSamples * Math.Clamp(blendFactor, 0f, 1f);

        if (previousWeight <= 1e-5f && newWeight <= 1e-5f)
        {
            return current;
        }

        if (previousWeight <= 1e-5f)
        {
            return current;
        }

        if (newWeight <= 1e-5f)
        {
            return previous;
        }

        float total = previousWeight + newWeight;
        return (previous * previousWeight + current * newWeight) / total;
    }

    private static float BlendScalar(
        float previous,
        float previousSamples,
        float current,
        float newSamples,
        float blendFactor)
    {
        float previousWeight = previousSamples * Math.Clamp(1f - blendFactor, 0f, 1f);
        float newWeight = newSamples * Math.Clamp(blendFactor, 0f, 1f);

        if (previousWeight <= 1e-5f && newWeight <= 1e-5f)
        {
            return current;
        }

        if (previousWeight <= 1e-5f)
        {
            return current;
        }

        if (newWeight <= 1e-5f)
        {
            return previous;
        }

        float total = previousWeight + newWeight;
        return (previous * previousWeight + current * newWeight) / total;
    }

    private static Vector3 BlendNormalWeighted(
        Vector3 previous,
        float previousSamples,
        Vector3 current,
        float newSamples,
        float blendFactor)
    {
        float previousWeight = previousSamples * Math.Clamp(1f - blendFactor, 0f, 1f);
        float newWeight = newSamples * Math.Clamp(blendFactor, 0f, 1f);
        Vector3 combined = previous * previousWeight + current * newWeight;
        if (combined.LengthSquared() > 1e-5f)
        {
            return Vector3.Normalize(combined);
        }

        if (newWeight > previousWeight && current.LengthSquared() > 1e-5f)
        {
            return Vector3.Normalize(current);
        }

        if (previous.LengthSquared() > 1e-5f)
        {
            return Vector3.Normalize(previous);
        }

        return Vector3.UnitY;
    }

    private int GetSampleBudget(float viewDistance, IrradianceCacheEntry? previousEntry, int bounceDepth)
    {
        if (_sampleCount <= 0)
        {
            return 0;
        }

        float distanceFactor = Math.Clamp((viewDistance - _adaptiveStartDistance) * _adaptiveInvRange, 0f, 1f);
        float baseSamples = Lerp(_sampleCount, _adaptiveMinSamples, distanceFactor);
        int target = Math.Clamp((int)MathF.Round(baseSamples), _adaptiveMinSamples, _sampleCount);

        if (bounceDepth > 0)
        {
            target = Math.Max(_adaptiveMinSamples, target / (bounceDepth + 1));
        }

        if (previousEntry.HasValue)
        {
            int previousSamples = Math.Clamp(previousEntry.Value.SampleCount, 0, _sampleCount);
            if (previousSamples >= target)
            {
                return 0;
            }

            int remaining = target - previousSamples;
            return Math.Clamp(Math.Max(1, remaining), 1, _sampleCount - previousSamples);
        }

        return target;
    }

    private VoxelDdaWalker<TBlock> CreateWalker(
        VoxelWorld<TBlock> world,
        Vector3 origin,
        Vector3 direction,
        float maxDistance,
        out ThreadLocal<VoxelWorld<TBlock>.BlockAccessCache>? cacheSource,
        out VoxelWorld<TBlock>.BlockAccessCache cache)
    {
        cacheSource = _walkerCache;
        cache = cacheSource.Value;
        return new VoxelDdaWalker<TBlock>(world, origin, direction, maxDistance, _maxRaymarchSteps, ref cache);
    }

    private static void FinalizeWalker(
        ref VoxelDdaWalker<TBlock> walker,
        ThreadLocal<VoxelWorld<TBlock>.BlockAccessCache>? cacheSource,
        ref VoxelWorld<TBlock>.BlockAccessCache cache)
    {
        if (cacheSource is null)
        {
            return;
        }

        walker.CopyCacheTo(ref cache);
        cacheSource.Value = cache;
    }

    private float TraceVisibility(
        VoxelWorld<TBlock> world,
        IVoxelMaterialProvider<TBlock> materials,
        Vector3 origin,
        Vector3 direction,
        float maxDistance)
    {
        ThreadLocal<VoxelWorld<TBlock>.BlockAccessCache>? cacheSource;
        VoxelWorld<TBlock>.BlockAccessCache cache;
        var walker = CreateWalker(world, origin, direction, maxDistance, out cacheSource, out cache);
        float visibility = 1f;

        while (walker.TryStep(out var step))
        {
            if (step.Distance > maxDistance)
            {
                break;
            }

            if (step.Kind == VoxelDdaHitKind.Sky)
            {
                break;
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
                visibility = 0f;
                break;
            }
        }

        FinalizeWalker(ref walker, cacheSource, ref cache);
        return visibility;
    }

    private Vector3 SampleGlobalIllumination(
        VoxelWorld<TBlock> world,
        IVoxelMaterialProvider<TBlock> materials,
        Vector3 hitPoint,
        Vector3 normal,
        int bounceDepth,
        int sampleCount,
        out float ambientOcclusion,
        out Vector3 bentNormal,
        out int samplesTaken)
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

        ReadOnlySpan<Vector3> samplePool = GlobalIlluminationSamples.GetHemisphereSamples(Math.Max(sampleCount, _sampleCount));
        int poolLength = samplePool.Length;
        int startIndex = (int)(hash % (uint)poolLength);
        int stride = poolLength switch
        {
            <= 128 => 17,
            <= 512 => 53,
            _ => 97
        };

        float maxDistance = _maxDistance;
        int taken = 0;

        int iterations = Math.Max(0, sampleCount);

        for (int i = 0; i < iterations; i++)
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

            ThreadLocal<VoxelWorld<TBlock>.BlockAccessCache>? cacheSource;
            VoxelWorld<TBlock>.BlockAccessCache cache;
            var walker = CreateWalker(world, sampleOrigin, dir, maxDistance, out cacheSource, out cache);
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

            taken++;

            FinalizeWalker(ref walker, cacheSource, ref cache);
        }

        float invWeight = totalWeight > 0f ? 1f / totalWeight : 1f;
        ambientOcclusion = Math.Clamp(1f - blockedWeight * invWeight * _occlusionStrength, 0.1f, 1f);
        bentNormal = bentAccum.LengthSquared() > 1e-4f ? Vector3.Normalize(bentAccum) : normal;

        Vector3 gi = accum * invWeight;
        samplesTaken = taken;
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

    private readonly record struct SunVisibilityCacheKey(
        int X,
        int Y,
        int Z,
        BlockFace Face,
        int U,
        int V);

    private readonly record struct IrradianceCacheKey(
        int X,
        int Y,
        int Z,
        BlockFace Face,
        int U,
        int V);

    private readonly record struct IrradianceCacheEntry(
        Vector3 Irradiance,
        Vector3 BentNormal,
        float AmbientOcclusion,
        int SampleCount);
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
