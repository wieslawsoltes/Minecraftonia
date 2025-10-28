using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Minecraftonia.Rendering.Pipelines;

public static class FxaaSharpenFilter
{
    private static readonly Vector4 LumaWeights = new(0.299f, 0.587f, 0.114f, 0f);

    public static unsafe void Apply(
        byte* buffer,
        int stride,
        int width,
        int height,
        float contrastThreshold,
        float relativeThreshold,
        bool applySharpen,
        float sharpenAmount,
        ParallelOptions parallelOptions)
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

            Parallel.For(0, height, parallelOptions, y =>
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
                    float threshold = Math.Max(contrastThreshold, lumaMax * relativeThreshold);

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

                    if (applySharpen && sharpenAmount > 0f)
                    {
                        Vector4 neighborAverage = (LoadColorVector(srcRow + (x - 1) * 4) +
                                                   LoadColorVector(srcRow + (x + 1) * 4) +
                                                   LoadColorVector(srcPrev + x * 4) +
                                                   LoadColorVector(srcNext + x * 4)) * 0.25f;

                        aaColor += (aaColor - neighborAverage) * sharpenAmount;
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
}
