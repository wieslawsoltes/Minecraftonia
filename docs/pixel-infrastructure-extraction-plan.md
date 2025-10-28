# Shared Pixel Infrastructure Extraction Plan

## Objectives
- Unlock reuse of the CPU voxel renderer, pixel buffers, and lighting helpers outside the game-specific projects.
- Decouple Avalonia/Skia presentation layers from the core voxel rendering pipeline while preserving current performance.
- Ship documentation, samples, and packaging guidance so other apps can consume the shared rendering primitives safely.

## Current Hotspots
- `Minecraftonia.Rendering.Pipelines` now hosts `VoxelRayTracer<TBlock>`, GI tooling, FXAA helpers, and the reusable `VoxelRaycaster`, replacing the legacy `Minecraftonia.VoxelRendering` assembly.
- `Minecraftonia.Rendering.Avalonia` provides `WritableBitmapFramePresenter` and `SkiaTextureFramePresenter` that expose Avalonia-specific frame presentation but sit directly on top of `IVoxelFrameBuffer`.
- `src/Minecraftonia.Game/GameControl.cs` wires together input, hosting, renderer selection, and presentation, making it hard to reuse rendering without the rest of the game shell.
- `Minecraftonia.Hosting.Avalonia` and `samples/Minecraftonia.Sample.BasicBlock` reimplement similar frame-buffer handling and presenter wiring logic.

## Pixel/Voxel Presentation Inventory
- **Controls & Hosts**
  - `src/Minecraftonia.Rendering.Avalonia/Controls/VoxelRenderControl.cs` – Avalonia control that queues renderer execution on the UI thread, manages `IVoxelFramePresenter`, and invalidates visuals when the CPU framebuffer updates.
  - `src/Minecraftonia.Game/GameControl.cs` – Game-specific control that now consumes `GameControlConfiguration` (`GameControl.Configure`) to obtain renderer/presenter factories before wiring `GameHost`, animation, and pause lifecycle hooks.
  - `samples/Minecraftonia.Sample.BasicBlock/SampleGameControl.cs` – Minimal hosting control mirroring `GameControl` concepts using `RenderingConfigurationFactory` for renderer/presenter composition.
  - `samples/Minecraftonia.Sample.Doom/Program.cs` – CLI renderer that builds a static voxel world and writes a PPM frame, exercising the shared pipelines without Avalonia.
  - `samples/Minecraftonia.Sample.Doom.Avalonia/DoomGameControl.cs` – Interactive Avalonia host that uses the shared rendering/input configuration to explore the Doom-inspired voxel hall with mouse look.
  - `src/Minecraftonia.Game/GameControlConfiguration.cs` & `Minecraftonia.Sample.BasicBlock/RenderingConfigurationFactory.cs` – Composition roots that bundle factories, materials, and world config for reuse.
  - `src/Minecraftonia.Hosting/GameHost.cs` – Simulation/render orchestrator that stores the reusable `IVoxelFrameBuffer` returned by the render pipeline.
- **Presenters & UI Bridges**
  - `src/Minecraftonia.Rendering.Avalonia/RenderingConfiguration.cs` – Bundles presenter and renderer factory references alongside materials for DI consumers.
  - `src/Minecraftonia.Rendering.Core/IVoxelFramePresenter.cs` – Abstraction for presenting CPU-generated pixel buffers shared across UI bridges.
  - `src/Minecraftonia.Rendering.Avalonia/Presenters/WritableBitmapFramePresenter.cs` – Copies BGRA8888 pixels into an Avalonia `WriteableBitmap` and draws it to the control surface.
  - `src/Minecraftonia.Rendering.Avalonia/Presenters/SkiaTextureFramePresenter.cs` – Uploads pixels into an Skia `SKSurface` for GPU-backed presentation, including a custom draw operation.
  - `src/Minecraftonia.Rendering.Avalonia/Presenters/IVoxelFramePresenterFactory.cs` & `DefaultVoxelFramePresenterFactory.cs` – Provide factory indirection so hosts can request presenters without referencing concrete Avalonia types.
  - `src/Minecraftonia.Game/GameControl.cs` – Requests presenters per `FramePresentationMode` through the factory, disposing the old presenter on swap.
