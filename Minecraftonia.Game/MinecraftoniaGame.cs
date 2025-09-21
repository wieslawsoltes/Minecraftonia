using System;
using System.Collections.Generic;
using System.Numerics;
using Minecraftonia.VoxelEngine;
using Minecraftonia.VoxelRendering;

namespace Minecraftonia.Game;

/// <summary>
/// Encapsulates simulation state and core gameplay logic so frontends can remain UI-focused.
/// </summary>
public sealed class MinecraftoniaGame
{
    public const float InteractionDistance = 6.5f;

    private readonly BlockType[] _palette =
    {
        BlockType.Grass,
        BlockType.Dirt,
        BlockType.Stone,
        BlockType.Sand,
        BlockType.Wood,
        BlockType.Leaves,
        BlockType.Water
    };

    private VoxelRaycastHit<BlockType> _currentHit;
    private bool _hasCurrentHit;

    public MinecraftoniaVoxelWorld World { get; }
    public Player Player { get; }
    public BlockTextures Textures { get; }

    public IReadOnlyList<BlockType> Palette => _palette;
    public int SelectedPaletteIndex { get; private set; }
    public BlockType SelectedBlock => _palette[SelectedPaletteIndex];

    public bool HasCurrentHit => _hasCurrentHit;
    public VoxelRaycastHit<BlockType> CurrentHit => _currentHit;

    public Vector3 DebugWishDirection { get; private set; }
    public Vector3 DebugVelocity { get; private set; }
    public bool DebugGrounded { get; private set; }

    public MinecraftoniaGame(
        int width,
        int height,
        int depth,
        int waterLevel = 8,
        int seed = 1337,
        float initialYaw = 180f,
        float initialPitch = -12f,
        BlockTextures? textures = null)
        : this(
            new MinecraftoniaVoxelWorld(width, height, depth, waterLevel, seed),
            textures ?? new BlockTextures(),
            new Player
            {
                Yaw = initialYaw,
                Pitch = initialPitch
            },
            selectedPaletteIndex: 0,
            initializeSpawn: true)
    {
    }

    private MinecraftoniaGame(
        MinecraftoniaVoxelWorld world,
        BlockTextures textures,
        Player player,
        int selectedPaletteIndex,
        bool initializeSpawn)
    {
        World = world;
        Textures = textures;
        Player = player;
        SelectedPaletteIndex = Math.Clamp(selectedPaletteIndex, 0, _palette.Length - 1);

        if (initializeSpawn)
        {
            InitializePlayerPosition();
        }
    }

    public static MinecraftoniaGame FromSave(GameSaveData save, BlockTextures? textures = null)
    {
        if (save.Version != GameSaveData.CurrentVersion)
        {
            throw new NotSupportedException($"Unsupported save version: {save.Version}");
        }

        int expected = save.Width * save.Height * save.Depth;
        if (save.Blocks.Length != expected)
        {
            throw new InvalidOperationException($"Corrupted save: expected {expected} blocks, found {save.Blocks.Length}.");
        }

        var blocks = new BlockType[save.Blocks.Length];
        for (int i = 0; i < blocks.Length; i++)
        {
            blocks[i] = (BlockType)save.Blocks[i];
        }

        var world = new MinecraftoniaVoxelWorld(
            save.Width,
            save.Height,
            save.Depth,
            save.WaterLevel,
            save.Seed,
            blocks);

        var player = new Player
        {
            Position = new Vector3(save.Player.X, save.Player.Y, save.Player.Z),
            Velocity = new Vector3(save.Player.VelocityX, save.Player.VelocityY, save.Player.VelocityZ),
            Yaw = save.Player.Yaw,
            Pitch = save.Player.Pitch,
            IsOnGround = save.Player.IsOnGround,
            EyeHeight = save.Player.EyeHeight
        };

        var texturesInstance = textures ?? new BlockTextures();

        return new MinecraftoniaGame(world, texturesInstance, player, save.SelectedPaletteIndex, initializeSpawn: false);
    }

