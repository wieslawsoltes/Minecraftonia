using System;
using System.Numerics;
using System.Threading.Tasks;
using Minecraftonia.Rendering.Core;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.Rendering.Pipelines;

public sealed class VoxelRayTracer<TBlock> : IVoxelRenderer<TBlock>
{
    public const float DefaultMaxTraceDistance = 90f;
    private const float MaxDistance = DefaultMaxTraceDistance;

    private readonly VoxelSize _renderSize;
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
    private readonly GlobalIlluminationEngine<TBlock>? _giEngine;
    private readonly int _maxRaymarchSteps;

    public VoxelRayTracer(
        VoxelSize renderSize,
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
        _sampleOffsets = RenderSamplePattern.CreateStratified(_samplesPerPixel);
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
        _maxRaymarchSteps = Math.Clamp(gi.MaxSecondarySteps, 16, 256);
        _giEngine = gi.Enabled
            ? new GlobalIlluminationEngine<TBlock>(gi, MaxDistance, _isEmpty, SampleSky)
            : null;
    }

    public IVoxelRenderResult<TBlock> Render(
        IVoxelWorld<TBlock> world,
        Player player,
        IVoxelMaterialProvider<TBlock> materials,
        IVoxelFrameBuffer? framebuffer = null)
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

        VoxelSize fbSize = fb.Size;
        int width = fbSize.Width;
        int height = fbSize.Height;
        int stride = fb.Stride;

        unsafe
        {
            fixed (byte* pixelsPtr = fb.Pixels)
            {
                float invWidth = 1f / width;
                float invHeight = 1f / height;
                nint baseAddress = (nint)pixelsPtr;

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
                    byte* row = (byte*)(baseAddress + y * stride);

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
                    FxaaSharpenFilter.Apply(
                        pixelsPtr,
                        stride,
                        width,
                        height,
                        _fxaaContrastThreshold,
                        _fxaaRelativeThreshold,
                        _applySharpen,
                        _sharpenAmount,
                        _parallelOptions);
                }
            }
        }

        return new VoxelRenderResult<TBlock>(fb, camera);
    }

    private Vector4 TraceRay(
        IVoxelWorld<TBlock> world,
        Vector3 origin,
        Vector3 direction,
        IVoxelMaterialProvider<TBlock> materials,
        out float outDistance)
    {
        origin += direction * 0.0005f;

        var walker = new VoxelDdaWalker<TBlock>(world, origin, direction, MaxDistance, _maxRaymarchSteps);
        Vector3 accumColor = Vector3.Zero;
        float accumAlpha = 0f;
        float hitDistance = MaxDistance;
        bool hitAnything = false;

        while (walker.TryStep(out var step))
        {
            if (step.Kind == VoxelDdaHitKind.Sky)
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
            Vector2 uv = VoxelLightingMath.ComputeFaceUv(step.Face, local);
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
        IVoxelWorld<TBlock> world,
        IVoxelMaterialProvider<TBlock> materials,
        Vector3 hitPoint,
        BlockFace face,
        VoxelMaterialSample material,
        int bounceDepth)
    {
        Vector3 baseColor = Vector3.Clamp(material.Color, Vector3.Zero, new Vector3(4f));
        Vector3 normal = VoxelLightingMath.FaceToNormal(face);

        float legacyLight = VoxelLightingMath.GetFaceLight(face);
        Vector3 legacyTerm = baseColor * legacyLight;

        if (_giEngine is null)
        {
            return Vector3.Clamp(legacyTerm, Vector3.Zero, Vector3.One);
        }

        LightingResult lighting = _giEngine.ComputeLighting(
            world,
            materials.Sample,
            hitPoint,
            normal,
            face,
            material,
            bounceDepth);

        Vector3 advancedLighting = lighting.CombinedLighting;
        Vector3 advanced = Vector3.Clamp(baseColor * advancedLighting, Vector3.Zero, new Vector3(4f));

        Vector3 combined = Vector3.Lerp(legacyTerm, advanced, 0.85f);
        return Vector3.Clamp(combined, Vector3.Zero, Vector3.One);
    }


    private IVoxelFrameBuffer EnsureFramebuffer(IVoxelFrameBuffer? framebuffer)
    {
        if (framebuffer is null)
        {
            return new VoxelFrameBuffer(_renderSize);
        }

        if (framebuffer.Size != _renderSize)
        {
            framebuffer.Resize(_renderSize);
        }

        return framebuffer;
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
