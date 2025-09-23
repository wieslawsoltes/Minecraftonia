# Minecraftonia

Minecraftonia is a work-in-progress voxel sandbox built with C# 13/.NET 9 and Avalonia. The project experiments with custom voxel ray tracing, procedural terrain, and responsive desktop UI while staying fully cross-platform.

## Features

- **Custom voxel engine** – handcrafted player physics, block interactions, and chunkless world representation tuned for experimentation.
- **Software ray tracing** – real-time voxel ray marcher that writes into a CPU framebuffer which can be presented either via the legacy Avalonia `WriteableBitmap` path or uploaded to a Skia `GRContext`-backed texture for crisp scaling without traditional GPU shaders.
- **Procedural terrain generation** – legacy layered noise with caves, shoreline dressing, and tree placement, plus a height-map importer for real-world terrain experiments.
- **Composable UI shell** – Avalonia-based menus with keyboard/mouse controls, full pointer capture, and configurable sensitivity/inversion.
- **Save and resume** – JSON save files capture world blocks, player state, and palette selection for persistence between sessions.

## Getting Started

### Prerequisites

- .NET SDK 9.0 preview or newer (`dotnet --info` to confirm)
- macOS, Windows, or Linux desktop with GPU capable of handling software rendering at 360×202 internal resolution
- Optional: IDE with Avalonia support (JetBrains Rider, Visual Studio, VS Code + Avalonia extension)

### Clone and run

```bash
git clone https://github.com/<your-account>/Minecraftonia.git
cd Minecraftonia

dotnet build Minecraftonia.sln

dotnet run --project Minecraftonia
```

The application launches directly into the main menu. Use the pointer to navigate or the keyboard (`Enter`/`Space`) to activate focused buttons.

## Build & Publish

### Local release publish

Minecraftonia ships as a self-contained desktop app for each OS. To publish locally:

```bash
# Linux x64
dotnet publish Minecraftonia/Minecraftonia.csproj -c Release -r linux-x64 --self-contained true -o publish/linux-x64

# macOS Apple Silicon
dotnet publish Minecraftonia/Minecraftonia.csproj -c Release -r osx-arm64 --self-contained true -o publish/osx-arm64

# Windows x64
dotnet publish Minecraftonia/Minecraftonia.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
```

Each command creates an OS-specific directory under `publish/` ready to zip and distribute.

### GitHub Actions workflow

The repository provides a cross-platform CI/CD pipeline in [`.github/workflows/build-and-release.yml`](.github/workflows/build-and-release.yml):

1. Triggered on pushes to `main`, pull requests, and tags prefixed with `v`.
2. Builds `linux-x64`, `osx-arm64`, and `win-x64` self-contained binaries using .NET 9 (`dotnet publish ... --self-contained true`).
3. Strips debug symbols for smaller artifacts and archives each build (`zip`/`Compress-Archive`).
4. Uploads zipped artifacts for each runtime identifier.
5. On tagged commits, creates a GitHub Release and attaches the platform archives automatically.

To cut a release, create and push a semver tag (e.g., `git tag v0.3.0 && git push origin v0.3.0`). The action produces ready-to-download archives under the new release.

## Gameplay Controls

| Action | Default | Notes |
| --- | --- | --- |
| Move | `W A S D` | Arrow keys rotate the camera; `Q`/`E` nudge yaw without the mouse |
| Sprint | `Shift` | Hold while moving to increase travel speed |
| Jump | `Space` | Requires ground contact |
| Look | Mouse | `F1` toggles pointer capture; `Esc` releases capture |
| Break Block | Left mouse button | Uses the current ray-traced hit |
| Place Block | Right mouse button | Places the selected hotbar block |
| Pause/Menu | `Esc` | Opens the in-game menu overlay |
| Invert X / Y | `F2` / `F3` | Toggles independently |
| Sensitivity | `+` / `-` | Adjusts mouse-look multiplier |
| Hotbar | `1`-`7`, `Tab`, scroll wheel | Scroll cycles palette |
| Re-roll world | `F5` | Swaps in a real-world inspired terrain snapshot with remapped materials |

Pause the game with `Esc`, then resume, save/exit, or return to the main menu.

