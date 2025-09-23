using System;
using System.Collections.Generic;

namespace Minecraftonia.Game.MarkovJunior;

/// <summary>
/// Maintains MarkovJunior symbols, metadata, and utility helpers for rule execution.
/// </summary>
internal sealed class MarkovJuniorState
{
    private readonly MarkovSymbol[,,] _grid;
    private readonly HashSet<string>[,,] _cellTags;

    public MarkovJuniorState(int sizeX, int sizeY, int sizeZ, MarkovSymbol emptySymbol)
    {
        if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeX), "Grid dimensions must be positive.");
        }

        EmptySymbol = emptySymbol ?? throw new ArgumentNullException(nameof(emptySymbol));
        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;
        _grid = new MarkovSymbol[sizeX, sizeY, sizeZ];
        _cellTags = new HashSet<string>[sizeX, sizeY, sizeZ];

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    _grid[x, y, z] = EmptySymbol;
                    _cellTags[x, y, z] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }
    }

    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }
    public MarkovSymbol EmptySymbol { get; }

    public MarkovSymbol GetSymbol(int x, int y, int z) => _grid[x, y, z];

    public void SetSymbol(int x, int y, int z, MarkovSymbol symbol)
    {
        _grid[x, y, z] = symbol ?? EmptySymbol;
    }

    public IReadOnlyCollection<string> GetCellTags(int x, int y, int z) => _cellTags[x, y, z];

    public void AddCellTag(int x, int y, int z, string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            _cellTags[x, y, z].Add(tag.ToLowerInvariant());
        }
    }

    public bool ContainsCellTag(int x, int y, int z, string tag) => _cellTags[x, y, z].Contains(tag);

    public bool InBounds(int x, int y, int z)
    {
        return x >= 0 && x < SizeX
            && y >= 0 && y < SizeY
            && z >= 0 && z < SizeZ;
    }
}
