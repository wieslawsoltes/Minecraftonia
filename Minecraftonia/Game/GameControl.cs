using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using Avalonia.Threading;
using Minecraftonia.Content;
using Minecraftonia.Core;
using Minecraftonia.Game.Rendering;
using Minecraftonia.Hosting;
using Minecraftonia.Hosting.Avalonia;
using Minecraftonia.Rendering.Avalonia.Controls;
using Minecraftonia.Rendering.Avalonia.Presenters;
using Minecraftonia.WaveFunctionCollapse;
using Minecraftonia.VoxelEngine;
using Minecraftonia.VoxelRendering;

namespace Minecraftonia.Game;

public sealed class GameControl : Control
{
    private const float FieldOfViewDegrees = 70f;

    private bool _isFrameScheduled;
    private TimeSpan _lastFrameTime;

    private bool _breakQueued;
    private bool _placeQueued;

    private MinecraftoniaGame _game;
    private readonly BlockTextures _textures;
    private readonly MinecraftoniaWorldConfig _worldConfig;
    private readonly int _defaultStreamingRadius;
    private IGameRenderer _gameRenderer;
    private readonly bool _ownsRenderer;

    private GameHost<BlockType>? _host;
    private MinecraftoniaGameSession? _session;
    private GameRenderPipeline? _pipeline;

    private IVoxelFrameBuffer? _framebuffer;
    private IVoxelFramePresenter _framePresenter = null!;
    private readonly Func<FramePresentationMode, IVoxelFramePresenter> _framePresenterFactory;
    private FramePresentationMode _presentationMode = FramePresentationMode.SkiaTexture;
    private readonly VoxelSize _renderSize = new VoxelSize(360, 202);

    private VoxelCamera _camera;
    private bool _hasCamera;

    private readonly Random _worldSeedGenerator = new();

    private readonly Typeface _hudTypeface = Typeface.Default;
    private float _smoothedFps = 60f;

    private KeyboardInputSource? _keyboard;
    private PointerInputSource? _pointer;
    private bool _mouseLookEnabled;
    private Cursor? _previousCursor;
    private float _mouseSensitivity = 0.32f;
    private bool _invertMouseX = true;
    private bool _invertMouseY;

    private GlobalIlluminationSettings _giSettings;
    private const int GiSamplesMin = 0;
    private const int GiSamplesMax = 24;

    private TopLevel? _topLevel;

    private int _paletteScrollDelta;
    private bool _isGameActive;

    public event EventHandler? PauseRequested;

    public bool IsGameActive => _isGameActive;

    public GameControl()
        : this(null, null, null, null)
    {
    }

    public GameControl(
        IGameRenderer? gameRenderer = null,
        Func<FramePresentationMode, IVoxelFramePresenter>? framePresenterFactory = null,
        BlockTextures? textures = null,
        MinecraftoniaWorldConfig? worldConfig = null)
    {
        Focusable = true;
        ClipToBounds = true;

        _textures = textures ?? new BlockTextures();
        _worldConfig = worldConfig ?? MinecraftoniaWorldConfig.FromDimensions(
            96,
            48,
            96,
            waterLevel: 8,
            seed: 1337);
        _defaultStreamingRadius = CalculateStreamingRadius(_worldConfig);
        _game = new MinecraftoniaGame(_worldConfig, textures: _textures, chunkStreamingRadius: _defaultStreamingRadius);
        _giSettings = GlobalIlluminationSettings.Default with
        {
            DiffuseSampleCount = 5,
            MaxDistance = 22f,
            Strength = 1.05f,
            AmbientLight = new Vector3(0.18f, 0.21f, 0.26f),
            SunShadowSoftness = 0.58f,
            Enabled = false
        };

        _ownsRenderer = gameRenderer is null;
        _gameRenderer = gameRenderer ?? new DefaultGameRenderer(CreateVoxelRenderer(_giSettings), _textures);
        _framePresenterFactory = framePresenterFactory ?? CreateFramePresenter;
        _framePresenter = _framePresenterFactory(_presentationMode);

        ConfigureHosting();
    }

