using System;
using System.Collections.Generic;
using Avalonia.Input;
using Avalonia.Controls;

namespace Minecraftonia.Hosting.Avalonia;

/// <summary>
/// Tracks keyboard state for a TopLevel and exposes per-frame key information.
/// </summary>
public sealed class KeyboardInputSource : IKeyboardInputSource
{
    private readonly HashSet<Key> _keysDown = new();
    private readonly HashSet<Key> _keysPressed = new();
    private readonly TopLevel _topLevel;

    public KeyboardInputSource(TopLevel topLevel)
    {
        _topLevel = topLevel ?? throw new ArgumentNullException(nameof(topLevel));
        _topLevel.KeyDown += OnKeyDown;
        _topLevel.KeyUp += OnKeyUp;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_keysDown.Add(e.Key))
        {
            _keysPressed.Add(e.Key);
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _keysDown.Remove(e.Key);
    }

    public bool IsDown(Key key) => _keysDown.Contains(key);

    public bool WasPressed(Key key) => _keysPressed.Contains(key);

    public void NextFrame() => _keysPressed.Clear();

    public void Dispose()
    {
        _topLevel.KeyDown -= OnKeyDown;
        _topLevel.KeyUp -= OnKeyUp;
    }
}
