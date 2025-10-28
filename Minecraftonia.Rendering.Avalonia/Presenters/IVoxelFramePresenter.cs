using System;
using Avalonia;
using Avalonia.Media;
using Minecraftonia.VoxelRendering;

namespace Minecraftonia.Rendering.Avalonia.Presenters;

public interface IVoxelFramePresenter : IDisposable
{
    void Render(DrawingContext context, IVoxelFrameBuffer framebuffer, Rect destination);
}