## Wave Function Collapse Terrain

Minecraftonia now ships with a voxel-aware Wave Function Collapse (WFC) generator tuned for Minecraft-like landscapes. Patterns are defined as 8×HEIGHT×8 voxel tiles with annotated edges (grass, shore, water, cliff, forest). The solver collapses a grid of these 3D patterns, honouring edge tags in all directions so terrain transitions stay coherent.

- Press `F5` to rebuild the world with a fresh WFC seed.
- Patterns live in `VoxelPatternLibraryFactory.CreateDefault` and can be extended or tweaked without touching the solver.
- Each pattern exposes a `BlockType[,,]` payload plus sets of edge tags. Two edges are compatible when they share at least one tag, mirroring MarkovJunior-style rules.
- A macro blueprint synthesised from elevation and biome masks biases the solver so rivers, mountains, wetlands, and deserts follow authored large-scale shapes. MarkovJunior-style rule passes grow settlements (streets, plazas, facades) on top of the collapsed terrain.

### MarkovJunior Architecture

In addition to terrain WFC, Minecraftonia includes a rule-driven architectural generator built on a MarkovJunior-inspired engine. The pipeline creates layered passes:

- Streets and plazas are carved first, establishing the circulation grid.
- Room footprints, courtyards, and gardens are stamped with stochastic grammars.
- Doorways, windows, and supporting pillars follow, respecting street adjacency and structural edges.
- Finally, façades are voxelised into wood/stone walls, stair blocks, and interior air volumes. Floors become multi-storey shells with openings carved where rules demand.

Rules live in `MarkovJunior/Architecture`, and settlements are placed automatically wherever the macro blueprint tags regions as `biome_settlement`. Export additional debug maps by setting `MINECRAFTONIA_ARCH_DEBUG=1` before launching; the engine will dump blueprint/layout snapshots under `docs/debug/`.

To add a new biome tile:

1. Create a `VoxelPattern3D` with the desired block arrangement.
2. Tag each edge (`WithEdgeTags`) to describe how it should connect (for example `"grass"`, `"water"`, `"cliff"`).
3. Register the pattern in the factory list and assign a weight to control its frequency.

The generator automatically retries when contradictions appear and falls back to the legacy noise terrain only if all attempts fail, keeping worlds playable.

## Project Layout

```text
Minecraftonia.sln
├── Minecraftonia/             # Avalonia desktop front-end
├── Minecraftonia.Game/        # Game orchestration, rendering bridge, save system
├── Minecraftonia.VoxelEngine/ # Core voxel world data structures & physics
├── Minecraftonia.VoxelRendering/ # Ray tracer, texture atlas, overlay helpers
└── publish/                   # Optional self-contained publish output
```

## Architecture & Algorithms

### `Minecraftonia` (Desktop UI frontend)

- **Avalonia navigation shell** – Hosts the `GameControl` custom control and overlays menus rendered via XAML. Menu state is driven from `MainWindow.axaml.cs`, which responds to pause events and manages new/load/save flows.
- **Pointer capture management** – Relies on `GameControl` APIs to toggle raw mouse capture, warp the pointer to the window center, and react to `Esc`/`F1` toggles, keeping UI and camera motion in sync.
- **Compositor-synced render loop** – `GameControl` now advances simulation via `TopLevel.RequestAnimationFrame`. The loop self-reschedules only while attached to the visual tree, keeping update cadence tightly coupled to Avalonia’s compositor and avoiding timer drift.
- **SIMD-aware frame orchestration** – Input marshalling and render scheduling remain on the UI thread, but AA and post-processing rely on `System.Numerics.Vector<T>` operations, so we structure pixel buffers and deltas to maximise contiguous memory access for vectorised stages downstream.

### `Minecraftonia.Game`

