using System.Collections.Generic;

namespace Minecraftonia.Game.MarkovJunior.Architecture;

internal static class ArchitectureSymbolSet
{
    public static MarkovSymbol Floor { get; } = new MarkovSymbol("floor", paletteIndex: 1).WithTags("structure", "walkable");
    public static MarkovSymbol Wall { get; } = new MarkovSymbol("wall", paletteIndex: 2).WithTags("structure", "wall");
    public static MarkovSymbol Pillar { get; } = new MarkovSymbol("pillar", paletteIndex: 3).WithTags("structure", "support");
    public static MarkovSymbol Doorway { get; } = new MarkovSymbol("doorway", paletteIndex: 4).WithTags("structure", "opening");
    public static MarkovSymbol Window { get; } = new MarkovSymbol("window", paletteIndex: 5).WithTags("structure", "opening", "window");
    public static MarkovSymbol Street { get; } = new MarkovSymbol("street", paletteIndex: 6).WithTags("street", "walkable");
    public static MarkovSymbol Plaza { get; } = new MarkovSymbol("plaza", paletteIndex: 7).WithTags("street", "plaza");
    public static MarkovSymbol Garden { get; } = new MarkovSymbol("garden", paletteIndex: 8).WithTags("vegetation", "decor");
    public static MarkovSymbol Stair { get; } = new MarkovSymbol("stair", paletteIndex: 9).WithTags("structure", "stairs", "walkable");
    public static MarkovSymbol Altar { get; } = new MarkovSymbol("altar", paletteIndex: 10).WithTags("structure", "sacred");
    public static MarkovSymbol Roof { get; } = new MarkovSymbol("roof", paletteIndex: 11).WithTags("structure", "roof");
    public static MarkovSymbol Stall { get; } = new MarkovSymbol("stall", paletteIndex: 12).WithTags("market", "structure");
    public static MarkovSymbol Empty { get; } = new MarkovSymbol("empty", paletteIndex: 0);

    public static IReadOnlyList<MarkovSymbol> All { get; } = new[]
    {
        Floor,
        Wall,
        Pillar,
        Doorway,
        Window,
        Street,
        Plaza,
        Garden,
        Stair,
        Altar,
        Roof,
        Stall,
        Empty
    };
}
