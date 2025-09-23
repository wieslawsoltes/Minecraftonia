using System;
using System.Collections.Generic;

namespace Minecraftonia.Game.MarkovJunior;

internal sealed class MarkovJuniorEngine
{
    private readonly List<MarkovLayer> _layers = new();
    private readonly Random _random;

    public MarkovJuniorEngine(int seed)
    {
        _random = new Random(seed);
    }

    public MarkovJuniorEngine AddLayer(MarkovLayer layer)
    {
        _layers.Add(layer ?? throw new ArgumentNullException(nameof(layer)));
        return this;
    }

    public bool Execute(MarkovJuniorState state)
    {
        bool changed = false;
        foreach (var layer in _layers)
        {
            if (layer.Execute(state, _random))
            {
                changed = true;
            }
        }

        return changed;
    }
}
