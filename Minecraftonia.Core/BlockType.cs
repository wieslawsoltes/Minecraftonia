namespace Minecraftonia.Core;

public enum BlockType
{
    Air = 0,
    Grass,
    Dirt,
    Stone,
    Sand,
    Water,
    Wood,
    Leaves
}

public static class BlockTypeExtensions
{
    public static bool IsSolid(this BlockType type)
    {
        return type switch
        {
            BlockType.Air => false,
            BlockType.Water => false,
            BlockType.Leaves => false,
            _ => true
        };
    }

    public static bool IsTransparent(this BlockType type)
    {
        return type switch
        {
            BlockType.Air => true,
            BlockType.Water => true,
            BlockType.Leaves => true,
            _ => false
        };
    }
}
