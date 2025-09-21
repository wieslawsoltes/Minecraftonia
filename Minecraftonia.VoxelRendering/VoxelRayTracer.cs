using System;
using System.Numerics;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.VoxelRendering;

public sealed class VoxelRayTracer<TBlock>
{
    private const float MaxDistance = 90f;

    private readonly PixelSize _renderSize;
    private readonly float _fieldOfViewDegrees;
    private readonly Func<TBlock, bool> _isSolid;
    private readonly Func<TBlock, bool> _isEmpty;
    private readonly float _fogStart;
    private readonly float _fogEnd;
    private readonly Vector3 _fogColor;

    public VoxelRayTracer(
        PixelSize renderSize,
        float fieldOfViewDegrees,
        Func<TBlock, bool> isSolid,
        Func<TBlock, bool> isEmpty,
        float fogStart = 45f,
        float fogEnd = 90f,
        Vector3? fogColor = null)
    {
        _renderSize = renderSize;
        _fieldOfViewDegrees = fieldOfViewDegrees;
        _isSolid = isSolid ?? throw new ArgumentNullException(nameof(isSolid));
        _isEmpty = isEmpty ?? throw new ArgumentNullException(nameof(isEmpty));
        _fogStart = fogStart;
        _fogEnd = fogEnd;
        _fogColor = fogColor ?? new Vector3(0.72f, 0.84f, 0.96f);
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
        var camera = new VoxelCamera(forward, right, up, tanHalfFov, aspect);

        Vector3 eye = player.EyePosition;

        using var fbLock = fb.Lock();
        unsafe
        {
            byte* buffer = (byte*)fbLock.Address;
            int stride = fbLock.RowBytes;
            int width = fb.PixelSize.Width;
            int height = fb.PixelSize.Height;

            for (int y = 0; y < height; y++)
            {
                byte* row = buffer + y * stride;
                float ndcY = 1f - ((y + 0.5f) / height) * 2f;

                for (int x = 0; x < width; x++)
                {
                    float ndcX = ((x + 0.5f) / width) * 2f - 1f;
                    Vector3 dir = forward
                                   + ndcX * aspect * tanHalfFov * right
                                   + ndcY * tanHalfFov * up;
                    dir = Vector3.Normalize(dir);

                    Vector4 sample = TraceRay(world, eye, dir, materials, out float distance);
                    sample = ApplyFog(sample, distance);
                    WritePixel(row, x, sample);
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

        int mapX = (int)MathF.Floor(origin.X);
        int mapY = (int)MathF.Floor(origin.Y);
        int mapZ = (int)MathF.Floor(origin.Z);

        float rayDirX = direction.X;
        float rayDirY = direction.Y;
        float rayDirZ = direction.Z;

        int stepX = rayDirX < 0 ? -1 : 1;
        int stepY = rayDirY < 0 ? -1 : 1;
        int stepZ = rayDirZ < 0 ? -1 : 1;

        float deltaDistX = rayDirX == 0 ? float.MaxValue : MathF.Abs(1f / rayDirX);
        float deltaDistY = rayDirY == 0 ? float.MaxValue : MathF.Abs(1f / rayDirY);
        float deltaDistZ = rayDirZ == 0 ? float.MaxValue : MathF.Abs(1f / rayDirZ);

        float sideDistX = rayDirX < 0
            ? (origin.X - mapX) * deltaDistX
            : (mapX + 1f - origin.X) * deltaDistX;

        float sideDistY = rayDirY < 0
            ? (origin.Y - mapY) * deltaDistY
            : (mapY + 1f - origin.Y) * deltaDistY;

        float sideDistZ = rayDirZ < 0
            ? (origin.Z - mapZ) * deltaDistZ
            : (mapZ + 1f - origin.Z) * deltaDistZ;

        Vector3 accumColor = Vector3.Zero;
        float accumAlpha = 0f;
        float hitDistance = MaxDistance;

        const int maxSteps = 512;

        for (int step = 0; step < maxSteps; step++)
        {
            BlockFace face;
            float traveled;

            if (sideDistX < sideDistY)
            {
                if (sideDistX < sideDistZ)
                {
                    mapX += stepX;
                    traveled = sideDistX;
                    sideDistX += deltaDistX;
                    face = stepX > 0 ? BlockFace.NegativeX : BlockFace.PositiveX;
                }
                else
                {
                    mapZ += stepZ;
                    traveled = sideDistZ;
                    sideDistZ += deltaDistZ;
                    face = stepZ > 0 ? BlockFace.NegativeZ : BlockFace.PositiveZ;
                }
            }
            else
            {
                if (sideDistY < sideDistZ)
                {
                    mapY += stepY;
                    traveled = sideDistY;
                    sideDistY += deltaDistY;
                    face = stepY > 0 ? BlockFace.NegativeY : BlockFace.PositiveY;
                }
                else
                {
                    mapZ += stepZ;
                    traveled = sideDistZ;
                    sideDistZ += deltaDistZ;
                    face = stepZ > 0 ? BlockFace.NegativeZ : BlockFace.PositiveZ;
                }
            }

            if (traveled >= MaxDistance)
            {
                break;
            }

            if (!world.InBounds(mapX, mapY, mapZ))
            {
                continue;
            }

            TBlock block = world.GetBlock(mapX, mapY, mapZ);
            if (_isEmpty(block))
            {
                continue;
            }

            Vector3 hitPoint = origin + direction * traveled;
            Vector3 local = hitPoint - new Vector3(mapX, mapY, mapZ);
            Vector2 uv = ComputeFaceUV(face, local);
            VoxelMaterialSample material = materials.Sample(block, face, uv.X, uv.Y);
            float light = GetFaceLight(face);
            Vector3 rgb = material.Color * light;
            float opacity = Math.Clamp(material.Opacity, 0f, 1f);

            accumColor += (1f - accumAlpha) * opacity * rgb;
            accumAlpha += (1f - accumAlpha) * opacity;
            hitDistance = traveled;

            if (accumAlpha >= 0.995f || _isSolid(block))
            {
                break;
            }
        }

        if (accumAlpha < 0.995f)
        {
            Vector3 sky = SampleSky(direction);
            accumColor += (1f - accumAlpha) * sky;
            accumAlpha = 1f;
            hitDistance = MaxDistance;
        }

        accumColor = Vector3.Clamp(accumColor, Vector3.Zero, Vector3.One);
        outDistance = hitDistance;
        return new Vector4(accumColor, 1f);
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

        float fogFactor = Math.Clamp((distance - _fogStart) / (_fogEnd - _fogStart), 0f, 1f);
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
