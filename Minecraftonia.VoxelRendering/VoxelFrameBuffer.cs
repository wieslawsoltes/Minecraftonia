using System;
using Avalonia;

namespace Minecraftonia.VoxelRendering;

public sealed class VoxelFrameBuffer : IDisposable
{
    private byte[] _pixels;
    private bool _disposed;

    public VoxelFrameBuffer(PixelSize size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        Size = size;
        Stride = size.Width * 4;
        _pixels = new byte[Stride * size.Height];
    }

    public PixelSize Size { get; private set; }

    public int Stride { get; private set; }

    public int Length => Stride * Size.Height;

    public bool IsDisposed => _disposed;

    public Span<byte> Span
    {
        get
        {
            ThrowIfDisposed();
            return _pixels.AsSpan(0, Length);
        }
    }

    public ReadOnlySpan<byte> ReadOnlySpan
    {
        get
        {
            ThrowIfDisposed();
            return _pixels.AsSpan(0, Length);
        }
    }

    public byte[] Pixels
    {
        get
        {
            ThrowIfDisposed();
            return _pixels;
        }
    }

    public void Resize(PixelSize size)
    {
        ThrowIfDisposed();

        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        Size = size;
        Stride = size.Width * 4;
        int required = Stride * size.Height;
        if (_pixels.Length < required)
        {
            _pixels = new byte[required];
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _pixels = Array.Empty<byte>();
        Size = default;
        Stride = 0;
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VoxelFrameBuffer));
        }
    }
}
