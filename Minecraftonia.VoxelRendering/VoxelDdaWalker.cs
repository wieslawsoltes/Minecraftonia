using System;
using System.Numerics;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.VoxelRendering;

internal enum VoxelDdaHitKind
{
    None,
    Block,
    Sky
}

internal readonly struct VoxelDdaHit<TBlock>
{
    public VoxelDdaHitKind Kind { get; init; }
    public BlockFace Face { get; init; }
    public float Distance { get; init; }
    public int VoxelX { get; init; }
    public int VoxelY { get; init; }
    public int VoxelZ { get; init; }
    public TBlock Block { get; init; }
}

internal struct VoxelDdaWalker<TBlock>
{
    private readonly IVoxelWorld<TBlock> _world;
    private readonly Vector3 _origin;
    private readonly Vector3 _direction;
    private readonly float _maxDistance;
    private readonly int _maxSteps;
    private readonly ChunkDimensions _dims;
    private readonly int _chunkCountX;
    private readonly int _chunkCountY;
    private readonly int _chunkCountZ;
    private VoxelBlockAccessCache<TBlock> _cache;

    private int _mapX;
    private int _mapY;
    private int _mapZ;

    private int _stepX;
    private int _stepY;
    private int _stepZ;

    private float _sideDistX;
    private float _sideDistY;
    private float _sideDistZ;

    private float _deltaDistX;
    private float _deltaDistY;
    private float _deltaDistZ;

    private int _chunkX;
    private int _chunkY;
    private int _chunkZ;

    private int _localX;
    private int _localY;
    private int _localZ;

    private int _stepsTaken;

    public VoxelDdaWalker(
        IVoxelWorld<TBlock> world,
        Vector3 origin,
        Vector3 direction,
        float maxDistance,
        int maxSteps)
    {
        _world = world;
        _origin = origin;
        _direction = direction;
        _maxDistance = MathF.Max(0.01f, maxDistance);
        _maxSteps = Math.Max(1, maxSteps);
        _dims = world.ChunkSize;
        _chunkCountX = world.ChunkCountX;
        _chunkCountY = world.ChunkCountY;
        _chunkCountZ = world.ChunkCountZ;
        _cache = default;

        _mapX = FloorToInt(origin.X);
        _mapY = FloorToInt(origin.Y);
        _mapZ = FloorToInt(origin.Z);

        _stepX = direction.X < 0f ? -1 : 1;
        _stepY = direction.Y < 0f ? -1 : 1;
        _stepZ = direction.Z < 0f ? -1 : 1;

        float invDirX = direction.X == 0f ? 0f : 1f / direction.X;
        float invDirY = direction.Y == 0f ? 0f : 1f / direction.Y;
        float invDirZ = direction.Z == 0f ? 0f : 1f / direction.Z;

        _deltaDistX = MathF.Abs(invDirX);
        _deltaDistY = MathF.Abs(invDirY);
        _deltaDistZ = MathF.Abs(invDirZ);

        float nextBoundaryX = _stepX > 0 ? _mapX + 1f : _mapX;
        float nextBoundaryY = _stepY > 0 ? _mapY + 1f : _mapY;
        float nextBoundaryZ = _stepZ > 0 ? _mapZ + 1f : _mapZ;

        _sideDistX = (_stepX > 0 ? (nextBoundaryX - origin.X) : (origin.X - nextBoundaryX)) * _deltaDistX;
        _sideDistY = (_stepY > 0 ? (nextBoundaryY - origin.Y) : (origin.Y - nextBoundaryY)) * _deltaDistY;
        _sideDistZ = (_stepZ > 0 ? (nextBoundaryZ - origin.Z) : (origin.Z - nextBoundaryZ)) * _deltaDistZ;

        if (!float.IsFinite(_sideDistX)) _sideDistX = 0f;
        if (!float.IsFinite(_sideDistY)) _sideDistY = 0f;
        if (!float.IsFinite(_sideDistZ)) _sideDistZ = 0f;

        SplitCoordinate(_mapX, _dims.SizeX, out _chunkX, out _localX);
        SplitCoordinate(_mapY, _dims.SizeY, out _chunkY, out _localY);
        SplitCoordinate(_mapZ, _dims.SizeZ, out _chunkZ, out _localZ);

        _stepsTaken = 0;
    }

