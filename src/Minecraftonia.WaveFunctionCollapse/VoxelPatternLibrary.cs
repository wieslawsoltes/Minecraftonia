using System;
using System.Collections.Generic;
using System.Linq;

namespace Minecraftonia.WaveFunctionCollapse;

public enum VoxelDirection
{
    PositiveX = 0,
    NegativeX = 1,
    PositiveY = 2,
    NegativeY = 3,
    PositiveZ = 4,
    NegativeZ = 5
}

public static class VoxelDirectionExtensions
{
    public static VoxelDirection Opposite(this VoxelDirection direction) => direction switch
    {
        VoxelDirection.PositiveX => VoxelDirection.NegativeX,
        VoxelDirection.NegativeX => VoxelDirection.PositiveX,
        VoxelDirection.PositiveY => VoxelDirection.NegativeY,
        VoxelDirection.NegativeY => VoxelDirection.PositiveY,
        VoxelDirection.PositiveZ => VoxelDirection.NegativeZ,
        VoxelDirection.NegativeZ => VoxelDirection.PositiveZ,
        _ => direction
    };
}

public sealed class VoxelPattern3D
{
    private readonly List<string>[] _edgeTags = new List<string>[6];
    private readonly HashSet<string> _blueprintTags = new(StringComparer.OrdinalIgnoreCase);
    private double _averageSurfaceHeight;

    public VoxelPattern3D(string name, BlockType[,,] blocks, float weight = 1f)
    {
        Name = name;
        Blocks = blocks;
        Weight = Math.Max(0.01f, weight);

        for (int i = 0; i < _edgeTags.Length; i++)
        {
            _edgeTags[i] = new List<string>();
        }
    }

    public string Name { get; }
    public BlockType[,,] Blocks { get; }
    public float Weight { get; }
    public IReadOnlyCollection<string> BlueprintTags => _blueprintTags;
    public double AverageSurfaceHeight => _averageSurfaceHeight;
    public double AverageSurfaceHeightNormalized { get; private set; }

    public IReadOnlyCollection<string> GetEdgeTags(VoxelDirection direction) => _edgeTags[(int)direction];

    public VoxelPattern3D WithEdgeTag(VoxelDirection direction, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("Tag cannot be empty.", nameof(tag));
        }

        var list = _edgeTags[(int)direction];
        if (!list.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(tag.ToLowerInvariant());
        }

        return this;
    }

    public VoxelPattern3D WithEdgeTags(VoxelDirection direction, params string[] tags)
    {
        foreach (var tag in tags)
        {
            WithEdgeTag(direction, tag);
        }

        return this;
    }

    public VoxelPattern3D WithBlueprintTags(params string[] tags)
    {
        if (tags is null)
        {
            return this;
        }

        foreach (var tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                _blueprintTags.Add(tag.ToLowerInvariant());
            }
        }

        return this;
    }

    public void SetAverageSurfaceHeight(double value, double normalized)
    {
        _averageSurfaceHeight = value;
        AverageSurfaceHeightNormalized = normalized;
    }
}

public sealed class VoxelPatternLibrary
{
    private readonly int[][][] _compatibility; // [direction][patternIndex] -> compatible patterns

    public VoxelPatternLibrary(
        IReadOnlyList<VoxelPattern3D> patterns,
        int tileSizeX,
        int tileSizeY,
        int tileSizeZ)
    {
        Patterns = patterns ?? throw new ArgumentNullException(nameof(patterns));
        TileSizeX = tileSizeX;
        TileSizeY = tileSizeY;
        TileSizeZ = tileSizeZ;

        int count = Patterns.Count;
        _compatibility = new int[6][][];
        for (int d = 0; d < 6; d++)
        {
            _compatibility[d] = new int[count][];
        }

        BuildCompatibility();
    }

    public IReadOnlyList<VoxelPattern3D> Patterns { get; }
    public int TileSizeX { get; }
    public int TileSizeY { get; }
    public int TileSizeZ { get; }
    public int PatternCount => Patterns.Count;

    public IReadOnlyList<int> GetCompatible(int patternIndex, VoxelDirection direction)
    {
        return _compatibility[(int)direction][patternIndex];
    }

    private void BuildCompatibility()
    {
        int count = Patterns.Count;
        for (int i = 0; i < count; i++)
        {
            for (int d = 0; d < 6; d++)
            {
                var direction = (VoxelDirection)d;
                var compatible = new List<int>();
                for (int j = 0; j < count; j++)
                {
                    if (IsCompatible(Patterns[i], Patterns[j], direction))
                    {
                        compatible.Add(j);
                    }
                }

                _compatibility[d][i] = compatible.ToArray();
            }
        }
    }

