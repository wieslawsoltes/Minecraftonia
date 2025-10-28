using System;
using Minecraftonia.VoxelRendering;

namespace Minecraftonia.Hosting;

/// <summary>
/// Coordinates simulation ticks and rendering using an <see cref="IGameSession{TBlock}"/> and <see cref="IRenderPipeline{TBlock}"/>.
/// </summary>
public sealed class GameHost<TBlock>
    where TBlock : struct
{
    private readonly IGameSession<TBlock> _session;
    private readonly IRenderPipeline<TBlock> _pipeline;
    private GameTime _time;
    private IVoxelFrameBuffer? _frameBuffer;

    public GameHost(IGameSession<TBlock> session, IRenderPipeline<TBlock> pipeline)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _time = new GameTime(TimeSpan.Zero, TimeSpan.Zero);
    }

    /// <summary>
    /// Advances the session and renders a frame.
    /// </summary>
    /// <param name="elapsed">Elapsed time since the last step.</param>
    public IVoxelRenderResult<TBlock> Step(TimeSpan elapsed)
    {
        _time = new GameTime(_time.Total + elapsed, elapsed);
        _session.Update(_time);
        var result = _pipeline.Render(_session, _frameBuffer);
        _frameBuffer = result.Framebuffer;
        LastResult = result;
        return result;
    }

    public IVoxelRenderResult<TBlock>? LastResult { get; private set; }
}
