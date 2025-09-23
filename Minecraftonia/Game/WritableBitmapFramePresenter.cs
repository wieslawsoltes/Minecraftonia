using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Minecraftonia.VoxelRendering;

namespace Minecraftonia.Game;

internal sealed class WritableBitmapFramePresenter : IVoxelFramePresenter
{
    private WriteableBitmap? _bitmap;
    private PixelSize _size;

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

        EnsureBitmap(framebuffer.Size);
        if (_bitmap is null)
        {
            return;
        }

        using (var lockResult = _bitmap.Lock())
        {
            Marshal.Copy(framebuffer.Pixels, 0, lockResult.Address, framebuffer.Length);
        }

        var sourceRect = new Rect(0, 0, _bitmap.PixelSize.Width, _bitmap.PixelSize.Height);
        context.DrawImage(_bitmap, sourceRect, destination);
    }

    public void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        _size = default;
    }

    private void EnsureBitmap(PixelSize size)
    {
        if (_bitmap is { } existing && existing.PixelSize == size)
        {
            return;
        }

        _bitmap?.Dispose();
        _bitmap = new WriteableBitmap(size, new Avalonia.Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        _size = size;
    }
}
