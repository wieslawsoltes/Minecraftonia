using System;
using System.Collections.Generic;

namespace Minecraftonia.Game;

internal enum TerrainTile
{
    Ocean,
    Coast,
    Plains,
    Forest,
    Hills,
    Mountain,
    Snow,
    Desert
}

internal sealed class WaveFunctionCollapseGenerator
{
    private readonly Random _random;
    private readonly TileDefinition[] _definitions;
    private readonly Dictionary<TerrainTile, int> _indexByTile;
    private readonly bool[,] _adjacency;

    private sealed record TileDefinition(TerrainTile Tile, float Weight, TerrainTile[] AllowedNeighbors);

    public WaveFunctionCollapseGenerator(int seed)
    {
        _random = new Random(seed);

        _definitions = new[]
        {
            new TileDefinition(
                TerrainTile.Ocean,
                2.4f,
                new[] { TerrainTile.Ocean, TerrainTile.Coast }),
            new TileDefinition(
                TerrainTile.Coast,
                1.8f,
                new[] { TerrainTile.Ocean, TerrainTile.Coast, TerrainTile.Plains, TerrainTile.Forest, TerrainTile.Desert }),
            new TileDefinition(
                TerrainTile.Plains,
                3.2f,
                new[] { TerrainTile.Coast, TerrainTile.Plains, TerrainTile.Forest, TerrainTile.Hills, TerrainTile.Desert }),
            new TileDefinition(
                TerrainTile.Forest,
                2.8f,
                new[] { TerrainTile.Plains, TerrainTile.Forest, TerrainTile.Hills, TerrainTile.Coast }),
            new TileDefinition(
                TerrainTile.Hills,
                2.1f,
                new[] { TerrainTile.Plains, TerrainTile.Forest, TerrainTile.Hills, TerrainTile.Mountain }),
            new TileDefinition(
                TerrainTile.Mountain,
                1.6f,
                new[] { TerrainTile.Hills, TerrainTile.Mountain, TerrainTile.Snow }),
            new TileDefinition(
                TerrainTile.Snow,
                1.0f,
                new[] { TerrainTile.Mountain, TerrainTile.Snow }),
            new TileDefinition(
                TerrainTile.Desert,
                1.2f,
                new[] { TerrainTile.Coast, TerrainTile.Plains, TerrainTile.Desert })
        };

        _indexByTile = new Dictionary<TerrainTile, int>(_definitions.Length);
        for (int i = 0; i < _definitions.Length; i++)
        {
            _indexByTile[_definitions[i].Tile] = i;
        }

        _adjacency = BuildAdjacencyMatrix(_definitions, _indexByTile);
    }

