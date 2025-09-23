using System;
using System.Collections.Generic;
using System.Numerics;
using Minecraftonia.WaveFunctionCollapse;
using Minecraftonia.VoxelEngine;
using Minecraftonia.VoxelRendering;

namespace Minecraftonia.Game;

public sealed class BlockTextures : IVoxelMaterialProvider<BlockType>
{
    private readonly Dictionary<(BlockType block, BlockFace face), BlockTexture> _textures = new();

    public BlockTextures()
    {
        var random = new Random(90210);

        // Grass
        var grassTop = CreateGrassTop(random);
        var grassSide = CreateGrassSide(random);
        var dirt = CreateDirt(random);

        Register(BlockType.Grass, BlockFace.PositiveY, grassTop);
        Register(BlockType.Grass, BlockFace.NegativeY, dirt);
        Register(BlockType.Grass, BlockFace.PositiveX, grassSide);
        Register(BlockType.Grass, BlockFace.NegativeX, grassSide);
        Register(BlockType.Grass, BlockFace.PositiveZ, grassSide);
        Register(BlockType.Grass, BlockFace.NegativeZ, grassSide);

        Register(BlockType.Dirt, BlockFace.PositiveY, dirt);
        Register(BlockType.Dirt, BlockFace.NegativeY, dirt);
        Register(BlockType.Dirt, BlockFace.PositiveX, dirt);
        Register(BlockType.Dirt, BlockFace.NegativeX, dirt);
        Register(BlockType.Dirt, BlockFace.PositiveZ, dirt);
        Register(BlockType.Dirt, BlockFace.NegativeZ, dirt);

        var stone = CreateStone(random);
        Register(BlockType.Stone, BlockFace.PositiveY, stone);
        Register(BlockType.Stone, BlockFace.NegativeY, stone);
        Register(BlockType.Stone, BlockFace.PositiveX, stone);
        Register(BlockType.Stone, BlockFace.NegativeX, stone);
        Register(BlockType.Stone, BlockFace.PositiveZ, stone);
        Register(BlockType.Stone, BlockFace.NegativeZ, stone);

        var sand = CreateSand(random);
        Register(BlockType.Sand, BlockFace.PositiveY, sand);
        Register(BlockType.Sand, BlockFace.NegativeY, sand);
        Register(BlockType.Sand, BlockFace.PositiveX, sand);
        Register(BlockType.Sand, BlockFace.NegativeX, sand);
        Register(BlockType.Sand, BlockFace.PositiveZ, sand);
        Register(BlockType.Sand, BlockFace.NegativeZ, sand);

        var water = CreateWater();
        Register(BlockType.Water, BlockFace.PositiveY, water);
        Register(BlockType.Water, BlockFace.NegativeY, water);
        Register(BlockType.Water, BlockFace.PositiveX, water);
        Register(BlockType.Water, BlockFace.NegativeX, water);
        Register(BlockType.Water, BlockFace.PositiveZ, water);
        Register(BlockType.Water, BlockFace.NegativeZ, water);

        var woodSide = CreateWoodSide(random);
        var woodTop = CreateWoodTop(random);
        Register(BlockType.Wood, BlockFace.PositiveY, woodTop);
        Register(BlockType.Wood, BlockFace.NegativeY, woodTop);
        Register(BlockType.Wood, BlockFace.PositiveX, woodSide);
        Register(BlockType.Wood, BlockFace.NegativeX, woodSide);
        Register(BlockType.Wood, BlockFace.PositiveZ, woodSide);
        Register(BlockType.Wood, BlockFace.NegativeZ, woodSide);

        var leaves = CreateLeaves(random);
        Register(BlockType.Leaves, BlockFace.PositiveY, leaves);
        Register(BlockType.Leaves, BlockFace.NegativeY, leaves);
        Register(BlockType.Leaves, BlockFace.PositiveX, leaves);
        Register(BlockType.Leaves, BlockFace.NegativeX, leaves);
        Register(BlockType.Leaves, BlockFace.PositiveZ, leaves);
        Register(BlockType.Leaves, BlockFace.NegativeZ, leaves);
    }

