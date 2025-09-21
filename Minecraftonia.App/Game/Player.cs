using System;
using System.Numerics;

namespace Minecraftonia.App.Game;

public sealed class Player
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Yaw;
    public float Pitch;
    public bool IsOnGround;

    public float EyeHeight { get; set; } = 1.62f;

    public Vector3 EyePosition => Position + new Vector3(0f, EyeHeight, 0f);

    public Vector3 Forward
    {
        get
        {
            float yawRad = Yaw * (MathF.PI / 180f);
            float pitchRad = Pitch * (MathF.PI / 180f);
            float cosPitch = MathF.Cos(pitchRad);
            return Vector3.Normalize(new Vector3(
                MathF.Sin(yawRad) * cosPitch,
                MathF.Sin(pitchRad),
                MathF.Cos(yawRad) * cosPitch
            ));
        }
    }

    public Vector3 Right
    {
        get
        {
            float yawRad = Yaw * (MathF.PI / 180f);
            return Vector3.Normalize(new Vector3(MathF.Cos(yawRad), 0f, -MathF.Sin(yawRad)));
        }
    }

    public Vector3 Up => Vector3.UnitY;
}