    public GameSaveData CreateSaveData()
    {
        var blocks = CaptureWorldBlocks();

        return new GameSaveData
        {
            Width = World.Width,
            Height = World.Height,
            Depth = World.Depth,
            WaterLevel = World.WaterLevel,
            Seed = World.Seed,
            Blocks = blocks,
            SelectedPaletteIndex = SelectedPaletteIndex,
            Player = new PlayerSaveData
            {
                X = Player.Position.X,
                Y = Player.Position.Y,
                Z = Player.Position.Z,
                VelocityX = Player.Velocity.X,
                VelocityY = Player.Velocity.Y,
                VelocityZ = Player.Velocity.Z,
                Yaw = Player.Yaw,
                Pitch = Player.Pitch,
                IsOnGround = Player.IsOnGround,
                EyeHeight = Player.EyeHeight
            }
        };
    }

    private byte[] CaptureWorldBlocks()
    {
        var data = new byte[World.Width * World.Height * World.Depth];
        int index = 0;
        for (int x = 0; x < World.Width; x++)
        {
            for (int y = 0; y < World.Height; y++)
            {
                for (int z = 0; z < World.Depth; z++)
                {
                    data[index++] = (byte)World.GetBlock(x, y, z);
                }
            }
        }

        return data;
    }

    public void Update(in GameInputState input, float deltaTime)
    {
        ApplyLook(input, deltaTime);
        ApplyMovement(input, deltaTime);
        UpdateSelection();
        HandleInteractions(input);
    }

    private void ApplyLook(in GameInputState input, float deltaTime)
    {
        const float lookSpeed = 95f;

        Player.Yaw += input.KeyboardYaw * lookSpeed * deltaTime;
        Player.Pitch += input.KeyboardPitch * lookSpeed * deltaTime;

        if (Math.Abs(input.MouseYawDelta) > float.Epsilon || Math.Abs(input.MousePitchDelta) > float.Epsilon)
        {
            Player.Yaw += input.MouseYawDelta;
            Player.Pitch += input.MousePitchDelta;
        }

        if (Player.Yaw < 0f)
        {
            Player.Yaw += 360f;
        }
        else if (Player.Yaw >= 360f)
        {
            Player.Yaw -= 360f;
        }

        Player.Pitch = Math.Clamp(Player.Pitch, -85f, 85f);
    }

    private void ApplyMovement(in GameInputState input, float deltaTime)
    {
        Vector3 forward = Player.Forward;
        Vector3 right = Player.Right;
        Vector3 forwardPlane = new(forward.X, 0f, forward.Z);
        Vector3 rightPlane = new(-right.X, 0f, -right.Z);

        forwardPlane = forwardPlane.LengthSquared() < 0.0001f
            ? Vector3.UnitZ
            : Vector3.Normalize(forwardPlane);

        rightPlane = rightPlane.LengthSquared() < 0.0001f
            ? Vector3.UnitX
            : Vector3.Normalize(rightPlane);

        Vector3 wishDir = Vector3.Zero;

        if (input.MoveForward)
        {
            wishDir += forwardPlane;
        }

        if (input.MoveBackward)
        {
            wishDir -= forwardPlane;
        }

        if (input.MoveLeft)
        {
            wishDir -= rightPlane;
        }

        if (input.MoveRight)
        {
            wishDir += rightPlane;
        }

        if (wishDir.LengthSquared() > 0.0001f)
        {
            wishDir = Vector3.Normalize(wishDir);
        }

        DebugWishDirection = wishDir;

        float moveSpeed = input.Sprint ? 3.5f : 6.5f;
        Vector3 horizontalVelocity = wishDir * moveSpeed;
        Player.Velocity = new Vector3(horizontalVelocity.X, Player.Velocity.Y, horizontalVelocity.Z);

        if (input.Jump && Player.IsOnGround)
        {
            Player.Velocity = new Vector3(Player.Velocity.X, 8.2f, Player.Velocity.Z);
            Player.IsOnGround = false;
        }

        Player.Velocity = new Vector3(Player.Velocity.X, MathF.Max(Player.Velocity.Y - 20f * deltaTime, -40f), Player.Velocity.Z);

        Vector3 position = Player.Position;
        Vector3 velocity = Player.Velocity;
        bool grounded = MoveWithCollisions(ref position, ref velocity, deltaTime);

        Player.Position = position;
        Player.Velocity = velocity;
        Player.IsOnGround = grounded;

        DebugVelocity = velocity;
        DebugGrounded = grounded;

        float minY = 1f;
        float maxY = World.Height - 2f;
        Player.Position = new Vector3(
            Math.Clamp(Player.Position.X, 1.5f, World.Width - 1.5f),
            Math.Clamp(Player.Position.Y, minY, maxY),
            Math.Clamp(Player.Position.Z, 1.5f, World.Depth - 1.5f));
    }

