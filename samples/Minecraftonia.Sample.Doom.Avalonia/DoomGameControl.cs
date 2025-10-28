using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Minecraftonia.Content;
using Minecraftonia.Core;
using Minecraftonia.Hosting;
using Minecraftonia.Hosting.Avalonia;
using Minecraftonia.Rendering.Avalonia.Controls;
using Minecraftonia.Rendering.Avalonia.Presenters;
using Minecraftonia.Rendering.Core;
using Minecraftonia.Rendering.Pipelines;
using Minecraftonia.Sample.Doom.Core;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.Sample.Doom.Avalonia;

internal sealed class DoomGameControl : Control
{
    private const float MouseSensitivity = 0.22f;

    private bool _frameScheduled;
    private TimeSpan _lastTimestamp;

    private BlockTextures _materials = null!;
    private DoomVoxelWorld _world = null!;
    private Player _player = null!;
    private IVoxelRendererFactory<BlockType> _rendererFactory = null!;
    private IVoxelFramePresenterFactory _framePresenterFactory = null!;
    private IVoxelRenderer<BlockType> _renderer = null!;
    private DoomRenderPipeline _pipeline = null!;
    private DoomGameSession _session = null!;
    private GameHost<BlockType> _host = null!;
    private IVoxelFramePresenter _framePresenter = null!;

    private TopLevel? _topLevel;
    private IKeyboardInputSource? _keyboard;
    private IPointerInputSource? _pointer;
    private bool _mouseLook;
    private Cursor? _previousCursor;

    private IVoxelFrameBuffer? _framebuffer;
    private VoxelCamera _camera;

    public DoomGameControl()
    {
        Focusable = true;
        ClipToBounds = true;

        Configure();
    }

    private void Configure()
    {
        _materials = new BlockTextures();
        _world = new DoomVoxelWorld();
        _world.PreloadAllChunks();

        _player = new Player
        {
            Position = new Vector3(DoomVoxelWorld.MapWidth / 2f, 1.0f, 5.5f),
            EyeHeight = 1.6f,
            Yaw = 0f,
            Pitch = -4f
        };

        _rendererFactory = new VoxelRayTracerFactory<BlockType>();
        _framePresenterFactory = new DefaultVoxelFramePresenterFactory();

        var options = new VoxelRendererOptions<BlockType>(
            new VoxelSize(320, 200),
            65f,
            block => block.IsSolid(),
            block => block == BlockType.Air,
            SamplesPerPixel: 1,
            EnableFxaa: true,
            EnableSharpen: true,
            GlobalIllumination: GlobalIlluminationSettings.Default with { Enabled = false });

        _renderer = _rendererFactory.Create(options);
        _framePresenter = _framePresenterFactory.Create(FramePresentationMode.SkiaTexture);

        _session = new DoomGameSession(_world, _materials, _player);
        _pipeline = new DoomRenderPipeline(_renderer, _materials);
        _host = new GameHost<BlockType>(_session, _pipeline);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);

        if (_topLevel is not null)
        {
            _keyboard = new KeyboardInputSource(_topLevel);
            _pointer = new PointerInputSource(_topLevel, this);
            if (_mouseLook)
            {
                _pointer.EnableMouseLook();
                _pointer.QueueWarpToCenter();
            }
        }

        RequestAnimationFrameLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _frameScheduled = false;
        _lastTimestamp = TimeSpan.Zero;

        SetMouseLook(false);

        _pointer?.Dispose();
        _pointer = null;
        _keyboard?.Dispose();
        _keyboard = null;
        _topLevel = null;

        var framebuffer = _framebuffer;
        _framebuffer = null;
        framebuffer?.Dispose();

