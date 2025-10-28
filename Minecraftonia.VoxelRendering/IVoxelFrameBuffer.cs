using System;

namespace Minecraftonia.VoxelRendering;

public interface IVoxelFrameBuffer : IDisposable
{
    VoxelSize Size { get; }
    int Stride { get; }
    int Length { get; }
    bool IsDisposed { get; }
    Span<byte> Span { get; }
    ReadOnlySpan<byte> ReadOnlySpan { get; }
    byte[] Pixels { get; }
    void Resize(VoxelSize size);
}
