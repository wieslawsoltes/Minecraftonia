using System;
using System.Numerics;
using Avalonia;
using Avalonia.Media;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.VoxelRendering;

public static class VoxelOverlayRenderer
{
    public static void DrawCrosshair(DrawingContext context, Size viewport, Pen? pen = null, double length = 9d)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        pen ??= new Pen(Brushes.White, 1);
        var center = new Point(viewport.Width / 2d, viewport.Height / 2d);
        context.DrawLine(pen, center + new Avalonia.Vector(-length, 0), center + new Avalonia.Vector(length, 0));
        context.DrawLine(pen, center + new Avalonia.Vector(0, -length), center + new Avalonia.Vector(0, length));
    }

    public static void DrawSelection<TBlock>(DrawingContext context, VoxelProjector projector, VoxelRaycastHit<TBlock> hit)
    {
        Span<Vector3> corners = stackalloc Vector3[4];
        GetFaceCorners(hit.Block, hit.Face, corners);

        var projectedPoints = new Point[4];
        for (int i = 0; i < corners.Length; i++)
        {
            if (!projector.TryProject(corners[i], out var screenPoint))
            {
                return;
            }

            projectedPoints[i] = screenPoint;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(projectedPoints[0], true);
            ctx.LineTo(projectedPoints[1]);
            ctx.LineTo(projectedPoints[2]);
            ctx.LineTo(projectedPoints[3]);
            ctx.EndFigure(true);
        }

        double thickness = Math.Max(1.5, projector.ViewportSize.Width * 0.002);
        var fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), thickness);
        context.DrawGeometry(fill, pen, geometry);
    }

    private static void GetFaceCorners(Int3 block, BlockFace face, Span<Vector3> destination)
    {
        Vector3 min = block.ToVector3();
        Vector3 max = min + Vector3.One;

        switch (face)
        {
            case BlockFace.PositiveX:
                destination[0] = new Vector3(max.X, min.Y, min.Z);
                destination[1] = new Vector3(max.X, max.Y, min.Z);
                destination[2] = new Vector3(max.X, max.Y, max.Z);
                destination[3] = new Vector3(max.X, min.Y, max.Z);
                break;
            case BlockFace.NegativeX:
                destination[0] = new Vector3(min.X, min.Y, min.Z);
                destination[1] = new Vector3(min.X, min.Y, max.Z);
                destination[2] = new Vector3(min.X, max.Y, max.Z);
                destination[3] = new Vector3(min.X, max.Y, min.Z);
                break;
            case BlockFace.PositiveY:
                destination[0] = new Vector3(min.X, max.Y, min.Z);
                destination[1] = new Vector3(max.X, max.Y, min.Z);
                destination[2] = new Vector3(max.X, max.Y, max.Z);
                destination[3] = new Vector3(min.X, max.Y, max.Z);
                break;
            case BlockFace.NegativeY:
                destination[0] = new Vector3(min.X, min.Y, min.Z);
                destination[1] = new Vector3(min.X, min.Y, max.Z);
                destination[2] = new Vector3(max.X, min.Y, max.Z);
                destination[3] = new Vector3(max.X, min.Y, min.Z);
                break;
            case BlockFace.PositiveZ:
                destination[0] = new Vector3(min.X, min.Y, max.Z);
                destination[1] = new Vector3(min.X, max.Y, max.Z);
                destination[2] = new Vector3(max.X, max.Y, max.Z);
                destination[3] = new Vector3(max.X, min.Y, max.Z);
                break;
            case BlockFace.NegativeZ:
                destination[0] = new Vector3(min.X, min.Y, min.Z);
                destination[1] = new Vector3(max.X, min.Y, min.Z);
                destination[2] = new Vector3(max.X, max.Y, min.Z);
                destination[3] = new Vector3(min.X, max.Y, min.Z);
                break;
            default:
                destination[0] = min;
                destination[1] = min;
                destination[2] = min;
                destination[3] = min;
                break;
        }
    }
}
