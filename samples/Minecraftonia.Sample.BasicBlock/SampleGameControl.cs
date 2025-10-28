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
using Minecraftonia.VoxelEngine;
using Minecraftonia.VoxelRendering;

namespace Minecraftonia.Sample.BasicBlock;

internal sealed class SampleGameControl : Control
{
    private const float MouseSensitivity = 0.22f;
    private const float HorizontalSpeed = 3.6f;
    private const float VerticalSpeed = 3.0f;

    private bool _frameScheduled;
    private TimeSpan _lastTimestamp;

    private readonly BlockTextures _materials = new();
    private readonly SampleWorld _world = new();
    private readonly Player _player;
    private readonly IVoxelRenderer<BlockType> _renderer;
    private readonly SampleRenderPipeline _pipeline;
    private readonly SampleGameSession _session;
    private readonly GameHost<BlockType> _host;
    private readonly IVoxelFramePresenter _framePresenter = new WritableBitmapFramePresenter();

    private TopLevel? _topLevel;
    private KeyboardInputSource? _keyboard;
    private PointerInputSource? _pointer;
    private bool _mouseLook;
    private Cursor? _previousCursor;

    private IVoxelFrameBuffer? _framebuffer;
    private VoxelCamera _camera;

    public SampleGameControl()
    {
        Focusable = true;
        ClipToBounds = true;

        _player = new Player
        {
            Position = new Vector3(8.5f, 0.2f, 12.5f),
            EyeHeight = 1.6f,
            Yaw = 180f,
            Pitch = -12f
        };

        _world.EnsureChunksInRange(new ChunkCoordinate(0, 0, 0), radius: 0);

        _renderer = new VoxelRayTracer<BlockType>(
            new VoxelSize(256, 144),
            70f,
            block => block.IsSolid(),
            block => block == BlockType.Air,
            samplesPerPixel: 1,
            enableFxaa: true,
            enableSharpen: true,
            globalIllumination: GlobalIlluminationSettings.Default with { Enabled = false });

        _session = new SampleGameSession(_world, _materials, _player, HorizontalSpeed, VerticalSpeed);
        _pipeline = new SampleRenderPipeline(_renderer, _materials);
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

        float moveX = 0f;
        float moveY = 0f;
        float moveZ = 0f;

        if (_keyboard.IsDown(Key.W)) moveZ += 1f;
        if (_keyboard.IsDown(Key.S)) moveZ -= 1f;
        if (_keyboard.IsDown(Key.D)) moveX += 1f;
        if (_keyboard.IsDown(Key.A)) moveX -= 1f;
        if (_keyboard.IsDown(Key.Space)) moveY += 1f;
        if (_keyboard.IsDown(Key.LeftShift) || _keyboard.IsDown(Key.RightShift)) moveY -= 1f;

        float yawDelta = 0f;
        float pitchDelta = 0f;

        if (_mouseLook)
        {
            yawDelta = -_pointer.DeltaX * MouseSensitivity;
            pitchDelta = _pointer.DeltaY * MouseSensitivity;
        }

        _pointer.NextFrame();

        var input = new SampleInputState(moveX, moveY, moveZ, yawDelta, pitchDelta);
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
            ? "Esc release pointer • WASD move • Space/Shift rise/fall"
            : "Click or press Tab to capture mouse • WASD move";

        var layout = new TextLayout(message, Typeface.Default, 14, Brushes.White);
        var rect = new Rect(
            padding,
            Bounds.Height - layout.Height - padding * 2,
            layout.Width + padding * 2,
            layout.Height + padding);

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(160, 20, 20, 24)), rect);
        context.DrawRectangle(new Pen(Brushes.White, 1), rect);
        layout.Draw(context, rect.TopLeft + new Avalonia.Vector(padding * 0.5, padding * 0.3));
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
}

internal readonly record struct SampleInputState(
    float MoveX,
    float MoveY,
    float MoveZ,
    float YawDelta,
    float PitchDelta);

internal sealed class SampleGameSession : IGameSession<BlockType>
{
    private readonly SampleWorld _world;
    private readonly IVoxelMaterialProvider<BlockType> _materials;
    private readonly Player _player;
    private readonly float _horizontalSpeed;
    private readonly float _verticalSpeed;

    private SampleInputState _pendingInput;
    private float _pendingDelta;
    private bool _hasPendingInput;

    public SampleGameSession(
        SampleWorld world,
        IVoxelMaterialProvider<BlockType> materials,
        Player player,
        float horizontalSpeed,
        float verticalSpeed)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _materials = materials ?? throw new ArgumentNullException(nameof(materials));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _horizontalSpeed = horizontalSpeed;
        _verticalSpeed = verticalSpeed;
    }

    public IVoxelWorld<BlockType> World => _world;
    public Player Player => _player;
    public IVoxelMaterialProvider<BlockType> Materials => _materials;

    public void QueueInput(SampleInputState input, float deltaTime)
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

        ApplyLook(_pendingInput);
        ApplyMovement(_pendingInput);
        _hasPendingInput = false;
    }

    private void ApplyLook(SampleInputState input)
    {
        _player.Yaw = NormalizeAngle(_player.Yaw + input.YawDelta);
        _player.Pitch = Math.Clamp(_player.Pitch + input.PitchDelta, -80f, 80f);
    }

    private void ApplyMovement(SampleInputState input)
    {
        var deltaTime = _pendingDelta;
        var yawRadians = MathF.PI / 180f * _player.Yaw;

        var forward = new Vector3(MathF.Sin(yawRadians), 0f, MathF.Cos(yawRadians));
        forward = Vector3.Normalize(forward);
        var right = new Vector3(forward.Z, 0f, -forward.X);

        var horizontal = forward * input.MoveZ + right * input.MoveX;
        if (horizontal.LengthSquared() > 0f)
        {
            horizontal = Vector3.Normalize(horizontal) * _horizontalSpeed * deltaTime;
            _player.Position += horizontal;
        }

        if (Math.Abs(input.MoveY) > float.Epsilon)
        {
            _player.Position += Vector3.UnitY * input.MoveY * _verticalSpeed * deltaTime;
        }
    }

    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360f;
        return degrees < 0f ? degrees + 360f : degrees;
    }
}

internal sealed class SampleRenderPipeline : IRenderPipeline<BlockType>
{
    private readonly IVoxelRenderer<BlockType> _renderer;
    private readonly IVoxelMaterialProvider<BlockType> _materials;

    public SampleRenderPipeline(IVoxelRenderer<BlockType> renderer, IVoxelMaterialProvider<BlockType> materials)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _materials = materials ?? throw new ArgumentNullException(nameof(materials));
    }

    public IVoxelRenderResult<BlockType> Render(IGameSession<BlockType> session, IVoxelFrameBuffer? framebuffer)
    {
        var result = _renderer.Render(session.World, session.Player, session.Materials, framebuffer);
        return new VoxelRenderResult<BlockType>(result.Framebuffer, result.Camera);
    }
}