- **CPU Rendering & Pipelines**
  - `src/Minecraftonia.Rendering.Pipelines/VoxelRayTracer.cs` – CPU ray-marched renderer generating `VoxelRenderResult<TBlock>` instances.
  - `src/Minecraftonia.Rendering.Pipelines/IVoxelRenderer.cs` – Renderer abstraction consumed by both game and sample pipelines.
  - `src/Minecraftonia.Rendering.Pipelines/IVoxelRendererFactory.cs` & `VoxelRayTracerFactory.cs` – Facade factories that construct renderers from neutral options so hosting layers never depend on implementation details.
  - `src/Minecraftonia.Game/Rendering/DefaultGameRenderer.cs` – Wraps an `IVoxelRenderer<BlockType>` and returns `GameRenderResult`.
  - `src/Minecraftonia.Game/Rendering/GameRenderResult.cs` – Carries `IVoxelFrameBuffer` and `VoxelCamera` for the game-specific pipeline.
  - `src/Minecraftonia.Game/GameControl.cs` (nested `GameRenderPipeline`) – Adapts `IGameSession<BlockType>` to `IRenderPipeline<BlockType>`.
  - `samples/Minecraftonia.Sample.BasicBlock/SampleGameControl.cs` (nested `SampleRenderPipeline`) – Sample-specific pipeline combining the ray tracer and writable-bitmap presenter.
- **Frame Buffers & Pixel Data**
  - `src/Minecraftonia.Rendering.Core/IVoxelFrameBuffer.cs` and `VoxelFrameBuffer.cs` – Allocate and resize BGRA8888 buffers, expose span accessors, guard against use-after-dispose.
  - `src/Minecraftonia.Rendering.Pipelines/VoxelRenderResult.cs` – Couples framebuffer and `VoxelCamera`.
  - `src/Minecraftonia.Hosting/GameHost.cs` – Persists the reusable framebuffer across render steps.
  - `src/Minecraftonia.Rendering.Avalonia/Controls/VoxelRenderControl.cs` – Holds onto the latest framebuffer to paint during `Render`.
- **Lighting & Global Illumination**
  - `src/Minecraftonia.Rendering.Core/VoxelLightingMath.cs` – Computes per-face normals, UVs, and light falloff.
  - `src/Minecraftonia.Rendering.Pipelines/GlobalIlluminationEngine.cs` – Performs secondary bounce sampling, sun shadowing, and ambient occlusion.
  - `src/Minecraftonia.Rendering.Pipelines/GlobalIlluminationSamples.cs` – Stratified hemisphere sample patterns used by GI routines.
  - `src/Minecraftonia.Rendering.Pipelines/RenderSamplePattern.cs` & `FxaaSharpenFilter.cs` – Provide reusable sampling and FXAA/sharpen tooling invoked by CPU renderers.
  - `src/Minecraftonia.Rendering.Pipelines/VoxelRayTracer.cs` – Integrates GI calculations and post-processing while deferring shared math to the pipelines project.

## Cross-Project Coupling & Contracts
- **Frame buffer ownership and reuse**
  - `src/Minecraftonia.Rendering.Pipelines/VoxelRayTracer.cs:301` calls `EnsureFramebuffer` to reuse the optional buffer instance, while `src/Minecraftonia.Hosting/GameHost.cs:32` persists the last `IVoxelFrameBuffer` returned by the pipeline and passes it back on the next step. Any alternative renderer or host must respect this reuse contract to avoid allocations or type mismatches.
  - `src/Minecraftonia.Game/GameControl.cs:355` and `samples/Minecraftonia.Sample.BasicBlock/SampleGameControl.cs:193` cache the framebuffer from `GameHost.Step`, assuming the object outlives the render pass and will be presented later on the UI thread.
