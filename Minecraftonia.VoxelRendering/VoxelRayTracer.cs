using System;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.VoxelRendering;

public sealed class VoxelRayTracer<TBlock>
{
    public const float DefaultMaxTraceDistance = 90f;
    private const float MaxDistance = DefaultMaxTraceDistance;

    private readonly PixelSize _renderSize;
    private readonly float _fieldOfViewDegrees;
    private readonly Func<TBlock, bool> _isSolid;
    private readonly Func<TBlock, bool> _isEmpty;
    private readonly float _fogStart;
    private readonly float _fogEnd;
    private readonly Vector3 _fogColor;
    private readonly float _fogInvRange;
    private readonly int _samplesPerPixel;
    private readonly Vector2[] _sampleOffsets;
    private readonly bool _applyFxaa;
    private readonly float _fxaaContrastThreshold;
    private readonly float _fxaaRelativeThreshold;
    private readonly bool _applySharpen;
    private readonly float _sharpenAmount;
    private readonly ParallelOptions _parallelOptions;
    private readonly bool _enableGlobalIllumination;
    private readonly int _giSampleCount;
    private readonly int _giBounceCount;
    private readonly float _giMaxDistance;
    private readonly float _giStrength;
    private readonly float _giSkyContribution;
    private readonly float _giOcclusionStrength;
    private readonly float _giDistanceAttenuation;
    private readonly float _shadowBias;
    private readonly float _sunShadowSoftness;
    private readonly float _sunMaxDistance;
    private readonly Vector3 _sunDirection;
    private readonly Vector3 _sunColor;
    private readonly float _sunIntensity;
    private readonly Vector3 _ambientLight;
    private readonly bool _giUseBentNormal;
    private readonly int _giMaxRaymarchSteps;

    private static readonly Vector3[] s_GiSamples = CreateHemisphereSamples(128);

    public VoxelRayTracer(
        PixelSize renderSize,
        float fieldOfViewDegrees,
        Func<TBlock, bool> isSolid,
        Func<TBlock, bool> isEmpty,
        int samplesPerPixel = 1,
        bool enableFxaa = true,
        float fxaaContrastThreshold = 0.0312f,
        float fxaaRelativeThreshold = 0.125f,
        bool enableSharpen = true,
        float sharpenAmount = 0.18f,
        float fogStart = 45f,
        float fogEnd = 90f,
        Vector3? fogColor = null,
        GlobalIlluminationSettings? globalIllumination = null)
    {
        _renderSize = renderSize;
        _fieldOfViewDegrees = fieldOfViewDegrees;
        _isSolid = isSolid ?? throw new ArgumentNullException(nameof(isSolid));
        _isEmpty = isEmpty ?? throw new ArgumentNullException(nameof(isEmpty));
        _fogStart = fogStart;
        _fogEnd = fogEnd;
        _fogColor = fogColor ?? new Vector3(0.72f, 0.84f, 0.96f);
        float range = Math.Max(_fogEnd - _fogStart, 0.0001f);
        _fogInvRange = 1f / range;
        _samplesPerPixel = Math.Max(1, samplesPerPixel);
        _sampleOffsets = CreateSamplePattern(_samplesPerPixel);
        _applyFxaa = enableFxaa;
        _fxaaContrastThreshold = fxaaContrastThreshold;
        _fxaaRelativeThreshold = fxaaRelativeThreshold;
        _applySharpen = enableSharpen;
        _sharpenAmount = Math.Clamp(sharpenAmount, 0f, 1f);
        _parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
        };

