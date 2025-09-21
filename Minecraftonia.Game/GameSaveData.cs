using System;

namespace Minecraftonia.Game;

public sealed class GameSaveData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Depth { get; set; }
    public int WaterLevel { get; set; }
    public int Seed { get; set; }
    public byte[] Blocks { get; set; } = Array.Empty<byte>();
    public PlayerSaveData Player { get; set; } = new();
    public int SelectedPaletteIndex { get; set; }
}

public sealed class PlayerSaveData
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float VelocityZ { get; set; }
    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public bool IsOnGround { get; set; }
    public float EyeHeight { get; set; } = 1.62f;
}
