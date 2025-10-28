using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Minecraftonia.Rendering.Core;

namespace Minecraftonia.Rendering.Avalonia.Presenters;

public sealed class WritableBitmapFramePresenter : IVoxelFramePresenter
{
    private WriteableBitmap? _bitmap;
    private VoxelSize _size;

    public void Render(DrawingContext context, IVoxelFrameBuffer framebuffer, Rect destination)
    {
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

    private void EnsureBitmap(VoxelSize size)
    {
        if (_bitmap is { } existing && existing.PixelSize.Width == size.Width && existing.PixelSize.Height == size.Height)
        {
            return;
        }

        _bitmap?.Dispose();
        var pixelSize = new PixelSize(size.Width, size.Height);
        _bitmap = new WriteableBitmap(pixelSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        _size = size;
    }
}
