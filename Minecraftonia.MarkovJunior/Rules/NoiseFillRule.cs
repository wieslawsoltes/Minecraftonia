using System;

namespace Minecraftonia.MarkovJunior.Rules;

public sealed class NoiseFillRule : MarkovRule
{
    private readonly MarkovSymbol _symbol;
    private readonly double _threshold;
    private readonly int _salt;

    public NoiseFillRule(string name, MarkovSymbol symbol, double threshold, int salt)
        : base(name)
    {
        _symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        _threshold = threshold;
        _salt = salt;
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
                    if (state.GetSymbol(x, y, z) != state.EmptySymbol)
                    {
                        continue;
                    }

                    double noise = HashToUnit(x, y, z);
                    if (noise < _threshold)
                    {
                        state.SetSymbol(x, y, z, _symbol);
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }

    private double HashToUnit(int x, int y, int z)
    {
        int h = _salt;
        h = unchecked(h * 73856093) ^ x;
        h = unchecked(h * 19349663) ^ y;
        h = unchecked(h * 83492791) ^ z;
        h ^= h >> 13;
        h ^= h << 7;
        h &= int.MaxValue;
        return h / (double)int.MaxValue;
    }
}