    private static bool IsCompatible(VoxelPattern3D a, VoxelPattern3D b, VoxelDirection direction)
    {
        var tagsA = a.GetEdgeTags(direction);
        var tagsB = b.GetEdgeTags(direction.Opposite());

        if (tagsA.Count == 0 || tagsB.Count == 0)
        {
            return true;
        }

        foreach (var tag in tagsA)
        {
            if (tagsB.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public static class VoxelPatternLibraryFactory
{
    private const int TileSizeX = 8;
    private const int TileSizeZ = 8;

    public const string GrassTag = "grass";
    public const string ForestTag = "forest";
    public const string HillTag = "hill";
    public const string MountainTag = "mountain";
    public const string CliffTag = "cliff";
    public const string WaterTag = "water";
    public const string ShoreTag = "shore";
    public const string RiverNSTag = "river_ns";
    public const string RiverEWTag = "river_ew";
    public const string RiverBankTag = "river_bank";
    public const string RiverCornerTag = "river_corner";
    public const string WetlandTag = "wetland";
    public const string DesertTag = "desert";

    public const string BlueprintGrassland = "biome_grassland";
    public const string BlueprintForest = "biome_forest";
    public const string BlueprintWater = "biome_water";
    public const string BlueprintRiver = "biome_river";
    public const string BlueprintWetland = "biome_wetland";
    public const string BlueprintMountain = "biome_mountain";
    public const string BlueprintDesert = "biome_desert";
    public const string BlueprintHills = "biome_hills";
    public const string BlueprintShore = "biome_shore";
    public const string BlueprintSettlement = "biome_settlement";

    public static VoxelPatternLibrary CreateDefault(int worldHeight, int waterLevel)
    {
        var patterns = new List<VoxelPattern3D>();

        patterns.AddRange(CreateGrasslandSet(worldHeight, waterLevel));
        patterns.AddRange(CreateForestSet(worldHeight, waterLevel));
        patterns.AddRange(CreateWaterSet(worldHeight, waterLevel));
        patterns.AddRange(CreateRiverSet(worldHeight, waterLevel));
        patterns.AddRange(CreateWetlandSet(worldHeight, waterLevel));
        patterns.AddRange(CreateMountainSet(worldHeight, waterLevel));
        patterns.AddRange(CreateDesertSet(worldHeight, waterLevel));

        return new VoxelPatternLibrary(patterns, TileSizeX, worldHeight, TileSizeZ);
    }

    private static IEnumerable<VoxelPattern3D> CreateGrasslandSet(int height, int waterLevel)
    {
        var patterns = new List<VoxelPattern3D>();

        var plains = CreateHeightFieldPattern(
            "Plains",
            height,
            waterLevel,
            (x, z) => waterLevel + 2 + OffsetFromNoise(x, z, 0x11, amplitude: 1),
            (x, z) => BlockType.Grass,
            (x, z) => BlockType.Dirt,
            weight: 1.4f);
        AddHorizontalTags(plains, GrassTag);
        AddVerticalTags(plains);
        plains.WithBlueprintTags(BlueprintGrassland);
        FinalizePattern(plains);
        patterns.Add(plains);

        var meadow = CreateHeightFieldPattern(
            "Meadow",
            height,
            waterLevel,
            (x, z) => waterLevel + 2 + OffsetFromNoise(x, z, 0x24, amplitude: 2),
            (x, z) => BlockType.Grass,
            (x, z) => BlockType.Dirt,
            weight: 1.2f);
        ScatterDecoration(meadow.Blocks, waterLevel, BlockType.Leaves, probability: 0.22f, salt: 0x42);
        AddHorizontalTags(meadow, GrassTag, RiverBankTag);
        AddVerticalTags(meadow);
        meadow.WithBlueprintTags(BlueprintGrassland, BlueprintRiver);
        FinalizePattern(meadow);
        patterns.Add(meadow);

        var flowerField = CreateHeightFieldPattern(
            "FlowerField",
            height,
            waterLevel,
            (x, z) => waterLevel + 3 + OffsetFromNoise(x, z, 0x37, amplitude: 2),
            (x, z) => BlockType.Grass,
            (x, z) => BlockType.Dirt,
            weight: 1.0f);
        ScatterDecoration(flowerField.Blocks, waterLevel, BlockType.Leaves, probability: 0.35f, salt: 0x57, heightOffset: 1);
        AddHorizontalTags(flowerField, GrassTag, RiverBankTag);
        AddVerticalTags(flowerField);
        flowerField.WithBlueprintTags(BlueprintGrassland, BlueprintRiver);
        FinalizePattern(flowerField);
        patterns.Add(flowerField);

        var rolling = CreateHeightFieldPattern(
            "RollingPlains",
            height,
            waterLevel,
            (x, z) => waterLevel + 3 + (int)Math.Round(GetRadialBump(x, z, TileSizeX, TileSizeZ) * 5f),
            (x, z) => BlockType.Grass,
            (x, z) => BlockType.Dirt,
            weight: 1.1f);
        AddHorizontalTags(rolling, GrassTag, HillTag);
        AddVerticalTags(rolling);
        rolling.WithBlueprintTags(BlueprintGrassland, BlueprintHills);
        FinalizePattern(rolling);
        patterns.Add(rolling);

        return patterns;
    }

    private static IEnumerable<VoxelPattern3D> CreateForestSet(int height, int waterLevel)
    {
        var patterns = new List<VoxelPattern3D>();

        var forest = CreateHeightFieldPattern(
            "Forest",
            height,
            waterLevel,
            (x, z) => waterLevel + 3 + OffsetFromNoise(x, z, 0x73, amplitude: 2),
            (x, z) => BlockType.Grass,
            (x, z) => BlockType.Dirt,
            weight: 1.0f);
        TryGrowTree(forest.Blocks, TileSizeX / 2, TileSizeZ / 2, waterLevel, minElevationAboveWater: 2, salt: 0x90);
        AddHorizontalTags(forest, ForestTag, GrassTag);
        AddVerticalTags(forest);
        forest.WithBlueprintTags(BlueprintForest);
        FinalizePattern(forest);
        patterns.Add(forest);

        var denseForest = CreateHeightFieldPattern(
            "DenseForest",
            height,
            waterLevel,
            (x, z) => waterLevel + 3 + OffsetFromNoise(x, z, 0x91, amplitude: 2),
            (x, z) => BlockType.Grass,
            (x, z) => BlockType.Dirt,
            weight: 0.9f);
        TryGrowTree(denseForest.Blocks, TileSizeX / 2 - 1, TileSizeZ / 2, waterLevel, 2, 0xa1);
        TryGrowTree(denseForest.Blocks, TileSizeX / 2 + 2, TileSizeZ / 2 - 1, waterLevel, 2, 0xa2);
        ScatterDecoration(denseForest.Blocks, waterLevel, BlockType.Leaves, probability: 0.18f, salt: 0xa3);
        AddHorizontalTags(denseForest, ForestTag, GrassTag);
        AddVerticalTags(denseForest);
        denseForest.WithBlueprintTags(BlueprintForest);
        FinalizePattern(denseForest);
        patterns.Add(denseForest);

        var grove = CreateHeightFieldPattern(
            "Grove",
            height,
            waterLevel,
            (x, z) => waterLevel + 4 + OffsetFromNoise(x, z, 0xb1, amplitude: 1),
            (x, z) => BlockType.Grass,
            (x, z) => BlockType.Dirt,
            weight: 0.75f);
        TryGrowTree(grove.Blocks, TileSizeX / 2 - 2, TileSizeZ / 2 - 1, waterLevel, 2, 0xb2);
        TryGrowTree(grove.Blocks, TileSizeX / 2 + 2, TileSizeZ / 2 + 2, waterLevel, 2, 0xb3);
        AddHorizontalTags(grove, ForestTag, GrassTag, RiverBankTag);
        AddVerticalTags(grove);
        grove.WithBlueprintTags(BlueprintForest, BlueprintRiver);
        FinalizePattern(grove);
        patterns.Add(grove);

        return patterns;
    }

    private static IEnumerable<VoxelPattern3D> CreateWaterSet(int height, int waterLevel)
    {
        var patterns = new List<VoxelPattern3D>();

        var lake = CreateHeightFieldPattern(
            "Lake",
            height,
            waterLevel,
            (x, z) => waterLevel - 1,
            (x, z) => BlockType.Sand,
            (x, z) => BlockType.Sand,
            weight: 0.8f);
        CarveCircularWater(lake.Blocks, waterLevel, radius: 2.6, floorDepth: waterLevel - 3);
        AddHorizontalTags(lake, WaterTag);
        AddVerticalTags(lake, bottomTag: "lake_floor", topTag: WaterTag);
        lake.WithBlueprintTags(BlueprintWater);
        FinalizePattern(lake);
        patterns.Add(lake);

        var shoreNS = CreateShorePattern(height, waterLevel, northSouth: true);
        patterns.Add(shoreNS);
        var shoreEW = CreateShorePattern(height, waterLevel, northSouth: false);
        patterns.Add(shoreEW);

        var lagoon = CreateHeightFieldPattern(
            "Lagoon",
            height,
            waterLevel,
            (x, z) => waterLevel,
            (x, z) => BlockType.Water,
            (x, z) => BlockType.Sand,
            weight: 0.6f);
        CarveCircularWater(lagoon.Blocks, waterLevel, radius: 3.0, floorDepth: waterLevel - 4, addIsland: true);
        AddHorizontalTags(lagoon, WaterTag);
        AddVerticalTags(lagoon, bottomTag: "lagoon_floor", topTag: WaterTag);
        lagoon.WithBlueprintTags(BlueprintWater, BlueprintShore);
        FinalizePattern(lagoon);
        patterns.Add(lagoon);

        return patterns;
    }

    private static IEnumerable<VoxelPattern3D> CreateRiverSet(int height, int waterLevel)
    {
        var patterns = new List<VoxelPattern3D>();

        var riverNS = CreateHeightFieldPattern(
            "RiverStraightNS",
            height,
            waterLevel,
            (x, z) => waterLevel + OffsetFromNoise(x, z, 0xc1, 1),
            (x, z) => BlockType.Grass,
            (x, z) => BlockType.Dirt,
            weight: 0.85f);
        CarveRiverChannel(riverNS.Blocks, waterLevel, (x, z) => Math.Abs(x - TileSizeX / 2) <= 1);
        riverNS.WithEdgeTags(VoxelDirection.NegativeZ, RiverNSTag, WaterTag);
        riverNS.WithEdgeTags(VoxelDirection.PositiveZ, RiverNSTag, WaterTag);
        riverNS.WithEdgeTags(VoxelDirection.NegativeX, RiverBankTag, GrassTag);
        riverNS.WithEdgeTags(VoxelDirection.PositiveX, RiverBankTag, GrassTag);
        AddVerticalTags(riverNS);
        riverNS.WithBlueprintTags(BlueprintRiver, BlueprintWater);
        FinalizePattern(riverNS);
        patterns.Add(riverNS);

        var riverEW = CreateHeightFieldPattern(
            "RiverStraightEW",
            height,
            waterLevel,
            (x, z) => waterLevel + OffsetFromNoise(x, z, 0xc2, 1),
            (x, z) => BlockType.Grass,
            (x, z) => BlockType.Dirt,
            weight: 0.85f);
        CarveRiverChannel(riverEW.Blocks, waterLevel, (x, z) => Math.Abs(z - TileSizeZ / 2) <= 1);
        riverEW.WithEdgeTags(VoxelDirection.NegativeX, RiverEWTag, WaterTag);
        riverEW.WithEdgeTags(VoxelDirection.PositiveX, RiverEWTag, WaterTag);
        riverEW.WithEdgeTags(VoxelDirection.NegativeZ, RiverBankTag, GrassTag);
        riverEW.WithEdgeTags(VoxelDirection.PositiveZ, RiverBankTag, GrassTag);
        AddVerticalTags(riverEW);
        riverEW.WithBlueprintTags(BlueprintRiver, BlueprintWater);
        FinalizePattern(riverEW);
        patterns.Add(riverEW);

        patterns.Add(FinalizeAndReturn(CreateRiverBend("RiverBendNE", height, waterLevel, bendNorth: true, bendEast: true)));
        patterns.Add(FinalizeAndReturn(CreateRiverBend("RiverBendNW", height, waterLevel, bendNorth: true, bendEast: false)));
        patterns.Add(FinalizeAndReturn(CreateRiverBend("RiverBendSE", height, waterLevel, bendNorth: false, bendEast: true)));
        patterns.Add(FinalizeAndReturn(CreateRiverBend("RiverBendSW", height, waterLevel, bendNorth: false, bendEast: false)));

        patterns.Add(FinalizeAndReturn(CreateRiverMouth("RiverMouthNorth", height, waterLevel, VoxelDirection.NegativeZ)));
        patterns.Add(FinalizeAndReturn(CreateRiverMouth("RiverMouthSouth", height, waterLevel, VoxelDirection.PositiveZ)));
        patterns.Add(FinalizeAndReturn(CreateRiverMouth("RiverMouthWest", height, waterLevel, VoxelDirection.NegativeX)));
        patterns.Add(FinalizeAndReturn(CreateRiverMouth("RiverMouthEast", height, waterLevel, VoxelDirection.PositiveX)));

        var floodplain = CreateHeightFieldPattern(
            "Floodplain",
            height,
            waterLevel,
            (x, z) => waterLevel + 1 + OffsetFromNoise(x, z, 0xc7, 1),
            (x, z) => BlockType.Grass,
            (x, z) => BlockType.Dirt,
            weight: 0.9f);
        ScatterDecoration(floodplain.Blocks, waterLevel, BlockType.Leaves, probability: 0.15f, salt: 0xc9);
        AddHorizontalTags(floodplain, GrassTag, RiverBankTag);
        AddVerticalTags(floodplain);
        floodplain.WithBlueprintTags(BlueprintWetland, BlueprintRiver, BlueprintGrassland);
        FinalizePattern(floodplain);
        patterns.Add(floodplain);

        return patterns;
    }

    private static IEnumerable<VoxelPattern3D> CreateWetlandSet(int height, int waterLevel)
    {
        var patterns = new List<VoxelPattern3D>();

        var marsh = CreateHeightFieldPattern(
            "Marsh",
            height,
            waterLevel,
            (x, z) => waterLevel + OffsetFromNoise(x, z, 0xd1, 1),
            (x, z) => BlockType.Dirt,
            (x, z) => BlockType.Dirt,
            weight: 0.6f);
        CarveShallowPools(marsh.Blocks, waterLevel, probability: 0.45f, salt: 0xd2);
        ScatterDecoration(marsh.Blocks, waterLevel, BlockType.Wood, probability: 0.08f, salt: 0xd3, heightOffset: 1);
        AddHorizontalTags(marsh, WetlandTag, RiverBankTag, GrassTag);
        AddVerticalTags(marsh);
        marsh.WithBlueprintTags(BlueprintWetland, BlueprintRiver);
        FinalizePattern(marsh);
        patterns.Add(marsh);

        var fen = CreateHeightFieldPattern(
            "Fen",
            height,
            waterLevel,
            (x, z) => waterLevel + 1,
            (x, z) => BlockType.Grass,
            (x, z) => BlockType.Dirt,
            weight: 0.5f);
        CarveShallowPools(fen.Blocks, waterLevel, probability: 0.25f, salt: 0xd4);
        AddHorizontalTags(fen, WetlandTag, GrassTag, RiverBankTag);
        AddVerticalTags(fen);
        fen.WithBlueprintTags(BlueprintWetland, BlueprintGrassland);
        FinalizePattern(fen);
        patterns.Add(fen);

        return patterns;
    }

    private static IEnumerable<VoxelPattern3D> CreateMountainSet(int height, int waterLevel)
    {
        var patterns = new List<VoxelPattern3D>();

        var foothill = CreateHeightFieldPattern(
            "Foothill",
            height,
            waterLevel,
            (x, z) => waterLevel + 6 + OffsetFromNoise(x, z, 0xe1, 2) + (int)Math.Round(GetRadialBump(x, z, TileSizeX, TileSizeZ) * 3f),
            (x, z) => BlockType.Grass,
            (x, z) => BlockType.Dirt,
            weight: 1.0f);
        AddHorizontalTags(foothill, GrassTag, HillTag);
        AddVerticalTags(foothill);
        foothill.WithBlueprintTags(BlueprintHills, BlueprintGrassland);
        FinalizePattern(foothill);
        patterns.Add(foothill);

        var hill = CreateHeightFieldPattern(
            "HighHill",
            height,
            waterLevel,
            (x, z) => waterLevel + 8 + (int)Math.Round(GetRadialBump(x, z, TileSizeX, TileSizeZ) * 6f),
            (x, z) => BlockType.Stone,
            (x, z) => BlockType.Stone,
            weight: 0.8f);
        AddHorizontalTags(hill, HillTag, MountainTag);
        AddVerticalTags(hill);
        hill.WithBlueprintTags(BlueprintHills, BlueprintMountain);
        FinalizePattern(hill);
        patterns.Add(hill);

        patterns.Add(CreateCliffPattern("CliffNorth", height, waterLevel, VoxelDirection.NegativeZ));
        patterns.Add(CreateCliffPattern("CliffSouth", height, waterLevel, VoxelDirection.PositiveZ));
        patterns.Add(CreateCliffPattern("CliffWest", height, waterLevel, VoxelDirection.NegativeX));
        patterns.Add(CreateCliffPattern("CliffEast", height, waterLevel, VoxelDirection.PositiveX));

        var ridgeNS = CreateHeightFieldPattern(
            "RidgeNS",
            height,
            waterLevel,
            (x, z) => waterLevel + 10 + OffsetFromNoise(x, z, 0xe5, 2) + (Math.Abs(x - TileSizeX / 2) <= 1 ? 4 : 0),
            (x, z) => BlockType.Stone,
            (x, z) => BlockType.Stone,
            weight: 0.6f);
        ridgeNS.WithEdgeTags(VoxelDirection.NegativeZ, MountainTag);
        ridgeNS.WithEdgeTags(VoxelDirection.PositiveZ, MountainTag);
        ridgeNS.WithEdgeTags(VoxelDirection.NegativeX, CliffTag, HillTag);
        ridgeNS.WithEdgeTags(VoxelDirection.PositiveX, CliffTag, HillTag);
        AddVerticalTags(ridgeNS);
        ridgeNS.WithBlueprintTags(BlueprintMountain);
        FinalizePattern(ridgeNS);
        patterns.Add(ridgeNS);

        var ridgeEW = CreateHeightFieldPattern(
            "RidgeEW",
            height,
            waterLevel,
            (x, z) => waterLevel + 10 + OffsetFromNoise(x, z, 0xe6, 2) + (Math.Abs(z - TileSizeZ / 2) <= 1 ? 4 : 0),
            (x, z) => BlockType.Stone,
            (x, z) => BlockType.Stone,
            weight: 0.6f);
        ridgeEW.WithEdgeTags(VoxelDirection.NegativeX, MountainTag);
        ridgeEW.WithEdgeTags(VoxelDirection.PositiveX, MountainTag);
        ridgeEW.WithEdgeTags(VoxelDirection.NegativeZ, CliffTag, HillTag);
        ridgeEW.WithEdgeTags(VoxelDirection.PositiveZ, CliffTag, HillTag);
        AddVerticalTags(ridgeEW);
        ridgeEW.WithBlueprintTags(BlueprintMountain);
        FinalizePattern(ridgeEW);
        patterns.Add(ridgeEW);

        var peak = CreateHeightFieldPattern(
            "MountainPeak",
            height,
            waterLevel,
            (x, z) => waterLevel + 14 + (int)Math.Round(GetRadialBump(x, z, TileSizeX, TileSizeZ) * 8f),
            (x, z) => BlockType.Stone,
            (x, z) => BlockType.Stone,
            weight: 0.4f);
        AddHorizontalTags(peak, MountainTag, CliffTag);
        AddVerticalTags(peak);
        peak.WithBlueprintTags(BlueprintMountain);
        FinalizePattern(peak);
        patterns.Add(peak);

        var plateau = CreateHeightFieldPattern(
            "MountainPlateau",
            height,
            waterLevel,
            (x, z) => waterLevel + 12 + OffsetFromNoise(x, z, 0xe7, 1),
            (x, z) => BlockType.Stone,
            (x, z) => BlockType.Stone,
            weight: 0.5f);
        AddHorizontalTags(plateau, MountainTag);
        AddVerticalTags(plateau);
        plateau.WithBlueprintTags(BlueprintMountain);
        FinalizePattern(plateau);
        patterns.Add(plateau);

        return patterns;
    }

    private static IEnumerable<VoxelPattern3D> CreateDesertSet(int height, int waterLevel)
    {
        var patterns = new List<VoxelPattern3D>();

        var desert = CreateHeightFieldPattern(
            "Desert",
            height,
            waterLevel,
            (x, z) => waterLevel + 2 + OffsetFromNoise(x, z, 0xf1, 2),
            (x, z) => BlockType.Sand,
            (x, z) => BlockType.Sand,
            weight: 0.9f);
        AddHorizontalTags(desert, DesertTag);
        AddVerticalTags(desert);
        desert.WithBlueprintTags(BlueprintDesert);
        FinalizePattern(desert);
        patterns.Add(desert);

        var dunes = CreateHeightFieldPattern(
            "Dunes",
            height,
            waterLevel,
            (x, z) => waterLevel + 3 + (int)Math.Round(Math.Sin((x + z) * 0.7) * 2) + OffsetFromNoise(x, z, 0xf2, 1),
            (x, z) => BlockType.Sand,
            (x, z) => BlockType.Sand,
            weight: 0.6f);
        AddHorizontalTags(dunes, DesertTag);
        AddVerticalTags(dunes);
        dunes.WithBlueprintTags(BlueprintDesert);
        FinalizePattern(dunes);
        patterns.Add(dunes);

        patterns.Add(CreatePrairieEdge("PrairieEdgeEW", height, waterLevel, eastWest: true));
        patterns.Add(CreatePrairieEdge("PrairieEdgeNS", height, waterLevel, eastWest: false));

        var oasis = CreateHeightFieldPattern(
            "Oasis",
            height,
            waterLevel,
            (x, z) => waterLevel + 2 + OffsetFromNoise(x, z, 0xf6, 1),
            (x, z) => BlockType.Sand,
            (x, z) => BlockType.Sand,
            weight: 0.3f);
        CarveCircularWater(oasis.Blocks, waterLevel, radius: 2.0, floorDepth: waterLevel - 3);
        TryGrowTree(oasis.Blocks, TileSizeX / 2 - 1, TileSizeZ / 2, waterLevel, 1, 0xf7);
        TryGrowTree(oasis.Blocks, TileSizeX / 2 + 2, TileSizeZ / 2 + 1, waterLevel, 1, 0xf8);
        AddHorizontalTags(oasis, DesertTag, RiverBankTag, GrassTag);
        AddVerticalTags(oasis);
        oasis.WithBlueprintTags(BlueprintDesert, BlueprintWater, BlueprintRiver);
        FinalizePattern(oasis);
        patterns.Add(oasis);

        return patterns;
    }

    private static VoxelPattern3D CreateRiverBend(string name, int height, int waterLevel, bool bendNorth, bool bendEast)
    {
        var pattern = CreateHeightFieldPattern(
            name,
            height,
            waterLevel,
            (x, z) => waterLevel + OffsetFromNoise(x, z, 0xc5, 1),
            (x, z) => BlockType.Grass,
            (x, z) => BlockType.Dirt,
            weight: 0.7f);

        int halfX = TileSizeX / 2;
        int halfZ = TileSizeZ / 2;
        CarveRiverChannel(pattern.Blocks, waterLevel, (x, z) =>
        {
            bool inNorth = bendNorth && z <= halfZ;
            bool inSouth = !bendNorth && z >= halfZ - 1;
            bool inEast = bendEast && x >= halfX;
            bool inWest = !bendEast && x <= halfX;

            return (bendNorth ? inNorth : inSouth) && Math.Abs(x - halfX) <= 1
                   || (bendEast ? inEast : inWest) && Math.Abs(z - halfZ) <= 1;
        });

        if (bendNorth)
        {
            pattern.WithEdgeTags(VoxelDirection.NegativeZ, RiverNSTag, WaterTag, RiverCornerTag);
            pattern.WithEdgeTags(VoxelDirection.PositiveZ, RiverBankTag, GrassTag);
        }
        else
        {
            pattern.WithEdgeTags(VoxelDirection.PositiveZ, RiverNSTag, WaterTag, RiverCornerTag);
            pattern.WithEdgeTags(VoxelDirection.NegativeZ, RiverBankTag, GrassTag);
        }

        if (bendEast)
        {
            pattern.WithEdgeTags(VoxelDirection.PositiveX, RiverEWTag, WaterTag, RiverCornerTag);
            pattern.WithEdgeTags(VoxelDirection.NegativeX, RiverBankTag, GrassTag);
        }
        else
        {
            pattern.WithEdgeTags(VoxelDirection.NegativeX, RiverEWTag, WaterTag, RiverCornerTag);
            pattern.WithEdgeTags(VoxelDirection.PositiveX, RiverBankTag, GrassTag);
        }

        AddVerticalTags(pattern);
        pattern.WithBlueprintTags(BlueprintRiver, BlueprintWater);
        return pattern;
    }

    private static VoxelPattern3D CreateRiverMouth(string name, int height, int waterLevel, VoxelDirection riverDirection)
    {
        var pattern = CreateHeightFieldPattern(
            name,
            height,
            waterLevel,
            (x, z) => waterLevel,
            (x, z) => BlockType.Sand,
            (x, z) => BlockType.Sand,
            weight: 0.55f);
        CarveCircularWater(pattern.Blocks, waterLevel, radius: 3.2, floorDepth: waterLevel - 4);

        pattern.WithEdgeTags(riverDirection, (riverDirection == VoxelDirection.NegativeX || riverDirection == VoxelDirection.PositiveX) ? RiverEWTag : RiverNSTag, WaterTag);
        foreach (var dir in Enum.GetValues<VoxelDirection>())
        {
            if (dir == riverDirection)
            {
                continue;
            }

            if (dir == VoxelDirection.PositiveY || dir == VoxelDirection.NegativeY)
            {
                continue;
            }

            pattern.WithEdgeTags(dir, WaterTag, ShoreTag);
        }

        AddVerticalTags(pattern, bottomTag: "river_delta", topTag: WaterTag);
        pattern.WithBlueprintTags(BlueprintRiver, BlueprintWater, BlueprintShore);
        return pattern;
    }

    private static VoxelPattern3D CreateShorePattern(int height, int waterLevel, bool northSouth)
    {
        var pattern = CreateHeightFieldPattern(
            northSouth ? "ShoreNS" : "ShoreEW",
            height,
            waterLevel,
            (x, z) => waterLevel + (northSouth ? (z > TileSizeZ / 2 ? 2 : -1) : (x > TileSizeX / 2 ? 2 : -1)),
            (x, z) => BlockType.Sand,
            (x, z) => BlockType.Sand,
            weight: 0.7f);

        if (northSouth)
        {
            pattern.WithEdgeTags(VoxelDirection.NegativeZ, WaterTag, ShoreTag);
            pattern.WithEdgeTags(VoxelDirection.PositiveZ, GrassTag, ShoreTag);
            pattern.WithEdgeTags(VoxelDirection.PositiveX, GrassTag, ShoreTag, RiverBankTag);
            pattern.WithEdgeTags(VoxelDirection.NegativeX, GrassTag, ShoreTag, RiverBankTag);
        }
        else
        {
            pattern.WithEdgeTags(VoxelDirection.NegativeX, WaterTag, ShoreTag);
            pattern.WithEdgeTags(VoxelDirection.PositiveX, GrassTag, ShoreTag);
            pattern.WithEdgeTags(VoxelDirection.PositiveZ, GrassTag, ShoreTag, RiverBankTag);
            pattern.WithEdgeTags(VoxelDirection.NegativeZ, GrassTag, ShoreTag, RiverBankTag);
        }

        AddVerticalTags(pattern, bottomTag: "shore", topTag: ShoreTag);
        pattern.WithBlueprintTags(BlueprintShore, BlueprintWater, BlueprintGrassland);
        FinalizePattern(pattern);
        return pattern;
    }

    private static VoxelPattern3D CreateCliffPattern(string name, int height, int waterLevel, VoxelDirection highSide)
    {
        var pattern = CreateHeightFieldPattern(
            name,
            height,
            waterLevel,
            (x, z) => GetCliffSurface(x, z, highSide, waterLevel, baseHeight: 7, cliffDrop: 5),
            (x, z) => (IsHighSide(highSide, x, z) ? BlockType.Stone : BlockType.Grass),
            (x, z) => (IsHighSide(highSide, x, z) ? BlockType.Stone : BlockType.Dirt),
            weight: 0.55f);

        switch (highSide)
        {
            case VoxelDirection.NegativeZ:
                pattern.WithEdgeTags(VoxelDirection.NegativeZ, MountainTag, CliffTag);
                pattern.WithEdgeTags(VoxelDirection.PositiveZ, HillTag, GrassTag, CliffTag);
                pattern.WithEdgeTags(VoxelDirection.PositiveX, HillTag, GrassTag);
                pattern.WithEdgeTags(VoxelDirection.NegativeX, HillTag, GrassTag);
                break;
            case VoxelDirection.PositiveZ:
                pattern.WithEdgeTags(VoxelDirection.PositiveZ, MountainTag, CliffTag);
                pattern.WithEdgeTags(VoxelDirection.NegativeZ, HillTag, GrassTag, CliffTag);
                pattern.WithEdgeTags(VoxelDirection.PositiveX, HillTag, GrassTag);
                pattern.WithEdgeTags(VoxelDirection.NegativeX, HillTag, GrassTag);
                break;
            case VoxelDirection.NegativeX:
                pattern.WithEdgeTags(VoxelDirection.NegativeX, MountainTag, CliffTag);
                pattern.WithEdgeTags(VoxelDirection.PositiveX, HillTag, GrassTag, CliffTag);
                pattern.WithEdgeTags(VoxelDirection.PositiveZ, HillTag, GrassTag);
                pattern.WithEdgeTags(VoxelDirection.NegativeZ, HillTag, GrassTag);
                break;
            case VoxelDirection.PositiveX:
                pattern.WithEdgeTags(VoxelDirection.PositiveX, MountainTag, CliffTag);
                pattern.WithEdgeTags(VoxelDirection.NegativeX, HillTag, GrassTag, CliffTag);
                pattern.WithEdgeTags(VoxelDirection.PositiveZ, HillTag, GrassTag);
                pattern.WithEdgeTags(VoxelDirection.NegativeZ, HillTag, GrassTag);
                break;
        }

        AddVerticalTags(pattern);
        pattern.WithBlueprintTags(BlueprintMountain, BlueprintHills);
        FinalizePattern(pattern);
        return pattern;
    }

    private static VoxelPattern3D CreatePrairieEdge(string name, int height, int waterLevel, bool eastWest)
    {
        var pattern = CreateHeightFieldPattern(
            name,
            height,
            waterLevel,
            (x, z) => waterLevel + 2,
            (x, z) => eastWest ? (x < TileSizeX / 2 ? BlockType.Grass : BlockType.Sand) : (z < TileSizeZ / 2 ? BlockType.Grass : BlockType.Sand),
            (x, z) => eastWest ? (x < TileSizeX / 2 ? BlockType.Dirt : BlockType.Sand) : (z < TileSizeZ / 2 ? BlockType.Dirt : BlockType.Sand),
            weight: 0.5f);

        if (eastWest)
        {
            pattern.WithEdgeTags(VoxelDirection.NegativeX, GrassTag, RiverBankTag);
            pattern.WithEdgeTags(VoxelDirection.PositiveX, DesertTag);
            pattern.WithEdgeTags(VoxelDirection.PositiveZ, GrassTag, DesertTag);
            pattern.WithEdgeTags(VoxelDirection.NegativeZ, GrassTag, DesertTag);
        }
        else
        {
            pattern.WithEdgeTags(VoxelDirection.NegativeZ, GrassTag, RiverBankTag);
            pattern.WithEdgeTags(VoxelDirection.PositiveZ, DesertTag);
            pattern.WithEdgeTags(VoxelDirection.PositiveX, GrassTag, DesertTag);
            pattern.WithEdgeTags(VoxelDirection.NegativeX, GrassTag, DesertTag);
        }

        AddVerticalTags(pattern);
        pattern.WithBlueprintTags(BlueprintGrassland, BlueprintDesert, BlueprintShore);
        FinalizePattern(pattern);
        return pattern;
    }

    private static BlockType[,,] CreateHeightFieldBlocks(int height)
    {
        var blocks = new BlockType[TileSizeX, height, TileSizeZ];
        for (int x = 0; x < TileSizeX; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < TileSizeZ; z++)
                {
                    blocks[x, y, z] = BlockType.Air;
                }
            }
        }

        return blocks;
    }

    private static VoxelPattern3D CreateHeightFieldPattern(
        string name,
        int height,
        int waterLevel,
        Func<int, int, int> surfaceSelector,
        Func<int, int, BlockType> topSelector,
        Func<int, int, BlockType> fillerSelector,
        float weight)
    {
        var blocks = CreateHeightFieldBlocks(height);

        for (int x = 0; x < TileSizeX; x++)
        {
            for (int z = 0; z < TileSizeZ; z++)
            {
                int surface = Math.Clamp(surfaceSelector(x, z), 2, height - 3);
                var top = topSelector(x, z);
                var filler = fillerSelector(x, z);
                SetColumn(blocks, x, z, surface, waterLevel, top, filler);
            }
        }

        var pattern = new VoxelPattern3D(name, blocks, weight);
        FinalizePattern(pattern, blocks);
        return pattern;
    }

    private static void SetColumn(BlockType[,,] blocks, int x, int z, int surface, int waterLevel, BlockType topBlock, BlockType fillerBlock)
    {
        int maxY = blocks.GetLength(1) - 1;
        surface = Math.Clamp(surface, 1, maxY - 1);

        for (int y = 0; y <= maxY; y++)
        {
            if (y <= surface)
            {
                if (y == surface)
                {
                    blocks[x, y, z] = topBlock;
                }
                else if (surface - y <= 3)
                {
                    blocks[x, y, z] = fillerBlock;
                }
                else
                {
                    blocks[x, y, z] = BlockType.Stone;
                }
            }
            else
            {
                blocks[x, y, z] = BlockType.Air;
            }
        }

        if (topBlock == BlockType.Water)
        {
            return;
        }

        if (surface < waterLevel)
        {
            int limit = Math.Min(waterLevel, maxY);
            for (int y = surface + 1; y <= limit; y++)
            {
                if (blocks[x, y, z] == BlockType.Air)
                {
                    blocks[x, y, z] = BlockType.Water;
                }
            }
        }
    }

    private static void CarveRiverChannel(BlockType[,,] blocks, int waterLevel, Func<int, int, bool> predicate)
    {
        int height = blocks.GetLength(1);
        for (int x = 0; x < TileSizeX; x++)
        {
            for (int z = 0; z < TileSizeZ; z++)
            {
                if (!predicate(x, z))
                {
                    continue;
                }

                FloodColumn(blocks, x, z, Math.Max(waterLevel - 2, 1), waterLevel);
            }
        }
    }

    private static void CarveCircularWater(BlockType[,,] blocks, int waterLevel, double radius, int floorDepth, bool addIsland = false)
    {
        double centerX = (TileSizeX - 1) / 2.0;
        double centerZ = (TileSizeZ - 1) / 2.0;

        for (int x = 0; x < TileSizeX; x++)
        {
            for (int z = 0; z < TileSizeZ; z++)
            {
                double dist = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(z - centerZ, 2));
                if (dist <= radius)
                {
                    FloodColumn(blocks, x, z, Math.Max(floorDepth, 1), waterLevel);
                }
            }
        }

        if (addIsland)
        {
            FloodColumn(blocks, (int)centerX, (int)centerZ, Math.Max(waterLevel - 3, 1), waterLevel - 2);
            SetColumn(blocks, (int)centerX, (int)centerZ, waterLevel - 2, waterLevel, BlockType.Grass, BlockType.Dirt);
        }
    }

    private static void CarveShallowPools(BlockType[,,] blocks, int waterLevel, float probability, int salt)
    {
        int floor = Math.Max(waterLevel - 1, 1);
        for (int x = 0; x < TileSizeX; x++)
        {
            for (int z = 0; z < TileSizeZ; z++)
            {
                if (Noise(x, z, salt) < probability)
                {
                    FloodColumn(blocks, x, z, Math.Max(floor - 1, 1), waterLevel - 1);
                    blocks[x, waterLevel - 1, z] = BlockType.Water;
                }
            }
        }
    }

    private static void FloodColumn(BlockType[,,] blocks, int x, int z, int waterFloor, int waterLevel)
    {
        int maxY = blocks.GetLength(1) - 1;
        waterFloor = Math.Clamp(waterFloor, 0, maxY - 1);
        waterLevel = Math.Clamp(waterLevel, waterFloor, maxY - 1);

        for (int y = 0; y <= maxY; y++)
        {
            if (y < waterFloor - 1)
            {
                blocks[x, y, z] = BlockType.Stone;
            }
            else if (y < waterFloor)
            {
                blocks[x, y, z] = BlockType.Sand;
            }
            else if (y <= waterLevel)
            {
                blocks[x, y, z] = BlockType.Water;
            }
            else
            {
                blocks[x, y, z] = BlockType.Air;
            }
        }
    }

    private static void ScatterDecoration(BlockType[,,] blocks, int waterLevel, BlockType decoration, float probability, int salt, int heightOffset = 1)
    {
        int sizeY = blocks.GetLength(1);
        for (int x = 1; x < TileSizeX - 1; x++)
        {
            for (int z = 1; z < TileSizeZ - 1; z++)
            {
                if (Noise(x, z, salt) >= probability)
                {
                    continue;
                }

                int surface = FindSurface(blocks, x, z);
                if (surface <= waterLevel || surface < 0)
                {
                    continue;
                }

                if (surface + heightOffset >= sizeY - 1)
                {
                    continue;
                }

                var baseBlock = blocks[x, surface, z];
                if (baseBlock != BlockType.Grass && baseBlock != BlockType.Dirt && baseBlock != BlockType.Sand)
                {
                    continue;
                }

                blocks[x, surface + heightOffset, z] = decoration;
            }
        }
    }

    private static void TryGrowTree(BlockType[,,] blocks, int x, int z, int waterLevel, int minElevationAboveWater, int salt)
    {
        int surface = FindSurface(blocks, x, z);
        if (surface < 0 || surface <= waterLevel + minElevationAboveWater)
        {
            return;
        }

        if (blocks[x, surface, z] != BlockType.Grass && blocks[x, surface, z] != BlockType.Dirt)
        {
            return;
        }

        int maxY = blocks.GetLength(1) - 1;
        int treeHeight = 4 + (int)(Noise(x, z, salt) * 3.5f);
        if (surface + treeHeight + 1 >= maxY)
        {
            return;
        }

        for (int i = 1; i <= treeHeight; i++)
        {
            blocks[x, surface + i, z] = BlockType.Wood;
        }

        int canopyY = surface + treeHeight;
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dz = -2; dz <= 2; dz++)
            {
                int px = x + dx;
                int pz = z + dz;
                if (px < 0 || px >= TileSizeX || pz < 0 || pz >= TileSizeZ)
                {
                    continue;
                }

                double dist = Math.Sqrt(dx * dx + dz * dz);
                if (dist <= 2.2)
                {
                    blocks[px, canopyY, pz] = BlockType.Leaves;
                    if (dist <= 1.2)
                    {
                        blocks[px, canopyY + 1, pz] = BlockType.Leaves;
                    }
                }
            }
        }
    }

    private static int FindSurface(BlockType[,,] blocks, int x, int z)
    {
        for (int y = blocks.GetLength(1) - 1; y >= 0; y--)
        {
            var block = blocks[x, y, z];
            if (block != BlockType.Air && block != BlockType.Water)
            {
                return y;
            }
        }

        return -1;
    }

    private static double GetRadialBump(int x, int z, int sizeX, int sizeZ)
    {
        double cx = (sizeX - 1) / 2.0;
        double cz = (sizeZ - 1) / 2.0;
        double dx = x - cx;
        double dz = z - cz;
        double dist = Math.Sqrt(dx * dx + dz * dz);
        double maxDist = Math.Max(1.0, Math.Sqrt(cx * cx + cz * cz));
        double normalized = 1.0 - dist / maxDist;
        return Math.Max(0.0, normalized);
    }

    private static void FinalizePattern(VoxelPattern3D pattern, BlockType[,,] blocks)
    {
        double average = ComputeAverageSurfaceHeight(blocks);
        double normalized = blocks.GetLength(1) > 1 ? average / (blocks.GetLength(1) - 1) : 0.0;
        pattern.SetAverageSurfaceHeight(average, normalized);
    }

    private static double ComputeAverageSurfaceHeight(BlockType[,,] blocks)
    {
        int sizeX = blocks.GetLength(0);
        int sizeZ = blocks.GetLength(2);
        int maxY = blocks.GetLength(1) - 1;
        double sum = 0.0;
        int count = 0;

        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                int surface = 0;
                for (int y = maxY; y >= 0; y--)
                {
                    var block = blocks[x, y, z];
                    if (block != BlockType.Air)
                    {
                        surface = y;
                        break;
                    }
                }

                sum += surface;
                count++;
            }
        }

        return count > 0 ? sum / count : 0.0;
    }

    private static void FinalizePattern(VoxelPattern3D pattern)
    {
        FinalizePattern(pattern, pattern.Blocks);
    }

    private static VoxelPattern3D FinalizeAndReturn(VoxelPattern3D pattern)
    {
        FinalizePattern(pattern, pattern.Blocks);
        return pattern;
    }

    private static int OffsetFromNoise(int x, int z, int salt, int amplitude)
    {
        double noise = Noise(x, z, salt) * 2.0 - 1.0;
        return (int)Math.Round(noise * amplitude);
    }

    private static bool IsHighSide(VoxelDirection highSide, int x, int z)
    {
        return highSide switch
        {
            VoxelDirection.NegativeZ => z < TileSizeZ / 2,
            VoxelDirection.PositiveZ => z > TileSizeZ / 2,
            VoxelDirection.NegativeX => x < TileSizeX / 2,
            VoxelDirection.PositiveX => x > TileSizeX / 2,
            _ => false
        };
    }

    private static int GetCliffSurface(int x, int z, VoxelDirection highSide, int waterLevel, int baseHeight, int cliffDrop)
    {
        int gradient = highSide switch
        {
            VoxelDirection.NegativeZ => (int)Math.Round((1.0 - z / (double)(TileSizeZ - 1)) * cliffDrop),
            VoxelDirection.PositiveZ => (int)Math.Round((z / (double)(TileSizeZ - 1)) * cliffDrop),
            VoxelDirection.NegativeX => (int)Math.Round((1.0 - x / (double)(TileSizeX - 1)) * cliffDrop),
            VoxelDirection.PositiveX => (int)Math.Round((x / (double)(TileSizeX - 1)) * cliffDrop),
            _ => 0
        };

        return waterLevel + baseHeight + gradient;
    }

    private static double Noise(int x, int z, int salt)
    {
        int h = salt;
        h = unchecked(h * 73856093) ^ x;
        h = unchecked(h * 19349663) ^ z;
        h ^= h >> 13;
        h ^= h << 7;
        h &= int.MaxValue;
        return h / (double)int.MaxValue;
    }

    private static void AddHorizontalTags(VoxelPattern3D pattern, params string[] tags)
    {
        pattern.WithEdgeTags(VoxelDirection.PositiveX, tags);
        pattern.WithEdgeTags(VoxelDirection.NegativeX, tags);
        pattern.WithEdgeTags(VoxelDirection.PositiveZ, tags);
        pattern.WithEdgeTags(VoxelDirection.NegativeZ, tags);
    }

    private static void AddVerticalTags(VoxelPattern3D pattern, string bottomTag = "ground", string topTag = "sky")
    {
        pattern.WithEdgeTags(VoxelDirection.NegativeY, bottomTag);
        pattern.WithEdgeTags(VoxelDirection.PositiveY, topTag);
    }
}
