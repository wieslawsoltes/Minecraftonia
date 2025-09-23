using System;

namespace Minecraftonia.Game.MarkovJunior.Rules;

/// <summary>
/// Stamps a rectangular prism of symbols when a predicate matches the anchor cell.
/// </summary>
internal sealed class PatternStampRule : MarkovRule
{
    private readonly MarkovSymbol[,,] _pattern;
    private readonly Func<MarkovJuniorState, int, int, int, bool> _predicate;
    private readonly int _offsetY;

    public PatternStampRule(
        string name,
        MarkovSymbol[,,] pattern,
        Func<MarkovJuniorState, int, int, int, bool> predicate,
        int offsetY = 0)
        : base(name)
    {
        _pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _offsetY = offsetY;
    }

    public override bool Apply(MarkovJuniorState state, Random random)
    {
        bool changed = false;
        int sizeX = _pattern.GetLength(0);
        int sizeY = _pattern.GetLength(1);
        int sizeZ = _pattern.GetLength(2);

        for (int x = 0; x < state.SizeX - sizeX + 1; x++)
        {
            for (int z = 0; z < state.SizeZ - sizeZ + 1; z++)
            {
                int anchorY = Math.Clamp(_offsetY, 0, state.SizeY - sizeY);
                if (!_predicate(state, x, anchorY, z))
                {
                    continue;
                }

                for (int px = 0; px < sizeX; px++)
                {
                    for (int py = 0; py < sizeY; py++)
                    {
                        for (int pz = 0; pz < sizeZ; pz++)
                        {
                            var symbol = _pattern[px, py, pz];
                            if (symbol == null)
                            {
                                continue;
                            }

                            int worldX = x + px;
                            int worldY = anchorY + py;
                            int worldZ = z + pz;

                            if (!state.InBounds(worldX, worldY, worldZ))
                            {
                                continue;
                            }

                            if (symbol == state.EmptySymbol)
                            {
                                continue;
                            }

                            if (state.GetSymbol(worldX, worldY, worldZ) == state.EmptySymbol)
                            {
                                state.SetSymbol(worldX, worldY, worldZ, symbol);
                                changed = true;
                            }
                        }
                    }
                }
            }
        }

        return changed;
    }
}
