using System;

namespace Minecraftonia.MarkovJunior;

/// <summary>
/// Base type for MarkovJunior-inspired rules. Each rule may mutate the grid and returns true when a change occurs.
/// </summary>
public abstract class MarkovRule
{
    protected MarkovRule(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public abstract bool Apply(MarkovJuniorState state, Random random);
}
