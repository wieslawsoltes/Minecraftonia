using System;
using Avalonia.Controls;
using Minecraftonia.Hosting.Avalonia;

namespace Minecraftonia.Game;

public sealed record GameInputConfiguration(
    Func<TopLevel, IKeyboardInputSource> CreateKeyboard,
    Func<TopLevel, Control, IPointerInputSource> CreatePointer);
