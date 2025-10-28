using System;

namespace Minecraftonia.MarkovJunior.Rules;

/// <summary>
/// Ensures that cells annotated with a tag are surrounded by specified neighbour tags (soft constraint).
/// </summary>
public sealed class AdjacencyConstraintRule : MarkovRule
{
    private readonly string _subjectTag;
    private readonly string[] _requiredNeighborTags;
    private readonly int _maxAttemptsPerCell;

    public AdjacencyConstraintRule(string name, string subjectTag, string[] requiredNeighborTags, int maxAttemptsPerCell = 3)
        : base(name)
    {
        _subjectTag = subjectTag ?? throw new ArgumentNullException(nameof(subjectTag));
        _requiredNeighborTags = requiredNeighborTags ?? Array.Empty<string>();
        _maxAttemptsPerCell = Math.Max(1, maxAttemptsPerCell);
    }

    public override bool Apply(MarkovJuniorState state, Random random)
    {
        bool changed = false;

        for (int x = 0; x < state.SizeX; x++)
        {
            for (int y = 0; y < state.SizeY; y++)
            {
                for (int z = 0; z < state.SizeZ; z++)
                {
                    if (!state.ContainsCellTag(x, y, z, _subjectTag))
                    {
                        continue;
                    }

                    if (SatisfiesNeighbors(state, x, y, z))
                    {
                        continue;
                    }

                    for (int attempt = 0; attempt < _maxAttemptsPerCell; attempt++)
                    {
                        int nx = x + random.Next(-1, 2);
                        int nz = z + random.Next(-1, 2);
                        int ny = y;
                        if (!state.InBounds(nx, ny, nz))
                        {
                            continue;
                        }

                        if (state.GetSymbol(nx, ny, nz) == state.EmptySymbol)
                        {
                            state.SetSymbol(nx, ny, nz, state.GetSymbol(x, y, z));
                            changed = true;
                            break;
                        }
                    }
                }
            }
        }

        return changed;
    }

    private bool SatisfiesNeighbors(MarkovJuniorState state, int x, int y, int z)
    {
        foreach (var tag in _requiredNeighborTags)
        {
            bool found = false;
            for (int dx = -1; dx <= 1 && !found; dx++)
            {
                for (int dz = -1; dz <= 1 && !found; dz++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }

                    int nx = x + dx;
                    int nz = z + dz;
                    if (!state.InBounds(nx, y, nz))
                    {
                        continue;
                    }

                    var symbol = state.GetSymbol(nx, y, nz);
                    if (symbol.HasTag(tag) || state.ContainsCellTag(nx, y, nz, tag))
                    {
                        found = true;
                    }
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }
}
