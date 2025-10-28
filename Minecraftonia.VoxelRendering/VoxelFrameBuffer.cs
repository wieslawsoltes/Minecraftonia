using System;

namespace Minecraftonia.VoxelRendering;

public sealed class VoxelFrameBuffer : IVoxelFrameBuffer
{
    private byte[] _pixels;
    private bool _disposed;

    public VoxelFrameBuffer(VoxelSize size)
    {
        EnsureValid(size);
        Size = size;
        Stride = size.Width * 4;
        _pixels = new byte[Stride * size.Height];
    }

    public VoxelSize Size { get; private set; }

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

    public void Resize(VoxelSize size)
    {
        ThrowIfDisposed();

        EnsureValid(size);
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

    private static void EnsureValid(VoxelSize size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Dimensions must be positive.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VoxelFrameBuffer));
        }
    }
}
