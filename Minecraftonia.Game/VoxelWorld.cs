using System;
using System.Numerics;
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
    private TerrainTile[,]? _tileMap;
    private int[,]? _heightMap;
    private BlockType[,,]? _legacyBlocks;
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
            if (_config.GenerationMode == TerrainGenerationMode.WaveFunctionCollapse)
            {
                InitializeWaveFunctionTerrain();
            }
            else
            {
                InitializeLegacyTerrain();
            }
        }
        else
        {
            _tileMap = null;
            _heightMap = null;
            _legacyBlocks = null;
            _preferredSpawn = null;
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

    private void InitializeWaveFunctionTerrain()
    {
        _legacyBlocks = null;
        var generator = new WaveFunctionCollapseGenerator(Seed);
        var tiles = generator.Generate(Width, Depth);

        SmoothTileMap(tiles, 2);
        EnsureSpawnablePatch(tiles);

        _tileMap = tiles;

        var heightMap = new int[Width, Depth];
        for (int x = 0; x < Width; x++)
        {
            for (int z = 0; z < Depth; z++)
            {
                var tile = tiles[x, z];
                int height = ComputeTileHeight(tile, x, z);
                heightMap[x, z] = Math.Clamp(height, 2, Height - 3);
            }
        }

        ApplyTileHeightConstraints(heightMap, tiles);
        SmoothHeightMap(heightMap, tiles, 2);
        ApplyTileHeightConstraints(heightMap, tiles);

        _heightMap = heightMap;
        _preferredSpawn = FindPreferredSpawnPosition();
    }

    private void InitializeLegacyTerrain()
    {
        _tileMap = null;
        _preferredSpawn = null;
        var blocks = new BlockType[Width, Height, Depth];

        GenerateLegacyColumns(blocks);
        CarveLegacyCaves(blocks);
        SprinkleLegacySurfaceDetails(blocks);
        PlantLegacyTrees(blocks);

        _legacyBlocks = blocks;
        _heightMap = BuildHeightMapFromBlocks(blocks);
    }

    private void GenerateLegacyColumns(BlockType[,,] blocks)
    {
        const float scale1 = 0.12f;
        const float scale2 = 0.04f;
        const float hillStrength = 6f;
        const float mountainStrength = 16f;
        float baseHeight = WaterLevel + 2f;

        for (int x = 0; x < Width; x++)
        {
            for (int z = 0; z < Depth; z++)
            {
                float nx = x * scale1;
                float nz = z * scale1;
                float nx2 = x * scale2;
                float nz2 = z * scale2;

                float undulation = MathF.Sin(nx) * MathF.Cos(nz) * hillStrength;
                float broad = MathF.Sin(nx2 * 0.7f + nz2 * 0.5f) * mountainStrength;
                float ridge = MathF.Cos((nx + nz) * 0.65f) * 4f;

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
                            var dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
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

    private int ComputeTileHeight(TerrainTile tile, int x, int z)
    {
        float macro = SampleFractalNoise(x, z, 0x31f7);
        float detail = SampleFractalNoise(x + 4096, z - 4096, 0x7b1c);

        float baseHeight = tile switch
        {
            TerrainTile.Ocean => WaterLevel - 3f,
            TerrainTile.Coast => WaterLevel + 1.2f,
            TerrainTile.Plains => WaterLevel + 3.5f,
            TerrainTile.Forest => WaterLevel + 4.2f,
            TerrainTile.Hills => WaterLevel + 7.5f,
            TerrainTile.Mountain => WaterLevel + 12.5f,
            TerrainTile.Snow => WaterLevel + 14.5f,
            TerrainTile.Desert => WaterLevel + 2.5f,
            _ => WaterLevel + 3f
        };

        float macroAmplitude = tile switch
        {
            TerrainTile.Ocean => 1.2f,
            TerrainTile.Coast => 1.8f,
            TerrainTile.Plains => 3.2f,
            TerrainTile.Forest => 3.6f,
            TerrainTile.Hills => 6.5f,
            TerrainTile.Mountain => 10.5f,
            TerrainTile.Snow => 11.5f,
            TerrainTile.Desert => 2.8f,
            _ => 3f
        };

        float detailAmplitude = macroAmplitude * 0.55f;

        float height = baseHeight + macro * macroAmplitude + detail * detailAmplitude;

        if (tile is TerrainTile.Mountain or TerrainTile.Snow)
        {
            float ridge = MathF.Abs(SampleFractalNoise(x - 2048, z + 2048, 0x55a1));
            height += ridge * (macroAmplitude * 1.45f);
        }

        if (tile == TerrainTile.Ocean)
        {
            height = Math.Min(height, WaterLevel - 2f);
        }
        else if (tile == TerrainTile.Coast)
        {
            height = Math.Clamp(height, WaterLevel - 1f, WaterLevel + 2.5f);
        }

        return (int)MathF.Round(height);
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

    private void ApplyTileHeightConstraints(int[,] heightMap, TerrainTile[,] tiles)
    {
        int width = heightMap.GetLength(0);
        int depth = heightMap.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                var tile = tiles[x, z];
                int value = heightMap[x, z];

                (int minHeight, int maxHeight) = tile switch
                {
                    TerrainTile.Ocean => (Math.Max(2, WaterLevel - 6), Math.Min(WaterLevel - 1, WaterLevel)),
                    TerrainTile.Coast => (Math.Max(2, WaterLevel - 1), Math.Min(WaterLevel + 3, Height - 4)),
                    TerrainTile.Plains => (Math.Max(2, WaterLevel + 2), Math.Min(WaterLevel + 7, Height - 4)),
                    TerrainTile.Forest => (Math.Max(2, WaterLevel + 3), Math.Min(WaterLevel + 8, Height - 4)),
                    TerrainTile.Hills => (Math.Max(3, WaterLevel + 5), Math.Min(WaterLevel + 12, Height - 4)),
                    TerrainTile.Mountain => (Math.Max(4, WaterLevel + 11), Math.Min(Height - 5, WaterLevel + 22)),
                    TerrainTile.Snow => (Math.Max(5, WaterLevel + 13), Math.Min(Height - 4, WaterLevel + 26)),
                    TerrainTile.Desert => (Math.Max(2, WaterLevel + 1), Math.Min(WaterLevel + 6, Height - 4)),
                    _ => (Math.Max(2, WaterLevel + 2), Height - 4)
                };

                value = Math.Clamp(value, minHeight, maxHeight);
                heightMap[x, z] = value;
            }
        }
    }

    private void SmoothHeightMap(int[,] heightMap, TerrainTile[,] tiles, int iterations)
    {
        int width = heightMap.GetLength(0);
        int depth = heightMap.GetLength(1);
        var buffer = new int[width, depth];

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    int total = heightMap[x, z] * 4;
                    int weight = 4;

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

                            total += heightMap[nx, nz];
                            weight++;
                        }
                    }

                    buffer[x, z] = (int)MathF.Round(total / (float)weight);
                }
            }

            Array.Copy(buffer, heightMap, buffer.Length);
        }

        ApplyTileHeightConstraints(heightMap, tiles);
    }

    private Vector3? FindPreferredSpawnPosition()
    {
        if (_tileMap is null || _heightMap is null)
        {
            return null;
        }

        int width = _tileMap.GetLength(0);
        int depth = _tileMap.GetLength(1);
        int centerX = width / 2;
        int centerZ = depth / 2;
        Vector2 center = new(centerX + 0.5f, centerZ + 0.5f);

        float bestScore = float.NegativeInfinity;
        Vector3? best = null;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                var tile = _tileMap[x, z];
                if (!IsSpawnableTile(tile))
                {
                    continue;
                }

                int surfaceY = _heightMap[x, z];
                if (surfaceY <= WaterLevel + 1 || surfaceY >= Height - 4)
                {
                    continue;
                }

                float slope = ComputeLocalSlope(_heightMap, x, z);
                if (slope > 5f)
                {
                    continue;
                }

                float distance = Vector2.Distance(new Vector2(x + 0.5f, z + 0.5f), center);
                float tilePreference = tile switch
                {
                    TerrainTile.Plains => 1.0f,
                    TerrainTile.Forest => 0.95f,
                    TerrainTile.Hills => 0.8f,
                    TerrainTile.Coast => 0.7f,
                    TerrainTile.Desert => 0.5f,
                    _ => 0.6f
                };

                float score = tilePreference * 12f - distance * 0.35f - slope * 1.7f;
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
                var tile = _tileMap[x, z];
                if (!IsSpawnableTile(tile))
                {
                    continue;
                }

                int surfaceY = _heightMap[x, z];
                if (surfaceY <= WaterLevel + 1 || surfaceY >= Height - 3)
                {
                    continue;
                }

                return new Vector3(x + 0.5f, surfaceY + 2f, z + 0.5f);
            }
        }

        return null;
    }

    private static bool IsSpawnableTile(TerrainTile tile)
    {
        return tile is TerrainTile.Plains or TerrainTile.Forest or TerrainTile.Hills or TerrainTile.Coast;
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

    private TerrainTile? TryGetTile(int x, int z)
    {
        if (_tileMap is null)
        {
            return null;
        }

        if (x < 0 || x >= _tileMap.GetLength(0) || z < 0 || z >= _tileMap.GetLength(1))
        {
            return null;
        }

        return _tileMap[x, z];
    }

    private void SmoothTileMap(TerrainTile[,] tiles, int iterations)
    {
        int width = tiles.GetLength(0);
        int depth = tiles.GetLength(1);
        int tileTypeCount = Enum.GetValues<TerrainTile>().Length;
        var buffer = new TerrainTile[width, depth];

        for (int iter = 0; iter < iterations; iter++)
        {
            var counts = new int[tileTypeCount];

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    Array.Clear(counts);
                    TerrainTile current = tiles[x, z];
                    counts[(int)current] += 3;

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
                            if (nx < 0 || nx >= width)
                            {
                                continue;
                            }

                            if (dx == 0 && dz == 0)
                            {
                                continue;
                            }

                            counts[(int)tiles[nx, nz]]++;
                        }
                    }

                    int bestIndex = (int)current;
                    int bestScore = counts[bestIndex];

                    for (int i = 0; i < tileTypeCount; i++)
                    {
                        if (counts[i] > bestScore + 1)
                        {
                            bestScore = counts[i];
                            bestIndex = i;
                        }
                    }

                    buffer[x, z] = (TerrainTile)bestIndex;
                }
            }

            Array.Copy(buffer, tiles, buffer.Length);
        }
    }

    private void EnsureSpawnablePatch(TerrainTile[,] tiles)
    {
        if (ContainsSpawnableTile(tiles))
        {
            return;
        }

        int centerX = tiles.GetLength(0) / 2;
        int centerZ = tiles.GetLength(1) / 2;
        int radius = Math.Max(4, Math.Min(centerX, centerZ) / 4);

        for (int dx = -radius; dx <= radius; dx++)
        {
            int x = centerX + dx;
            if (x < 0 || x >= tiles.GetLength(0))
            {
                continue;
            }

            for (int dz = -radius; dz <= radius; dz++)
            {
                int z = centerZ + dz;
                if (z < 0 || z >= tiles.GetLength(1))
                {
                    continue;
                }

                tiles[x, z] = TerrainTile.Plains;
            }
        }
    }

    private bool ContainsSpawnableTile(TerrainTile[,] tiles)
    {
        int width = tiles.GetLength(0);
        int depth = tiles.GetLength(1);
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                if (IsSpawnableTile(tiles[x, z]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void PopulateLegacyChunk(VoxelChunk<BlockType> chunk)
    {
        if (_legacyBlocks is null)
        {
            throw new InvalidOperationException("Legacy terrain has not been initialised.");
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

    protected override void PopulateChunk(VoxelChunk<BlockType> chunk)
    {
        if (_config.GenerationMode == TerrainGenerationMode.Legacy)
        {
            PopulateLegacyChunk(chunk);
        }
        else
        {
            PopulateWaveFunctionChunk(chunk);
        }
    }

    private void PopulateWaveFunctionChunk(VoxelChunk<BlockType> chunk)
    {
        var dims = chunk.Dimensions;
        int baseX = chunk.Coordinate.X * dims.SizeX;
        int baseY = chunk.Coordinate.Y * dims.SizeY;
        int baseZ = chunk.Coordinate.Z * dims.SizeZ;

        for (int localX = 0; localX < dims.SizeX; localX++)
        {
            int worldX = baseX + localX;
            for (int localZ = 0; localZ < dims.SizeZ; localZ++)
            {
                int worldZ = baseZ + localZ;
                TerrainTile? tile = TryGetTile(worldX, worldZ);
                int surfaceY = ComputeSurfaceHeight(worldX, worldZ);
                bool isBeach = surfaceY <= WaterLevel + 1;
                if (tile is TerrainTile tileValue)
                {
                    isBeach = tileValue is TerrainTile.Desert or TerrainTile.Ocean or TerrainTile.Coast || isBeach;
                }

                for (int localY = 0; localY < dims.SizeY; localY++)
                {
                    int worldY = baseY + localY;
                    BlockType block = DetermineBaseBlock(tile, worldY, surfaceY, isBeach);

                    if (block != BlockType.Air && ShouldCarveCave(worldX, worldY, worldZ))
                    {
                        block = BlockType.Air;
                    }

                    if (block == BlockType.Air && worldY <= WaterLevel)
                    {
                        block = BlockType.Water;
                    }

                    chunk.SetBlock(localX, localY, localZ, block, markDirty: false);
                }

                if (chunk.Coordinate.Y == surfaceY / ChunkSize.SizeY)
                {
                    DecorateSurface(worldX, surfaceY, worldZ, tile);
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

        _tileMap = null;
        var legacyBlocks = new BlockType[Width, Height, Depth];
        _legacyBlocks = legacyBlocks;
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
                                span[chunkIndex] = block;
                                legacyBlocks[worldX, worldY, worldZ] = block;
                            }
                        }
                    }

                    LoadChunk(coord, span, markDirty: false);
                }
            }
        }

        _heightMap = BuildHeightMapFromBlocks(legacyBlocks);
    }

    private int ComputeSurfaceHeight(int worldX, int worldZ)
    {
        if (_heightMap is { } map)
        {
            int maxX = map.GetLength(0) - 1;
            int maxZ = map.GetLength(1) - 1;
            int clampedX = Math.Clamp(worldX, 0, maxX);
            int clampedZ = Math.Clamp(worldZ, 0, maxZ);
            return map[clampedX, clampedZ];
        }

        return ComputeSurfaceHeightFallback(worldX, worldZ);
    }

    private int ComputeSurfaceHeightFallback(int worldX, int worldZ)
    {
        float nx = (worldX + Seed * 0.1337f) * _heightScale1;
        float nz = (worldZ - Seed * 0.213f) * _heightScale1;
        float nx2 = (worldX - Seed * 0.071f) * _heightScale2;
        float nz2 = (worldZ + Seed * 0.057f) * _heightScale2;

        float undulation = MathF.Sin(nx) * MathF.Cos(nz) * _hillStrength;
        float broad = MathF.Sin(nx2 * 0.7f + nz2 * 0.5f) * _mountainStrength;
        float ridge = MathF.Cos((nx + nz) * 0.65f) * _ridgeStrength;
        float baseHeight = WaterLevel + 2f;

        float heightF = baseHeight + undulation + broad + ridge;
        int surface = Math.Clamp((int)MathF.Round(heightF), 2, Height - 3);
        return surface;
    }

    private BlockType DetermineBaseBlock(TerrainTile? tile, int worldY, int surfaceY, bool isBeach)
    {
        if (worldY > surfaceY)
        {
            return BlockType.Air;
        }

        if (tile is TerrainTile tileValue)
        {
            return tileValue switch
            {
                TerrainTile.Ocean => worldY >= surfaceY - 1 ? BlockType.Sand : BlockType.Stone,
                TerrainTile.Coast => worldY >= surfaceY - 2 ? BlockType.Sand : BlockType.Dirt,
                TerrainTile.Desert => worldY >= surfaceY - 3 ? BlockType.Sand : BlockType.Stone,
                TerrainTile.Mountain => DetermineMountainBlock(worldY, surfaceY),
                TerrainTile.Snow => DetermineMountainBlock(worldY, surfaceY),
                TerrainTile.Hills => DetermineHillsBlock(worldY, surfaceY),
                TerrainTile.Forest => DetermineLushBlock(worldY, surfaceY),
                TerrainTile.Plains => DetermineLushBlock(worldY, surfaceY),
                _ => BlockType.Stone
            };
        }

        if (isBeach)
        {
            return worldY >= surfaceY - 2 ? BlockType.Sand : BlockType.Stone;
        }

        if (worldY == surfaceY)
        {
            return BlockType.Grass;
        }

        if (worldY >= surfaceY - 3)
        {
            return BlockType.Dirt;
        }

        return BlockType.Stone;
    }

    private static BlockType DetermineLushBlock(int worldY, int surfaceY)
    {
        if (worldY == surfaceY)
        {
            return BlockType.Grass;
        }

        if (surfaceY - worldY <= 3)
        {
            return BlockType.Dirt;
        }

        return BlockType.Stone;
    }

    private BlockType DetermineHillsBlock(int worldY, int surfaceY)
    {
        if (worldY == surfaceY)
        {
            return BlockType.Grass;
        }

        if (surfaceY - worldY <= 2)
        {
            return BlockType.Dirt;
        }

        if (surfaceY - worldY <= 5)
        {
            return BlockType.Stone;
        }

        return BlockType.Stone;
    }

    private BlockType DetermineMountainBlock(int worldY, int surfaceY)
    {
        bool highAltitude = surfaceY >= WaterLevel + 9;

        if (worldY == surfaceY)
        {
            return BlockType.Stone;
        }

        if (surfaceY - worldY <= 3)
        {
            return highAltitude ? BlockType.Stone : BlockType.Dirt;
        }

        return BlockType.Stone;
    }

    private bool ShouldCarveCave(int worldX, int worldY, int worldZ)
    {
        if (worldY <= 3 || worldY >= WaterLevel - 2)
        {
            return false;
        }

        float noise = SampleCaveNoise(worldX, worldY, worldZ);
        return noise > 0.72f;
    }

    private float SampleCaveNoise(int x, int y, int z)
    {
        float f = MathF.Sin((x + Seed * 0.37f) * 0.19f)
                  + MathF.Sin((y - Seed * 0.22f) * 0.31f)
                  + MathF.Sin((z + Seed * 0.41f) * 0.23f);
        return (f + 3f) / 6f;
    }

    private void DecorateSurface(int worldX, int surfaceY, int worldZ, TerrainTile? tile)
    {
        if (surfaceY <= 0 || surfaceY >= Height - 6)
        {
            return;
        }

        TerrainTile tileValue = tile ?? TerrainTile.Plains;
        if (tileValue == TerrainTile.Ocean)
        {
            return;
        }

        var surfaceBlock = GetBlock(worldX, surfaceY, worldZ);
        if (surfaceBlock == BlockType.Grass)
        {
            float noise = Hash01(worldX, worldZ, 0x1f4d3);
            float treeThreshold = tileValue switch
            {
                TerrainTile.Forest => 0.75f,
                TerrainTile.Plains => 0.35f,
                TerrainTile.Hills => 0.22f,
                TerrainTile.Coast => 0.18f,
                TerrainTile.Snow => 0.1f,
                _ => 0.2f
            };

            float foliageThreshold = tileValue switch
            {
                TerrainTile.Forest => treeThreshold + 0.18f,
                TerrainTile.Hills => treeThreshold + 0.09f,
                TerrainTile.Coast => treeThreshold + 0.05f,
                TerrainTile.Snow => treeThreshold + 0.04f,
                _ => treeThreshold + 0.08f
            };

            if (noise < treeThreshold)
            {
                PlaceTree(worldX, surfaceY + 1, worldZ, noise, tileValue);
            }
            else if (noise < foliageThreshold)
            {
                int aboveY = surfaceY + 1;
                if (aboveY < Height - 1 && GetBlock(worldX, aboveY, worldZ) == BlockType.Air)
                {
                    SetBlock(worldX, aboveY, worldZ, BlockType.Leaves);
                }
            }
        }
        else if (surfaceBlock == BlockType.Sand)
        {
            float driftwoodNoise = Hash01(worldX, worldZ, 0x7a2b5);
            float driftwoodChance = tileValue switch
            {
                TerrainTile.Desert => 0.03f,
                TerrainTile.Coast => 0.028f,
                _ => 0.018f
            };

            if (driftwoodNoise < driftwoodChance)
            {
                int aboveY = surfaceY + 1;
                if (aboveY < Height - 1 && GetBlock(worldX, aboveY, worldZ) == BlockType.Air)
                {
                    SetBlock(worldX, aboveY, worldZ, BlockType.Wood);
                }
            }
        }
    }

    private void PlaceTree(int worldX, int trunkBaseY, int worldZ, float noise, TerrainTile tile)
    {
        int maxHeight = Height - 2;
        float baseHeight = tile switch
        {
            TerrainTile.Forest => 5.6f,
            TerrainTile.Hills => 4.8f,
            TerrainTile.Mountain => 3.6f,
            TerrainTile.Snow => 3.4f,
            TerrainTile.Coast => 4.2f,
            _ => 4.4f
        };

        float heightVariation = tile switch
        {
            TerrainTile.Forest => 3.4f,
            TerrainTile.Hills => 2.0f,
            TerrainTile.Mountain => 1.4f,
            TerrainTile.Snow => 1.1f,
            TerrainTile.Coast => 1.5f,
            _ => 2.4f
        };

        int trunkHeight = (int)MathF.Round(baseHeight + noise * heightVariation);
        trunkHeight = Math.Clamp(trunkHeight, 3, 7);

        for (int i = 0; i < trunkHeight; i++)
        {
            int y = trunkBaseY + i;
            if (y >= maxHeight)
            {
                break;
            }

            if (!InBounds(worldX, y, worldZ))
            {
                break;
            }

            SetBlock(worldX, y, worldZ, BlockType.Wood);
        }

        int canopyBase = trunkBaseY + trunkHeight - 1;
        int canopyRadius = tile switch
        {
            TerrainTile.Forest => 3,
            TerrainTile.Hills => 2,
            TerrainTile.Mountain => 1,
            TerrainTile.Snow => 1,
            _ => 2
        };

        for (int dy = -canopyRadius; dy <= canopyRadius + 1; dy++)
        {
            int y = canopyBase + dy;
            if (y >= Height - 1 || y < 0)
            {
                continue;
            }

            float layerRadius = canopyRadius - MathF.Abs(dy) * 0.6f;
            if (layerRadius <= 0.4f)
            {
                layerRadius = 0.6f;
            }

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

                    int px = worldX + dx;
                    int pz = worldZ + dz;
                    if (!InBounds(px, y, pz))
                    {
                        continue;
                    }

                    if (GetBlock(px, y, pz) == BlockType.Air)
                    {
                        SetBlock(px, y, pz, BlockType.Leaves);
                    }
                }
            }
        }
    }

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
