using System;
using System.Numerics;
using Avalonia;
using Minecraftonia.VoxelRendering;

namespace Minecraftonia.Rendering.Avalonia.Controls;

public readonly struct VoxelProjector
{
    public VoxelProjector(VoxelCamera camera, Vector3 eyePosition, Size viewportSize)
    {
        Camera = camera;
        EyePosition = eyePosition;
        ViewportSize = viewportSize;
    }

    public VoxelCamera Camera { get; }
    public Vector3 EyePosition { get; }
    public Size ViewportSize { get; }

    public bool TryProject(Vector3 worldPoint, out Point projected)
    {
        projected = default;

        if (ViewportSize.Width <= 0 || ViewportSize.Height <= 0)
        {
            return false;
        }

        if (Camera.TanHalfFov <= float.Epsilon || Camera.Aspect <= float.Epsilon)
        {
            return false;
        }

        Vector3 toPoint = worldPoint - EyePosition;

        float x = Vector3.Dot(toPoint, Camera.Right);
        float y = Vector3.Dot(toPoint, Camera.Up);
        float z = Vector3.Dot(toPoint, Camera.Forward);

        if (z <= 0.05f)
        {
            return false;
        }

        float ndcX = x / (z * Camera.TanHalfFov * Camera.Aspect);
        float ndcY = y / (z * Camera.TanHalfFov);

        double screenX = (ndcX + 1d) * 0.5d * ViewportSize.Width;
        double screenY = (1d - ndcY) * 0.5d * ViewportSize.Height;

        if (!double.IsFinite(screenX) || !double.IsFinite(screenY))
        {
            return false;
        }

        projected = new Point(screenX, screenY);
        return true;
    }
}
