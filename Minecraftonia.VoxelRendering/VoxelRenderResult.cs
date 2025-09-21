using Avalonia.Media.Imaging;

namespace Minecraftonia.VoxelRendering;

public readonly struct VoxelRenderResult<TBlock>
{
    public VoxelRenderResult(WriteableBitmap framebuffer, VoxelCamera camera)
    {
        Framebuffer = framebuffer;
        Camera = camera;
    }

    public WriteableBitmap Framebuffer { get; }
    public VoxelCamera Camera { get; }
}
