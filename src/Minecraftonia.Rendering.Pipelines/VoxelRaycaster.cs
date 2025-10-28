using System;
using System.Numerics;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.Rendering.Pipelines;

public static class VoxelRaycaster
{
    public static bool TryPick<TBlock>(
        IVoxelWorld<TBlock> world,
        Vector3 origin,
        Vector3 direction,
        float maxDistance,
        Func<TBlock, bool> isEmpty,
        out VoxelRaycastHit<TBlock> hit)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (isEmpty is null) throw new ArgumentNullException(nameof(isEmpty));

        origin += direction * 0.0005f;

        int mapX = (int)MathF.Floor(origin.X);
        int mapY = (int)MathF.Floor(origin.Y);
        int mapZ = (int)MathF.Floor(origin.Z);

        float rayDirX = direction.X;
        float rayDirY = direction.Y;
        float rayDirZ = direction.Z;

        int stepX = rayDirX < 0 ? -1 : 1;
        int stepY = rayDirY < 0 ? -1 : 1;
        int stepZ = rayDirZ < 0 ? -1 : 1;

        float deltaDistX = rayDirX == 0 ? float.MaxValue : MathF.Abs(1f / rayDirX);
        float deltaDistY = rayDirY == 0 ? float.MaxValue : MathF.Abs(1f / rayDirY);
        float deltaDistZ = rayDirZ == 0 ? float.MaxValue : MathF.Abs(1f / rayDirZ);

        float sideDistX = rayDirX < 0
            ? (origin.X - mapX) * deltaDistX
            : (mapX + 1f - origin.X) * deltaDistX;

        float sideDistY = rayDirY < 0
            ? (origin.Y - mapY) * deltaDistY
            : (mapY + 1f - origin.Y) * deltaDistY;

        float sideDistZ = rayDirZ < 0
            ? (origin.Z - mapZ) * deltaDistZ
            : (mapZ + 1f - origin.Z) * deltaDistZ;

        const int maxSteps = 256;

        for (int step = 0; step < maxSteps; step++)
        {
            BlockFace face;
            float traveled;

            if (sideDistX < sideDistY)
            {
                if (sideDistX < sideDistZ)
                {
                    mapX += stepX;
                    traveled = sideDistX;
                    sideDistX += deltaDistX;
                    face = stepX > 0 ? BlockFace.NegativeX : BlockFace.PositiveX;
                }
                else
                {
                    mapZ += stepZ;
                    traveled = sideDistZ;
                    sideDistZ += deltaDistZ;
                    face = stepZ > 0 ? BlockFace.NegativeZ : BlockFace.PositiveZ;
                }
            }
            else
            {
                if (sideDistY < sideDistZ)
                {
                    mapY += stepY;
                    traveled = sideDistY;
                    sideDistY += deltaDistY;
                    face = stepY > 0 ? BlockFace.NegativeY : BlockFace.PositiveY;
                }
                else
                {
                    mapZ += stepZ;
                    traveled = sideDistZ;
                    sideDistZ += deltaDistZ;
                    face = stepZ > 0 ? BlockFace.NegativeZ : BlockFace.PositiveZ;
                }
            }

            if (traveled >= maxDistance)
            {
                break;
            }

            if (!world.InBounds(mapX, mapY, mapZ))
            {
                continue;
            }

            TBlock block = world.GetBlock(mapX, mapY, mapZ);
            if (isEmpty(block))
            {
                continue;
            }

            Vector3 hitPoint = origin + direction * traveled;
            hit = new VoxelRaycastHit<TBlock>(new Int3(mapX, mapY, mapZ), face, block, hitPoint, traveled);
            return true;
        }

        hit = default;
        return false;
    }
}
