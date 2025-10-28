using System;

namespace Minecraftonia.Hosting;

/// <summary>
/// Represents timing information for a simulation tick.
/// </summary>
public readonly record struct GameTime(TimeSpan Total, TimeSpan Elapsed)
{
    public static GameTime FromElapsed(TimeSpan elapsed, TimeSpan total) => new(total, elapsed);
}
