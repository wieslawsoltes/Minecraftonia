using System;
using System.Numerics;
using Minecraftonia.Game.MarkovJunior.Architecture;
using Minecraftonia.OpenStreetMap;
using Minecraftonia.WaveFunctionCollapse;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.Game;

public sealed class MinecraftoniaVoxelWorld : VoxelWorld<BlockType>
{
    private readonly MinecraftoniaWorldConfig _config;
    private readonly Random _random;
    private readonly float _heightScale1 = 0.12f;
    private readonly float _heightScale2 = 0.04f;
    private readonly float _hillStrength = 6f;
    private readonly float _mountainStrength = 16f;
    private readonly float _ridgeStrength = 4f;

    private BlockType[,,]? _legacyBlocks;
    private int[,]? _heightMap;
    private Vector3? _preferredSpawn;

    public MinecraftoniaVoxelWorld(MinecraftoniaWorldConfig config, bool initializeTerrain = true)
        : base(
            new ChunkDimensions(config.ChunkSizeX, config.ChunkSizeY, config.ChunkSizeZ),
            config.ChunkCountX,
            config.ChunkCountY,
            config.ChunkCountZ)
    {
        if (config.WaterLevel < 1 || config.WaterLevel >= config.Height - 2)
        {
            throw new ArgumentOutOfRangeException(nameof(config), "Water level must sit within world bounds.");
        }

        _config = config;
        _random = new Random(Seed);

        if (initializeTerrain)
        {
            switch (_config.GenerationMode)
            {
                case TerrainGenerationMode.WaveFunctionCollapse:
                    InitializeWaveFunctionTerrain();
                    break;
                default:
                    InitializeLegacyTerrain();
                    break;
            }
        }
    }

    public MinecraftoniaVoxelWorld(
        int width,
        int height,
        int depth,
        int waterLevel = 8,
        int seed = 1337,
        ReadOnlySpan<BlockType> blockData = default)
        : this(
            MinecraftoniaWorldConfig.FromDimensions(width, height, depth, waterLevel, seed),
            initializeTerrain: blockData.IsEmpty)
    {
        if (!blockData.IsEmpty)
        {
            LoadFromFlatArray(blockData);
        }
    }

    public static MinecraftoniaVoxelWorld FromHeightMap(
        int[,] heightMap,
        int verticalScale = 1,
        int waterLevel = 32,
        int chunkSize = 16,
        int seed = 1337)
    {
        if (heightMap is null)
        {
            throw new ArgumentNullException(nameof(heightMap));
        }

        if (verticalScale < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(verticalScale), "Scale must be at least 1.");
        }

        if (chunkSize < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be at least 4.");
        }

        int sourceWidth = heightMap.GetLength(0);
        int sourceDepth = heightMap.GetLength(1);
        int chunkCountX = Math.Max(1, (sourceWidth + chunkSize - 1) / chunkSize);
        int chunkCountZ = Math.Max(1, (sourceDepth + chunkSize - 1) / chunkSize);
        int paddedWidth = chunkCountX * chunkSize;
        int paddedDepth = chunkCountZ * chunkSize;

        int maxHeight = 0;
        for (int x = 0; x < sourceWidth; x++)
        {
            for (int z = 0; z < sourceDepth; z++)
            {
                if (heightMap[x, z] > maxHeight)
                {
                    maxHeight = heightMap[x, z];
                }
            }
        }

        int desiredHeight = Math.Max(maxHeight * verticalScale + 8, waterLevel + 6);
        int chunkCountY = Math.Max(1, (desiredHeight + chunkSize - 1) / chunkSize);
        int worldHeight = chunkCountY * chunkSize;

        var config = new MinecraftoniaWorldConfig
        {
            ChunkSizeX = chunkSize,
            ChunkSizeY = chunkSize,
            ChunkSizeZ = chunkSize,
            ChunkCountX = chunkCountX,
            ChunkCountY = chunkCountY,
            ChunkCountZ = chunkCountZ,
            WaterLevel = Math.Clamp(waterLevel, 2, worldHeight - 3),
            Seed = seed,
            GenerationMode = TerrainGenerationMode.Legacy
        };