        _framePresenter.Dispose();
    }

    private void RequestAnimationFrameLoop()
    {
        if (_frameScheduled || _topLevel is not { } topLevel)
        {
            return;
        }

        _frameScheduled = true;
        topLevel.RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame(TimeSpan timestamp)
    {
        _frameScheduled = false;

        if (VisualRoot is null)
        {
            _lastTimestamp = TimeSpan.Zero;
            return;
        }

        double deltaSeconds = 0.016;
        if (_lastTimestamp != TimeSpan.Zero)
        {
            deltaSeconds = Math.Clamp((timestamp - _lastTimestamp).TotalSeconds, 0.001, 0.1);
        }

        _lastTimestamp = timestamp;
        Step((float)deltaSeconds);
        RequestAnimationFrameLoop();
    }

    private void Step(float deltaSeconds)
    {
        if (_keyboard is null || _pointer is null)
        {
            return;
        }

        if (_keyboard.WasPressed(Key.Tab))
        {
            SetMouseLook(!_mouseLook);
        }

        if (_keyboard.WasPressed(Key.Escape))
        {
            SetMouseLook(false);
        }

        float yawDelta = 0f;
        float pitchDelta = 0f;

        if (_mouseLook)
        {
            yawDelta = -_pointer.DeltaX * MouseSensitivity;
            pitchDelta = _pointer.DeltaY * MouseSensitivity;
        }

        _pointer.NextFrame();

        var input = new DoomInputState(yawDelta, pitchDelta);
        _session.QueueInput(input, deltaSeconds);

        var result = _host.Step(TimeSpan.FromSeconds(deltaSeconds));
        _framebuffer = result.Framebuffer;
        _camera = result.Camera;

        _keyboard.NextFrame();
        InvalidateVisual();
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

        VoxelOverlayRenderer.DrawCrosshair(context, Bounds.Size);
        DrawHud(context);
    }

    private void DrawHud(DrawingContext context)
    {
        const double padding = 12;
        var message = _mouseLook
            ? "Esc release pointer • Mouse to look"
            : "Click or press Tab to capture mouse";

        var layout = new TextLayout(message, Typeface.Default, 14, Brushes.White);
        var rect = new Rect(
            padding,
            Bounds.Height - layout.Height - padding * 2,
            layout.Width + padding * 2,
            layout.Height + padding);

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(160, 20, 20, 24)), rect);
        context.DrawRectangle(new Pen(Brushes.White, 1), rect);
        layout.Draw(context, rect.TopLeft + new global::Avalonia.Vector(padding * 0.5, padding * 0.3));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            SetMouseLook(true);
        }
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        SetMouseLook(false);
        _keyboard?.NextFrame();
        _pointer?.NextFrame();
    }

    private void SetMouseLook(bool enabled)
    {
        if (_mouseLook == enabled)
        {
            return;
        }

        _mouseLook = enabled;

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

    private readonly record struct DoomInputState(float YawDelta, float PitchDelta);

    private sealed class DoomGameSession : IGameSession<BlockType>
    {
        private readonly DoomVoxelWorld _world;
        private readonly IVoxelMaterialProvider<BlockType> _materials;
        private readonly Player _player;
        private DoomInputState _pendingInput;
        private float _pendingDelta;
        private bool _hasPendingInput;

        public DoomGameSession(DoomVoxelWorld world, IVoxelMaterialProvider<BlockType> materials, Player player)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _materials = materials ?? throw new ArgumentNullException(nameof(materials));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public IVoxelWorld<BlockType> World => _world;
        public Player Player => _player;
        public IVoxelMaterialProvider<BlockType> Materials => _materials;

        public void QueueInput(DoomInputState input, float deltaTime)
        {
            _pendingInput = input;
            _pendingDelta = deltaTime;
            _hasPendingInput = true;
        }

        public void Update(GameTime time)
        {
            if (!_hasPendingInput)
            {
                return;
            }

            _player.Yaw = NormalizeAngle(_player.Yaw + _pendingInput.YawDelta);
            _player.Pitch = Math.Clamp(_player.Pitch + _pendingInput.PitchDelta, -80f, 80f);

            _hasPendingInput = false;
        }

        private static float NormalizeAngle(float degrees)
        {
            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }
    }

    private sealed class DoomRenderPipeline : IRenderPipeline<BlockType>
    {
        private readonly IVoxelRenderer<BlockType> _renderer;
        private readonly IVoxelMaterialProvider<BlockType> _materials;

        public DoomRenderPipeline(IVoxelRenderer<BlockType> renderer, IVoxelMaterialProvider<BlockType> materials)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            _materials = materials ?? throw new ArgumentNullException(nameof(materials));
        }

        public IVoxelRenderResult<BlockType> Render(IGameSession<BlockType> session, IVoxelFrameBuffer? framebuffer)
        {
            var result = _renderer.Render(session.World, session.Player, _materials, framebuffer);
            return new VoxelRenderResult<BlockType>(result.Framebuffer, result.Camera);
        }
    }
}
