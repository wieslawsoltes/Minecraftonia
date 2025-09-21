using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Minecraftonia.App.Game;

public sealed class GameControl : Control
{
    private const float FieldOfViewDegrees = 70f;
    private const float InteractionDistance = 6.5f;

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _lastTicks;

    private readonly HashSet<Key> _keysDown = new();
    private readonly HashSet<Key> _keysPressed = new();

    private bool _breakQueued;
    private bool _placeQueued;

    private readonly VoxelWorld _world;
    private readonly Player _player;
    private readonly BlockTextures _textures;
    private WriteableBitmap? _framebuffer;
    private readonly PixelSize _renderSize = new PixelSize(360, 202);

    private Vector3 _cameraForward = Vector3.UnitZ;
    private Vector3 _cameraRight = Vector3.UnitX;
    private Vector3 _cameraUp = Vector3.UnitY;
    private float _cameraTanHalfFov;
    private float _cameraAspect;

    private RaycastHit _currentHit;
    private bool _hasCurrentHit;

    private readonly Typeface _hudTypeface = Typeface.Default;
    private float _smoothedFps = 60f;

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

    private int _selectedIndex;

    public GameControl()
    {
        Focusable = true;
        ClipToBounds = true;

        _world = new VoxelWorld(96, 48, 96);
        _textures = new BlockTextures();

        _player = new Player
        {
            Position = new Vector3(_world.Width / 2f, _world.WaterLevel + 8f, _world.Depth / 2f),
            Yaw = 180f,
            Pitch = -12f
        };

        _cameraTanHalfFov = MathF.Tan(FieldOfViewDegrees * 0.5f * MathF.PI / 180f);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _lastTicks = _stopwatch.ElapsedTicks;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_timer.IsEnabled)
        {
            _lastTicks = _stopwatch.ElapsedTicks;
            _timer.Start();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_timer.IsEnabled)
        {
            _timer.Stop();
        }

        _framebuffer?.Dispose();
        _framebuffer = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var currentTicks = _stopwatch.ElapsedTicks;
        var delta = (currentTicks - _lastTicks) / (double)Stopwatch.Frequency;
        _lastTicks = currentTicks;

        if (delta <= 0)
        {
            delta = 0.001;
        }