        var world = new MinecraftoniaVoxelWorld(config, initializeTerrain: false);
        world.LoadFromHeightMap(heightMap, sourceWidth, sourceDepth, verticalScale, config.WaterLevel);
        return world;
    }

    public MinecraftoniaWorldConfig Config => _config;
    public int WaterLevel => _config.WaterLevel;
    public int Seed => _config.Seed;

    public bool TryGetPreferredSpawn(out Vector3 spawn)
    {
        if (_preferredSpawn is { } value)
        {
            spawn = value;
            return true;
        }

        spawn = default;
        return false;
    }

    public void EnsureChunksAround(Vector3 position, int radius)
    {
        var chunk = GetChunkCoordinate(position);
        EnsureChunksInRange(chunk, radius);
    }

    private void InitializeLegacyTerrain()
    {
        var blocks = new BlockType[Width, Height, Depth];

        GenerateLegacyColumns(blocks);
        CarveLegacyCaves(blocks);
        SprinkleLegacySurfaceDetails(blocks);
        PlantLegacyTrees(blocks);

        _legacyBlocks = blocks;
        _heightMap = BuildHeightMapFromBlocks(blocks);
        _preferredSpawn = FindLegacySpawnPosition();
    }

    private void InitializeWaveFunctionTerrain()
    {
        var library = VoxelPatternLibraryFactory.CreateDefault(Height, WaterLevel);
        int tileSizeX = library.TileSizeX;
        int tileSizeZ = library.TileSizeZ;

        if (Width % tileSizeX != 0 || Depth % tileSizeZ != 0)
        {
            throw new InvalidOperationException("World dimensions must be divisible by the WFC tile size.");
        }

        int gridSizeX = Width / tileSizeX;
        int gridSizeZ = Depth / tileSizeZ;
        const int maxAttempts = 5;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                var blueprint = SelectBlueprint(gridSizeX, gridSizeZ, attempt);
                if (!string.IsNullOrWhiteSpace(blueprint.Link))
                {
                    string regionLabel = string.IsNullOrWhiteSpace(blueprint.Source) ? "(random OSM region)" : blueprint.Source!;
                    Console.WriteLine($"[OpenStreetMap] Using region {regionLabel} -> {blueprint.Link}");
                }

                var generator = new WaveFunctionCollapseGenerator3D(library, gridSizeX, gridSizeZ, _random, blueprint.Blueprint);
                _legacyBlocks = generator.Generate();
                _heightMap = BuildHeightMapFromBlocks(_legacyBlocks);
                string? debugSource = ComposeDebugSource(blueprint.Source, blueprint.Link);
                ArchitectureVoxelPainter.Apply(_legacyBlocks, _heightMap, blueprint.Blueprint, tileSizeX, tileSizeZ, _random, debugSource);
                _preferredSpawn = FindLegacySpawnPosition();
                return;
            }
            catch (OpenStreetMapUnavailableException)
            {
                throw;
            }
            catch
            {
                // Retry with the same random stream; failure will trigger legacy fallback after attempts.
            }
        }

        InitializeLegacyTerrain();
    }

    private readonly record struct BlueprintSelection(MacroBlueprint Blueprint, string? Source, string? Link);

    private BlueprintSelection SelectBlueprint(int gridSizeX, int gridSizeZ, int attempt)
    {
        if (_config.UseOpenStreetMap)
        {
            try
            {
                if (OpenStreetMapBlueprintGenerator.TryCreate(
                        gridSizeX,
                        gridSizeZ,
                        _config.Seed ^ (attempt * 24023),
                        out var osmBlueprint,
                        out var description,
                        out var link) && osmBlueprint is not null)
                {
                    return new BlueprintSelection(osmBlueprint, description, link);
                }

                if (attempt == 0)
                {
                    Console.WriteLine("[OpenStreetMap] Query returned no usable features.");
                }

                if (_config.RequireOpenStreetMap)
                {
                    throw new OpenStreetMapUnavailableException("OpenStreetMap-only generation requested but no usable region was found.");
                }
            }
            catch (Exception ex)
            {
                if (attempt == 0)
                {
                    Console.WriteLine($"[OpenStreetMap] Failed to fetch region: {ex.Message}.");
                }

                if (_config.RequireOpenStreetMap)
                {
                    throw new OpenStreetMapUnavailableException("OpenStreetMap-only generation requested but fetch failed.", ex);
                }
            }
        }
        else if (attempt == 0)
        {
            Console.WriteLine("[OpenStreetMap] Disabled in world config; using procedural blueprint.");
        }

        var fallback = MacroBlueprintGenerator.Create(gridSizeX, gridSizeZ, _config.Seed ^ (attempt * 7919));
        return new BlueprintSelection(fallback, null, null);
    }

    private static string? ComposeDebugSource(string? label, string? link)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.IsNullOrWhiteSpace(link) ? null : link;
        }

        if (string.IsNullOrWhiteSpace(link))
        {
            return label;
        }

        return $"{label} | {link}";
    }

    private sealed class OpenStreetMapUnavailableException : Exception
    {
        public OpenStreetMapUnavailableException(string message)
            : base(message)
        {
        }

        public OpenStreetMapUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    private void LoadFromHeightMap(int[,] heightMap, int sourceWidth, int sourceDepth, int verticalScale, int waterLevel)
    {
        var blocks = new BlockType[Width, Height, Depth];

        for (int x = 0; x < Width; x++)
        {
            for (int z = 0; z < Depth; z++)
            {
                int sampleX = Math.Min(x, sourceWidth - 1);
                int sampleZ = Math.Min(z, sourceDepth - 1);

                int target = Math.Clamp(heightMap[sampleX, sampleZ], 0, int.MaxValue) * verticalScale;
                target = Math.Clamp(target, 1, Height - 2);

                for (int y = 0; y <= target; y++)
                {
                    BlockType block = y == target ? BlockType.Grass : (y >= target - 3 ? BlockType.Dirt : BlockType.Stone);
                    if (y <= waterLevel - 2)
                    {
                        block = BlockType.Stone;
                    }

                    blocks[x, y, z] = block;
                }

                for (int y = waterLevel; y >= 0; y--)
                {
                    if (blocks[x, y, z] == BlockType.Air && y <= waterLevel)
                    {
                        blocks[x, y, z] = BlockType.Water;
                    }
                }

                int surface = target;
                if (surface <= waterLevel + 1)
                {
                    blocks[x, surface, z] = BlockType.Sand;
                    for (int offset = 1; offset <= 2 && surface - offset >= 0; offset++)
                    {
                        blocks[x, surface - offset, z] = BlockType.Sand;
                    }
                }
            }
        }

        _legacyBlocks = blocks;
        _heightMap = BuildHeightMapFromBlocks(blocks);
        _preferredSpawn = FindLegacySpawnPosition();
    }

    private void GenerateLegacyColumns(BlockType[,,] blocks)
    {
        float baseHeight = WaterLevel + 2f;

        for (int x = 0; x < Width; x++)
        {
            for (int z = 0; z < Depth; z++)
            {
                float nx = x * _heightScale1;
                float nz = z * _heightScale1;
                float nx2 = x * _heightScale2;
                float nz2 = z * _heightScale2;

                float undulation = MathF.Sin(nx) * MathF.Cos(nz) * _hillStrength;
                float broad = MathF.Sin(nx2 * 0.7f + nz2 * 0.5f) * _mountainStrength;
                float ridge = MathF.Cos((nx + nz) * 0.65f) * _ridgeStrength;

                float heightF = baseHeight + undulation + broad + ridge;
                int columnHeight = Math.Clamp((int)MathF.Round(heightF), 2, Height - 2);
                int surfaceY = columnHeight;

                for (int y = 0; y <= columnHeight; y++)
                {
                    BlockType block = BlockType.Stone;
                    if (y == columnHeight)
                    {
                        block = BlockType.Grass;
                    }
                    else if (y >= columnHeight - 3)
                    {
                        block = BlockType.Dirt;
                    }

                    if (y <= WaterLevel - 2)
                    {
                        block = BlockType.Stone;
                    }

                    blocks[x, y, z] = block;
                }

                for (int y = WaterLevel; y >= 0; y--)
                {
                    if (blocks[x, y, z] == BlockType.Air && y <= WaterLevel)
                    {
                        blocks[x, y, z] = BlockType.Water;
                    }
                }

                if (surfaceY <= WaterLevel + 1)
                {
                    blocks[x, surfaceY, z] = BlockType.Sand;
                    for (int offset = 1; offset <= 2 && surfaceY - offset >= 0; offset++)
                    {
                        blocks[x, surfaceY - offset, z] = BlockType.Sand;
                    }
                }
            }
        }
    }

    private void CarveLegacyCaves(BlockType[,,] blocks)
    {
        int tunnelCount = Width * Depth / 64;
        for (int i = 0; i < tunnelCount; i++)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float pitch = (float)(_random.NextDouble() * Math.PI / 6 - Math.PI / 12);
            Vector3 direction = new(
                MathF.Cos(angle) * MathF.Cos(pitch),
                MathF.Sin(pitch),
                MathF.Sin(angle) * MathF.Cos(pitch));

            Vector3 position = new(
                _random.Next(4, Width - 4),
                _random.Next(4, Math.Clamp(WaterLevel - 3, 3, Height - 4)),
                _random.Next(4, Depth - 4));

            float length = _random.Next(20, 45);
            float radius = _random.NextSingle() * 1.8f + 1.2f;

            for (int step = 0; step < length; step++)
            {
                position += direction;
                int px = (int)MathF.Round(position.X);
                int py = (int)MathF.Round(position.Y);
                int pz = (int)MathF.Round(position.Z);

                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        for (int dz = -2; dz <= 2; dz++)
                        {
                            float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                            if (dist > radius)
                            {
                                continue;
                            }

                            int x = px + dx;
                            int y = py + dy;
                            int z = pz + dz;
                            if (InBounds(x, y, z) && y > 1)
                            {
                                blocks[x, y, z] = BlockType.Air;
                            }
                        }
                    }
                }

                if (_random.NextDouble() < 0.12)
                {
                    angle += (float)(_random.NextDouble() * 0.6 - 0.3);
                    pitch += (float)(_random.NextDouble() * 0.3 - 0.15);
                    direction = Vector3.Normalize(new Vector3(
                        MathF.Cos(angle) * MathF.Cos(pitch),
                        MathF.Sin(pitch),
                        MathF.Sin(angle) * MathF.Cos(pitch)));
                }
            }
        }
    }

    private void SprinkleLegacySurfaceDetails(BlockType[,,] blocks)
    {
        for (int x = 2; x < Width - 2; x++)
        {
            for (int z = 2; z < Depth - 2; z++)
            {
                for (int y = Height - 2; y >= 1; y--)
                {
                    var block = blocks[x, y, z];
                    if (block == BlockType.Grass)
                    {
                        if (_random.NextDouble() < 0.05)
                        {
                            blocks[x, y + 1, z] = BlockType.Leaves;
                        }
                        break;
                    }

                    if (block == BlockType.Sand)
                    {
                        if (_random.NextDouble() < 0.02)
                        {
                            blocks[x, y + 1, z] = BlockType.Wood;
                        }
                        break;
                    }

                    if (block != BlockType.Air)
                    {
                        break;
                    }
                }
            }
        }
    }

    private void PlantLegacyTrees(BlockType[,,] blocks)
    {
        int treeAttempts = Width * Depth / 48;
        for (int i = 0; i < treeAttempts; i++)
        {
            int x = _random.Next(2, Width - 2);
            int z = _random.Next(2, Depth - 2);

            int surfaceY = FindLegacySurfaceY(blocks, x, z);
            if (surfaceY < 1 || surfaceY >= Height - 6)
            {
                continue;
            }

            if (blocks[x, surfaceY, z] != BlockType.Grass)
            {
                continue;
            }

            if (_random.NextDouble() < 0.35)
            {
                CreateLegacyTree(blocks, x, surfaceY + 1, z);
            }
        }
    }

    private int FindLegacySurfaceY(BlockType[,,] blocks, int x, int z)
    {
        for (int y = Height - 2; y >= 1; y--)
        {
            var block = blocks[x, y, z];
            if (block == BlockType.Grass || block == BlockType.Sand)
            {
                return y;
            }
        }

        return -1;
    }

    private void CreateLegacyTree(BlockType[,,] blocks, int x, int y, int z)
    {
        int trunkHeight = _random.Next(4, 6);

        for (int i = 0; i < trunkHeight; i++)
        {
            int py = y + i;
            if (!InBounds(x, py, z))
            {
                break;
            }

            blocks[x, py, z] = BlockType.Wood;
        }

        int canopyBase = y + trunkHeight - 1;
        int canopyRadius = 2;

        for (int dy = -canopyRadius; dy <= canopyRadius + 1; dy++)
        {
            float layerRadius = canopyRadius - MathF.Abs(dy) * 0.6f;
            int radiusInt = Math.Max(1, (int)MathF.Round(layerRadius));

            for (int dx = -radiusInt; dx <= radiusInt; dx++)
            {
                for (int dz = -radiusInt; dz <= radiusInt; dz++)
                {
                    float dist = MathF.Sqrt(dx * dx + dz * dz);
                    if (dist > layerRadius + 0.3f)
                    {
                        continue;
                    }

                    int px = x + dx;
                    int py = canopyBase + dy;
                    int pz = z + dz;
                    if (!InBounds(px, py, pz))
                    {
                        continue;
                    }

                    if (blocks[px, py, pz] == BlockType.Air)
                    {
                        blocks[px, py, pz] = BlockType.Leaves;
                    }
                }
            }
        }
    }

    private int[,] BuildHeightMapFromBlocks(BlockType[,,] blocks)
    {
        var map = new int[Width, Depth];
        for (int x = 0; x < Width; x++)
        {
            for (int z = 0; z < Depth; z++)
            {
                int height = 0;
                for (int y = Height - 2; y >= 0; y--)
                {
                    var block = blocks[x, y, z];
                    if (block != BlockType.Air && block != BlockType.Water)
                    {
                        height = y;
                        break;
                    }
                }

                map[x, z] = height;
            }
        }

        return map;
    }

    private Vector3? FindLegacySpawnPosition()
    {
        if (_legacyBlocks is null || _heightMap is null)
        {
            return null;
        }

        int width = _heightMap.GetLength(0);
        int depth = _heightMap.GetLength(1);
        int centerX = width / 2;
        int centerZ = depth / 2;
        Vector2 center = new(centerX + 0.5f, centerZ + 0.5f);

        float bestScore = float.NegativeInfinity;
        Vector3? best = null;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                int surfaceY = _heightMap[x, z];
                if (surfaceY <= WaterLevel + 1 || surfaceY >= Height - 4)
                {
                    continue;
                }

                var ground = _legacyBlocks[x, surfaceY, z];
                if (ground != BlockType.Grass)
                {
                    continue;
                }

                float slope = ComputeLocalSlope(_heightMap, x, z);
                if (slope > 4f)
                {
                    continue;
                }

                float distance = Vector2.Distance(new Vector2(x + 0.5f, z + 0.5f), center);
                float score = 12f - distance * 0.35f - slope * 1.5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = new Vector3(x + 0.5f, surfaceY + 2f, z + 0.5f);
                }
            }
        }

        if (best.HasValue)
        {
            return best;
        }

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                int surfaceY = _heightMap[x, z];
                if (surfaceY <= WaterLevel + 1 || surfaceY >= Height - 3)
                {
                    continue;
                }

                if (_legacyBlocks[x, surfaceY, z] == BlockType.Grass)
                {
                    return new Vector3(x + 0.5f, surfaceY + 2f, z + 0.5f);
                }
            }
        }

        return new Vector3(Width / 2f, WaterLevel + 4f, Depth / 2f);
    }

    private float ComputeLocalSlope(int[,] heightMap, int x, int z)
    {
        int width = heightMap.GetLength(0);
        int depth = heightMap.GetLength(1);
        int centerHeight = heightMap[x, z];
        int maxDiff = 0;

        for (int dz = -1; dz <= 1; dz++)
        {
            int nz = z + dz;
            if (nz < 0 || nz >= depth)
            {
                continue;
            }

            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx;
                if (nx < 0 || nx >= width || (dx == 0 && dz == 0))
                {
                    continue;
                }

                int diff = Math.Abs(heightMap[nx, nz] - centerHeight);
                if (diff > maxDiff)
                {
                    maxDiff = diff;
                }
            }
        }

        return maxDiff;
    }

    protected override void PopulateChunk(VoxelChunk<BlockType> chunk)
    {
        if (_legacyBlocks is null)
        {
            return;
        }

        var dims = chunk.Dimensions;
        int baseX = chunk.Coordinate.X * dims.SizeX;
        int baseY = chunk.Coordinate.Y * dims.SizeY;
        int baseZ = chunk.Coordinate.Z * dims.SizeZ;

        for (int localX = 0; localX < dims.SizeX; localX++)
        {
            int worldX = baseX + localX;
            if (worldX < 0 || worldX >= Width)
            {
                continue;
            }

            for (int localZ = 0; localZ < dims.SizeZ; localZ++)
            {
                int worldZ = baseZ + localZ;
                if (worldZ < 0 || worldZ >= Depth)
                {
                    continue;
                }

                for (int localY = 0; localY < dims.SizeY; localY++)
                {
                    int worldY = baseY + localY;
                    if (worldY < 0 || worldY >= Height)
                    {
                        continue;
                    }

                    var block = _legacyBlocks[worldX, worldY, worldZ];
                    chunk.SetBlock(localX, localY, localZ, block, markDirty: false);
                }
            }
        }
    }

    private void LoadFromFlatArray(ReadOnlySpan<BlockType> blockData)
    {
        int expected = Width * Height * Depth;
        if (blockData.Length != expected)
        {
            throw new ArgumentException($"Block data must contain {expected} entries.", nameof(blockData));
        }

        var blocks = new BlockType[Width, Height, Depth];
        _legacyBlocks = blocks;
        _preferredSpawn = null;

        var dims = ChunkSize;
        var buffer = new BlockType[dims.Volume];

        for (int chunkX = 0; chunkX < ChunkCountX; chunkX++)
        {
            for (int chunkY = 0; chunkY < ChunkCountY; chunkY++)
            {
                for (int chunkZ = 0; chunkZ < ChunkCountZ; chunkZ++)
                {
                    var coord = new ChunkCoordinate(chunkX, chunkY, chunkZ);
                    Span<BlockType> span = buffer.AsSpan();

                    for (int localX = 0; localX < dims.SizeX; localX++)
                    {
                        int worldX = chunkX * dims.SizeX + localX;
                        for (int localY = 0; localY < dims.SizeY; localY++)
                        {
                            int worldY = chunkY * dims.SizeY + localY;
                            for (int localZ = 0; localZ < dims.SizeZ; localZ++)
                            {
                                int worldZ = chunkZ * dims.SizeZ + localZ;
                                int globalIndex = ((worldX * Height) + worldY) * Depth + worldZ;
                                int chunkIndex = (localY * dims.SizeZ + localZ) * dims.SizeX + localX;
                                var block = blockData[globalIndex];
                                blocks[worldX, worldY, worldZ] = block;
                                span[chunkIndex] = block;
                            }
                        }
                    }

                    LoadChunk(coord, span, markDirty: false);
                }
            }
        }

        _heightMap = BuildHeightMapFromBlocks(blocks);
        _preferredSpawn = FindLegacySpawnPosition();
    }

    private float SampleFractalNoise(int x, int z, int salt)
    {
        float total = 0f;
        float amplitude = 1f;
        float frequency = 0.01f;
        float sumAmplitude = 0f;

        for (int octave = 0; octave < 3; octave++)
        {
            float value = ValueNoise(x, z, frequency, salt + octave * 7349) - 0.5f;
            total += value * amplitude;
            sumAmplitude += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return sumAmplitude > 0f ? total / sumAmplitude : 0f;
    }

    private float ValueNoise(int x, int z, float frequency, int salt)
    {
        float fx = x * frequency;
        float fz = z * frequency;
        int x0 = (int)MathF.Floor(fx);
        int z0 = (int)MathF.Floor(fz);
        int x1 = x0 + 1;
        int z1 = z0 + 1;
        float tx = fx - x0;
        float tz = fz - z0;

        float v00 = Hash01(x0, z0, salt);
        float v10 = Hash01(x1, z0, salt);
        float v01 = Hash01(x0, z1, salt);
        float v11 = Hash01(x1, z1, salt);

        float ix0 = Lerp(v00, v10, SmoothStep(tx));
        float ix1 = Lerp(v01, v11, SmoothStep(tx));
        return Lerp(ix0, ix1, SmoothStep(tz));
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float SmoothStep(float t) => t * t * (3f - 2f * t);

    private float Hash01(int x, int z, int salt)
    {
        int h = Seed ^ salt;
        h = unchecked(h * 73856093) ^ x;
        h = unchecked(h * 19349663) ^ z;
        h ^= h >> 16;
        h &= int.MaxValue;
        return h / (float)int.MaxValue;
    }
}