    public FramePresentationMode PresentationMode
    {
        get => _presentationMode;
        set
        {
            if (_presentationMode == value)
            {
                return;
            }

            _presentationMode = value;
            ReplaceFramePresenter(CreateFramePresenter(value));
            InvalidateVisual();
        }
    }

    private void ConfigureHosting()
    {
        if (_session is null)
        {
            _session = new MinecraftoniaGameSession(_game, _textures);
            _pipeline = new GameRenderPipeline(this);
            _host = new GameHost<BlockType>(_session, _pipeline);
        }
        else
        {
            _session.SetGame(_game);
        }

        _session.SetActive(_isGameActive);
    }

    private IVoxelRenderer<BlockType> CreateVoxelRenderer(GlobalIlluminationSettings giSettings)
    {
        return new VoxelRayTracer<BlockType>(
            _renderSize,
            FieldOfViewDegrees,
            block => block.IsSolid(),
            block => block == BlockType.Air,
            samplesPerPixel: 1,
            enableFxaa: true,
            fxaaContrastThreshold: 0.0312f,
            fxaaRelativeThreshold: 0.125f,
            enableSharpen: true,
            sharpenAmount: 0.18f,
            globalIllumination: giSettings);
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

        if (_topLevel is not null)
        {
            _keyboard = new KeyboardInputSource(_topLevel);
            _pointer = new PointerInputSource(_topLevel, this);

            if (_mouseLookEnabled)
            {
                _pointer.EnableMouseLook();
                _pointer.QueueWarpToCenter();
                _previousCursor ??= _topLevel.Cursor;
                _topLevel.Cursor = new Cursor(StandardCursorType.None);
                Focus();
            }
        }

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

        if (_mouseLookEnabled)
        {
            SetMouseLook(false);
        }

        _pointer?.Dispose();
        _pointer = null;
        _keyboard?.Dispose();
        _keyboard = null;
        _topLevel = null;

        var framebuffer = _framebuffer;
        _framebuffer = null;
        framebuffer?.Dispose();

        ReplaceFramePresenter(_framePresenterFactory(_presentationMode));
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
        if (_host is null || _session is null)
        {
            return;
        }

        if (!_isGameActive)
        {
            _keyboard?.NextFrame();
            _pointer?.NextFrame();
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
        if (_pointer is { } pointer)
        {
            float yawDelta = pointer.DeltaX * _mouseSensitivity;
            float pitchDelta = pointer.DeltaY * _mouseSensitivity;

            if (Math.Abs(yawDelta) > float.Epsilon || Math.Abs(pitchDelta) > float.Epsilon)
            {
                mouseYawDelta = _invertMouseX ? -yawDelta : yawDelta;
                mousePitchDelta = _invertMouseY ? pitchDelta : -pitchDelta;
            }

            pointer.NextFrame();
        }

        bool moveForward = IsMovementKeyDown(Key.W, Key.Z, Key.Up);
        bool moveBackward = IsMovementKeyDown(Key.S, Key.Down);
        bool moveLeft = IsMovementKeyDown(Key.A, Key.Q, Key.Left);
        bool moveRight = IsMovementKeyDown(Key.D, Key.Right);
        bool sprint = IsMovementKeyDown(Key.LeftShift, Key.RightShift);
        bool jumpPressed = IsKeyPressed(Key.Space);

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

        _session.QueueInput(input, deltaTime);
        var result = _host.Step(TimeSpan.FromSeconds(deltaTime));

        _framebuffer = result.Framebuffer;
        _camera = result.Camera;
        _hasCamera = true;

        _breakQueued = false;
        _placeQueued = false;
        _paletteScrollDelta = 0;

        float instantaneousFps = deltaTime > 0.0001f ? 1f / deltaTime : 0f;
        _smoothedFps += (instantaneousFps - _smoothedFps) * 0.1f;

        _keyboard?.NextFrame();
        InvalidateVisual();
    }

    private void RenderScene()
    {
        if (_pipeline is null || _session is null)
        {
            return;
        }

        var result = _pipeline.Render(_session, _framebuffer);
        _framebuffer = result.Framebuffer;
        _camera = result.Camera;
        _hasCamera = true;
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

    private bool IsKeyDown(Key key) => _keyboard?.IsDown(key) ?? false;
    private bool IsKeyPressed(Key key) => _keyboard?.WasPressed(key) ?? false;

    private bool IsMovementKeyDown(params Key[] keys)
    {
        if (_keyboard is null)
        {
            return false;
        }

        foreach (var key in keys)
        {
            if (key != Key.None && _keyboard.IsDown(key))
            {
                return true;
            }
        }

        return false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_framebuffer is { IsDisposed: false } framebuffer)
        {
            var destRect = new Rect(Bounds.Size);
            if (destRect.Width > 0 && destRect.Height > 0)
            {
                _framePresenter.Render(context, framebuffer, destRect);
            }
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
            "+/- adjust sensitivity",
            "F6 toggle GI, F7/F8 sample count",
            $"F9 toggle render mode ({_presentationMode})"
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

        double giStartY = yOffset + 4 + settingsLayout.Height + 2;
        var giBrush = _giSettings.Enabled ? Brushes.LightGreen : Brushes.LightGray;
        var giLayout = new TextLayout(
            $"GI: {(_giSettings.Enabled ? "On" : "Off")}  Samples: {_giSettings.DiffuseSampleCount}",
            _hudTypeface,
            13,
            giBrush);
        giLayout.Draw(context, new Point(padding, giStartY));

        var invertLayout = new TextLayout(
            $"Invert X: {(_invertMouseX ? "On" : "Off")}  Y: {(_invertMouseY ? "On" : "Off")}",
            _hudTypeface,
            13,
            Brushes.White);
        double debugStartY = giStartY + giLayout.Height + 2;
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

        if (IsKeyPressed(Key.F6))
        {
            ToggleGlobalIllumination();
        }

        if (IsKeyPressed(Key.F7))
        {
            AdjustGiSamples(-1);
        }

        if (IsKeyPressed(Key.F8))
        {
            AdjustGiSamples(1);
        }

        if (IsKeyPressed(Key.F9))
        {
            TogglePresentationMode();
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

    private void RefreshRenderer()
    {
        if (!_ownsRenderer)
        {
            return;
        }

        _gameRenderer = new DefaultGameRenderer(CreateVoxelRenderer(_giSettings), _textures);
    }

    private void ToggleGlobalIllumination()
    {
        _giSettings = _giSettings with { Enabled = !_giSettings.Enabled };
        RefreshRenderer();
    }

    private void AdjustGiSamples(int delta)
    {
        int newSamples = Math.Clamp(_giSettings.DiffuseSampleCount + delta, GiSamplesMin, GiSamplesMax);
        if (newSamples == _giSettings.DiffuseSampleCount)
        {
            return;
        }

        _giSettings = _giSettings with { DiffuseSampleCount = newSamples };
        RefreshRenderer();
    }

    private void TogglePresentationMode()
    {
        PresentationMode = _presentationMode switch
        {
            FramePresentationMode.SkiaTexture => FramePresentationMode.WritableBitmap,
            FramePresentationMode.WritableBitmap => FramePresentationMode.SkiaTexture,
            _ => FramePresentationMode.SkiaTexture
        };
    }

    private void RegenerateWorld()
    {
        int newSeed = _worldSeedGenerator.Next(int.MinValue, int.MaxValue);
        var currentConfig = _game.World.Config;

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
            GenerationMode = TerrainGenerationMode.WaveFunctionCollapse,
            UseOpenStreetMap = true,
            RequireOpenStreetMap = currentConfig.RequireOpenStreetMap
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

        var framebuffer = _framebuffer;
        _framebuffer = null;
        framebuffer?.Dispose();
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
            _pointer?.EnableMouseLook();
            _pointer?.QueueWarpToCenter();
            if (_topLevel is not null)
            {
                _previousCursor = _topLevel.Cursor;
                _topLevel.Cursor = new Cursor(StandardCursorType.None);
            }

            Focus();
        }
        else
        {
            _pointer?.DisableMouseLook();
            if (_topLevel is not null)
            {
                _topLevel.Cursor = _previousCursor ?? new Cursor(StandardCursorType.Arrow);
            }

            _previousCursor = null;
        }
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        _keyboard?.NextFrame();
        _pointer?.NextFrame();
        if (_mouseLookEnabled)
        {
            SetMouseLook(false);
        }
    }

    public void StartNewGame()
    {
        _game = new MinecraftoniaGame(_worldConfig, textures: _textures, chunkStreamingRadius: _defaultStreamingRadius);
        ConfigureHosting();
        _isGameActive = true;
        _session?.SetActive(true);
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
        ConfigureHosting();
        _isGameActive = true;
        _session?.SetActive(true);
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
        _session?.SetActive(false);
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
        _session?.SetActive(true);
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
        _paletteScrollDelta = 0;
        _keyboard?.NextFrame();
        _pointer?.NextFrame();
    }

    private void RequestPause()
    {
        PauseGame();
        PauseRequested?.Invoke(this, EventArgs.Empty);
    }

    private IVoxelFramePresenter CreateFramePresenter(FramePresentationMode mode)
    {
        return mode switch
        {
            FramePresentationMode.WritableBitmap => new WritableBitmapFramePresenter(),
            FramePresentationMode.SkiaTexture => new SkiaTextureFramePresenter(),
            _ => new SkiaTextureFramePresenter()
        };
    }

    private void ReplaceFramePresenter(IVoxelFramePresenter presenter)
    {
        _framePresenter?.Dispose();
        _framePresenter = presenter;
    }

    private sealed class MinecraftoniaGameSession : IGameSession<BlockType>
    {
        private MinecraftoniaGame _game;
        private readonly BlockTextures _textures;
        private GameInputState _pendingInput;
        private float _pendingDeltaTime;
        private bool _hasPendingInput;
        private bool _isActive = true;

        public MinecraftoniaGameSession(MinecraftoniaGame game, BlockTextures textures)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _textures = textures ?? throw new ArgumentNullException(nameof(textures));
        }

        public void SetGame(MinecraftoniaGame game) => _game = game ?? throw new ArgumentNullException(nameof(game));
        public void SetActive(bool active) => _isActive = active;

        public void QueueInput(GameInputState input, float deltaTime)
        {
            _pendingInput = input;
            _pendingDeltaTime = deltaTime;
            _hasPendingInput = true;
        }

        public MinecraftoniaGame Game => _game;

        public IVoxelWorld<BlockType> World => _game.World;
        public Player Player => _game.Player;
        public IVoxelMaterialProvider<BlockType> Materials => _textures;

        public void Update(GameTime time)
        {
            if (!_isActive || !_hasPendingInput)
            {
                return;
            }

            _game.Update(_pendingInput, _pendingDeltaTime);
            _hasPendingInput = false;
        }
    }

    private sealed class GameRenderPipeline : IRenderPipeline<BlockType>
    {
        private readonly GameControl _owner;

        public GameRenderPipeline(GameControl owner)
        {
            _owner = owner;
        }

        public IVoxelRenderResult<BlockType> Render(IGameSession<BlockType> session, IVoxelFrameBuffer? framebuffer)
        {
            if (session is not MinecraftoniaGameSession actual)
            {
                throw new InvalidOperationException("Unexpected session type.");
            }

            var result = _owner._gameRenderer.Render(actual.Game, framebuffer);
            return new VoxelRenderResult<BlockType>(result.Framebuffer, result.Camera);
        }
    }
}

public enum FramePresentationMode
{
    WritableBitmap,
    SkiaTexture
}
