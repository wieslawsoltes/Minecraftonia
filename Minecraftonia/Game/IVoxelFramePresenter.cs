using System;
using Avalonia;
using Avalonia.Media;
using Minecraftonia.VoxelRendering;

namespace Minecraftonia.Game;

internal interface IVoxelFramePresenter : IDisposable
{
    void Render(DrawingContext context, VoxelFrameBuffer framebuffer, Rect destination);
}
