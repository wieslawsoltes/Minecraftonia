using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Minecraftonia.VoxelEngine;

public sealed class VoxelChunk<TBlock>
{
    private readonly TBlock[] _blocks;
    private int _solidCount;
    private static readonly EqualityComparer<TBlock> Comparer = EqualityComparer<TBlock>.Default;

    public VoxelChunk(ChunkCoordinate coordinate, ChunkDimensions dimensions)
    {
        Coordinate = coordinate;
        Dimensions = dimensions;
        _blocks = new TBlock[dimensions.Volume];
        _solidCount = 0;
    }

    public ChunkCoordinate Coordinate { get; }
    public ChunkDimensions Dimensions { get; }

    public bool IsPopulated { get; private set; }
    public bool IsDirty { get; private set; }
    public bool IsEmpty => _solidCount == 0;

    public Span<TBlock> DataSpan => _blocks.AsSpan();
    public ReadOnlySpan<TBlock> DataReadOnlySpan => _blocks.AsSpan();
    internal TBlock[] RawBlocks => _blocks;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Index(int x, int y, int z)
    {
        return (y * Dimensions.SizeZ + z) * Dimensions.SizeX + x;
    }

    public TBlock GetBlock(int x, int y, int z)
    {
        return _blocks[Index(x, y, z)];
    }

    public void SetBlock(int x, int y, int z, TBlock value, bool markDirty)
    {
        int index = Index(x, y, z);
        var previous = _blocks[index];
        _blocks[index] = value;
        UpdateSolidCount(previous, value);
        if (markDirty)
        {
            IsDirty = true;
        }
    }

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public void ClearDirty()
    {
        IsDirty = false;
    }

    public void MarkPopulated()
    {
        IsPopulated = true;
    }

    public void CopyTo(Span<TBlock> destination)
    {
        if (destination.Length != _blocks.Length)
        {
            throw new ArgumentException($"Destination length {destination.Length} does not match chunk volume {_blocks.Length}.", nameof(destination));
        }

        _blocks.AsSpan().CopyTo(destination);
    }

    internal void RecalculateOccupancy()
    {
        _solidCount = 0;
        foreach (var block in _blocks)
        {
            if (!IsEmptyValue(block))
            {
                _solidCount++;
            }
        }
    }

    private static bool IsEmptyValue(TBlock value)
    {
        return Comparer.Equals(value, default!);
    }

    private void UpdateSolidCount(TBlock previous, TBlock current)
    {
        bool wasSolid = !IsEmptyValue(previous);
        bool isSolid = !IsEmptyValue(current);
        if (wasSolid == isSolid)
        {
            return;
        }

        if (isSolid)
        {
            _solidCount++;
        }
        else
        {
            _solidCount--;
        }
    }
}
