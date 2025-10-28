using Minecraftonia.Core;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.Sample.Doom.Core;

public sealed class DoomVoxelWorld : VoxelWorld<BlockType>
{
    public const int MapWidth = 32;
    public const int MapDepth = 32;

    private const int LocalChunkSize = 16;
    private const int ChunkCountXConst = MapWidth / LocalChunkSize;
    private const int ChunkCountZConst = MapDepth / LocalChunkSize;
    private const int ChunkCountYConst = 1;
    private const int MapHeight = 16;

    private const int WallHeight = 5;
    private const int ColumnHeight = 4;

    public DoomVoxelWorld()
        : base(new ChunkDimensions(LocalChunkSize, MapHeight, LocalChunkSize), ChunkCountXConst, ChunkCountYConst, ChunkCountZConst)
    {
    }

    public void PreloadAllChunks()
    {
        var center = new ChunkCoordinate(ChunkCountX / 2, 0, ChunkCountZ / 2);
        int radius = Math.Max(Math.Max(ChunkCountX, ChunkCountZ), 1);
        EnsureChunksInRange(center, radius);
    }

    protected override void PopulateChunk(VoxelChunk<BlockType> chunk)
    {
        var dims = chunk.Dimensions;
        var coordinate = chunk.Coordinate;

        for (int y = 0; y < dims.SizeY; y++)
        {
            for (int z = 0; z < dims.SizeZ; z++)
            {
                for (int x = 0; x < dims.SizeX; x++)
                {
                    int globalX = coordinate.X * dims.SizeX + x;
                    int globalY = coordinate.Y * dims.SizeY + y;
                    int globalZ = coordinate.Z * dims.SizeZ + z;

                    var block = SampleBlock(globalX, globalY, globalZ);
                    chunk.SetBlock(x, y, z, block, markDirty: false);
                }
            }
        }
    }

    private static BlockType SampleBlock(int x, int y, int z)
    {
        char cell = GetCell(x, z);

        if (y == 0)
        {
            return cell switch
            {
                '~' => BlockType.Sand,
                '.' => BlockType.Grass,
                'C' => BlockType.Grass,
                _ => BlockType.Stone
            };
        }

        if (cell == '~')
        {
            return y == 1 ? BlockType.Water : BlockType.Air;
        }

        if (cell == '#')
        {
            return y <= WallHeight ? BlockType.Stone : BlockType.Air;
        }

        if (cell == 'C')
        {
            return y <= ColumnHeight ? BlockType.Wood : BlockType.Air;
        }

        if (y == 1)
        {
            // Add a subtle trim along the floor to evoke Doom's industrial vibe.
            if ((x + z) % 8 == 0)
            {
                return BlockType.Wood;
            }
        }

        return BlockType.Air;
    }

    private static char GetCell(int x, int z)
    {
        if (x < 0 || x >= MapWidth || z < 0 || z >= MapDepth)
        {
            return '#';
        }

        if (x <= 1 || z <= 1 || x >= MapWidth - 2 || z >= MapDepth - 2)
        {
            return '#';
        }

        // Entry corridor leading into the hangar.
        if (z <= 6 && x >= MapWidth / 2 - 3 && x <= MapWidth / 2 + 3)
        {
            return '.';
        }

        // Main hangar bounds.
        if (x >= 4 && x <= MapWidth - 5 && z >= 6 && z <= MapDepth - 6)
        {
            // Acid pit inspired by the classic central slime pool.
            if (z >= MapDepth / 2 + 2 && z <= MapDepth / 2 + 5 && x >= 10 && x <= MapWidth - 11)
            {
                return '~';
            }

            // Support columns.
            if ((x % 6 == 0) && (z % 6 == 0))
            {
                return 'C';
            }

            // Inner wall ring.
            if ((x == 6 || x == MapWidth - 7) && z >= 10 && z <= MapDepth - 10)
            {
                return '#';
            }

            return '.';
        }

        // Side corridors.
        if (x >= MapWidth / 2 - 2 && x <= MapWidth / 2 + 2)
        {
            return '.';
        }

        return '#';
    }
}
