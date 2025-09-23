using System;
using System.Collections.Generic;

namespace Minecraftonia.Game.MarkovJunior;

/// <summary>
/// Groups rules into passes that execute in sequence, similar to MarkovJunior schedules.
/// </summary>
internal sealed class MarkovLayer
{
    private readonly List<MarkovRule> _rules = new();

    public MarkovLayer(string name, int maxIterations = 50)
    {
        if (maxIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxIterations));
        }

        Name = name;
        MaxIterations = maxIterations;
    }

    public string Name { get; }
    public int MaxIterations { get; }

    public MarkovLayer AddRule(MarkovRule rule)
    {
        _rules.Add(rule ?? throw new ArgumentNullException(nameof(rule)));
        return this;
    }

    public bool Execute(MarkovJuniorState state, Random random)
    {
        bool anyChange = false;
        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            bool iterationChange = false;
            foreach (var rule in _rules)
            {
                if (rule.Apply(state, random))
                {
                    iterationChange = true;
                    anyChange = true;
                }
            }

            if (!iterationChange)
            {
                break;
            }
        }

        return anyChange;
    }
}