- **Pixel layout assumptions**
  - `src/Minecraftonia.Rendering.Core/VoxelFrameBuffer.cs:10` fixes stride to `width * 4`, and `src/Minecraftonia.Rendering.Pipelines/VoxelRayTracer.cs:506` writes pixels in BGRA order with premultiplied alpha. Both `src/Minecraftonia.Rendering.Avalonia/Presenters/WritableBitmapFramePresenter.cs:34` and `src/Minecraftonia.Rendering.Avalonia/Presenters/SkiaTextureFramePresenter.cs:42` rely on that layout by copying into `PixelFormat.Bgra8888`/`SKColorType.Bgra8888` surfaces using the provided stride. Any alternative framebuffer implementation must expose identical layout semantics via `Pixels`, `Span`, and `Stride`.
- **Disposal responsibilities**
  - UI layers own presenter lifetimes: `src/Minecraftonia.Rendering.Avalonia/Controls/VoxelRenderControl.cs:56` and `src/Minecraftonia.Game/GameControl.cs:861` dispose the current `IVoxelFramePresenter` when swapping implementations, mirroring the sample control’s cleanup (`samples/Minecraftonia.Sample.BasicBlock/SampleGameControl.cs:116`).
  - Framebuffers returned from renderers are disposed by their owning controls when detached (`src/Minecraftonia.Rendering.Avalonia/Controls/VoxelRenderControl.cs:171`, `src/Minecraftonia.Game/GameControl.cs:223`, sample control at `samples/Minecraftonia.Sample.BasicBlock/SampleGameControl.cs:112`), reinforcing that render pipelines must not dispose the buffer themselves.
- **Camera and material coupling**
  - `src/Minecraftonia.Game/Rendering/DefaultGameRenderer.cs:25` forwards `VoxelRenderResult.Framebuffer` and `VoxelRenderResult.Camera` directly into `GameRenderResult`, while UI controls store the `VoxelCamera` for HUD/use (`src/Minecraftonia.Game/GameControl.cs:357`, `samples/Minecraftonia.Sample.BasicBlock/SampleGameControl.cs:195`). Renderer outputs therefore need to maintain camera state consistency across projects.
- **Presenter dependency on concrete buffers**
  - Avalonia presenters access the raw `byte[]` via `IVoxelFrameBuffer.Pixels` (`src/Minecraftonia.Rendering.Avalonia/Presenters/WritableBitmapFramePresenter.cs:34`, `src/Minecraftonia.Rendering.Avalonia/Presenters/SkiaTextureFramePresenter.cs:47`) rather than spans only, binding the abstraction to GC-backed arrays. Any move to native memory or GPU surfaces must preserve this API or introduce adapters.
- **Render pipeline interfaces**
  - `src/Minecraftonia.Hosting/IRenderPipeline.cs:7` expects implementations to accept an optional framebuffer and return `IVoxelRenderResult<TBlock>`; both `src/Minecraftonia.Game/GameControl.cs:372` and the sample pipeline delegate to renderer services that honor this contract. Future shared pipelines must keep the optional framebuffer parameter to remain drop-in compatible.
- **Factory indirection**
  - `src/Minecraftonia.Game/GameControl.cs:119` and `samples/Minecraftonia.Sample.BasicBlock/SampleGameControl.cs:64` now rely on `src/Minecraftonia.Rendering.Pipelines/IVoxelRendererFactory.cs` and `src/Minecraftonia.Rendering.Avalonia/Presenters/IVoxelFramePresenterFactory.cs` to acquire renderers/presenters, keeping Avalonia/Skia specifics out of hosting logic.
  - `src/Minecraftonia/MainWindow.axaml.cs:32` configures `GameControl` with `GameControlConfiguration.CreateDefault()`, providing a single composition point for renderer and presenter services in the main application.
  - `src/Minecraftonia.Game/GameControlConfiguration.cs:23` also wires `GameInputConfiguration` and `IGameSaveService` so hosts provide keyboard/pointer capture and persistence without touching Avalonia-specific types or filesystem details.