    private void InitializePlayerPosition()
    {
        Vector3 spawn = FindSpawnPosition();
        Player.Position = spawn;
        Player.Velocity = Vector3.Zero;
        Player.IsOnGround = false;

        ResolveInitialPenetration();
    }

    private Vector3 FindSpawnPosition()
    {
        int centerX = World.Width / 2;
        int centerZ = World.Depth / 2;

        Span<(int dx, int dz)> offsets = stackalloc (int dx, int dz)[]
        {
            (0, 0),
            (1, 0), (-1, 0), (0, 1), (0, -1),
            (2, 0), (-2, 0), (0, 2), (0, -2),
            (1, 1), (-1, 1), (1, -1), (-1, -1),
            (3, 0), (-3, 0), (0, 3), (0, -3)
        };

        foreach (var (dx, dz) in offsets)
        {
            int x = centerX + dx;
            int z = centerZ + dz;
            if (TryFindSurface(x, z, out var spawn))
            {
                return spawn;
            }
        }

        return new Vector3(centerX + 0.5f, World.Height - 6f, centerZ + 0.5f);
    }

    private bool TryFindSurface(int x, int z, out Vector3 spawn)
    {
        spawn = default;

        if (!World.InBounds(x, 0, z))
        {
            return false;
        }

        for (int y = World.Height - 3; y >= 1; y--)
        {
            var block = World.GetBlock(x, y, z);
            if (!block.IsSolid())
            {
                continue;
            }

            if (block == BlockType.Water)
            {
                continue;
            }

            int headY = y + 1;
            int topY = y + 2;
            if (topY >= World.Height - 1)
            {
                continue;
            }

            if (World.GetBlock(x, headY, z).IsSolid())
            {
                continue;
            }

            if (World.GetBlock(x, topY, z).IsSolid())
            {
                continue;
            }

            spawn = new Vector3(x + 0.5f, headY + 0.02f, z + 0.5f);
            return true;
        }

        return false;
    }

    private void ResolveInitialPenetration()
    {
        const int maxIterations = 12;
        int iterations = 0;

        while (Collides(Player.Position))
        {
            Player.Position += Vector3.UnitY * 0.5f;
            iterations++;
            if (iterations >= maxIterations)
            {
                break;
            }
        }
    }

    private bool MoveWithCollisions(ref Vector3 position, ref Vector3 velocity, float deltaTime)
    {
        bool grounded = false;

        MoveAxis(ref position, ref velocity.X, deltaTime, axis: 0);
        MoveAxis(ref position, ref velocity.Z, deltaTime, axis: 2);
        grounded = MoveAxis(ref position, ref velocity.Y, deltaTime, axis: 1);

        return grounded;
    }

