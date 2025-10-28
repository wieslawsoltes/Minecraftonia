# Reusable Rendering Refactor Plan

## Key Findings

- `Minecraftonia/Game/GameControl.cs:19` currently owns input, camera, ray tracing, and frame-presenter logic, making it difficult to reuse the renderer without the rest of the game.
- `Minecraftonia/Game/WritableBitmapFramePresenter.cs:11` and `Minecraftonia/Game/SkiaTextureFramePresenter.cs:11` implement generic bitmap/Skia presentation inside the game project instead of a shared UI bridge.
- `Minecraftonia.VoxelRendering/VoxelFrameBuffer.cs:6` and related renderer types depend on `Avalonia.PixelSize`, preventing consumption from other UI stacks.
- `Minecraftonia.Game/MinecraftoniaGame.cs:33` and `Minecraftonia.Game/BlockTextures.cs:10` bundle palette, materials, saves, and world config together; meanwhile `Minecraftonia.WaveFunctionCollapse/BlockType.cs:3` defines `BlockType`, pulling in optional systems for any consumer.

## Target Modularization

- Establish `Minecraftonia.Core` for shared primitives such as block enums, world configuration, and math helpers, with no UI dependencies.
- Keep `Minecraftonia.VoxelEngine` focused on simulation while depending only on the new core; expose a slim `IVoxelWorld<TBlock>` API for renderers and tooling.
- Refactor `Minecraftonia.VoxelRendering` into a UI-agnostic library that works with framework-neutral buffer and size abstractions.
- Introduce `Minecraftonia.Rendering.Avalonia` to host writable-bitmap and Skia presenters along with an Avalonia `VoxelRenderControl`.
- Move reusable material and camera helpers into a dedicated shared library (e.g. `Minecraftonia.Content`), leaving `Minecraftonia.Game` with game-specific orchestration, menus, saves, and optional WFC/OSM modules.

## Phased Work Plan

1. Baseline and Core Extraction  
   Build the current solution, then add `Minecraftonia.Core` and migrate `BlockType`, extensions, and `MinecraftoniaWorldConfig` into it with minimal disruption.
2. Renderer Decoupling  
   Remove Avalonia-specific structs from `Minecraftonia.VoxelRendering`, replacing them with neutral abstractions and introducing renderer interfaces.
3. UI Bridges  
   Create `Minecraftonia.Rendering.Avalonia`, move the frame presenters there, and refactor `GameControl` to compose injected renderer services.
4. Gameplay Library Split  
   Relocate shared content helpers into the new shared library and slim `Minecraftonia.Game` to orchestrate gameplay features.
5. Cleanup and Documentation  
   Update solution/project files, CI pipelines, and documentation; perform smoke tests for both the main game and new sample app.

## Sample App Outline

- Create `samples/Minecraftonia.Sample.BasicBlock` referencing the shared core, engine, rendering, and Avalonia bridge projects.
- Demonstrate writable-bitmap rendering of a single block with simple camera orbit controls and an optional toggle for the Skia pathway.
- Document how the sample wires abstractions together so developers can plug in their own voxel content.

## Task Checklist

1. [x] Verify the current solution builds and the existing game runs to capture a baseline before code moves.
2. [x] Introduce a `Minecraftonia.Core` project that hosts shared primitives (block enums, world config, math helpers) with zero Avalonia dependencies.
3. [x] Relocate `BlockType`, extension helpers, and `MinecraftoniaWorldConfig` into `Minecraftonia.Core`, then update all projects to consume the new types.
4. [x] Review `Minecraftonia.VoxelEngine` so it depends only on `Minecraftonia.Core`; extract an `IVoxelWorld<TBlock>` surface for downstream consumers.
5. [x] Refactor `Minecraftonia.VoxelRendering` to remove Avalonia-specific structs, relying on framework-neutral buffer and size abstractions.
6. [x] Add renderer interfaces (`IVoxelFrameBuffer`, `IVoxelRenderer`, material samplers) that separate simulation data from presentation.
7. [x] Create a `Minecraftonia.Rendering.Avalonia` project containing writable-bitmap and Skia presenters plus an Avalonia `VoxelRenderControl`.
8. [x] Update `GameControl` to compose the new Avalonia presenter, delegating engine, input, and rendering responsibilities to injectable collaborators.
9. [x] Move reusable content helpers (`BlockTextures`, camera utilities) into a `Minecraftonia.Content` (or similarly named) shared library.
10. [x] Slim `Minecraftonia.Game` to game-specific orchestration (menus, saves, WFC/OSM opt-in modules) while consuming the shared libraries.
11. [x] Add a `samples/Minecraftonia.Sample.BasicBlock` project showcasing writable-bitmap rendering of a single block with simple camera controls.
12. [x] Refresh documentation and CI scripts to include the new projects, and record smoke-test notes for both the main game and sample app.
