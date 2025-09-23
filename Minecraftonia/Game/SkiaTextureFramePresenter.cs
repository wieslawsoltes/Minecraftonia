using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Minecraftonia.VoxelRendering;
using SkiaSharp;

namespace Minecraftonia.Game;

internal sealed class SkiaTextureFramePresenter : IVoxelFramePresenter
{
    private GRContext? _grContext;
    private SKSurface? _surface;
    private PixelSize _size;
    private readonly SKPaint _paint = new() { FilterQuality = SKFilterQuality.None, IsAntialias = false };

    public void Render(DrawingContext context, VoxelFrameBuffer framebuffer, Rect destination)
    {
        if (framebuffer is null || framebuffer.IsDisposed)
        {
            return;
        }

        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        context.Custom(new SkiaDrawOperation(destination, framebuffer, this));
    }

    public void Dispose()
    {
        ReleaseSurface();
        _paint.Dispose();
    }

    private void RenderToCanvas(SKCanvas canvas, GRContext? grContext, VoxelFrameBuffer frame, Rect destination)
    {
        var info = new SKImageInfo(frame.Size.Width, frame.Size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var target = ToSkRect(destination);

        unsafe
        {
            fixed (byte* src = frame.Pixels)
            {
                if (grContext is not null && EnsureSurface(grContext, frame.Size, info))
                {
                    var surface = _surface!;
                    using var bitmap = new SKBitmap();
                    if (!bitmap.InstallPixels(info, (IntPtr)src, frame.Stride))
                    {
                        ReleaseSurface();
                    }
                    else
                    {
                        surface.Canvas.Clear(SKColors.Transparent);
                        surface.Canvas.DrawBitmap(bitmap, SKRect.Create(info.Width, info.Height));
                        surface.Canvas.Flush();

                        using var image = surface.Snapshot();
                        canvas.DrawImage(image, target, _paint);
                        return;
                    }
                }

                ReleaseSurface();
                using var fallback = SKImage.FromPixels(info, (IntPtr)src, frame.Stride);
                canvas.DrawImage(fallback, target, _paint);
            }
        }
    }

    private bool EnsureSurface(GRContext context, PixelSize size, SKImageInfo info)
    {
        if (_surface != null)
        {
            bool contextChanged = !ReferenceEquals(_grContext, context);
            bool sizeChanged = _size != size;
            if (contextChanged || sizeChanged)
            {
                _surface.Dispose();
                _surface = null;
                _grContext = null;
            }
        }

        if (_surface == null)
        {
            _surface = SKSurface.Create(context, true, info);
            if (_surface == null)
            {
                return false;
            }

            _grContext = context;
            _size = size;
        }

        return true;
    }

    private void ReleaseSurface()
    {
        _surface?.Dispose();
        _surface = null;
        _grContext = null;
        _size = default;
    }

    private static SKRect ToSkRect(Rect rect)
    {
        return new SKRect(
            (float)rect.X,
            (float)rect.Y,
            (float)(rect.X + rect.Width),
            (float)(rect.Y + rect.Height));
    }

    private sealed class SkiaDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _destination;
        private readonly VoxelFrameBuffer _frame;
        private readonly SkiaTextureFramePresenter _presenter;

        public SkiaDrawOperation(Rect destination, VoxelFrameBuffer frame, SkiaTextureFramePresenter presenter)
        {
            _destination = destination;
            _frame = frame;
            _presenter = presenter;
        }

        public Rect Bounds => _destination;

        public void Dispose()
        {
        }

        public bool HitTest(Point p) => _destination.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            if (_frame.IsDisposed)
            {
                return;
            }

            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } leaseFeature)
            {
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            if (canvas is null)
            {
                return;
            }

            _presenter.RenderToCanvas(canvas, lease.GrContext, _frame, _destination);
        }

        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);
    }
}