    private bool MoveAxis(ref Vector3 position, ref float velocityComponent, float deltaTime, int axis)
    {
        float delta = velocityComponent * deltaTime;
        if (Math.Abs(delta) < 0.0005f)
        {
            velocityComponent = 0f;
            return false;
        }

        Vector3 originalPosition = position;
        position = axis switch
        {
            0 => new Vector3(position.X + delta, position.Y, position.Z),
            1 => new Vector3(position.X, position.Y + delta, position.Z),
            2 => new Vector3(position.X, position.Y, position.Z + delta),
            _ => position
        };

        if (Collides(position))
        {
            if (axis != 1 && TryStepUp(ref position, axis, delta, originalPosition))
            {
                return false;
            }

            position = originalPosition;
            velocityComponent = 0f;

            if (axis == 1 && delta < 0f)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryStepUp(ref Vector3 position, int axis, float delta, Vector3 originalPosition)
    {
        const float stepHeight = 0.6f;
        Vector3 stepped = originalPosition + new Vector3(0f, stepHeight, 0f);

        if (Collides(stepped))
        {
            return false;
        }

        Vector3 target = axis switch
        {
            0 => new Vector3(stepped.X + delta, stepped.Y, stepped.Z),
            2 => new Vector3(stepped.X, stepped.Y, stepped.Z + delta),
            _ => stepped
        };

        if (Collides(target))
        {
            return false;
        }

        position = target;
        return true;
    }

    private bool Collides(Vector3 position)
    {
        float halfWidth = 0.3f;
        float height = 1.8f;

        int minX = (int)MathF.Floor(position.X - halfWidth);
        int maxX = (int)MathF.Floor(position.X + halfWidth);
        int minY = (int)MathF.Floor(position.Y);
        int maxY = (int)MathF.Floor(position.Y + height - 0.05f);
        int minZ = (int)MathF.Floor(position.Z - halfWidth);
        int maxZ = (int)MathF.Floor(position.Z + halfWidth);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (!World.InBounds(x, y, z))
                    {
                        return true;
                    }

                    if (World.GetBlock(x, y, z).IsSolid())
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void UpdateSelection()
    {
        Vector3 origin = Player.EyePosition;
        Vector3 direction = Player.Forward;

        _hasCurrentHit = VoxelRaycaster.TryPick(
            World,
            origin,
            direction,
            InteractionDistance,
            block => block == BlockType.Air,
            out _currentHit);
    }

    private void HandleInteractions(in GameInputState input)
    {
        if (input.BreakBlock && _hasCurrentHit)
        {
            BreakTargetBlock();
        }

        if (input.PlaceBlock && _hasCurrentHit)
        {
            PlaceBlockAtHit();
        }

        HandlePaletteInput(input);
    }

    private void HandlePaletteInput(in GameInputState input)
    {
        if (input.HotbarSelection.HasValue)
        {
            SelectedPaletteIndex = Math.Clamp(input.HotbarSelection.Value, 0, _palette.Length - 1);
        }

        if (input.HotbarScrollDelta != 0)
        {
            int length = _palette.Length;
            int delta = input.HotbarScrollDelta % length;
            if (delta < 0)
            {
                delta += length;
            }

            SelectedPaletteIndex = (SelectedPaletteIndex + delta) % length;
        }
    }

    private void BreakTargetBlock()
    {
        if (_currentHit.BlockType == BlockType.Air)
        {
            return;
        }

        World.SetBlock(_currentHit.Block, BlockType.Air);
    }

    private void PlaceBlockAtHit()
    {
        if (SelectedPaletteIndex < 0 || SelectedPaletteIndex >= _palette.Length)
        {
            return;
        }

        Int3 target = _currentHit.Block + _currentHit.Face.ToOffset();
        if (!World.InBounds(target.X, target.Y, target.Z))
        {
            return;
        }

        var existing = World.GetBlock(target);
        if (existing != BlockType.Air && existing != BlockType.Water && existing != BlockType.Leaves)
        {
            return;
        }

        if (BlockIntersectsPlayer(target))
        {
            return;
        }

        BlockType blockToPlace = _palette[SelectedPaletteIndex];
        World.SetBlock(target, blockToPlace);
    }

    private bool BlockIntersectsPlayer(Int3 block)
    {
        Vector3 blockMin = block.ToVector3();
        Vector3 blockMax = blockMin + Vector3.One;

        float halfWidth = 0.3f;
        float height = 1.8f;
        Vector3 playerMin = new(Player.Position.X - halfWidth, Player.Position.Y, Player.Position.Z - halfWidth);
        Vector3 playerMax = new(Player.Position.X + halfWidth, Player.Position.Y + height, Player.Position.Z + halfWidth);

        return blockMin.X < playerMax.X && blockMax.X > playerMin.X &&
               blockMin.Y < playerMax.Y && blockMax.Y > playerMin.Y &&
               blockMin.Z < playerMax.Z && blockMax.Z > playerMin.Z;
    }
}

/// <summary>
/// Raw player input captured by the frontend for the current update tick.
/// </summary>
public readonly record struct GameInputState(
    bool MoveForward,
    bool MoveBackward,
    bool MoveLeft,
    bool MoveRight,
    bool Sprint,
    bool Jump,
    float KeyboardYaw,
    float KeyboardPitch,
    float MouseYawDelta,
    float MousePitchDelta,
    bool BreakBlock,
    bool PlaceBlock,
    int? HotbarSelection,
    int HotbarScrollDelta);
