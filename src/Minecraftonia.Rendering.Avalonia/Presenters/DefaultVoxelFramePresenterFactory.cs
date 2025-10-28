namespace Minecraftonia.Rendering.Avalonia.Presenters;

public sealed class DefaultVoxelFramePresenterFactory : IVoxelFramePresenterFactory
{
    public IVoxelFramePresenter Create(FramePresentationMode mode)
    {
        return mode switch
        {
            FramePresentationMode.WritableBitmap => new WritableBitmapFramePresenter(),
            FramePresentationMode.SkiaTexture => new SkiaTextureFramePresenter(),
            _ => new SkiaTextureFramePresenter()
        };
    }
}
