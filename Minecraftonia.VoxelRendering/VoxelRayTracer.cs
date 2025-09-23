using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;
using Avalonia;
using Minecraftonia.VoxelEngine;
using Minecraftonia.VoxelRendering.Lighting;

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
    private readonly GlobalIlluminationEngine<TBlock>? _giEngine;
    private readonly int _maxRaymarchSteps;

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
        _maxRaymarchSteps = Math.Clamp(gi.MaxSecondarySteps, 16, 256);
        _giEngine = gi.Enabled
            ? new GlobalIlluminationEngine<TBlock>(gi, MaxDistance, _isEmpty, SampleSky)
            : null;
    }

    public VoxelRenderResult<TBlock> Render(
        VoxelWorld<TBlock> world,
        Player player,
        IVoxelMaterialProvider<TBlock> materials,
        VoxelFrameBuffer? framebuffer = null)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (player is null) throw new ArgumentNullException(nameof(player));
        if (materials is null) throw new ArgumentNullException(nameof(materials));

        var fb = EnsureFramebuffer(framebuffer);

        _giEngine?.BeginFrame();

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

        PixelSize fbSize = fb.Size;
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

                int tileHeight = Math.Max(1, height / (Math.Max(1, _parallelOptions.MaxDegreeOfParallelism) * 4));
                var partitioner = Partitioner.Create(0, height, tileHeight);

                Parallel.ForEach(partitioner, _parallelOptions, range =>
                {
                    int startY = range.Item1;
                    int endY = range.Item2;

                    for (int y = startY; y < endY; y++)
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
                    }
                });

                if (_applyFxaa)
                {
                    ApplyFxaaAndSharpenParallel(pixelsPtr, stride, width, height);
                }
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
                in step,
                hitPoint,
                uv,
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
        in VoxelDdaHit<TBlock> hit,
        Vector3 hitPoint,
        Vector2 uv,
        VoxelMaterialSample material,
        int bounceDepth)
    {
        Vector3 baseColor = Vector3.Clamp(material.Color, Vector3.Zero, new Vector3(4f));
        BlockFace face = hit.Face;
        Vector3 normal = VoxelLightingMath.FaceToNormal(face);

        float legacyLight = VoxelLightingMath.GetFaceLight(face);
        Vector3 legacyTerm = baseColor * legacyLight;

        if (_giEngine is null)
        {
            return Vector3.Clamp(legacyTerm, Vector3.Zero, Vector3.One);
        }

        LightingResult lighting = _giEngine.ComputeLighting(
            world,
            materials,
            in hit,
            hitPoint,
            uv,
            material,
            bounceDepth);

        Vector3 advancedLighting = lighting.CombinedLighting;
        Vector3 advanced = Vector3.Clamp(baseColor * advancedLighting, Vector3.Zero, new Vector3(4f));

        Vector3 combined = Vector3.Lerp(legacyTerm, advanced, 0.85f);
        return Vector3.Clamp(combined, Vector3.Zero, Vector3.One);
    }


    private VoxelFrameBuffer EnsureFramebuffer(VoxelFrameBuffer? framebuffer)
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
