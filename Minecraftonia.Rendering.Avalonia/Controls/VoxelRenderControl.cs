using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Minecraftonia.Rendering.Avalonia.Presenters;
using Minecraftonia.Rendering.Core;
using Minecraftonia.Rendering.Pipelines;
using Minecraftonia.VoxelEngine;

namespace Minecraftonia.Rendering.Avalonia.Controls;

public sealed class VoxelRenderControl<TBlock> : Control where TBlock : struct
{
    private IVoxelFrameBuffer? _framebuffer;
    private VoxelCamera _camera;
    private bool _renderQueued;

    private IVoxelRenderer<TBlock>? _renderer;
    private IVoxelMaterialProvider<TBlock>? _materials;
    private IVoxelFramePresenter _framePresenter = new WritableBitmapFramePresenter();
    private IVoxelWorld<TBlock>? _world;
    private Player? _player;
    private bool _renderContinuously = true;

    public VoxelRenderControl()
    {
        Focusable = true;
    }

    public IVoxelRenderer<TBlock>? Renderer
    {
        get => _renderer;
        set
        {
            if (!ReferenceEquals(_renderer, value))
            {
                _renderer = value;
                QueueRender();
            }
        }
    }

    public IVoxelMaterialProvider<TBlock>? Materials
    {
        get => _materials;
        set
        {
            if (!ReferenceEquals(_materials, value))
            {
                _materials = value;
                QueueRender();
            }
        }
    }

    public IVoxelFramePresenter FramePresenter
    {
        get => _framePresenter;
        set
        {
            if (!ReferenceEquals(_framePresenter, value))
            {
                _framePresenter.Dispose();
                _framePresenter = value ?? throw new ArgumentNullException(nameof(value));
                QueueRender();
            }
        }
    }

    public IVoxelWorld<TBlock>? World
    {
        get => _world;
        set
        {
            if (!ReferenceEquals(_world, value))
            {
                _world = value;
                QueueRender();
            }
        }
    }

    public Player? Player
    {
        get => _player;
        set
        {
            _player = value;
            QueueRender();
        }
    }

    public bool RenderContinuously
    {
        get => _renderContinuously;
        set
        {
            _renderContinuously = value;
            if (value)
            {
                QueueRender();
            }
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        QueueRender();
        return base.ArrangeOverride(finalSize);
    }

    private void QueueRender()
    {
        if (_renderQueued)
        {
            return;
        }

        _renderQueued = true;
        Dispatcher.UIThread.Post(RenderFrame, DispatcherPriority.Render);
    }

    private void RenderFrame()
    {
        _renderQueued = false;

        var renderer = _renderer;
        var world = _world;
        var materials = _materials;
        var player = _player;

        if (renderer is null || world is null || materials is null || player is null)
        {
            return;
        }

        var result = renderer.Render(world, player, materials, _framebuffer);
        _framebuffer = result.Framebuffer;
        _camera = result.Camera;

        InvalidateVisual();

        if (RenderContinuously)
        {
            QueueRender();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_framebuffer is { IsDisposed: false } framebuffer)
        {
            var destRect = new Rect(Bounds.Size);
            if (destRect.Width <= 0 || destRect.Height <= 0)
            {
                return;
            }

            _framePresenter.Render(context, framebuffer, destRect);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _framebuffer?.Dispose();
        _framebuffer = null;
        _framePresenter.Dispose();
    }
}