- **`MinecraftoniaGame` orchestration** – Central façade coordinating player input, movement integration, voxel manipulation, and save/load. Capsule-vs-voxel sweeps handle collisions while the interaction system retains the closest ray hit for block breaking/placing. Chunk ranges are warmed before spawn to avoid hitching.
- **Wave Function Collapse terrain** – `MinecraftoniaVoxelWorld` provides both legacy layered-noise terrain and a 3D-pattern WFC pipeline. Patterns encode voxel microstructures with edge tags, letting the solver assemble rivers, shores, cliffs, plains, and forests while respecting block-level adjacency.
- **Save system** – `GameSaveData`/`PlayerSaveData` capture world dimensions, chunk parameters, flattened block arrays, palette index, and player kinematics. Loading reconstructs chunks via span hydration to minimise allocations.
- **Input pipeline** – `GameControl` converts keyboard/mouse/pointer events into `GameInputState` per frame and forwards it to `_game.Update`.

### `Minecraftonia.VoxelEngine`

- **Chunk-backed voxel store** – `VoxelWorld<TBlock>` maintains chunk dictionaries, but each chunk owns a contiguous block span. `BlockAccessCache` caches the current chunk/raw array so hot loops (ray tracing, serialisation) avoid repeated dictionary lookups.
- **Occupancy tracking** – Chunks count solid blocks when hydrated or mutated, providing hooks for future culling/streaming heuristics.
- **Player physics** – Movement solves planar wish directions, gravity, sprint, and jump. `MoveWithCollisions` performs axis-separated sweeps with step-up logic, delivering responsive first-person motion.

### `Minecraftonia.VoxelRendering`

- **Adaptive ray marcher** – `VoxelRayTracer` casts one centre ray per pixel, then conditionally adds stratified jitter samples when colour/alpha variance signals edge detail. DDA traversal caches chunk/local coordinates and runs per-scanline in parallel.
- **SIMD colour pipeline & FXAA** – All colour blending, luma evaluation, and sharpening use `Vector4` maths, letting .NET emit SSE/AVX instructions. The FXAA/sharpen pass copies the render buffer once and processes rows in parallel, preserving alpha while smoothing edges without supersampling cost.
- **SIMD colour pipeline** – Colour maths leverages `System.Numerics.Vector4` dot products and lerps for luma detection, blending, and sharpening, automatically mapping to hardware SIMD instructions on supported CPUs.
- **HUD overlay** – `GameControl.Render` draws crosshair, FPS counter, hotbar selection, and block outlines over the framebuffer via Avalonia drawing primitives.

### Data flow summary

1. `GameControl` gathers input (keyboard, mouse, pointer delta) each frame, marshals it into `GameInputState`, and calls `_game.Update`.
2. `MinecraftoniaGame` applies look, movement, interactions, and updates the `MinecraftoniaVoxelWorld` accordingly.
3. `VoxelRayTracer` fills a reusable CPU framebuffer; `GameControl` then presents it using either the legacy `WriteableBitmap` pathway or the Skia texture-backed presenter, depending on the configured mode.
4. HUD overlays draw palette status, FPS, crosshair, and block outlines on top of the framebuffer.
5. Menu actions invoke `StartNewGame`, `LoadGame`, `CreateSaveData`, or pointer capture toggles, coordinating via dispatcher calls to maintain smooth focus changes.

## Save Files

Saves are stored under the user Application Data folder:

- macOS/Linux: `~/.config/Minecraftonia/Saves/latest.json`
- Windows: `%APPDATA%\Minecraftonia\Saves\latest.json`

Files are JSON encoded `GameSaveData` objects. Corrupt or mismatched versions are rejected with clear error messaging in the UI.

## Contributing

1. Fork and branch from `main` (e.g., `feature/improve-water`)
2. Keep changes scoped and run `dotnet build Minecraftonia.sln`
3. If you add assets or new save schema, document them in this README
4. Open a PR with a concise description and screenshots/video where helpful

## Roadmap

- Multiple save slots and save previews
- Configurable world generation seeds and biome layers
- Dynamic day/night lighting and weather
- Improved accessibility for colorblind/high-contrast modes
- Multiplayer prototype (long-term)

## License

Minecraftonia is released under the [GNU Affero General Public License v3.0](LICENSE).

## Credits

- **Creator**: Wiesław Šoltés (@wieslawsoltes)
- **Engine Foundations**: Custom voxel math, physics, and rendering authored specifically for this project
- **Libraries**: Avalonia UI, System.Numerics (SIMD), .NET runtime

Have fun exploring! Contributions, bug reports, and feature suggestions are always welcome.
