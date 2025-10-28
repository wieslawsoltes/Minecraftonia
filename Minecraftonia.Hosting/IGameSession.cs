using Minecraftonia.Core;
using Minecraftonia.VoxelEngine;
using Minecraftonia.VoxelRendering;

namespace Minecraftonia.Hosting;

/// <summary>
/// Represents a minimal game session that can be driven by the shared hosting infrastructure.
/// </summary>
/// <typeparam name="TBlock">Block type used by the voxel world.</typeparam>
public interface IGameSession<TBlock>
    where TBlock : struct
{
    IVoxelWorld<TBlock> World { get; }
    Player Player { get; }
    IVoxelMaterialProvider<TBlock> Materials { get; }

    /// <summary>
    /// Allows the session to advance its simulation.
    /// </summary>
    /// <param name="time">Timing information for the tick.</param>
    void Update(GameTime time);
}