        delta = Math.Min(delta, 0.1);
        UpdateGame((float)delta);
    }

    private void UpdateGame(float deltaTime)
    {
        HandleLook(deltaTime);
        HandleMovement(deltaTime);
        UpdateSelection();
        HandleInteractions();
        RenderScene();

        float instantaneousFps = deltaTime > 0.0001f ? 1f / deltaTime : 0f;
        _smoothedFps += (instantaneousFps - _smoothedFps) * 0.1f;

        _keysPressed.Clear();
        InvalidateVisual();
    }

    private void HandleLook(float deltaTime)
    {
        float lookSpeed = 95f;

        if (IsKeyDown(Key.Left))
        {
            _player.Yaw -= lookSpeed * deltaTime;
        }

        if (IsKeyDown(Key.Right))
        {
            _player.Yaw += lookSpeed * deltaTime;
        }

        if (IsKeyDown(Key.Up))
        {
            _player.Pitch += lookSpeed * deltaTime;
        }

        if (IsKeyDown(Key.Down))
        {
            _player.Pitch -= lookSpeed * deltaTime;
        }

        if (IsKeyDown(Key.Q))
        {
            _player.Yaw -= lookSpeed * 0.5f * deltaTime;
        }

        if (IsKeyDown(Key.E))
        {
            _player.Yaw += lookSpeed * 0.5f * deltaTime;
        }

        _player.Pitch = Math.Clamp(_player.Pitch, -85f, 85f);

        if (_player.Yaw < 0f)
        {
            _player.Yaw += 360f;
        }
        else if (_player.Yaw >= 360f)
        {
            _player.Yaw -= 360f;
        }
    }

    private void HandleMovement(float deltaTime)
    {
        Vector3 forward = _player.Forward;
        Vector3 right = _player.Right;
        Vector3 forwardPlane = new(forward.X, 0f, forward.Z);
        Vector3 rightPlane = new(right.X, 0f, right.Z);

        if (forwardPlane.LengthSquared() < 0.0001f)
        {
            forwardPlane = Vector3.UnitZ;
        }
        else
        {
            forwardPlane = Vector3.Normalize(forwardPlane);
        }

        if (rightPlane.LengthSquared() < 0.0001f)
        {
            rightPlane = Vector3.UnitX;
        }
        else
        {
            rightPlane = Vector3.Normalize(rightPlane);
        }

        Vector3 wishDir = Vector3.Zero;

        if (IsKeyDown(Key.W))
        {
            wishDir += forwardPlane;
        }

        if (IsKeyDown(Key.S))
        {
            wishDir -= forwardPlane;
        }

        if (IsKeyDown(Key.A))
        {
            wishDir -= rightPlane;
        }

        if (IsKeyDown(Key.D))
        {
            wishDir += rightPlane;
        }

        if (wishDir.LengthSquared() > 0.0001f)
        {
            wishDir = Vector3.Normalize(wishDir);
        }

        float moveSpeed = IsKeyDown(Key.LeftShift) ? 3.5f : 6.5f;
        Vector3 horizontalVelocity = wishDir * moveSpeed;
        _player.Velocity = new Vector3(horizontalVelocity.X, _player.Velocity.Y, horizontalVelocity.Z);

        if (IsKeyPressed(Key.Space) && _player.IsOnGround)
        {
            _player.Velocity = new Vector3(_player.Velocity.X, 8.2f, _player.Velocity.Z);
            _player.IsOnGround = false;
        }

        _player.Velocity = new Vector3(_player.Velocity.X, MathF.Max(_player.Velocity.Y - 20f * deltaTime, -40f), _player.Velocity.Z);

        Vector3 position = _player.Position;
        Vector3 velocity = _player.Velocity;
        bool grounded = MoveWithCollisions(ref position, ref velocity, deltaTime);

        _player.Position = position;
        _player.Velocity = velocity;
        _player.IsOnGround = grounded;

        float minY = 1.0f;
        float maxY = _world.Height - 2f;
        _player.Position = new Vector3(
            Math.Clamp(_player.Position.X, 1.5f, _world.Width - 1.5f),
            Math.Clamp(_player.Position.Y, minY, maxY),
            Math.Clamp(_player.Position.Z, 1.5f, _world.Depth - 1.5f));
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
        float movement = velocityComponent * deltaTime;
        if (MathF.Abs(movement) < 0.00001f)
        {
            return false;
        }

        float sign = MathF.Sign(movement);
        float remaining = MathF.Abs(movement);
        bool grounded = false;

        const float stepSize = 0.1f;

        while (remaining > 0f)
        {
            float step = MathF.Min(remaining, stepSize);
            float delta = step * sign;

            switch (axis)
            {
                case 0:
                    position = new Vector3(position.X + delta, position.Y, position.Z);
                    break;
                case 1:
                    position = new Vector3(position.X, position.Y + delta, position.Z);
                    break;
                case 2:
                    position = new Vector3(position.X, position.Y, position.Z + delta);
                    break;
            }

            if (Collides(position))
            {
                switch (axis)
                {
                    case 0:
                        position = new Vector3(position.X - delta, position.Y, position.Z);
                        break;
                    case 1:
                        position = new Vector3(position.X, position.Y - delta, position.Z);
                        if (sign < 0f)
                        {
                            grounded = true;
                        }
                        break;
                    case 2:
                        position = new Vector3(position.X, position.Y, position.Z - delta);
                        break;
                }

                velocityComponent = 0f;
                break;
            }

            remaining -= step;
        }

        return grounded;
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
                    if (!_world.InBounds(x, y, z))
                    {
                        return true;
                    }

                    if (_world.GetBlock(x, y, z).IsSolid())
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
        Vector3 origin = _player.EyePosition;
        Vector3 direction = _player.Forward;

        _hasCurrentHit = TryPickBlock(origin, direction, InteractionDistance, out _currentHit);
    }

    private void HandleInteractions()
    {
        if (_breakQueued && _hasCurrentHit)
        {
            BreakTargetBlock();
            _breakQueued = false;
        }

        if (_placeQueued && _hasCurrentHit)
        {
            PlaceBlockAtHit();
            _placeQueued = false;
        }

        HandlePaletteInput();
    }

    private void HandlePaletteInput()
    {
        if (IsKeyPressed(Key.D1)) _selectedIndex = 0;
        if (IsKeyPressed(Key.D2)) _selectedIndex = Math.Min(1, _palette.Length - 1);
        if (IsKeyPressed(Key.D3)) _selectedIndex = Math.Min(2, _palette.Length - 1);
        if (IsKeyPressed(Key.D4)) _selectedIndex = Math.Min(3, _palette.Length - 1);
        if (IsKeyPressed(Key.D5)) _selectedIndex = Math.Min(4, _palette.Length - 1);
        if (IsKeyPressed(Key.D6)) _selectedIndex = Math.Min(5, _palette.Length - 1);
        if (IsKeyPressed(Key.D7)) _selectedIndex = Math.Min(6, _palette.Length - 1);

        if (IsKeyPressed(Key.Tab))
        {
            _selectedIndex = (_selectedIndex + 1) % _palette.Length;
        }
    }

    private void BreakTargetBlock()
    {
        if (_currentHit.BlockType == BlockType.Air)
        {
            return;
        }

        _world.SetBlock(_currentHit.Block, BlockType.Air);
    }

    private void PlaceBlockAtHit()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _palette.Length)
        {
            return;
        }

        Int3 target = _currentHit.Block + _currentHit.Face.ToOffset();
        if (!_world.InBounds(target.X, target.Y, target.Z))
        {
            return;
        }

        var existing = _world.GetBlock(target);
        if (existing != BlockType.Air && existing != BlockType.Water && existing != BlockType.Leaves)
        {
            return;
        }

        if (BlockIntersectsPlayer(target))
        {
            return;
        }

        BlockType blockToPlace = _palette[_selectedIndex];
        _world.SetBlock(target, blockToPlace);
    }

    private bool BlockIntersectsPlayer(Int3 block)
    {
        Vector3 blockMin = block.ToVector3();
        Vector3 blockMax = blockMin + Vector3.One;

        float halfWidth = 0.3f;
        float height = 1.8f;
        Vector3 playerMin = new(_player.Position.X - halfWidth, _player.Position.Y, _player.Position.Z - halfWidth);
        Vector3 playerMax = new(_player.Position.X + halfWidth, _player.Position.Y + height, _player.Position.Z + halfWidth);

        return blockMin.X < playerMax.X && blockMax.X > playerMin.X &&
               blockMin.Y < playerMax.Y && blockMax.Y > playerMin.Y &&
               blockMin.Z < playerMax.Z && blockMax.Z > playerMin.Z;
    }

    private void RenderScene()
    {
        EnsureFramebuffer();
        if (_framebuffer is null)
        {
            return;
        }

        Vector3 eye = _player.EyePosition;
        Vector3 forward = Vector3.Normalize(_player.Forward);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        if (right.LengthSquared() < 0.0001f)
        {
            right = Vector3.UnitX;
        }

        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));

        _cameraForward = forward;
        _cameraRight = right;
        _cameraUp = up;
        _cameraAspect = _renderSize.Width / (float)_renderSize.Height;
        _cameraTanHalfFov = MathF.Tan(FieldOfViewDegrees * 0.5f * MathF.PI / 180f);

        using var fb = _framebuffer.Lock();
        unsafe
        {
            byte* buffer = (byte*)fb.Address;
            int stride = fb.RowBytes;
            int width = _framebuffer.PixelSize.Width;
            int height = _framebuffer.PixelSize.Height;

            for (int y = 0; y < height; y++)
            {
                byte* row = buffer + y * stride;
                float ndcY = 1f - ((y + 0.5f) / height) * 2f;

                for (int x = 0; x < width; x++)
                {
                    float ndcX = ((x + 0.5f) / width) * 2f - 1f;
                    Vector3 dir = forward
                                   + ndcX * _cameraAspect * _cameraTanHalfFov * right
                                   + ndcY * _cameraTanHalfFov * up;
                    dir = Vector3.Normalize(dir);

                    Vector4 sample = TraceRay(eye, dir, 90f, out float distance);
                    sample = ApplyFog(sample, distance);
                    WritePixel(row, x, sample);
                }
            }
        }
    }

    private Vector4 TraceRay(Vector3 origin, Vector3 direction, float maxDistance, out float outDistance)
    {
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

        Vector3 accumColor = Vector3.Zero;
        float accumAlpha = 0f;
        float hitDistance = maxDistance;

        const int maxSteps = 512;

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

            if (!_world.InBounds(mapX, mapY, mapZ))
            {
                continue;
            }

            BlockType block = _world.GetBlock(mapX, mapY, mapZ);
            if (block == BlockType.Air)
            {
                continue;
            }

            Vector3 hitPoint = origin + direction * traveled;
            Vector3 local = hitPoint - new Vector3(mapX, mapY, mapZ);
            Vector2 uv = ComputeFaceUV(face, local);
            Vector4 sample = _textures.Sample(block, face, uv.X, uv.Y);
            float light = GetFaceLight(face);
            Vector3 rgb = new(sample.X, sample.Y, sample.Z);
            rgb *= light;

            float opacity = sample.W;
            if (block.IsSolid())
            {
                opacity = 1f;
            }
            else if (block == BlockType.Water)
            {
                opacity = MathF.Min(0.6f, opacity + 0.1f);
                rgb *= 0.85f;
            }
            else if (block == BlockType.Leaves)
            {
                opacity = MathF.Min(0.65f, opacity);
            }

            accumColor += (1f - accumAlpha) * opacity * rgb;
            accumAlpha += (1f - accumAlpha) * opacity;
            hitDistance = traveled;

            if (accumAlpha >= 0.995f || block.IsSolid())
            {
                break;
            }
        }

        if (accumAlpha < 0.995f)
        {
            Vector3 sky = SampleSky(direction);
            accumColor += (1f - accumAlpha) * sky;
            accumAlpha = 1f;
            hitDistance = maxDistance;
        }

        accumColor = Vector3.Clamp(accumColor, Vector3.Zero, Vector3.One);
        outDistance = hitDistance;
        return new Vector4(accumColor, 1f);
    }

    private bool TryPickBlock(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
    {
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

            if (!_world.InBounds(mapX, mapY, mapZ))
            {
                continue;
            }

            BlockType block = _world.GetBlock(mapX, mapY, mapZ);
            if (block == BlockType.Air)
            {
                continue;
            }

            Vector3 hitPoint = origin + direction * traveled;
            hit = new RaycastHit(new Int3(mapX, mapY, mapZ), face, block, hitPoint, traveled);
            return true;
        }

        hit = default;
        return false;
    }

    private static Vector2 ComputeFaceUV(BlockFace face, Vector3 local)
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

    private static float GetFaceLight(BlockFace face)
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

    private static Vector4 ApplyFog(Vector4 color, float distance)
    {
        float fogStart = 45f;
        float fogEnd = 90f;
        if (distance <= fogStart)
        {
            return color;
        }

        float fogFactor = Math.Clamp((distance - fogStart) / (fogEnd - fogStart), 0f, 1f);
        var fogColor = new Vector3(0.72f, 0.84f, 0.96f);
        var rgb = new Vector3(color.X, color.Y, color.Z);
        rgb = Vector3.Lerp(rgb, fogColor, fogFactor);
        return new Vector4(rgb, color.W);
    }

    private static Vector3 SampleSky(Vector3 direction)
    {
        direction = Vector3.Normalize(direction);
        float t = Math.Clamp(direction.Y * 0.5f + 0.5f, 0f, 1f);
        Vector3 horizon = new(0.78f, 0.87f, 0.95f);
        Vector3 zenith = new(0.18f, 0.32f, 0.58f);
        Vector3 sky = Vector3.Lerp(horizon, zenith, t);

        Vector3 sunDirection = Vector3.Normalize(new Vector3(-0.35f, 0.88f, 0.25f));
        float sunFactor = MathF.Max(0f, Vector3.Dot(direction, sunDirection));
        float sunGlow = MathF.Pow(sunFactor, 32f) * 0.35f;
        sky += new Vector3(1f, 0.93f, 0.78f) * sunGlow;

        return Vector3.Clamp(sky, Vector3.Zero, Vector3.One);
    }

    private static unsafe void WritePixel(byte* row, int x, Vector4 color)
    {
        float alpha = Math.Clamp(color.W, 0f, 1f);
        Vector3 rgb = new(color.X, color.Y, color.Z);
        rgb = Vector3.Clamp(rgb, Vector3.Zero, Vector3.One);
        Vector3 premul = rgb * alpha;

        int index = x * 4;
        row[index + 0] = (byte)(premul.Z * 255f);
        row[index + 1] = (byte)(premul.Y * 255f);
        row[index + 2] = (byte)(premul.X * 255f);
        row[index + 3] = (byte)(alpha * 255f);
    }

    private void EnsureFramebuffer()
    {
        if (_framebuffer is null || _framebuffer.PixelSize != _renderSize)
        {
            _framebuffer?.Dispose();
            _framebuffer = new WriteableBitmap(_renderSize, new Avalonia.Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!_keysDown.Contains(e.Key))
        {
            _keysDown.Add(e.Key);
            _keysPressed.Add(e.Key);
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _keysDown.Remove(e.Key);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed)
        {
            _breakQueued = true;
        }

        if (properties.IsRightButtonPressed)
        {
            _placeQueued = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Delta.Y > 0)
        {
            _selectedIndex = (_selectedIndex + 1) % _palette.Length;
        }
        else if (e.Delta.Y < 0)
        {
            _selectedIndex = (_selectedIndex - 1 + _palette.Length) % _palette.Length;
        }
    }

    private bool IsKeyDown(Key key) => _keysDown.Contains(key);
    private bool IsKeyPressed(Key key) => _keysPressed.Contains(key);

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_framebuffer != null)
        {
            var sourceRect = new Rect(0, 0, _framebuffer.PixelSize.Width, _framebuffer.PixelSize.Height);
            var destRect = new Rect(Bounds.Size);
            context.DrawImage(_framebuffer, sourceRect, destRect);
        }

        DrawCrosshair(context);
        DrawHud(context);
    }

    private void DrawCrosshair(DrawingContext context)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var pen = new Pen(Brushes.White, 1);
        context.DrawLine(pen, center + new Avalonia.Vector(-9, 0), center + new Avalonia.Vector(9, 0));
        context.DrawLine(pen, center + new Avalonia.Vector(0, -9), center + new Avalonia.Vector(0, 9));
    }

    private void DrawHud(DrawingContext context)
    {
        const double padding = 10;
        var backgroundBrush = new SolidColorBrush(Color.FromArgb(180, 20, 20, 24));
        var border = new Pen(Brushes.White, 1, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        string selectedName = _palette[_selectedIndex].ToString();
        var selectedLayout = new TextLayout($"[{_selectedIndex + 1}] {selectedName}", _hudTypeface, 18, Brushes.White);
        var hudRect = new Rect(
            padding,
            Bounds.Height - selectedLayout.Height - padding * 2,
            selectedLayout.Width + padding * 2,
            selectedLayout.Height + padding * 1.6);

        context.FillRectangle(backgroundBrush, hudRect);
        context.DrawRectangle(border, hudRect);
        selectedLayout.Draw(context, hudRect.TopLeft + new Avalonia.Vector(padding * 0.6, padding * 0.3));

        var fpsLayout = new TextLayout($"{_smoothedFps,5:0} fps", _hudTypeface, 14, Brushes.White);
        fpsLayout.Draw(context, new Point(padding, padding));

        string[] infoLines =
        {
            "WASD move, Space jump",
            "Arrow keys / Q,E look",
            "Mouse: left break, right place",
            "Wheel/1-7 choose block"
        };

        double yOffset = padding + fpsLayout.Height + padding * 0.8;
        foreach (var line in infoLines)
        {
            var infoLayout = new TextLayout(line, _hudTypeface, 13, Brushes.White);
            infoLayout.Draw(context, new Point(padding, yOffset));
            yOffset += infoLayout.Height + 2;
        }

        if (_hasCurrentHit)
        {
            var hitLayout = new TextLayout(
                $"Target: {_currentHit.BlockType} @ {_currentHit.Block.X},{_currentHit.Block.Y},{_currentHit.Block.Z}",
                _hudTypeface,
                14,
                Brushes.White);

            var textPos = new Point(Bounds.Width / 2 - hitLayout.Width / 2, Bounds.Height * 0.12);
            hitLayout.Draw(context, textPos);
        }
    }
}