## Dependency Map
- `Minecraftonia.Rendering.Pipelines` → `Minecraftonia.Rendering.Core`, `Minecraftonia.VoxelEngine` (renderer implementations depend on shared buffers and voxel world abstractions).
- `Minecraftonia.Rendering.Avalonia` → `Minecraftonia.Rendering.Pipelines`, `Minecraftonia.Rendering.Core`, `Minecraftonia.Core` (requires pipeline renderers, buffer primitives, and game block metadata for control bindings).
- `Minecraftonia.Hosting` → `Minecraftonia.Rendering.Pipelines`, `Minecraftonia.VoxelEngine`, `Minecraftonia.Core` (manages game sessions and render pipelines while retaining framebuffer instances).
- `Minecraftonia.Hosting.Avalonia` → `Minecraftonia.Hosting`, `Minecraftonia.Rendering.Avalonia`, `Minecraftonia.Core` (wraps Avalonia input and forwards to hosting abstractions).
- `Minecraftonia.Game` → `Minecraftonia.Rendering.Avalonia`, `Minecraftonia.Rendering.Pipelines`, `Minecraftonia.Hosting`, `Minecraftonia.Hosting.Avalonia`, plus content/core projects (assembles renderer, materials, and presenters for the main game control).
- `samples/Minecraftonia.Sample.BasicBlock` → `Minecraftonia.Rendering.Avalonia`, `Minecraftonia.Rendering.Pipelines`, `Minecraftonia.Hosting`, `Minecraftonia.Hosting.Avalonia`, `Minecraftonia.Content`, `Minecraftonia.Core` (validates shared abstractions in a lightweight host).

## Proposed Library Layout & Public Surface
- **Minecraftonia.Rendering.Core**
  - **Purpose**: Provide platform-neutral pixel buffer, camera, and lighting math primitives with no UI dependencies.
  - **Contents**: `VoxelSize`, `VoxelCamera`, `VoxelFrameBuffer`, `IVoxelFrameBuffer`, `IPixelSpan` helper abstractions, `VoxelLightingMath`, FXAA/sharpen helpers, global illumination settings/value types.
  - **Target framework**: `net8.0` (to increase downstream compatibility) with conditional `net9.0` intrinsics where available via `$(TargetFrameworks)`.
  - **Public API**: Minimal surface exposing immutable camera structs, buffer management interfaces, lighting math utilities, and factory helpers for creating frame buffers or sample patterns.
- **Minecraftonia.Rendering.Pipelines**
  - **Purpose**: Host CPU rendering algorithms and higher-level renderer abstractions that depend on the core primitives but remain UI-agnostic.
  - **Contents**: `IVoxelRenderer<T>`, `IVoxelRenderResult<T>`, `VoxelRenderResult<T>`, `VoxelRayTracer<T>`, global illumination engine, sampling pattern services, renderer configuration records.
  - **Target framework**: `net8.0` with optional `net9.0` multi-targeting for SIMD intrinsics; references `Minecraftonia.Rendering.Core` and `Minecraftonia.VoxelEngine`.
  - **Public API**: Renderer factory methods, configuration records, and interfaces that accept core abstractions (`IVoxelFrameBuffer`, `VoxelCamera`). Keep implementation details internal while allowing consumers to subclass or compose render pipelines.
- **Minecraftonia.Rendering.Avalonia**
  - **Purpose**: Provide Avalonia-specific adapters that present CPU buffers through writable bitmap and Skia pathways, plus ready-made controls for Avalonia apps.
  - **Contents**: `IVoxelFramePresenter`, writable bitmap/Skia presenters, `VoxelRenderControl<T>`, cursor utilities. Update to depend on `Minecraftonia.Rendering.Core` and `Minecraftonia.Rendering.Pipelines`.
  - **Target framework**: `net9.0` (aligned with Avalonia 11 requirements).
  - **Public API**: Presenter interfaces, control types, and factory helpers. Keep Avalonia glue internal to preserve abstraction boundaries.
