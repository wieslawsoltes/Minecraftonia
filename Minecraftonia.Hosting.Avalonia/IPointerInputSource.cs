using System;

namespace Minecraftonia.Hosting.Avalonia;

public interface IPointerInputSource : IDisposable
{
    float DeltaX { get; }
    float DeltaY { get; }
    bool IsMouseLookEnabled { get; }

    void EnableMouseLook();
    void DisableMouseLook();
    void QueueWarpToCenter();
    void NextFrame();
}
