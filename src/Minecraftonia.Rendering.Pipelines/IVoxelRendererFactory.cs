namespace Minecraftonia.Rendering.Pipelines;

public interface IVoxelRendererFactory<TBlock>
{
    IVoxelRenderer<TBlock> Create(VoxelRendererOptions<TBlock> options);
}