- **Minecraftonia.Rendering.Hosting**
  - **Purpose**: Surface reusable hosting primitives (current `GameHost<T>`, `IRenderPipeline<T>`, sample loop utilities) without UI dependencies.
  - **Contents**: Move hosting types from `Minecraftonia.Hosting` that relate purely to render loop orchestration into this library, referencing `Minecraftonia.Rendering.Pipelines`.
  - **Target framework**: `net8.0`.
  - **Public API**: `IRenderPipeline<T>`, `GameHost<T>`, timing utilities and pipeline adapters for sessions.
- **Minecraftonia.Rendering.Hosting.Avalonia**
  - **Purpose**: Avalonia-specific input bridges layered over the hosting abstractions.
  - **Contents**: Current `KeyboardInputSource`, `PointerInputSource`, focus/mouse look helpers.
  - **Target framework**: `net9.0`.
  - **Public API**: Input source types and registration helpers.
- **Packaging Guidance**
  - Publish `Minecraftonia.Rendering.Core` (now created) and `Minecraftonia.Rendering.Pipelines` (now hosting GI, DDA, and FXAA helpers) as independent NuGet packages to enable reuse in non-Avalonia scenarios.
  - Bundle Avalonia-specific projects (`Minecraftonia.Rendering.Avalonia`, `Minecraftonia.Rendering.Hosting.Avalonia`) separately to keep UI dependencies optional.
  - Ensure `internalsVisibleTo` is avoided so consumers rely on documented interfaces; provide XML docs and simple samples for each package.

## Shared Core Abstractions (Draft Surface)
- **Pixel Buffers**
  - `IPixelBuffer` – exposes `PixelSize`, `Stride`, `ReadOnlySpan<byte> GetReadOnlySpan()`, `Span<byte> GetSpan()`, `MemoryHandle Pin()` for unsafe interop, and `Resize(PixelSize size)` to formalize resizing semantics while allowing alternative storage backends (managed arrays, pooled memory, GPU staging buffers).
  - `IPixelBufferAllocator` – factory interface returning `IPixelBuffer` instances sized for a given format; enables dependency injection in render pipelines and hosting environments.
  - `PixelFormatDescriptor` – value type documenting channel order (`Bgra8888` by default), premultiplication state, and bytes-per-pixel to avoid hard-coded assumptions in presenters.
- **Frame Presentation Contracts**
  - `IFramePresenter` – framework-neutral contract that accepts an `IPixelBuffer` plus a destination descriptor (e.g., viewport size or target surface abstraction) to decouple presentation from Avalonia-specific `DrawingContext`.
  - `FramePresentationContext` – record providing target bounds, scaling hints, and timing metadata so presenters can optimize uploads (e.g., throttle when off-screen).
  - `IFramePresenterFactory` – responsible for constructing presenters based on environment capabilities (software copy, GPU texture upload, remote streaming).
- **Render Camera & Context**
  - `ICameraLens` – interface describing FOV, aspect ratio, and projection helper methods; `VoxelCamera` becomes a concrete implementation for voxel renderers while allowing other camera models.
  - `IRenderViewTransform` – encapsulates camera orientation vectors (`Forward`, `Right`, `Up`) and helper methods for ray generation to centralize the math currently spread across `VoxelRayTracer`.
  - `IRenderSamplePattern` – provides stratified sample offsets and metadata (sample count, jitter type) so anti-aliasing strategies can be swapped without embedding arrays inside the renderer.
  - `IRenderPipelineContext` – aggregates references to `IVoxelWorld<T>`, `IPixelBuffer`, `ICameraLens`, and timing data, giving pipelines a single object to consume when rendering frames.
