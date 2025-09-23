using System;
using System.IO;
using System.Text;

namespace Minecraftonia.Game.MarkovJunior.Architecture;

internal static class ArchitectureDebugExporter
{
    public static void ExportCluster(
        MacroBlueprint blueprint,
        MarkovJuniorState state,
        SettlementCluster cluster,
        ArchitectureClusterContext context,
        int originX,
        int originZ,
        string? source = null)
    {
        var flag = Environment.GetEnvironmentVariable("MINECRAFTONIA_ARCH_DEBUG");
        if (!string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine("docs", "debug"));
        var path = Path.Combine("docs", "debug", "architecture.txt");
        using var writer = new StreamWriter(path, append: true, Encoding.UTF8);
        writer.WriteLine($"# Cluster {cluster.Id} ({cluster.ModuleType}) origin=({originX},{originZ}) tiles={cluster.Area}");
        if (!string.IsNullOrWhiteSpace(source))
        {
            writer.WriteLine($"# Source: {source}");
        }
        writer.WriteLine($"# Bounds grid=({cluster.MinX},{cluster.MinZ})-({cluster.MaxX},{cluster.MaxZ}) layout={state.SizeX}x{state.SizeZ}");

        writer.WriteLine("Cluster Mask:");
        for (int tileZ = 0; tileZ < context.TileCountZ; tileZ++)
        {
            var line = new StringBuilder();
            for (int tileX = 0; tileX < context.TileCountX; tileX++)
            {
                line.Append(context.IsTileOccupied(tileX, tileZ) ? '#' : '.');
            }
            writer.WriteLine(line.ToString());
        }

        writer.WriteLine("Layout Symbols:");
        for (int z = 0; z < state.SizeZ; z++)
        {
            var line = new StringBuilder();
            for (int x = 0; x < state.SizeX; x++)
            {
                var symbol = state.GetSymbol(x, 0, z);
                bool multiLevel = state.ContainsCellTag(x, 0, z, ArchitectureRuleSet.MultiLevelTag);
                bool canopy = state.ContainsCellTag(x, 0, z, "market_canopy");

                char c = symbol.Id switch
                {
                    "street" => '=',
                    "plaza" => 'P',
                    "garden" => 'g',
                    "floor" => multiLevel ? 'H' : 'F',
                    "doorway" => 'D',
                    "window" => 'W',
                    "pillar" => '+',
                    "stair" => 'S',
                    "stall" => canopy ? 'c' : 'm',
                    "roof" => 'R',
                    "altar" => 'A',
                    _ => '.'
                };
                line.Append(c);
            }
            writer.WriteLine(line.ToString());
        }

        writer.WriteLine();
    }
}