        var gi = globalIllumination ?? GlobalIlluminationSettings.Default;
        _enableGlobalIllumination = gi.Enabled;
        _giSampleCount = Math.Clamp(gi.DiffuseSampleCount, 0, s_GiSamples.Length);
        _giBounceCount = Math.Max(0, gi.BounceCount);
        _giMaxDistance = Math.Clamp(gi.MaxDistance, 0.5f, MaxDistance);
        _giStrength = MathF.Max(0f, gi.Strength);
        _giSkyContribution = MathF.Max(0f, gi.SkyContribution);
        _giOcclusionStrength = Math.Clamp(gi.OcclusionStrength, 0f, 1.5f);
        _giDistanceAttenuation = MathF.Max(0.0001f, gi.DistanceAttenuation);
        _shadowBias = MathF.Max(1e-4f, gi.ShadowBias);
        _sunShadowSoftness = Math.Clamp(gi.SunShadowSoftness, 0f, 1f);
        _sunMaxDistance = Math.Clamp(gi.SunMaxDistance, 0.5f, MaxDistance);
        _sunDirection = Vector3.Normalize(gi.SunDirection.LengthSquared() > 0.0001f ? gi.SunDirection : new Vector3(-0.35f, 0.88f, 0.25f));
        _sunColor = Vector3.Clamp(gi.SunColor, Vector3.Zero, new Vector3(4f));
        _sunIntensity = MathF.Max(0f, gi.SunIntensity);
        _ambientLight = Vector3.Clamp(gi.AmbientLight, Vector3.Zero, new Vector3(4f));
        _giUseBentNormal = gi.UseBentNormalForAmbient;
        _giMaxRaymarchSteps = Math.Clamp(gi.MaxSecondarySteps, 16, 256);
    }

    public VoxelRenderResult<TBlock> Render(
        VoxelWorld<TBlock> world,
        Player player,
        IVoxelMaterialProvider<TBlock> materials,
        WriteableBitmap? framebuffer = null)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (player is null) throw new ArgumentNullException(nameof(player));
        if (materials is null) throw new ArgumentNullException(nameof(materials));

        var fb = EnsureFramebuffer(framebuffer);

        Vector3 forward = player.Forward;
        if (!float.IsFinite(forward.X + forward.Y + forward.Z) || forward.LengthSquared() < 0.0001f)
        {
            forward = Vector3.UnitZ;
        }
        else
        {
            forward = Vector3.Normalize(forward);
        }

        Vector3 right = Vector3.Cross(forward, Vector3.UnitY);
        if (right.LengthSquared() < 0.0001f)
        {
            right = Vector3.UnitX;
        }
        else
        {
            right = Vector3.Normalize(right);
        }

        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));
        float aspect = _renderSize.Width / (float)_renderSize.Height;
        float tanHalfFov = MathF.Tan(_fieldOfViewDegrees * 0.5f * MathF.PI / 180f);
        Vector3 basisX = right * (aspect * tanHalfFov);
        Vector3 basisY = up * tanHalfFov;
        var camera = new VoxelCamera(forward, right, up, tanHalfFov, aspect);

        Vector3 eye = player.EyePosition;

        using var fbLock = fb.Lock();
        unsafe
        {
            byte* buffer = (byte*)fbLock.Address;
            int stride = fbLock.RowBytes;
            int width = fb.PixelSize.Width;
            int height = fb.PixelSize.Height;
            float invWidth = 1f / width;
            float invHeight = 1f / height;

            Vector4 SamplePixel(float samplePx, float samplePy)
            {
                float ndcX = samplePx * 2f - 1f;
                float ndcYLocal = 1f - samplePy * 2f;
                Vector3 dir = forward + ndcX * basisX + ndcYLocal * basisY;
                float lenSq = dir.X * dir.X + dir.Y * dir.Y + dir.Z * dir.Z;
                if (lenSq > 1e-12f)
                {
                    float invLen = MathF.ReciprocalSqrtEstimate(lenSq);
                    dir *= invLen;
                }
                else
                {
                    dir = forward;
                }

                Vector4 sample = TraceRay(world, eye, dir, materials, out float distance);
                return ApplyFog(sample, distance);
            }

            Parallel.For(0, height, _parallelOptions, y =>
            {
                byte* row = buffer + y * stride;

                for (int x = 0; x < width; x++)
                {
                    Vector4 firstSample = SamplePixel((x + _sampleOffsets[0].X) * invWidth, (y + _sampleOffsets[0].Y) * invHeight);
                    Vector4 accum = firstSample;
                    int sampleCount = 1;

                    for (int i = 1; i < _samplesPerPixel && i < _sampleOffsets.Length; i++)
                    {
                        var offset = _sampleOffsets[i];
                        Vector4 sample = SamplePixel((x + offset.X) * invWidth, (y + offset.Y) * invHeight);
                        accum += sample;
                        sampleCount++;

                        if (Vector4.DistanceSquared(sample, firstSample) < 0.0005f)
                        {
                            break;
                        }
                    }

                    float invSamples = 1f / sampleCount;
                    Vector4 averaged = accum * invSamples;
                    WritePixel(row, x, averaged);
                }
            });

            if (_applyFxaa)
            {
                ApplyFxaaAndSharpenParallel(buffer, stride, width, height);
            }
        }

        return new VoxelRenderResult<TBlock>(fb, camera);
    }

    private Vector4 TraceRay(
        VoxelWorld<TBlock> world,
        Vector3 origin,
        Vector3 direction,
        IVoxelMaterialProvider<TBlock> materials,
        out float outDistance)
    {
        origin += direction * 0.0005f;

        var walker = new DdaWalker(world, origin, direction, MaxDistance, _giMaxRaymarchSteps);
        Vector3 accumColor = Vector3.Zero;
        float accumAlpha = 0f;
        float hitDistance = MaxDistance;
        bool hitAnything = false;

        while (walker.TryStep(out var step))
        {
            if (step.Kind == DdaHitKind.Sky)
            {
                Vector3 sky = SampleSky(direction);
                accumColor += (1f - accumAlpha) * sky;
                accumAlpha = 1f;
                hitDistance = step.Distance;
                hitAnything = true;
                break;
            }

            TBlock block = step.Block;
            if (_isEmpty(block))
            {
                continue;
            }

            Vector3 hitPoint = origin + direction * step.Distance;
            Vector3 local = hitPoint - new Vector3(step.VoxelX, step.VoxelY, step.VoxelZ);
            Vector2 uv = ComputeFaceUV(step.Face, local);
            VoxelMaterialSample material = materials.Sample(block, step.Face, uv.X, uv.Y);
            float opacity = Math.Clamp(material.Opacity, 0f, 1f);
            if (opacity <= 0f)
            {
                continue;
            }

            Vector3 shaded = ComputeShading(
                world,
                materials,
                hitPoint,
                step.Face,
                material,
                bounceDepth: 0);

            accumColor += (1f - accumAlpha) * opacity * shaded;
            accumAlpha += (1f - accumAlpha) * opacity;
            hitDistance = step.Distance;
            hitAnything = true;

            if (accumAlpha >= 0.995f || _isSolid(block))
            {
                break;
            }
        }

        if (!hitAnything || accumAlpha < 0.995f)
        {
            Vector3 sky = SampleSky(direction);
            accumColor += (1f - accumAlpha) * sky;
            accumAlpha = 1f;
            hitDistance = Math.Min(hitDistance, MaxDistance);
        }

        accumColor = Vector3.Clamp(accumColor, Vector3.Zero, Vector3.One);
        outDistance = hitDistance;
        return new Vector4(accumColor, 1f);
    }

    private Vector3 ComputeShading(
        VoxelWorld<TBlock> world,
        IVoxelMaterialProvider<TBlock> materials,
        Vector3 hitPoint,
        BlockFace face,
        VoxelMaterialSample material,
        int bounceDepth)
    {
        Vector3 baseColor = Vector3.Clamp(material.Color, Vector3.Zero, new Vector3(4f));
        Vector3 normal = FaceToNormal(face);

        float legacyLight = GetFaceLight(face);
        Vector3 legacyTerm = baseColor * legacyLight;

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

        Vector3 bentNormal = normal;
        float ambientOcclusion = 1f;
        Vector3 gi = Vector3.Zero;

        bool allowGi = _enableGlobalIllumination && _giSampleCount > 0 && bounceDepth < _giBounceCount;
        if (allowGi)
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

        Vector3 ambientNormal = (_giUseBentNormal && bentNormal.LengthSquared() > 1e-4f) ? bentNormal : normal;
        float ambientWrap = Math.Clamp(Vector3.Dot(ambientNormal, _sunDirection) * 0.5f + 0.5f, 0.1f, 1f);
        Vector3 ambient = _ambientLight * ambientWrap * ambientOcclusion;

        Vector3 lighting = ambient + direct + gi * _giStrength;
        Vector3 advanced = Vector3.Clamp(baseColor * lighting, Vector3.Zero, new Vector3(4f));

        Vector3 combined = Vector3.Lerp(legacyTerm, advanced, 0.85f);
        return Vector3.Clamp(combined, Vector3.Zero, Vector3.One);
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
        var walker = new DdaWalker(world, origin, direction, maxDistance, _giMaxRaymarchSteps);
        float visibility = 1f;

        while (walker.TryStep(out var step))
        {
            if (step.Distance > maxDistance)
            {
                break;
            }

            if (step.Kind == DdaHitKind.Sky)
            {
                return visibility;
            }

            TBlock block = step.Block;
            if (_isEmpty(block))
            {
                continue;
            }

            Vector3 hit = origin + direction * step.Distance;
            Vector3 local = hit - new Vector3(step.VoxelX, step.VoxelY, step.VoxelZ);
            Vector2 uv = ComputeFaceUV(step.Face, local);
            VoxelMaterialSample sample = materials.Sample(block, step.Face, uv.X, uv.Y);
            float opacity = Math.Clamp(sample.Opacity, 0f, 1f);

            if (opacity <= 0.05f)
            {
                continue;
            }

            if (_isSolid(block) || opacity >= 0.85f)
            {
                return 0f;
            }

            visibility *= MathF.Max(0f, 1f - opacity);
            if (visibility <= 0.05f)
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
        if (!_enableGlobalIllumination || _giSampleCount <= 0 || bounceDepth >= _giBounceCount)
        {
            ambientOcclusion = 1f;
            bentNormal = normal;
            return Vector3.Zero;
        }

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

        int samplePool = s_GiSamples.Length;
        int startIndex = (int)(hash % (uint)samplePool);
        const int stride = 17;

        float maxDistance = MathF.Min(_giMaxDistance, MaxDistance);

        for (int i = 0; i < _giSampleCount; i++)
        {
            int index = (startIndex + i * stride) % samplePool;
            Vector3 local = s_GiSamples[index];
            Vector3 dir = TransformHemisphereSample(rotatedTangent, rotatedBitangent, normal, local);
            dir = Vector3.Normalize(dir);

            float weight = MathF.Max(local.Y, 0.0001f);
            Vector3 sampleOrigin = hitPoint + dir * (_shadowBias * 2f);

            Vector3 sampleRadiance = Vector3.Zero;
            bool blocked = false;
            float opacityAccum = 0f;

            var walker = new DdaWalker(world, sampleOrigin, dir, maxDistance, _giMaxRaymarchSteps);
            while (walker.TryStep(out var step))
            {
                if (step.Kind == DdaHitKind.Sky)
                {
                    sampleRadiance = SampleSky(dir) * _giSkyContribution;
                    break;
                }

                TBlock block = step.Block;
                if (_isEmpty(block))
                {
                    continue;
                }

                Vector3 bouncePoint = sampleOrigin + dir * step.Distance;
                Vector3 localHit = bouncePoint - new Vector3(step.VoxelX, step.VoxelY, step.VoxelZ);
                Vector2 uv = ComputeFaceUV(step.Face, localHit);
                VoxelMaterialSample bounceSample = materials.Sample(block, step.Face, uv.X, uv.Y);
                float opacity = Math.Clamp(bounceSample.Opacity, 0f, 1f);
                if (opacity <= 0.01f)
                {
                    continue;
                }

                Vector3 bounceNormal = FaceToNormal(step.Face);
                float lambert = MathF.Max(0.05f, Vector3.Dot(bounceNormal, -dir));
                float falloff = MathF.Exp(-step.Distance * _giDistanceAttenuation);
                Vector3 bounceColor = Vector3.Clamp(bounceSample.Color, Vector3.Zero, new Vector3(4f));
                Vector3 bounceLighting = ApproximateBounceLighting(step.Face, bounceNormal, bounceColor);
                sampleRadiance = bounceLighting * lambert * falloff;
                blocked = true;
                opacityAccum = opacity;
                break;
            }

            if (!blocked && sampleRadiance == Vector3.Zero)
            {
                sampleRadiance = SampleSky(dir) * _giSkyContribution;
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
        ambientOcclusion = Math.Clamp(1f - blockedWeight * invWeight * _giOcclusionStrength, 0.1f, 1f);
        bentNormal = bentAccum.LengthSquared() > 1e-4f ? Vector3.Normalize(bentAccum) : normal;

        Vector3 gi = accum * invWeight;
        return Vector3.Clamp(gi, Vector3.Zero, new Vector3(4f));
    }

    private Vector3 ApproximateBounceLighting(BlockFace face, Vector3 normal, Vector3 albedo)
    {
        float faceLight = MathF.Max(0.35f, GetFaceLight(face));
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

    private static Vector2 Hammersley(int index, int count)
    {
        float e1 = index / (float)count;
        float e2 = RadicalInverseVdC(index);
        return new Vector2(e1, e2);
    }

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

    private static void SplitCoordinate(int value, int size, out int chunk, out int local)
    {
        chunk = value / size;
        local = value - chunk * size;
        if (local < 0)
        {
            local += size;
            chunk -= 1;
        }
    }

    private static void NormalizeAxis(ref int chunk, ref int local, int size)
    {
        if (local >= size)
        {
            local -= size;
            chunk += 1;
        }
        else if (local < 0)
        {
            local += size;
            chunk -= 1;
        }
    }

    private static int FloorToInt(float value)
    {
        return value >= 0f ? (int)value : (int)MathF.Floor(value);
    }

    private enum DdaHitKind
    {
        None,
        Block,
        Sky
    }

    private readonly struct DdaHit
    {
        public DdaHitKind Kind { get; init; }
        public BlockFace Face { get; init; }
        public float Distance { get; init; }
        public int VoxelX { get; init; }
        public int VoxelY { get; init; }
        public int VoxelZ { get; init; }
        public TBlock Block { get; init; }
    }

    private struct DdaWalker
    {
        private readonly VoxelWorld<TBlock> _world;
        private readonly Vector3 _origin;
        private readonly Vector3 _direction;
        private readonly float _maxDistance;
        private readonly int _maxSteps;
        private readonly ChunkDimensions _dims;
        private readonly int _chunkCountX;
        private readonly int _chunkCountY;
        private readonly int _chunkCountZ;
        private VoxelWorld<TBlock>.BlockAccessCache _cache;

        private int _mapX;
        private int _mapY;
        private int _mapZ;

        private int _stepX;
        private int _stepY;
        private int _stepZ;

        private float _sideDistX;
        private float _sideDistY;
        private float _sideDistZ;

        private float _deltaDistX;
        private float _deltaDistY;
        private float _deltaDistZ;

        private int _chunkX;
        private int _chunkY;
        private int _chunkZ;

        private int _localX;
        private int _localY;
        private int _localZ;

        private int _stepsTaken;

        public DdaWalker(
            VoxelWorld<TBlock> world,
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            int maxSteps)
        {
            _world = world;
            _origin = origin;
            _direction = direction;
            _maxDistance = MathF.Max(0.01f, maxDistance);
            _maxSteps = Math.Max(1, maxSteps);
            _dims = world.ChunkSize;
            _chunkCountX = world.ChunkCountX;
            _chunkCountY = world.ChunkCountY;
            _chunkCountZ = world.ChunkCountZ;
            _cache = new VoxelWorld<TBlock>.BlockAccessCache();

            _mapX = FloorToInt(origin.X);
            _mapY = FloorToInt(origin.Y);
            _mapZ = FloorToInt(origin.Z);

            _stepX = direction.X < 0f ? -1 : 1;
            _stepY = direction.Y < 0f ? -1 : 1;
            _stepZ = direction.Z < 0f ? -1 : 1;

            float invDirX = direction.X == 0f ? 0f : 1f / direction.X;
            float invDirY = direction.Y == 0f ? 0f : 1f / direction.Y;
            float invDirZ = direction.Z == 0f ? 0f : 1f / direction.Z;

            _deltaDistX = MathF.Abs(invDirX);
            _deltaDistY = MathF.Abs(invDirY);
            _deltaDistZ = MathF.Abs(invDirZ);

            float nextBoundaryX = _stepX > 0 ? _mapX + 1f : _mapX;
            float nextBoundaryY = _stepY > 0 ? _mapY + 1f : _mapY;
            float nextBoundaryZ = _stepZ > 0 ? _mapZ + 1f : _mapZ;

            _sideDistX = (_stepX > 0 ? (nextBoundaryX - origin.X) : (origin.X - nextBoundaryX)) * _deltaDistX;
            _sideDistY = (_stepY > 0 ? (nextBoundaryY - origin.Y) : (origin.Y - nextBoundaryY)) * _deltaDistY;
            _sideDistZ = (_stepZ > 0 ? (nextBoundaryZ - origin.Z) : (origin.Z - nextBoundaryZ)) * _deltaDistZ;

            if (!float.IsFinite(_sideDistX)) _sideDistX = 0f;
            if (!float.IsFinite(_sideDistY)) _sideDistY = 0f;
            if (!float.IsFinite(_sideDistZ)) _sideDistZ = 0f;

            SplitCoordinate(_mapX, _dims.SizeX, out _chunkX, out _localX);
            SplitCoordinate(_mapY, _dims.SizeY, out _chunkY, out _localY);
            SplitCoordinate(_mapZ, _dims.SizeZ, out _chunkZ, out _localZ);

            _stepsTaken = 0;
        }

        public bool TryStep(out DdaHit hit)
        {
            hit = default;

            if (_stepsTaken >= _maxSteps)
            {
                return false;
            }

            float traveled;
            BlockFace face;

            if (_sideDistX < _sideDistY)
            {
                if (_sideDistX < _sideDistZ)
                {
                    _mapX += _stepX;
                    _localX += _stepX;
                    NormalizeAxis(ref _chunkX, ref _localX, _dims.SizeX);
                    traveled = _sideDistX;
                    _sideDistX += _deltaDistX;
                    face = _stepX > 0 ? BlockFace.NegativeX : BlockFace.PositiveX;
                }
                else
                {
                    _mapZ += _stepZ;
                    _localZ += _stepZ;
                    NormalizeAxis(ref _chunkZ, ref _localZ, _dims.SizeZ);
                    traveled = _sideDistZ;
                    _sideDistZ += _deltaDistZ;
                    face = _stepZ > 0 ? BlockFace.NegativeZ : BlockFace.PositiveZ;
                }
            }
            else
            {
                if (_sideDistY < _sideDistZ)
                {
                    _mapY += _stepY;
                    _localY += _stepY;
                    NormalizeAxis(ref _chunkY, ref _localY, _dims.SizeY);
                    traveled = _sideDistY;
                    _sideDistY += _deltaDistY;
                    face = _stepY > 0 ? BlockFace.NegativeY : BlockFace.PositiveY;
                }
                else
                {
                    _mapZ += _stepZ;
                    _localZ += _stepZ;
                    NormalizeAxis(ref _chunkZ, ref _localZ, _dims.SizeZ);
                    traveled = _sideDistZ;
                    _sideDistZ += _deltaDistZ;
                    face = _stepZ > 0 ? BlockFace.NegativeZ : BlockFace.PositiveZ;
                }
            }

            _stepsTaken++;

            if (traveled < 0f)
            {
                traveled = 0f;
            }

            if (!float.IsFinite(traveled) || traveled >= _maxDistance)
            {
                return false;
            }

            if (_chunkX < 0 || _chunkX >= _chunkCountX ||
                _chunkY < 0 || _chunkY >= _chunkCountY ||
                _chunkZ < 0 || _chunkZ >= _chunkCountZ)
            {
                hit = new DdaHit
                {
                    Kind = DdaHitKind.Sky,
                    Face = face,
                    Distance = traveled,
                    VoxelX = _mapX,
                    VoxelY = _mapY,
                    VoxelZ = _mapZ,
                    Block = default!
                };
                return true;
            }

            TBlock block = _world.GetBlockFast(_chunkX, _chunkY, _chunkZ, _localX, _localY, _localZ, ref _cache);

            hit = new DdaHit
            {
                Kind = DdaHitKind.Block,
                Face = face,
                Distance = traveled,
                VoxelX = _mapX,
                VoxelY = _mapY,
                VoxelZ = _mapZ,
                Block = block
            };
            return true;
        }
    }

    private WriteableBitmap EnsureFramebuffer(WriteableBitmap? framebuffer)
    {
        if (framebuffer is { PixelSize: var size } && size == _renderSize)
        {
            return framebuffer;
        }

        framebuffer?.Dispose();
        return new WriteableBitmap(_renderSize, new Avalonia.Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
    }

    private Vector4 ApplyFog(Vector4 color, float distance)
    {
        if (distance <= _fogStart)
        {
            return color;
        }

        float fogFactor = Math.Clamp((distance - _fogStart) * _fogInvRange, 0f, 1f);
        var rgb = new Vector3(color.X, color.Y, color.Z);
        rgb = Vector3.Lerp(rgb, _fogColor, fogFactor);
        return new Vector4(rgb, color.W);
    }

    private static Vector2[] CreateSamplePattern(int samples)
    {
        var offsets = new Vector2[samples];
        offsets[0] = new Vector2(0.5f, 0.5f);

        if (samples == 1)
        {
            return offsets;
        }

        int grid = (int)Math.Ceiling(MathF.Sqrt(samples));
        float step = 1f / grid;
        float half = step / 2f;

        int index = 1;
        for (int gy = 0; gy < grid && index < samples; gy++)
        {
            for (int gx = 0; gx < grid && index < samples; gx++)
            {
                float ox = half + gx * step;
                float oy = half + gy * step;
                if (Math.Abs(ox - 0.5f) < 0.001f && Math.Abs(oy - 0.5f) < 0.001f)
                {
                    continue;
                }

                offsets[index++] = new Vector2(ox, oy);
            }
        }

        // Fallback in case grid skips some slots due to center filtering.
        while (index < samples)
        {
            offsets[index] = new Vector2(0.5f, 0.5f);
            index++;
        }

        return offsets;
    }

    private unsafe void ApplyFxaaAndSharpenParallel(byte* buffer, int stride, int width, int height)
    {
        if (width < 3 || height < 3)
        {
            return;
        }
        int totalBytes = stride * height;
        var srcBuffer = new byte[totalBytes];
        new Span<byte>(buffer, totalBytes).CopyTo(srcBuffer);

        fixed (byte* srcPtr = srcBuffer)
        {
            nint srcBase = (nint)srcPtr;
            nint dstBase = (nint)buffer;

            Parallel.For(0, height, _parallelOptions, y =>
            {
                byte* srcRow = (byte*)(srcBase + y * stride);
                byte* dstRow = (byte*)(dstBase + y * stride);

                if (y == 0 || y == height - 1)
                {
                    Buffer.MemoryCopy(srcRow, dstRow, stride, width * 4);
                    return;
                }

                byte* srcPrev = srcRow - stride;
                byte* srcNext = srcRow + stride;

                for (int x = 0; x < width; x++)
                {
                    byte* srcPixel = srcRow + x * 4;
                    byte* dstPixel = dstRow + x * 4;

                    if (x == 0 || x == width - 1)
                    {
                        CopyPixel(dstPixel, srcPixel);
                        continue;
                    }

                    float lumaCenter = ComputeLuma(srcPixel);
                    float lumaLeft = ComputeLuma(srcRow + (x - 1) * 4);
                    float lumaRight = ComputeLuma(srcRow + (x + 1) * 4);
                    float lumaUp = ComputeLuma(srcPrev + x * 4);
                    float lumaDown = ComputeLuma(srcNext + x * 4);

                    float lumaMin = MathF.Min(lumaCenter, MathF.Min(MathF.Min(lumaLeft, lumaRight), MathF.Min(lumaUp, lumaDown)));
                    float lumaMax = MathF.Max(lumaCenter, MathF.Max(MathF.Max(lumaLeft, lumaRight), MathF.Max(lumaUp, lumaDown)));
                    float contrast = lumaMax - lumaMin;
                    float threshold = Math.Max(_fxaaContrastThreshold, lumaMax * _fxaaRelativeThreshold);

                    if (contrast < threshold)
                    {
                        CopyPixel(dstPixel, srcPixel);
                        continue;
                    }

                    bool horizontal = Math.Abs(lumaLeft - lumaRight) >= Math.Abs(lumaUp - lumaDown);

                    Vector4 colorMinus = horizontal ? LoadColorVector(srcPrev + x * 4) : LoadColorVector(srcRow + (x - 1) * 4);
                    Vector4 colorPlus = horizontal ? LoadColorVector(srcNext + x * 4) : LoadColorVector(srcRow + (x + 1) * 4);
                    float lumaMinus = horizontal ? ComputeLuma(srcPrev + x * 4) : lumaLeft;
                    float lumaPlus = horizontal ? ComputeLuma(srcNext + x * 4) : lumaRight;

                    Vector4 centerColor = LoadColorVector(srcPixel);
                    Vector4 blended = (colorMinus + colorPlus) * 0.5f;

                    float subpixelBlend = Math.Clamp((contrast - threshold) / (contrast + 1e-4f), 0f, 1f);
                    float gradientBlend = Math.Clamp(Math.Abs(lumaMinus - lumaPlus), 0f, 1f);
                    float blend = Math.Clamp(subpixelBlend * 0.65f + gradientBlend * 0.35f, 0f, 0.65f);

                    Vector4 aaColor = Vector4.Lerp(centerColor, blended, blend);
                    aaColor.W = centerColor.W;

                    if (_applySharpen && _sharpenAmount > 0f)
                    {
                        Vector4 neighborAverage = (LoadColorVector(srcRow + (x - 1) * 4) +
                                                  LoadColorVector(srcRow + (x + 1) * 4) +
                                                  LoadColorVector(srcPrev + x * 4) +
                                                  LoadColorVector(srcNext + x * 4)) * 0.25f;

                        aaColor += (aaColor - neighborAverage) * _sharpenAmount;
                        aaColor.W = centerColor.W;
                    }

                    StoreColor(dstPixel, aaColor);
                }
            });
        }
    }

    private static unsafe void CopyPixel(byte* dst, byte* src)
    {
        dst[0] = src[0];
        dst[1] = src[1];
        dst[2] = src[2];
        dst[3] = src[3];
    }

    private static readonly Vector4 LumaWeights = new(0.299f, 0.587f, 0.114f, 0f);

    private static unsafe float ComputeLuma(byte* pixel)
    {
        Vector4 color = LoadColorVector(pixel);
        return Vector4.Dot(color, LumaWeights) / 255f;
    }

    private static unsafe Vector4 LoadColorVector(byte* pixel)
    {
        return new Vector4(pixel[2], pixel[1], pixel[0], pixel[3]);
    }

    private static unsafe void StoreColor(byte* pixel, Vector4 color)
    {
        color = Vector4.Clamp(color, Vector4.Zero, new Vector4(255f, 255f, 255f, 255f));
        pixel[2] = (byte)color.X;
        pixel[1] = (byte)color.Y;
        pixel[0] = (byte)color.Z;
        pixel[3] = (byte)color.W;
    }

    private static Vector3 SampleSky(Vector3 direction)
    {
        direction = Vector3.Normalize(direction);
        float t = Math.Clamp(direction.Y * 0.5f + 0.5f, 0f, 1f);
        Vector3 horizon = new(0.78f, 0.87f, 0.95f);
        Vector3 zenith = new(0.18f, 0.32f, 0.58f);
        Vector3 sky = Vector3.Lerp(horizon, zenith, t);

        Vector3 sunDirection = Vector3.Normalize(new Vector3(-0.35f, 0.88f, 0.25f));
        float sunFactor = MathF.Max(0f, Vector3.Dot(direction, sunDirection));
        float sunGlow = MathF.Pow(sunFactor, 32f) * 0.35f;
        sky += new Vector3(1f, 0.93f, 0.78f) * sunGlow;

        return Vector3.Clamp(sky, Vector3.Zero, Vector3.One);
    }

    private static Vector2 ComputeFaceUV(BlockFace face, Vector3 local)
    {
        local = new Vector3(
            Math.Clamp(local.X, 0f, 0.999f),
            Math.Clamp(local.Y, 0f, 0.999f),
            Math.Clamp(local.Z, 0f, 0.999f));

        return face switch
        {
            BlockFace.PositiveX => new Vector2(1f - local.Z, 1f - local.Y),
            BlockFace.NegativeX => new Vector2(local.Z, 1f - local.Y),
            BlockFace.PositiveZ => new Vector2(local.X, 1f - local.Y),
            BlockFace.NegativeZ => new Vector2(1f - local.X, 1f - local.Y),
            BlockFace.PositiveY => new Vector2(local.X, local.Z),
            BlockFace.NegativeY => new Vector2(local.X, 1f - local.Z),
            _ => new Vector2(local.X, local.Y)
        };
    }

    private static Vector3 FaceToNormal(BlockFace face)
    {
        return face switch
        {
            BlockFace.PositiveX => Vector3.UnitX,
            BlockFace.NegativeX => -Vector3.UnitX,
            BlockFace.PositiveY => Vector3.UnitY,
            BlockFace.NegativeY => -Vector3.UnitY,
            BlockFace.PositiveZ => Vector3.UnitZ,
            BlockFace.NegativeZ => -Vector3.UnitZ,
            _ => Vector3.UnitY
        };
    }

    private static float GetFaceLight(BlockFace face)
    {
        return face switch
        {
            BlockFace.PositiveY => 1.0f,
            BlockFace.NegativeY => 0.55f,
            BlockFace.PositiveX => 0.9f,
            BlockFace.NegativeX => 0.75f,
            BlockFace.PositiveZ => 0.85f,
            BlockFace.NegativeZ => 0.7f,
            _ => 1f
        };
    }

    private static unsafe void WritePixel(byte* row, int x, Vector4 color)
    {
        float alpha = Math.Clamp(color.W, 0f, 1f);
        Vector3 rgb = new(color.X, color.Y, color.Z);
        rgb = Vector3.Clamp(rgb, Vector3.Zero, Vector3.One);
        Vector3 premul = rgb * alpha;

        int index = x * 4;
        row[index + 0] = (byte)(premul.Z * 255f);
        row[index + 1] = (byte)(premul.Y * 255f);
        row[index + 2] = (byte)(premul.X * 255f);
        row[index + 3] = (byte)(alpha * 255f);
    }
}
