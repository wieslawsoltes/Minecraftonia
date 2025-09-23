using System;
using System.Numerics;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.VoxelRendering;

public static class VoxelLightingMath
{
    public static Vector3 FaceToNormal(BlockFace face)
    {
        return face switch
        {
            BlockFace.PositiveX => Vector3.UnitX,
            BlockFace.NegativeX => -Vector3.UnitX,
            BlockFace.PositiveY => Vector3.UnitY,
            BlockFace.NegativeY => -Vector3.UnitY,
            BlockFace.PositiveZ => Vector3.UnitZ,
            BlockFace.NegativeZ => -Vector3.UnitZ,
            _ => Vector3.UnitY
        };
    }

    public static float GetFaceLight(BlockFace face)
    {
        return face switch
        {
            BlockFace.PositiveY => 1.0f,
            BlockFace.NegativeY => 0.55f,
            BlockFace.PositiveX => 0.9f,
            BlockFace.NegativeX => 0.75f,
            BlockFace.PositiveZ => 0.85f,
            BlockFace.NegativeZ => 0.7f,
            _ => 1f
        };
    }

    public static Vector2 ComputeFaceUv(BlockFace face, Vector3 local)
    {
        local = new Vector3(
            Math.Clamp(local.X, 0f, 0.999f),
            Math.Clamp(local.Y, 0f, 0.999f),
            Math.Clamp(local.Z, 0f, 0.999f));

        return face switch
        {
            BlockFace.PositiveX => new Vector2(1f - local.Z, 1f - local.Y),
            BlockFace.NegativeX => new Vector2(local.Z, 1f - local.Y),
            BlockFace.PositiveZ => new Vector2(local.X, 1f - local.Y),
            BlockFace.NegativeZ => new Vector2(1f - local.X, 1f - local.Y),
            BlockFace.PositiveY => new Vector2(local.X, local.Z),
            BlockFace.NegativeY => new Vector2(local.X, 1f - local.Z),
            _ => new Vector2(local.X, local.Y)
        };
    }
}