## Numbered Tasks
1. [ ] Capture a performance and correctness baseline by building the solution (`Minecraftonia.sln`) and running the main game plus `samples/Minecraftonia.Sample.BasicBlock` with diagnostics enabled to record current frame times and output parity.
2. [ ] Inventory every pixel/voxel presentation touchpoint across the solution (controls, presenters, ray tracers, lighting math, GI) and map dependencies between `Minecraftonia.Rendering.Pipelines`, `Minecraftonia.Rendering.Avalonia`, `Minecraftonia.Game`, and hosting projects.
3. [ ] Document cross-project coupling (e.g., `VoxelFrameBuffer` used in `Minecraftonia.Game.Rendering.DefaultGameRenderer`, Skia presenters depending on `IVoxelFrameBuffer`) along with implicit contracts such as BGRA8888 layout and stride alignment requirements.
4. [ ] Propose a shared library layout that splits responsibilities into (a) pixel buffer & math primitives, (b) CPU rendering pipelines, and (c) UI bridge adapters, including target frameworks and desired public API surface.
5. [ ] Define neutral abstractions for pixel buffers, frame presentation, and render contexts (e.g., `IPixelBuffer`, `IRenderSamplePattern`, `ICameraLens`) that live in the new shared core and formalize requirements currently expressed informally.
6. [x] Create a new `Minecraftonia.Rendering.Core` project (or equivalent name) with zero UI dependencies that hosts `VoxelSize`, `VoxelCamera`, `VoxelFrameBuffer`, math helpers, and shared enums/records.
7. [x] Extract buffer management code (`VoxelFrameBuffer`, `IVoxelFrameBuffer`) and related validation logic from `Minecraftonia.VoxelRendering` into the new core while maintaining binary compatibility for existing consumers through forwarding or type moves.
8. [x] Relocate camera, sampling pattern, and lighting math (`VoxelLightingMath`, `Lighting/GlobalIlluminationEngine<TBlock>`, FXAA/sharpen helpers) into the core or a dedicated `Minecraftonia.Rendering.Pipelines` assembly with appropriate namespaces.
9. [x] Move `VoxelRayTracer<TBlock>` and associated pipeline structs into the shared pipeline project, refactoring constructor dependencies to rely solely on the new abstractions instead of concrete game types.
10. [x] Introduce facade interfaces or factory services so hosting layers (`Minecraftonia.Hosting`, `Minecraftonia.Game`) can request renderers and presenters without direct knowledge of Avalonia/Skia implementation details.
11. [x] Update `Minecraftonia.Rendering.Avalonia` to depend on the shared core for buffer primitives, move `IVoxelFramePresenter` into a reusable abstractions namespace, and strip duplicate buffer safety checks now handled centrally.
12. [x] Refactor `src/Minecraftonia.Game/GameControl.cs`, `Minecraftonia.Game.Rendering`, and `Minecraftonia.Hosting.Avalonia` to compose the new shared services through dependency injection or explicit configuration objects.
13. [x] Adjust `Minecraftonia.VoxelRendering` (if kept) to either wrap the new pipeline types or slim down to extension helpers, ensuring project references and `csproj` files remain consistent across the solution.
14. [ ] Build targeted regression and performance tests covering buffer resizing, FXAA/sharpen passes, GI toggles, and multi-sample rendering to guard the extracted libraries against regressions.
15. [x] Expand the samples folder with at least one non-Avalonia consumer (e.g., a CLI image renderer or different UI stack) to validate that the shared pixel primitives work outside the Avalonia ecosystem.
16. [ ] Update documentation (`docs/refactor-plan.md`, README, and new API references) to explain the shared rendering architecture, including migration instructions for downstream projects and packaging strategy (NuGet metadata, versioning).
17. [ ] Run end-to-end smoke tests on the main game, hosting layer, and all samples to verify rendering output matches the baseline, and capture before/after metrics for future regression tracking.
