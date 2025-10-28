using System;
using Avalonia.Input;

namespace Minecraftonia.Hosting.Avalonia;

public interface IKeyboardInputSource : IDisposable
{
    bool IsDown(Key key);
    bool WasPressed(Key key);
    void NextFrame();
}
