using System;
using System.Numerics;

namespace Minecraftonia.Rendering.Pipelines;

public static class RenderSamplePattern
{
    public static Vector2[] CreateStratified(int samples)
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

        while (index < samples)
        {
            offsets[index] = new Vector2(0.5f, 0.5f);
            index++;
        }

        return offsets;
    }
}