    public TerrainTile[,] Generate(int width, int depth)
    {
        if (width <= 0 || depth <= 0)
        {
            throw new ArgumentOutOfRangeException("World dimensions must be positive.");
        }

        int tileCount = _definitions.Length;
        var possibilities = new bool[width, depth, tileCount];
        var counts = new int[width, depth];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                for (int t = 0; t < tileCount; t++)
                {
                    possibilities[x, z, t] = true;
                }

                counts[x, z] = tileCount;
            }
        }

        var propagationQueue = new Queue<(int X, int Z)>();
        var candidateBuffer = new List<(int X, int Z)>();
        var tileBuffer = new List<int>();

        while (TryFindLowestEntropyCell(counts, candidateBuffer, out var collapseX, out var collapseZ))
        {
            tileBuffer.Clear();
            for (int t = 0; t < tileCount; t++)
            {
                if (possibilities[collapseX, collapseZ, t])
                {
                    tileBuffer.Add(t);
                }
            }

            if (tileBuffer.Count == 0)
            {
                throw new InvalidOperationException("Wave function collapse ran out of valid tiles.");
            }

            int chosen = SelectWeightedTile(tileBuffer);

            for (int t = 0; t < tileCount; t++)
            {
                if (t != chosen && possibilities[collapseX, collapseZ, t])
                {
                    possibilities[collapseX, collapseZ, t] = false;
                    counts[collapseX, collapseZ]--;
                }
            }

            propagationQueue.Enqueue((collapseX, collapseZ));
            PropagateConstraints(possibilities, counts, propagationQueue, tileCount);
        }

        var tiles = new TerrainTile[width, depth];
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                TerrainTile selected = TerrainTile.Plains;
                for (int t = 0; t < tileCount; t++)
                {
                    if (possibilities[x, z, t])
                    {
                        selected = _definitions[t].Tile;
                        break;
                    }
                }

                tiles[x, z] = selected;
            }
        }

        return tiles;
    }

    private static bool[,] BuildAdjacencyMatrix(TileDefinition[] definitions, Dictionary<TerrainTile, int> indexByTile)
    {
        int count = definitions.Length;
        var matrix = new bool[count, count];

        for (int i = 0; i < count; i++)
        {
            matrix[i, i] = true;
            foreach (var neighbor in definitions[i].AllowedNeighbors)
            {
                int j = indexByTile[neighbor];
                matrix[i, j] = true;
                matrix[j, i] = true;
            }
        }

        return matrix;
    }

    private bool TryFindLowestEntropyCell(int[,] counts, List<(int X, int Z)> candidates, out int bestX, out int bestZ)
    {
        candidates.Clear();
        int bestEntropy = int.MaxValue;
        bestX = -1;
        bestZ = -1;

        int width = counts.GetLength(0);
        int depth = counts.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                int count = counts[x, z];
                if (count <= 1)
                {
                    continue;
                }

                if (count < bestEntropy)
                {
                    bestEntropy = count;
                    candidates.Clear();
                    candidates.Add((x, z));
                }
                else if (count == bestEntropy)
                {
                    candidates.Add((x, z));
                }
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        var choice = candidates[_random.Next(candidates.Count)];
        bestX = choice.X;
        bestZ = choice.Z;
        return true;
    }

    private int SelectWeightedTile(List<int> options)
    {
        double totalWeight = 0;
        foreach (int option in options)
        {
            totalWeight += _definitions[option].Weight;
        }

        double roll = _random.NextDouble() * totalWeight;
        foreach (int option in options)
        {
            roll -= _definitions[option].Weight;
            if (roll <= 0)
            {
                return option;
            }
        }

        return options[^1];
    }

    private void PropagateConstraints(bool[,,] possibilities, int[,] counts, Queue<(int X, int Z)> queue, int tileCount)
    {
        Span<int> currentTiles = stackalloc int[tileCount];
        Span<(int X, int Z)> neighbors = stackalloc (int X, int Z)[4];

        while (queue.Count > 0)
        {
            var (cx, cz) = queue.Dequeue();

            int currentCount = 0;
            for (int t = 0; t < tileCount; t++)
            {
                if (possibilities[cx, cz, t])
                {
                    currentTiles[currentCount++] = t;
                }
            }

            if (currentCount == 0)
            {
                throw new InvalidOperationException("Wave function collapse encountered an empty cell.");
            }

            int neighborCount = CollectNeighbors(cx, cz, counts.GetLength(0), counts.GetLength(1), neighbors);
            for (int i = 0; i < neighborCount; i++)
            {
                var (nx, nz) = neighbors[i];
                bool changed = false;
                int remaining = counts[nx, nz];

                for (int t = 0; t < tileCount; t++)
                {
                    if (!possibilities[nx, nz, t])
                    {
                        continue;
                    }

                    bool allowed = false;
                    for (int ct = 0; ct < currentCount; ct++)
                    {
                        if (_adjacency[currentTiles[ct], t])
                        {
                            allowed = true;
                            break;
                        }
                    }

                    if (!allowed)
                    {
                        possibilities[nx, nz, t] = false;
                        remaining--;
                        changed = true;
                    }
                }

                if (changed)
                {
                    if (remaining <= 0)
                    {
                        throw new InvalidOperationException("Wave function collapse exhausted a neighbor cell.");
                    }

                    counts[nx, nz] = remaining;
                    queue.Enqueue((nx, nz));
                }
            }
        }
    }

    private static int CollectNeighbors(int x, int z, int width, int depth, Span<(int X, int Z)> buffer)
    {
        int count = 0;

        if (x > 0)
        {
            buffer[count++] = (x - 1, z);
        }

        if (x < width - 1)
        {
            buffer[count++] = (x + 1, z);
        }

        if (z > 0)
        {
            buffer[count++] = (x, z - 1);
        }

        if (z < depth - 1)
        {
            buffer[count++] = (x, z + 1);
        }

        return count;
    }
}
