using System;
using System.Numerics;

namespace Minecraftonia.Game;

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
            Vector3 forward = Forward;
            Vector3 horizontalForward = new(forward.X, 0f, forward.Z);
            if (horizontalForward.LengthSquared() < 0.0001f)
            {
                return Vector3.UnitX;
            }

            horizontalForward = Vector3.Normalize(horizontalForward);
            Vector3 right = new Vector3(horizontalForward.Z, 0f, -horizontalForward.X);
            if (right.LengthSquared() < 0.0001f)
            {
                return Vector3.UnitX;
            }

            return Vector3.Normalize(right);
        }
    }

    public Vector3 Up => Vector3.UnitY;
}
