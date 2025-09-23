using System;
using System.Collections.Generic;
using Minecraftonia.Game.MarkovJunior;

namespace Minecraftonia.Game;

internal sealed class WaveFunctionCollapseGenerator3D
{
    private readonly VoxelPatternLibrary _library;
    private readonly int _gridSizeX;
    private readonly int _gridSizeZ;
    private readonly Random _random;
    private readonly MacroBlueprint? _blueprint;

    private readonly bool[][,,] _possibilityGrid;
    private readonly int[,,] _possibilityCount;

    public WaveFunctionCollapseGenerator3D(
        VoxelPatternLibrary library,
        int gridSizeX,
        int gridSizeZ,
        Random random,
        MacroBlueprint? blueprint = null)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _gridSizeX = gridSizeX;
        _gridSizeZ = gridSizeZ;
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _blueprint = blueprint;

        int patternCount = _library.PatternCount;
        _possibilityGrid = new bool[patternCount][,,];
        for (int p = 0; p < patternCount; p++)
        {
            _possibilityGrid[p] = new bool[_gridSizeX, 1, _gridSizeZ];
        }

        _possibilityCount = new int[_gridSizeX, 1, _gridSizeZ];
        InitializePossibilities();
    }

    public BlockType[,,] Generate()
    {
        CollapseWaveFunction();
        return BakeBlocks();
    }

    private void InitializePossibilities()
    {
        int patternCount = _library.PatternCount;
        for (int x = 0; x < _gridSizeX; x++)
        {
            for (int z = 0; z < _gridSizeZ; z++)
            {
                for (int p = 0; p < patternCount; p++)
                {
                    _possibilityGrid[p][x, 0, z] = true;
                }

                _possibilityCount[x, 0, z] = patternCount;
            }
        }
    }

    private void CollapseWaveFunction()
    {
        var propagationQueue = new Queue<(int X, int Z)>();
        const int maxIterations = 4096;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            if (!TryFindLowestEntropyCell(out int cellX, out int cellZ))
            {
                return;
            }

            int pattern = SelectPattern(cellX, cellZ);
            Observe(cellX, cellZ, pattern);
            propagationQueue.Enqueue((cellX, cellZ));
            Propagate(propagationQueue);
        }

        throw new InvalidOperationException("Wave function collapse exceeded iteration budget.");
    }

    private bool TryFindLowestEntropyCell(out int bestX, out int bestZ)
    {
        double bestEntropy = double.MaxValue;
        bestX = -1;
        bestZ = -1;

        for (int x = 0; x < _gridSizeX; x++)
        {
            for (int z = 0; z < _gridSizeZ; z++)
            {
                int count = _possibilityCount[x, 0, z];
                if (count <= 1)
                {
                    continue;
                }

                double entropy = ComputeEntropy(x, z);
                double noise = _random.NextDouble() * 0.0001;
                entropy += noise;

                if (entropy < bestEntropy)
                {
                    bestEntropy = entropy;
                    bestX = x;
                    bestZ = z;
                }
            }
        }

        return bestX >= 0;
    }

    private double ComputeEntropy(int x, int z)
    {
        double sum = 0;
        double sumLog = 0;
        int patternCount = _library.PatternCount;

        for (int p = 0; p < patternCount; p++)
        {
            if (!_possibilityGrid[p][x, 0, z])
            {
                continue;
            }

            double weight = GetPatternWeight(x, z, p);
            sum += weight;
            sumLog += weight * Math.Log(weight + 1e-6);
        }

        return Math.Log(sum + 1e-6) - (sumLog / (sum + 1e-6));
    }

    private int SelectPattern(int x, int z)
    {
        double totalWeight = 0;
        int patternCount = _library.PatternCount;
        for (int p = 0; p < patternCount; p++)
        {
            if (_possibilityGrid[p][x, 0, z])
            {
                totalWeight += GetPatternWeight(x, z, p);
            }
        }

        if (totalWeight <= 0)
        {
            throw new InvalidOperationException("No patterns available for selection.");
        }

        double choice = _random.NextDouble() * totalWeight;
        for (int p = 0; p < patternCount; p++)
        {
            if (!_possibilityGrid[p][x, 0, z])
            {
                continue;
            }

            choice -= GetPatternWeight(x, z, p);
            if (choice <= 0)
            {
                return p;
            }
        }

        for (int p = 0; p < patternCount; p++)
        {
            if (_possibilityGrid[p][x, 0, z])
            {
                return p;
            }
        }

        throw new InvalidOperationException("Unable to select a pattern.");
    }

    private void Observe(int x, int z, int pattern)
    {
        int patternCount = _library.PatternCount;
        for (int p = 0; p < patternCount; p++)
        {
            if (p == pattern)
            {
                continue;
            }

            if (_possibilityGrid[p][x, 0, z])
            {
                _possibilityGrid[p][x, 0, z] = false;
                _possibilityCount[x, 0, z]--;
            }
        }
    }

    private void Propagate(Queue<(int X, int Z)> queue)
    {
        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            foreach (var offset in NeighborOffsets)
            {
                int nx = x + offset.dx;
                int nz = z + offset.dz;
                if (!InBounds(nx, nz))
                {
                    continue;
                }

                if (ReduceNeighbor(nx, nz, offset.direction, x, z))
                {
                    queue.Enqueue((nx, nz));
                }
            }
        }
    }

    private bool ReduceNeighbor(int nx, int nz, VoxelDirection directionFromNeighbor, int sourceX, int sourceZ)
    {
        var allowed = new bool[_library.PatternCount];
        var sourcePossibilities = GetPossiblePatterns(sourceX, sourceZ);
        foreach (int sourcePattern in sourcePossibilities)
        {
            foreach (var target in _library.GetCompatible(sourcePattern, directionFromNeighbor.Opposite()))
            {
                allowed[target] = true;
            }
        }

        bool changed = false;
        for (int p = 0; p < _library.PatternCount; p++)
        {
            if (!_possibilityGrid[p][nx, 0, nz])
            {
                continue;
            }

            if (!allowed[p])
            {
                _possibilityGrid[p][nx, 0, nz] = false;
                _possibilityCount[nx, 0, nz]--;
                if (_possibilityCount[nx, 0, nz] <= 0)
                {
                    throw new InvalidOperationException("Wave function collapse produced an impossible state.");
                }

                changed = true;
            }
        }

        return changed;
    }

    private IEnumerable<int> GetPossiblePatterns(int x, int z)
    {
        for (int p = 0; p < _library.PatternCount; p++)
        {
            if (_possibilityGrid[p][x, 0, z])
            {
                yield return p;
            }
        }
    }

    private bool InBounds(int x, int z)
    {
        return x >= 0 && x < _gridSizeX && z >= 0 && z < _gridSizeZ;
    }

    private BlockType[,,] BakeBlocks()
    {
        int sizeX = _library.TileSizeX;
        int sizeY = _library.TileSizeY;
        int sizeZ = _library.TileSizeZ;
        var blocks = new BlockType[_gridSizeX * sizeX, sizeY, _gridSizeZ * sizeZ];

        for (int x = 0; x < _gridSizeX; x++)
        {
            for (int z = 0; z < _gridSizeZ; z++)
            {
                int patternIndex = GetCollapsedPattern(x, z);
                var pattern = _library.Patterns[patternIndex];

                for (int px = 0; px < sizeX; px++)
                {
                    for (int py = 0; py < sizeY; py++)
                    {
                        for (int pz = 0; pz < sizeZ; pz++)
                        {
                            blocks[x * sizeX + px, py, z * sizeZ + pz] = pattern.Blocks[px, py, pz];
                        }
                    }
                }
            }
        }

        return blocks;
    }

    private int GetCollapsedPattern(int x, int z)
    {
        for (int p = 0; p < _library.PatternCount; p++)
        {
            if (_possibilityGrid[p][x, 0, z])
            {
                return p;
            }
        }

        throw new InvalidOperationException("Cell was not collapsed to a single pattern.");
    }

    private static readonly (int dx, int dz, VoxelDirection direction)[] NeighborOffsets =
    {
        (1, 0, VoxelDirection.PositiveX),
        (-1, 0, VoxelDirection.NegativeX),
        (0, 1, VoxelDirection.PositiveZ),
        (0, -1, VoxelDirection.NegativeZ)
    };

    private double GetPatternWeight(int x, int z, int patternIndex)
    {
        double baseWeight = _library.Patterns[patternIndex].Weight;
        if (_blueprint is null)
        {
            return baseWeight;
        }

        double multiplier = _blueprint.GetMultiplier(_library.Patterns[patternIndex], x, z);
        return Math.Max(0.0001, baseWeight * multiplier);
    }
}