    public bool TryStep(out VoxelDdaHit<TBlock> hit)
    {
        hit = default;

        if (_stepsTaken >= _maxSteps)
        {
            return false;
        }

        float traveled;
        BlockFace face;

        if (_sideDistX < _sideDistY)
        {
            if (_sideDistX < _sideDistZ)
            {
                _mapX += _stepX;
                _localX += _stepX;
                NormalizeAxis(ref _chunkX, ref _localX, _dims.SizeX);
                traveled = _sideDistX;
                _sideDistX += _deltaDistX;
                face = _stepX > 0 ? BlockFace.NegativeX : BlockFace.PositiveX;
            }
            else
            {
                _mapZ += _stepZ;
                _localZ += _stepZ;
                NormalizeAxis(ref _chunkZ, ref _localZ, _dims.SizeZ);
                traveled = _sideDistZ;
                _sideDistZ += _deltaDistZ;
                face = _stepZ > 0 ? BlockFace.NegativeZ : BlockFace.PositiveZ;
            }
        }
        else
        {
            if (_sideDistY < _sideDistZ)
            {
                _mapY += _stepY;
                _localY += _stepY;
                NormalizeAxis(ref _chunkY, ref _localY, _dims.SizeY);
                traveled = _sideDistY;
                _sideDistY += _deltaDistY;
                face = _stepY > 0 ? BlockFace.NegativeY : BlockFace.PositiveY;
            }
            else
            {
                _mapZ += _stepZ;
                _localZ += _stepZ;
                NormalizeAxis(ref _chunkZ, ref _localZ, _dims.SizeZ);
                traveled = _sideDistZ;
                _sideDistZ += _deltaDistZ;
                face = _stepZ > 0 ? BlockFace.NegativeZ : BlockFace.PositiveZ;
            }
        }

        _stepsTaken++;

        if (traveled < 0f)
        {
            traveled = 0f;
        }

        if (!float.IsFinite(traveled) || traveled >= _maxDistance)
        {
            return false;
        }

        if (_chunkX < 0 || _chunkX >= _chunkCountX ||
            _chunkY < 0 || _chunkY >= _chunkCountY ||
            _chunkZ < 0 || _chunkZ >= _chunkCountZ)
        {
            hit = new VoxelDdaHit<TBlock>
            {
                Kind = VoxelDdaHitKind.Sky,
                Face = face,
                Distance = traveled,
                VoxelX = _mapX,
                VoxelY = _mapY,
                VoxelZ = _mapZ,
                Block = default!
            };
            return true;
        }

        TBlock block = _world.GetBlockFast(_chunkX, _chunkY, _chunkZ, _localX, _localY, _localZ, ref _cache);

        hit = new VoxelDdaHit<TBlock>
        {
            Kind = VoxelDdaHitKind.Block,
            Face = face,
            Distance = traveled,
            VoxelX = _mapX,
            VoxelY = _mapY,
            VoxelZ = _mapZ,
            Block = block
        };
        return true;
    }

    private static void SplitCoordinate(int value, int size, out int chunk, out int local)
    {
        chunk = value / size;
        local = value - chunk * size;
        if (local < 0)
        {
            local += size;
            chunk -= 1;
        }
    }

    private static void NormalizeAxis(ref int chunk, ref int local, int size)
    {
        if (local >= size)
        {
            local -= size;
            chunk += 1;
        }
        else if (local < 0)
        {
            local += size;
            chunk -= 1;
        }
    }

    private static int FloorToInt(float value)
    {
        return value >= 0f ? (int)value : (int)MathF.Floor(value);
    }
}
