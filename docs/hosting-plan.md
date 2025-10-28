# Hosting Refactor Plan

1. [x] Create `Minecraftonia.Hosting` library with reusable render loop infrastructure – includes `GameTime`, `IGameSession<TBlock>`, `IRenderPipeline<TBlock>`, and `GameHost<TBlock>` wrappers that coordinate updates and rendering.
2. [x] Extract shared input and camera controllers into `Minecraftonia.Hosting.Avalonia`, reusing logic from `GameControl`.
3. [x] Refactor `GameControl` to compose the hosting services instead of owning render/input loops directly.
4. [x] Update `samples/Minecraftonia.Sample.BasicBlock` to rely on the hosting package for game-like behaviour.
5. [x] Refresh docs and CI samples to point to the new hosting primitives, and verify builds/tests.
