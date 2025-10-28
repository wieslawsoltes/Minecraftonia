namespace Minecraftonia.Rendering.Avalonia.Presenters;

public interface IVoxelFramePresenterFactory
{
    IVoxelFramePresenter Create(FramePresentationMode mode);
}