    public VoxelMaterialSample Sample(BlockType type, BlockFace face, float u, float v)
    {
        if (!_textures.TryGetValue((type, face), out var texture))
        {
            texture = _textures[(type, BlockFace.PositiveY)];
        }

        Vector4 sample = texture.Sample(u, v);
        Vector3 color = new(sample.X, sample.Y, sample.Z);
        float opacity = sample.W;

        if (type.IsSolid())
        {
            opacity = 1f;
        }
        else if (type == BlockType.Water)
        {
            opacity = MathF.Min(0.6f, opacity + 0.1f);
            color *= 0.85f;
        }
        else if (type == BlockType.Leaves)
        {
            opacity = MathF.Min(0.65f, opacity);
        }

        color = Vector3.Clamp(color, Vector3.Zero, Vector3.One);
        return new VoxelMaterialSample(color, opacity);
    }

    private void Register(BlockType block, BlockFace face, BlockTexture texture)
    {
        _textures[(block, face)] = texture;
    }

    private static BlockTexture CreateGrassTop(Random random)
    {
        const int size = 16;
        return new BlockTexture(size, (x, y) =>
        {
            float noise = Noise(random, x, y, 0.25f);
            float highlight = MathF.Pow(1f - MathF.Abs(y - size * 0.65f) / size, 2f) * 0.25f;
            var baseColor = new Vector3(38, 142, 63) / 255f;
            var tint = new Vector3(40, 196, 74) / 255f;
            var mixed = Vector3.Lerp(baseColor, tint, 0.35f + noise * 0.3f + highlight);
            return new Vector4(mixed, 1f);
        });
    }

    private static BlockTexture CreateGrassSide(Random random)
    {
        const int size = 16;
        return new BlockTexture(size, (x, y) =>
        {
            float normalizedY = y / (float)(size - 1);
            if (normalizedY < 0.35f)
            {
                float noise = Noise(random, x, y, 0.2f);
                var dir = new Vector3(99, 65, 36) / 255f;
                var dir2 = new Vector3(109, 75, 40) / 255f;
                var color = Vector3.Lerp(dir, dir2, normalizedY * 3f + noise * 0.2f);
                return new Vector4(color, 1f);
            }
            else if (normalizedY < 0.45f)
            {
                var band = new Vector3(72, 112, 46) / 255f;
                return new Vector4(band, 1f);
            }
           else
           {
               float noise = Noise(random, x, y, 0.25f);
               var top = new Vector3(46, 160, 70) / 255f;
                var color = Vector3.Clamp(top + new Vector3(noise * 0.08f), Vector3.Zero, Vector3.One);
                return new Vector4(color, 1f);
           }
       });
   }

    private static BlockTexture CreateDirt(Random random)
    {
        const int size = 16;
        return new BlockTexture(size, (x, y) =>
        {
            float n1 = Noise(random, x, y, 0.3f);
            float n2 = Noise(random, y, x, 0.18f);
            var baseColor = new Vector3(92, 62, 43) / 255f;
            var tint = new Vector3(72, 43, 25) / 255f;
            var mix = Vector3.Lerp(baseColor, tint, (n1 + n2) * 0.5f + 0.25f);
            return new Vector4(mix, 1f);
        });
    }

    private static BlockTexture CreateStone(Random random)
    {
        const int size = 16;
        return new BlockTexture(size, (x, y) =>
        {
            float n1 = Noise(random, x, y, 0.35f);
            float n2 = Noise(random, y, x, 0.45f);
            var baseColor = new Vector3(116, 120, 126) / 255f;
            var darker = new Vector3(76, 82, 88) / 255f;
            float mixValue = 0.4f + n1 * 0.3f + n2 * 0.2f;
            var mix = Vector3.Lerp(darker, baseColor, mixValue);
            return new Vector4(mix, 1f);
        });
    }

    private static BlockTexture CreateSand(Random random)
    {
        const int size = 16;
        return new BlockTexture(size, (x, y) =>
        {
            float n1 = Noise(random, x, y, 0.4f);
            float n2 = Noise(random, y, x, 0.2f);
            var baseColor = new Vector3(219, 203, 151) / 255f;
            var accent = new Vector3(202, 187, 142) / 255f;
            float t = 0.3f + n1 * 0.3f + n2 * 0.3f;
            var mix = Vector3.Lerp(baseColor, accent, t);
            if ((x + y) % 6 == 0)
            {
                mix -= new Vector3(0.06f, 0.06f, 0.04f);
            }
            return new Vector4(Vector3.Clamp(mix, Vector3.Zero, Vector3.One), 1f);
        });
    }

