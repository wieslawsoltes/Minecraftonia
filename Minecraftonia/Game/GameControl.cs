using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    private bool _isFrameScheduled;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan _lastFrameTime;

    private readonly HashSet<Key> _keysDown = new();
    private readonly HashSet<Key> _keysPressed = new();

    private bool _breakQueued;
    private bool _placeQueued;

    private MinecraftoniaGame _game;
    private readonly BlockTextures _textures;
    private readonly MinecraftoniaWorldConfig _worldConfig;
    private readonly int _defaultStreamingRadius;
    private readonly VoxelRayTracer<BlockType> _rayTracer;
    private WriteableBitmap? _framebuffer;
    private readonly PixelSize _renderSize = new PixelSize(360, 202);

    private VoxelCamera _camera;
    private bool _hasCamera;

    private readonly Random _worldSeedGenerator = new();

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

    private int _paletteScrollDelta;
    private bool _isGameActive;

    public event EventHandler? PauseRequested;

    public bool IsGameActive => _isGameActive;

    public GameControl()
    {
        Focusable = true;
        ClipToBounds = true;

        _textures = new BlockTextures();
        _worldConfig = MinecraftoniaWorldConfig.FromDimensions(
            96,
            48,
            96,
            waterLevel: 8,
            seed: 1337,
            generationMode: TerrainGenerationMode.Legacy);
        _defaultStreamingRadius = CalculateStreamingRadius(_worldConfig);
        _game = new MinecraftoniaGame(_worldConfig, textures: _textures, chunkStreamingRadius: _defaultStreamingRadius);
        _rayTracer = new VoxelRayTracer<BlockType>(
            _renderSize,
            FieldOfViewDegrees,
            block => block.IsSolid(),
            block => block == BlockType.Air);

    }

    private static int CalculateStreamingRadius(MinecraftoniaWorldConfig config)
    {
        int minChunkEdge = Math.Max(1, Math.Min(config.ChunkSizeX, config.ChunkSizeZ));
        int viewRadius = (int)Math.Ceiling(VoxelRayTracer<BlockType>.DefaultMaxTraceDistance / minChunkEdge);
        int baseRadius = Math.Max(3, viewRadius + 1);
        int maxFeasible = Math.Max(Math.Max(config.ChunkCountX, config.ChunkCountZ), 1);
        return Math.Min(baseRadius, maxFeasible);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        SubscribeToTopLevelInput();

        if (_isGameActive && !_mouseLookEnabled)
        {
            SetMouseLook(true);
        }

        RequestAnimationFrameLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isFrameScheduled = false;
        _lastFrameTime = TimeSpan.Zero;

        UnsubscribeFromTopLevelInput();
        _topLevel = null;

        if (_mouseLookEnabled)
        {
            SetMouseLook(false);
        }

        _framebuffer?.Dispose();
        _framebuffer = null;
    }

    private void OnAnimationFrame(TimeSpan timestamp)
    {
        _isFrameScheduled = false;

        if (VisualRoot is null)
        {
            _lastFrameTime = TimeSpan.Zero;
            return;
        }

        double delta;
        if (_lastFrameTime == TimeSpan.Zero)
        {
            delta = 0.016;
        }
        else
        {
            delta = (timestamp - _lastFrameTime).TotalSeconds;
        }
        _lastFrameTime = timestamp;

        if (delta <= 0)
        {
            delta = 0.001;
        }

        delta = Math.Min(delta, 0.1);
        UpdateGame((float)delta);
        RequestAnimationFrameLoop();
    }

    private void RequestAnimationFrameLoop()
    {
        if (_isFrameScheduled || _topLevel is not { } topLevel)
        {
            return;
        }

        _isFrameScheduled = true;
        topLevel.RequestAnimationFrame(OnAnimationFrame);
    }

    private void UpdateGame(float deltaTime)
    {
        if (!_isGameActive)
        {
            _keysPressed.Clear();
            _physicalKeysPressed.Clear();
            RenderScene();
            InvalidateVisual();
            return;
        }

        HandleToggleInput();
        float keyboardYaw = 0f;
        if (IsKeyDown(Key.Left)) keyboardYaw -= 1f;
        if (IsKeyDown(Key.Right)) keyboardYaw += 1f;
        if (IsKeyDown(Key.Q)) keyboardYaw -= 0.5f;
        if (IsKeyDown(Key.E)) keyboardYaw += 0.5f;

        float keyboardPitch = 0f;
        if (IsKeyDown(Key.Up)) keyboardPitch += 1f;
        if (IsKeyDown(Key.Down)) keyboardPitch -= 1f;

        float mouseYawDelta = 0f;
        float mousePitchDelta = 0f;
        if (Math.Abs(_pendingMouseDeltaX) > float.Epsilon || Math.Abs(_pendingMouseDeltaY) > float.Epsilon)
        {
            float yawDelta = _pendingMouseDeltaX * _mouseSensitivity;
            float pitchDelta = _pendingMouseDeltaY * _mouseSensitivity;

            mouseYawDelta = _invertMouseX ? -yawDelta : yawDelta;
            mousePitchDelta = _invertMouseY ? pitchDelta : -pitchDelta;
            _pendingMouseDeltaX = 0f;
            _pendingMouseDeltaY = 0f;
        }

        bool moveForward = IsMovementKeyDown(Key.W, Key.Z, Key.Up) || IsMovementPhysicalKeyDown(PhysicalKey.W, PhysicalKey.Z);
        bool moveBackward = IsMovementKeyDown(Key.S, Key.Down) || IsMovementPhysicalKeyDown(PhysicalKey.S);
        bool moveLeft = IsMovementKeyDown(Key.A, Key.Q, Key.Left) || IsMovementPhysicalKeyDown(PhysicalKey.A, PhysicalKey.Q);
        bool moveRight = IsMovementKeyDown(Key.D, Key.Right) || IsMovementPhysicalKeyDown(PhysicalKey.D);

        bool sprint =
            IsMovementKeyDown(Key.LeftShift, Key.RightShift) ||
            IsMovementPhysicalKeyDown(PhysicalKey.ShiftLeft, PhysicalKey.ShiftRight);

        bool jumpPressed = IsKeyPressed(Key.Space) || IsPhysicalKeyPressed(PhysicalKey.Space);

        int maxPaletteIndex = Math.Max(0, _game.Palette.Count - 1);
        int? hotbarSelection = null;
        if (IsKeyPressed(Key.D1)) hotbarSelection = 0;
        if (IsKeyPressed(Key.D2)) hotbarSelection = Math.Min(1, maxPaletteIndex);
        if (IsKeyPressed(Key.D3)) hotbarSelection = Math.Min(2, maxPaletteIndex);
        if (IsKeyPressed(Key.D4)) hotbarSelection = Math.Min(3, maxPaletteIndex);
        if (IsKeyPressed(Key.D5)) hotbarSelection = Math.Min(4, maxPaletteIndex);
        if (IsKeyPressed(Key.D6)) hotbarSelection = Math.Min(5, maxPaletteIndex);
        if (IsKeyPressed(Key.D7)) hotbarSelection = Math.Min(6, maxPaletteIndex);

        if (IsKeyPressed(Key.Tab))
        {
            _paletteScrollDelta += 1;
        }

        var input = new GameInputState(
            moveForward,
            moveBackward,
            moveLeft,
            moveRight,
            sprint,
            jumpPressed,
            keyboardYaw,
            keyboardPitch,
            mouseYawDelta,
            mousePitchDelta,
            _breakQueued,
            _placeQueued,
            hotbarSelection,
            _paletteScrollDelta);

        _game.Update(input, deltaTime);

        _breakQueued = false;
        _placeQueued = false;
        _paletteScrollDelta = 0;

        RenderScene();

        float instantaneousFps = deltaTime > 0.0001f ? 1f / deltaTime : 0f;
        _smoothedFps += (instantaneousFps - _smoothedFps) * 0.1f;

        _keysPressed.Clear();
        _physicalKeysPressed.Clear();
        InvalidateVisual();
    }



    private void RenderScene()
    {
        var result = _rayTracer.Render(_game.World, _game.Player, _game.Textures, _framebuffer);
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
        if (!_isGameActive)
        {
            return;
        }

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
        if (!_isGameActive)
        {
            return;
        }

        if (e.Delta.Y > 0)
        {
            _paletteScrollDelta += 1;
        }
        else if (e.Delta.Y < 0)
        {
            _paletteScrollDelta -= 1;
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
        if (!_game.HasCurrentHit || !_hasCamera)
        {
            return;
        }

        var viewport = Bounds.Size;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        var projector = new VoxelProjector(_camera, _game.Player.EyePosition, viewport);
        VoxelOverlayRenderer.DrawSelection(context, projector, _game.CurrentHit);
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

        var palette = _game.Palette;
        int selectedIndex = _game.SelectedPaletteIndex;
        string selectedName = _game.SelectedBlock.ToString();
        var selectedLayout = new TextLayout($"[{selectedIndex + 1}] {selectedName}", _hudTypeface, 18, Brushes.White);
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
            $"WishDir: {_game.DebugWishDirection.X,5:0.00},{_game.DebugWishDirection.Y,5:0.00},{_game.DebugWishDirection.Z,5:0.00}",
            _hudTypeface,
            12,
            Brushes.LightGray);
        movementDebugLayout.Draw(context, new Point(padding, debugStartY));

        debugStartY += movementDebugLayout.Height + 2;
        var velocityDebugLayout = new TextLayout(
            $"Velocity: {_game.DebugVelocity.X,5:0.00},{_game.DebugVelocity.Y,5:0.00},{_game.DebugVelocity.Z,5:0.00}  Grounded: {_game.DebugGrounded}",
            _hudTypeface,
            12,
            Brushes.LightGray);
        velocityDebugLayout.Draw(context, new Point(padding, debugStartY));

        if (_game.HasCurrentHit)
        {
            var hitLayout = new TextLayout(
                $"Target: {_game.CurrentHit.BlockType} @ {_game.CurrentHit.Block.X},{_game.CurrentHit.Block.Y},{_game.CurrentHit.Block.Z}",
                _hudTypeface,
                14,
                Brushes.White);

            var textPos = new Point(Bounds.Width / 2 - hitLayout.Width / 2, Bounds.Height * 0.12);
            hitLayout.Draw(context, textPos);
        }
    }

    private void HandleToggleInput()
    {
        if (_isGameActive)
        {
            if (IsKeyPressed(Key.F1))
            {
                SetMouseLook(!_mouseLookEnabled);
            }

            if (IsKeyPressed(Key.Escape))
            {
                RequestPause();
            }
        }

        if (IsKeyPressed(Key.F2))
        {
            _invertMouseX = !_invertMouseX;
        }

        if (IsKeyPressed(Key.F3))
        {
            _invertMouseY = !_invertMouseY;
        }

        if (IsKeyPressed(Key.F5))
        {
            RegenerateWorld();
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

    private void RegenerateWorld()
    {
        var currentConfig = _game.World.Config;
        int newSeed = _worldSeedGenerator.Next(int.MinValue, int.MaxValue);

        var regeneratedConfig = new MinecraftoniaWorldConfig
        {
            ChunkSizeX = currentConfig.ChunkSizeX,
            ChunkSizeY = currentConfig.ChunkSizeY,
            ChunkSizeZ = currentConfig.ChunkSizeZ,
            ChunkCountX = currentConfig.ChunkCountX,
            ChunkCountY = currentConfig.ChunkCountY,
            ChunkCountZ = currentConfig.ChunkCountZ,
            WaterLevel = currentConfig.WaterLevel,
            Seed = newSeed,
            GenerationMode = TerrainGenerationMode.WaveFunctionCollapse
        };

        float yaw = _game.Player.Yaw;
        float pitch = _game.Player.Pitch;

        int streamingRadius = CalculateStreamingRadius(regeneratedConfig);

        _game = new MinecraftoniaGame(
            regeneratedConfig,
            initialYaw: yaw,
            initialPitch: pitch,
            textures: _textures,
            chunkStreamingRadius: streamingRadius);

        _framebuffer?.Dispose();
        _framebuffer = null;
        _hasCamera = false;
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

    public void StartNewGame()
    {
        _game = new MinecraftoniaGame(_worldConfig, textures: _textures, chunkStreamingRadius: _defaultStreamingRadius);
        _isGameActive = true;
        ResetTransientInputState();
        _smoothedFps = 60f;
        RenderScene();
        InvalidateVisual();
        SetMouseLook(true);
        Focus();
    }

    public void LoadGame(GameSaveData save)
    {
        _game = MinecraftoniaGame.FromSave(save, _textures);
        _isGameActive = true;
        ResetTransientInputState();
        _smoothedFps = 60f;
        RenderScene();
        InvalidateVisual();
        SetMouseLook(true);
        Focus();
    }

    public GameSaveData CreateSaveData() => _game.CreateSaveData();

    public void PauseGame()
    {
        if (!_isGameActive)
        {
            return;
        }

        _isGameActive = false;
        if (_mouseLookEnabled)
        {
            SetMouseLook(false);
        }
    }

    public void ResumeGame()
    {
        if (_isGameActive)
        {
            return;
        }

        _isGameActive = true;
        ResetTransientInputState();
        _smoothedFps = 60f;
        RenderScene();
        InvalidateVisual();
        SetMouseLook(true);
        Focus();
    }

    private void ResetTransientInputState()
    {
        _breakQueued = false;
        _placeQueued = false;
        _pendingMouseDeltaX = 0f;
        _pendingMouseDeltaY = 0f;
        _paletteScrollDelta = 0;
        _keysDown.Clear();
        _keysPressed.Clear();
        _physicalKeysDown.Clear();
        _physicalKeysPressed.Clear();
        _capturedPointer = null;
        _requestPointerCapture = false;
        _lastPointerPosition = null;
    }

    private void RequestPause()
    {
        PauseGame();
        PauseRequested?.Invoke(this, EventArgs.Empty);
    }
}
