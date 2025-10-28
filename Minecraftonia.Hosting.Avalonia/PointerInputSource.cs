using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Minecraftonia.Rendering.Avalonia.Input;

namespace Minecraftonia.Hosting.Avalonia;

/// <summary>
/// Recreates the game control's pointer-capture behaviour so hosted controls can consume relative mouse deltas.
/// </summary>
public sealed class PointerInputSource : IDisposable
{
    private readonly TopLevel _topLevel;
    private readonly Control _target;

    private IPointer? _capturedPointer;
    private bool _requestPointerCapture;
    private bool _ignoreWarpPointerMove;
    private Point? _lastPointerPosition;
    private PixelPoint? _lastWarpPoint;

    public float DeltaX { get; private set; }
    public float DeltaY { get; private set; }

    public bool IsMouseLookEnabled { get; private set; }

    public PointerInputSource(TopLevel topLevel, Control target)
    {
        _topLevel = topLevel ?? throw new ArgumentNullException(nameof(topLevel));
        _target = target ?? throw new ArgumentNullException(nameof(target));

        _topLevel.PointerMoved += OnPointerMoved;
        _topLevel.PointerPressed += OnPointerPressed;
        _topLevel.PointerReleased += OnPointerReleased;
        _topLevel.PointerCaptureLost += OnPointerCaptureLost;
    }

    public void EnableMouseLook()
    {
        if (IsMouseLookEnabled)
        {
            return;
        }

        IsMouseLookEnabled = true;
        _requestPointerCapture = true;
        _lastPointerPosition = null;
        QueueWarpToCenter();
    }

    public void DisableMouseLook()
    {
        if (!IsMouseLookEnabled)
        {
            return;
        }

        ReleaseCapture();
        IsMouseLookEnabled = false;
        _requestPointerCapture = false;
        _ignoreWarpPointerMove = false;
        _lastPointerPosition = null;
        _lastWarpPoint = null;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_requestPointerCapture)
        {
            CapturePointer(e.Pointer);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsMouseLookEnabled)
        {
            ReleaseCapture();
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer == _capturedPointer)
        {
            _capturedPointer = null;
            _lastPointerPosition = null;
            if (IsMouseLookEnabled)
            {
                _requestPointerCapture = true;
            }
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsMouseLookEnabled)
        {
            return;
        }

        if ((_capturedPointer is null || _capturedPointer != e.Pointer) &&
            (_requestPointerCapture || e.Pointer.Captured != _target))
        {
            CapturePointer(e.Pointer);
            _requestPointerCapture = false;
            _lastPointerPosition = null;
        }

        var position = e.GetPosition(_target);

        if (_ignoreWarpPointerMove)
        {
            _ignoreWarpPointerMove = false;
            _lastPointerPosition = position;
            return;
        }

        if (_lastPointerPosition.HasValue)
        {
            var delta = position - _lastPointerPosition.Value;
            DeltaX += (float)delta.X;
            DeltaY += (float)delta.Y;
        }

        _lastPointerPosition = position;
        QueueWarpToCenter();
    }

    private void CapturePointer(IPointer pointer)
    {
        _capturedPointer = pointer;
        pointer.Capture(_target);
    }

    private void ReleaseCapture()
    {
        if (_capturedPointer is not null)
        {
            _capturedPointer.Capture(null);
            _capturedPointer = null;
        }
    }

    private void QueueWarpToCenter(bool allowRetry = true)
    {
        if (!IsMouseLookEnabled)
        {
            return;
        }

        var bounds = _target.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            if (allowRetry)
            {
                Dispatcher.UIThread.Post(() => QueueWarpToCenter(false), DispatcherPriority.Input);
            }

            return;
        }

        var center = new Point(bounds.Width / 2d, bounds.Height / 2d);
        var screenPoint = _target.PointToScreen(center);

        if (MouseCursorUtils.TryWarpPointer(screenPoint))
        {
            _ignoreWarpPointerMove = true;
            _lastPointerPosition = center;
            _lastWarpPoint = screenPoint;
        }
    }

    public void QueueWarpToCenter() => QueueWarpToCenter(true);

    public void NextFrame()
    {
        DeltaX = 0f;
        DeltaY = 0f;
    }

    public void Dispose()
    {
        DisableMouseLook();
        _topLevel.PointerMoved -= OnPointerMoved;
        _topLevel.PointerPressed -= OnPointerPressed;
        _topLevel.PointerReleased -= OnPointerReleased;
        _topLevel.PointerCaptureLost -= OnPointerCaptureLost;
    }
}