    private static BlockTexture CreateWater()
    {
        const int size = 16;
        return new BlockTexture(size, (x, y) =>
        {
            float wave = MathF.Sin((x + y) * 0.5f) * 0.08f;
            var baseColor = new Vector3(54, 136, 206) / 255f;
            var highlight = new Vector3(96, 174, 226) / 255f;
            var mix = Vector3.Lerp(baseColor, highlight, 0.4f + wave);
            return new Vector4(mix, 0.5f);
        });
    }

    private static BlockTexture CreateWoodSide(Random random)
    {
        const int size = 16;
        return new BlockTexture(size, (x, y) =>
        {
           float ring = MathF.Sin((y / (float)size) * MathF.PI * 6f) * 0.15f;
           var baseColor = new Vector3(138, 92, 54) / 255f;
           var dark = new Vector3(102, 69, 40) / 255f;
           float stripe = ((x + 1) % 4 == 0) ? 0.25f : 0f;
           var mix = Vector3.Lerp(dark, baseColor, 0.4f + ring + stripe);
            float noise = Noise(random, x, y, 0.15f);
            mix += new Vector3(noise * 0.05f);
            return new Vector4(Vector3.Clamp(mix, Vector3.Zero, Vector3.One), 1f);
        });
    }

    private static BlockTexture CreateWoodTop(Random random)
    {
        const int size = 16;
        Vector2 center = new(size / 2f, size / 2f);
        return new BlockTexture(size, (x, y) =>
        {
            var pos = new Vector2(x, y);
            float distance = Vector2.Distance(pos, center) / size;
            var baseColor = new Vector3(170, 117, 66) / 255f;
            var ringColor = new Vector3(124, 81, 44) / 255f;
           float ring = MathF.Sin(distance * MathF.PI * 6.5f) * 0.5f + 0.5f;
           var color = Vector3.Lerp(ringColor, baseColor, ring);
            float noise = Noise(random, x, y, 0.2f);
            color += new Vector3(noise * 0.05f);
            return new Vector4(Vector3.Clamp(color, Vector3.Zero, Vector3.One), 1f);
        });
    }

    private static BlockTexture CreateLeaves(Random random)
    {
        const int size = 16;
        return new BlockTexture(size, (x, y) =>
        {
            float noise = Noise(random, x, y, 0.35f);
            var baseColor = new Vector3(62, 130, 58) / 255f;
            var highlight = new Vector3(110, 176, 98) / 255f;
            float blotch = ((x + y) % 5 == 0) ? 0.2f : 0f;
            var color = Vector3.Lerp(baseColor, highlight, 0.4f + blotch + noise * 0.3f);
            return new Vector4(Vector3.Clamp(color, Vector3.Zero, Vector3.One), 0.7f);
        });
    }

    private static float Noise(Random random, int x, int y, float strength)
    {
        int hash = Hash(x, y);
        random = new Random(hash);
        return ((float)random.NextDouble() - 0.5f) * 2f * strength;
    }

    private static int Hash(int x, int y)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + x;
            hash = hash * 31 + y * 131;
            hash ^= hash << 7;
            hash += unchecked((int)0x9e3779b9);
            return hash;
        }
    }
}

public sealed class BlockTexture
{
    private readonly Vector4[] _pixels;
    private readonly int _size;

    public BlockTexture(int size, Func<int, int, Vector4> generator)
    {
        _size = size;
        _pixels = new Vector4[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                _pixels[y * size + x] = Vector4.Clamp(generator(x, y), Vector4.Zero, Vector4.One);
            }
        }
    }

    public Vector4 Sample(float u, float v)
    {
        u = Wrap01(u);
        v = Wrap01(v);

        int x = (int)(u * (_size - 1));
        int y = (int)(v * (_size - 1));
        return _pixels[y * _size + x];
    }

    private static float Wrap01(float value)
    {
        if (float.IsNaN(value))
        {
            return 0f;
        }

        value -= MathF.Floor(value);
        return value;
    }
}
