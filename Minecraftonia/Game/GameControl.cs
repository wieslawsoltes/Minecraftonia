using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using Avalonia.Threading;
using Minecraftonia.VoxelEngine;
using Minecraftonia.VoxelRendering;

namespace Minecraftonia.Game;

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

    private readonly MinecraftoniaVoxelWorld _world;
    private readonly Player _player;
    private readonly BlockTextures _textures;
    private readonly VoxelRayTracer<BlockType> _rayTracer;
    private WriteableBitmap? _framebuffer;
    private readonly PixelSize _renderSize = new PixelSize(360, 202);

    private VoxelCamera _camera;
    private bool _hasCamera;

    private VoxelRaycastHit<BlockType> _currentHit;
    private bool _hasCurrentHit;

    private readonly Typeface _hudTypeface = Typeface.Default;
    private float _smoothedFps = 60f;

    private bool _mouseLookEnabled;
    private Cursor? _previousCursor;
    private IPointer? _capturedPointer;
    private bool _requestPointerCapture;
    private float _pendingMouseDeltaX;
    private float _pendingMouseDeltaY;
    private Point? _lastPointerPosition;
    private bool _ignoreWarpPointerMove;
    private float _mouseSensitivity = 0.32f;
    private bool _invertMouseX = true;
    private bool _invertMouseY;

    private readonly HashSet<PhysicalKey> _physicalKeysDown = new();
    private readonly HashSet<PhysicalKey> _physicalKeysPressed = new();
    private TopLevel? _topLevel;

    private Vector3 _debugWishDir = Vector3.Zero;
    private Vector3 _debugVelocity = Vector3.Zero;
    private bool _debugGrounded;

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

        _world = new MinecraftoniaVoxelWorld(96, 48, 96);
        _textures = new BlockTextures();
        _rayTracer = new VoxelRayTracer<BlockType>(
            _renderSize,
            FieldOfViewDegrees,
            block => block.IsSolid(),
            block => block == BlockType.Air);

        _player = new Player
        {
            Yaw = 180f,
            Pitch = -12f
        };

        InitializePlayerPosition();

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

        _topLevel = TopLevel.GetTopLevel(this);
        SubscribeToTopLevelInput();

        if (!_mouseLookEnabled)
        {
            SetMouseLook(true);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_timer.IsEnabled)
        {
            _timer.Stop();
        }

        UnsubscribeFromTopLevelInput();
        _topLevel = null;

        if (_mouseLookEnabled)
        {
            SetMouseLook(false);
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
        HandleToggleInput();
        HandleLook(deltaTime);
        HandleMovement(deltaTime);
        UpdateSelection();
        HandleInteractions();
        RenderScene();

        float instantaneousFps = deltaTime > 0.0001f ? 1f / deltaTime : 0f;
        _smoothedFps += (instantaneousFps - _smoothedFps) * 0.1f;

        _keysPressed.Clear();
        _physicalKeysPressed.Clear();
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

        if (Math.Abs(_pendingMouseDeltaX) > float.Epsilon || Math.Abs(_pendingMouseDeltaY) > float.Epsilon)
        {
            float yawDelta = _pendingMouseDeltaX * _mouseSensitivity;
            float pitchDelta = _pendingMouseDeltaY * _mouseSensitivity;

            _player.Yaw += _invertMouseX ? -yawDelta : yawDelta;
            _player.Pitch += _invertMouseY ? pitchDelta : -pitchDelta;
            _pendingMouseDeltaX = 0f;
            _pendingMouseDeltaY = 0f;

            if (_player.Yaw < 0f)
            {
                _player.Yaw += 360f;
            }
            else if (_player.Yaw >= 360f)
            {
                _player.Yaw -= 360f;
            }

            _player.Pitch = Math.Clamp(_player.Pitch, -85f, 85f);
        }
    }

    private void HandleMovement(float deltaTime)
    {
        Vector3 forward = _player.Forward;
        Vector3 right = _player.Right;
        Vector3 forwardPlane = new(forward.X, 0f, forward.Z);
        Vector3 rightPlane = new(-right.X, 0f, -right.Z);

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

        if (IsMovementKeyDown(Key.W, Key.Z, Key.Up) || IsMovementPhysicalKeyDown(PhysicalKey.W, PhysicalKey.Z))
        {
            wishDir += forwardPlane;
        }

        if (IsMovementKeyDown(Key.S, Key.Down) || IsMovementPhysicalKeyDown(PhysicalKey.S))
        {
            wishDir -= forwardPlane;
        }

        if (IsMovementKeyDown(Key.A, Key.Q, Key.Left) || IsMovementPhysicalKeyDown(PhysicalKey.A, PhysicalKey.Q))
        {
            wishDir -= rightPlane;
        }

        if (IsMovementKeyDown(Key.D, Key.Right) || IsMovementPhysicalKeyDown(PhysicalKey.D))
        {
            wishDir += rightPlane;
        }

        if (wishDir.LengthSquared() > 0.0001f)
        {
            wishDir = Vector3.Normalize(wishDir);
        }

        _debugWishDir = wishDir;

        bool isShiftDown =
            IsMovementKeyDown(Key.LeftShift, Key.RightShift) ||
            IsMovementPhysicalKeyDown(PhysicalKey.ShiftLeft, PhysicalKey.ShiftRight);

        float moveSpeed = isShiftDown ? 3.5f : 6.5f;
        Vector3 horizontalVelocity = wishDir * moveSpeed;
        _player.Velocity = new Vector3(horizontalVelocity.X, _player.Velocity.Y, horizontalVelocity.Z);

        bool jumpPressed = IsKeyPressed(Key.Space) || IsPhysicalKeyPressed(PhysicalKey.Space);
        if (jumpPressed && _player.IsOnGround)
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

        _debugVelocity = velocity;
        _debugGrounded = grounded;

        float minY = 1.0f;
        float maxY = _world.Height - 2f;
        _player.Position = new Vector3(
            Math.Clamp(_player.Position.X, 1.5f, _world.Width - 1.5f),
            Math.Clamp(_player.Position.Y, minY, maxY),
            Math.Clamp(_player.Position.Z, 1.5f, _world.Depth - 1.5f));
    }

    private void InitializePlayerPosition()
    {
        Vector3 spawn = FindSpawnPosition();
        _player.Position = spawn;
        _player.Velocity = Vector3.Zero;
        _player.IsOnGround = false;

        ResolveInitialPenetration();
    }

    private Vector3 FindSpawnPosition()
    {
        int centerX = _world.Width / 2;
        int centerZ = _world.Depth / 2;

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

        // Fallback to a high safe position if no surface is found.
        return new Vector3(centerX + 0.5f, _world.Height - 6f, centerZ + 0.5f);
    }

    private bool TryFindSurface(int x, int z, out Vector3 spawn)
    {
        spawn = default;

        if (!_world.InBounds(x, 0, z))
        {
            return false;
        }

        for (int y = _world.Height - 3; y >= 1; y--)
        {
            var block = _world.GetBlock(x, y, z);
            if (!block.IsSolid())
            {
                continue;
            }

            // Avoid spawning directly on top of water.
            if (block == BlockType.Water)
            {
                continue;
            }

            int headY = y + 1;
            int topY = y + 2;
            if (topY >= _world.Height - 1)
            {
                continue;
            }

            if (_world.GetBlock(x, headY, z).IsSolid())
            {
                continue;
            }

            if (_world.GetBlock(x, topY, z).IsSolid())
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

        while (Collides(_player.Position))
        {
            _player.Position += Vector3.UnitY * 0.5f;
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

            Vector3 originalPosition = position;

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
                if (axis != 1 && TryStepUp(ref position, axis, delta, originalPosition))
                {
                    remaining -= step;
                    continue;
                }

                switch (axis)
                {
                    case 0:
                        position = originalPosition;
                        break;
                    case 1:
                        position = originalPosition;
                        if (sign < 0f)
                        {
                            grounded = true;
                        }
                        break;
                    case 2:
                        position = originalPosition;
                        break;
                }

                velocityComponent = 0f;
                break;
            }

            remaining -= step;
        }

        return grounded;
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

        _hasCurrentHit = VoxelRaycaster.TryPick(
            _world,
            origin,
            direction,
            InteractionDistance,
            block => block == BlockType.Air,
            out _currentHit);
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
        var result = _rayTracer.Render(_world, _player, _textures, _framebuffer);
        _framebuffer = result.Framebuffer;
        _camera = result.Camera;
        _hasCamera = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        HandleKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        HandleKeyUp(e);
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
    private bool IsPhysicalKeyDown(PhysicalKey key) => _physicalKeysDown.Contains(key);
    private bool IsPhysicalKeyPressed(PhysicalKey key) => _physicalKeysPressed.Contains(key);

    private bool IsMovementKeyDown(Key primary, Key? alt1 = null, Key? alt2 = null)
    {
        if (primary != Key.None && IsKeyDown(primary))
        {
            return true;
        }

        if (alt1.HasValue && alt1.Value != Key.None && IsKeyDown(alt1.Value))
        {
            return true;
        }

        if (alt2.HasValue && alt2.Value != Key.None && IsKeyDown(alt2.Value))
        {
            return true;
        }

        return false;
    }

    private bool IsMovementPhysicalKeyDown(params PhysicalKey[] keys)
    {
        foreach (var key in keys)
        {
            if (key != PhysicalKey.None && IsPhysicalKeyDown(key))
            {
                return true;
            }
        }

        return false;
    }

    private void HandleKeyDown(KeyEventArgs e)
    {
        if (!_keysDown.Contains(e.Key))
        {
            _keysDown.Add(e.Key);
            _keysPressed.Add(e.Key);
        }

        if (!_physicalKeysDown.Contains(e.PhysicalKey))
        {
            _physicalKeysDown.Add(e.PhysicalKey);
            _physicalKeysPressed.Add(e.PhysicalKey);
        }
    }

    private void HandleKeyUp(KeyEventArgs e)
    {
        _keysDown.Remove(e.Key);
        _physicalKeysDown.Remove(e.PhysicalKey);
    }

    private void SubscribeToTopLevelInput()
    {
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.AddHandler(InputElement.KeyDownEvent, TopLevelOnKeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        _topLevel.AddHandler(InputElement.KeyUpEvent, TopLevelOnKeyUp, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
    }

    private void UnsubscribeFromTopLevelInput()
    {
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.RemoveHandler(InputElement.KeyDownEvent, TopLevelOnKeyDown);
        _topLevel.RemoveHandler(InputElement.KeyUpEvent, TopLevelOnKeyUp);
    }

    private void TopLevelOnKeyDown(object? sender, KeyEventArgs e)
    {
        HandleKeyDown(e);
    }

    private void TopLevelOnKeyUp(object? sender, KeyEventArgs e)
    {
        HandleKeyUp(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_framebuffer != null)
        {
            var sourceRect = new Rect(0, 0, _framebuffer.PixelSize.Width, _framebuffer.PixelSize.Height);
            var destRect = new Rect(Bounds.Size);
            context.DrawImage(_framebuffer, sourceRect, destRect);
        }

        DrawBlockHighlight(context);
        DrawCrosshair(context);
        DrawHud(context);
    }

    private void DrawBlockHighlight(DrawingContext context)
    {
        if (!_hasCurrentHit || !_hasCamera)
        {
            return;
        }

        var viewport = Bounds.Size;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        var projector = new VoxelProjector(_camera, _player.EyePosition, viewport);
        VoxelOverlayRenderer.DrawSelection(context, projector, _currentHit);
    }

    private void DrawCrosshair(DrawingContext context)
    {
        VoxelOverlayRenderer.DrawCrosshair(context, Bounds.Size);
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
            "Wheel/1-7 choose block",
            "F1 toggle mouse look (Esc release)",
            "F2/F3 invert X/Y",
            "+/- adjust sensitivity"
        };

        double yOffset = padding + fpsLayout.Height + padding * 0.8;
        foreach (var line in infoLines)
        {
            var infoLayout = new TextLayout(line, _hudTypeface, 13, Brushes.White);
            infoLayout.Draw(context, new Point(padding, yOffset));
            yOffset += infoLayout.Height + 2;
        }

        var settingsLayout = new TextLayout(
            $"Mouse: {( _mouseLookEnabled ? "On" : "Off") } | Sens {_mouseSensitivity:0.00}",
            _hudTypeface,
            13,
            Brushes.White);
        settingsLayout.Draw(context, new Point(padding, yOffset + 4));

        var invertLayout = new TextLayout(
            $"Invert X: {(_invertMouseX ? "On" : "Off")}  Y: {(_invertMouseY ? "On" : "Off")}",
            _hudTypeface,
            13,
            Brushes.White);
        double debugStartY = yOffset + 4 + settingsLayout.Height + 2;
        invertLayout.Draw(context, new Point(padding, debugStartY));

        debugStartY += invertLayout.Height + 2;
        var movementDebugLayout = new TextLayout(
            $"WishDir: {_debugWishDir.X,5:0.00},{_debugWishDir.Y,5:0.00},{_debugWishDir.Z,5:0.00}",
            _hudTypeface,
            12,
            Brushes.LightGray);
        movementDebugLayout.Draw(context, new Point(padding, debugStartY));

        debugStartY += movementDebugLayout.Height + 2;
        var velocityDebugLayout = new TextLayout(
            $"Velocity: {_debugVelocity.X,5:0.00},{_debugVelocity.Y,5:0.00},{_debugVelocity.Z,5:0.00}  Grounded: {_debugGrounded}",
            _hudTypeface,
            12,
            Brushes.LightGray);
        velocityDebugLayout.Draw(context, new Point(padding, debugStartY));

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

    private void HandleToggleInput()
    {
        if (IsKeyPressed(Key.F1))
        {
            SetMouseLook(!_mouseLookEnabled);
        }

        if (_mouseLookEnabled && IsKeyPressed(Key.Escape))
        {
            SetMouseLook(false);
        }

        if (IsKeyPressed(Key.F2))
        {
            _invertMouseX = !_invertMouseX;
        }

        if (IsKeyPressed(Key.F3))
        {
            _invertMouseY = !_invertMouseY;
        }

        if (IsKeyPressed(Key.Add) || IsKeyPressed(Key.OemPlus))
        {
            AdjustMouseSensitivity(0.02f);
        }

        if (IsKeyPressed(Key.Subtract) || IsKeyPressed(Key.OemMinus))
        {
            AdjustMouseSensitivity(-0.02f);
        }
    }

    private void AdjustMouseSensitivity(float delta)
    {
        _mouseSensitivity = Math.Clamp(_mouseSensitivity + delta, 0.05f, 0.6f);
    }

    private void SetMouseLook(bool enabled)
    {
        if (_mouseLookEnabled == enabled)
        {
            return;
        }

        _mouseLookEnabled = enabled;

        if (enabled)
        {
            _requestPointerCapture = true;
            _lastPointerPosition = null;
            if (TopLevel.GetTopLevel(this) is { } topLevel)
            {
                _previousCursor = topLevel.Cursor;
                topLevel.Cursor = new Cursor(StandardCursorType.None);
            }

            Focus();
            WarpPointerToCenter();
        }
        else
        {
            _requestPointerCapture = false;
            _pendingMouseDeltaX = 0f;
            _pendingMouseDeltaY = 0f;
            _lastPointerPosition = null;
            _ignoreWarpPointerMove = false;

            if (_capturedPointer is not null)
            {
                _capturedPointer.Capture(null);
                _capturedPointer = null;
            }

            if (TopLevel.GetTopLevel(this) is { } topLevel)
            {
                topLevel.Cursor = _previousCursor ?? new Cursor(StandardCursorType.Arrow);
            }

            _previousCursor = null;
        }
    }

    private void WarpPointerToCenter(bool allowRetry = true)
    {
        if (!_mouseLookEnabled || _topLevel is null)
        {
            return;
        }

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            if (allowRetry)
            {
                Dispatcher.UIThread.Post(() => WarpPointerToCenter(false), DispatcherPriority.Input);
            }

            return;
        }

        var center = new Point(Bounds.Width / 2d, Bounds.Height / 2d);
        var screenPoint = _topLevel.PointToScreen(center);

        if (MouseCursorUtils.TryWarpPointer(screenPoint))
        {
            _ignoreWarpPointerMove = true;
            _lastPointerPosition = center;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_mouseLookEnabled)
        {
            return;
        }

        if ((_capturedPointer is null || _capturedPointer != e.Pointer) && (_requestPointerCapture || e.Pointer.Captured != this))
        {
            e.Pointer.Capture(this);
            _capturedPointer = e.Pointer;
            _requestPointerCapture = false;
            _lastPointerPosition = null;
        }

        var position = e.GetPosition(this);

        if (_ignoreWarpPointerMove)
        {
            _ignoreWarpPointerMove = false;
            _lastPointerPosition = position;
            return;
        }

        if (_lastPointerPosition.HasValue)
        {
            Avalonia.Vector delta = position - _lastPointerPosition.Value;
            _pendingMouseDeltaX += (float)delta.X;
            _pendingMouseDeltaY += (float)delta.Y;
        }

        _lastPointerPosition = position;
        WarpPointerToCenter();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        if (e.Pointer == _capturedPointer)
        {
            _capturedPointer = null;
            _lastPointerPosition = null;
        if (_mouseLookEnabled)
        {
            _requestPointerCapture = true;
        }
    }
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        _keysDown.Clear();
        _keysPressed.Clear();
        _physicalKeysDown.Clear();
        _physicalKeysPressed.Clear();
        if (_mouseLookEnabled)
        {
            SetMouseLook(false);
        }
    }
}
