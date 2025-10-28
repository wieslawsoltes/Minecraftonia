# Minecraftonia

Minecraftonia is a work-in-progress voxel sandbox built with C# 13/.NET 9 and Avalonia. The project experiments with custom voxel ray tracing, procedural terrain, and responsive desktop UI while staying fully cross-platform.

https://github.com/user-attachments/assets/7d2c04ab-40cb-4eb7-9b3a-8634d4ab98ed

## NuGet packages

| Package | Version | Downloads | Description |
| --- | --- | --- | --- |
| `Minecraftonia.Core` | [![NuGet](https://img.shields.io/nuget/v/Minecraftonia.Core.svg)](https://www.nuget.org/packages/Minecraftonia.Core/) | [![NuGet](https://img.shields.io/nuget/dt/Minecraftonia.Core.svg)](https://www.nuget.org/packages/Minecraftonia.Core/) | Core domain abstractions shared across Minecraftonia apps. |
| `Minecraftonia.Content` | [![NuGet](https://img.shields.io/nuget/v/Minecraftonia.Content.svg)](https://www.nuget.org/packages/Minecraftonia.Content/) | [![NuGet](https://img.shields.io/nuget/dt/Minecraftonia.Content.svg)](https://www.nuget.org/packages/Minecraftonia.Content/) | Reusable voxel content assets and texture helpers. |
| `Minecraftonia.VoxelEngine` | [![NuGet](https://img.shields.io/nuget/v/Minecraftonia.VoxelEngine.svg)](https://www.nuget.org/packages/Minecraftonia.VoxelEngine/) | [![NuGet](https://img.shields.io/nuget/dt/Minecraftonia.VoxelEngine.svg)](https://www.nuget.org/packages/Minecraftonia.VoxelEngine/) | High-performance voxel storage, physics, and world utilities. |
| `Minecraftonia.Rendering.Core` | [![NuGet](https://img.shields.io/nuget/v/Minecraftonia.Rendering.Core.svg)](https://www.nuget.org/packages/Minecraftonia.Rendering.Core/) | [![NuGet](https://img.shields.io/nuget/dt/Minecraftonia.Rendering.Core.svg)](https://www.nuget.org/packages/Minecraftonia.Rendering.Core/) | Rendering primitives, ray tracer, and material pipelines. |
| `Minecraftonia.Rendering.Pipelines` | [![NuGet](https://img.shields.io/nuget/v/Minecraftonia.Rendering.Pipelines.svg)](https://www.nuget.org/packages/Minecraftonia.Rendering.Pipelines/) | [![NuGet](https://img.shields.io/nuget/dt/Minecraftonia.Rendering.Pipelines.svg)](https://www.nuget.org/packages/Minecraftonia.Rendering.Pipelines/) | Composable render pipelines and post-processing effects. |
| `Minecraftonia.Rendering.Avalonia` | [![NuGet](https://img.shields.io/nuget/v/Minecraftonia.Rendering.Avalonia.svg)](https://www.nuget.org/packages/Minecraftonia.Rendering.Avalonia/) | [![NuGet](https://img.shields.io/nuget/dt/Minecraftonia.Rendering.Avalonia.svg)](https://www.nuget.org/packages/Minecraftonia.Rendering.Avalonia/) | Avalonia presenters and controls for Minecraftonia rendering. |
| `Minecraftonia.Hosting` | [![NuGet](https://img.shields.io/nuget/v/Minecraftonia.Hosting.svg)](https://www.nuget.org/packages/Minecraftonia.Hosting/) | [![NuGet](https://img.shields.io/nuget/dt/Minecraftonia.Hosting.svg)](https://www.nuget.org/packages/Minecraftonia.Hosting/) | Framework-neutral game loop and service hosting infrastructure. |
| `Minecraftonia.Hosting.Avalonia` | [![NuGet](https://img.shields.io/nuget/v/Minecraftonia.Hosting.Avalonia.svg)](https://www.nuget.org/packages/Minecraftonia.Hosting.Avalonia/) | [![NuGet](https://img.shields.io/nuget/dt/Minecraftonia.Hosting.Avalonia.svg)](https://www.nuget.org/packages/Minecraftonia.Hosting.Avalonia/) | Avalonia-specific input capture and hosting adapters. |
| `Minecraftonia.Game` | [![NuGet](https://img.shields.io/nuget/v/Minecraftonia.Game.svg)](https://www.nuget.org/packages/Minecraftonia.Game/) | [![NuGet](https://img.shields.io/nuget/dt/Minecraftonia.Game.svg)](https://www.nuget.org/packages/Minecraftonia.Game/) | Shared gameplay systems, menus, and save orchestration. |
| `Minecraftonia.MarkovJunior` | [![NuGet](https://img.shields.io/nuget/v/Minecraftonia.MarkovJunior.svg)](https://www.nuget.org/packages/Minecraftonia.MarkovJunior/) | [![NuGet](https://img.shields.io/nuget/dt/Minecraftonia.MarkovJunior.svg)](https://www.nuget.org/packages/Minecraftonia.MarkovJunior/) | Markov Junior-based procedural content generation helpers. |
| `Minecraftonia.MarkovJunior.Architecture` | [![NuGet](https://img.shields.io/nuget/v/Minecraftonia.MarkovJunior.Architecture.svg)](https://www.nuget.org/packages/Minecraftonia.MarkovJunior.Architecture/) | [![NuGet](https://img.shields.io/nuget/dt/Minecraftonia.MarkovJunior.Architecture.svg)](https://www.nuget.org/packages/Minecraftonia.MarkovJunior.Architecture/) | Modular architecture patterns powered by Markov Junior. |
| `Minecraftonia.OpenStreetMap` | [![NuGet](https://img.shields.io/nuget/v/Minecraftonia.OpenStreetMap.svg)](https://www.nuget.org/packages/Minecraftonia.OpenStreetMap/) | [![NuGet](https://img.shields.io/nuget/dt/Minecraftonia.OpenStreetMap.svg)](https://www.nuget.org/packages/Minecraftonia.OpenStreetMap/) | OpenStreetMap ingestion and conversion utilities. |
| `Minecraftonia.WaveFunctionCollapse` | [![NuGet](https://img.shields.io/nuget/v/Minecraftonia.WaveFunctionCollapse.svg)](https://www.nuget.org/packages/Minecraftonia.WaveFunctionCollapse/) | [![NuGet](https://img.shields.io/nuget/dt/Minecraftonia.WaveFunctionCollapse.svg)](https://www.nuget.org/packages/Minecraftonia.WaveFunctionCollapse/) | Wave Function Collapse solvers tailored for voxel structures. |

## Repository Structure

Minecraftonia.sln organizes the modular libraries, the desktop client, and supporting samples.

```text
.
├── src/
│   ├── Minecraftonia/                      # Avalonia desktop client and launcher
│   ├── Minecraftonia.Core/                 # Core domain primitives (BlockType, configs, enums)
│   ├── Minecraftonia.Content/              # Procedural block textures and material providers
│   ├── Minecraftonia.VoxelEngine/          # Chunk streaming, voxel storage, and player math
│   ├── Minecraftonia.Rendering.Core/       # Frame buffers, camera math, and material sampling
│   ├── Minecraftonia.Rendering.Pipelines/  # CPU ray tracer, FXAA, GI, and renderer factories
│   ├── Minecraftonia.Rendering.Avalonia/   # Avalonia render control and frame presenters
│   ├── Minecraftonia.Hosting/              # Game loop host abstractions shared across frontends
│   ├── Minecraftonia.Hosting.Avalonia/     # Avalonia keyboard/pointer capture utilities
│   ├── Minecraftonia.Game/                 # Gameplay orchestration, saves, and world bootstrap
│   ├── Minecraftonia.MarkovJunior/         # MarkovJunior-inspired rule engine
│   ├── Minecraftonia.MarkovJunior.Architecture/ # Procedural settlement layout helpers
│   ├── Minecraftonia.OpenStreetMap/        # Overpass-powered blueprint importer
│   └── Minecraftonia.WaveFunctionCollapse/ # 3D WFC solver and pattern library
├── samples/                                # Minimal frontends demonstrating the libraries
│   ├── Minecraftonia.Sample.BasicBlock/
│   ├── Minecraftonia.Sample.Doom.Core/
│   ├── Minecraftonia.Sample.Doom/
│   └── Minecraftonia.Sample.Doom.Avalonia/
├── docs/                                   # Reference docs and smoke-test guides
├── tools/                                  # Utility scripts
└── publish/                                # Local publish output
```

- Each directory in `src/` maps 1:1 to a published NuGet package (or the desktop app) so you can pull in only what you need.
- Samples remain thin front-ends that show how to combine the libraries without the full desktop shell.
- Quick smoke-test walkthroughs live in [`docs/smoke-tests.md`](docs/smoke-tests.md).

## Using the NuGet packages

Each package ships with XML docs and guards, but the snippets below show a quick integration recipe. Install the packages with `dotnet add package <Name>`.

### Minecraftonia.Core

Install: `dotnet add package Minecraftonia.Core`

```csharp
using Minecraftonia.Core;

var config = MinecraftoniaWorldConfig.FromDimensions(
    width: 256,
    height: 96,
    depth: 256,
    waterLevel: 48,
    seed: 0xC0FFEE);

Console.WriteLine($"World size: {config.Width}×{config.Height}×{config.Depth}");
```

### Minecraftonia.Content

Install: `dotnet add package Minecraftonia.Content`

```csharp
using System.Numerics;
using Minecraftonia.Content;
using Minecraftonia.Core;
using Minecraftonia.VoxelEngine;

var textures = new BlockTextures();
var sample = textures.Sample(BlockType.Grass, BlockFace.PositiveY, 0.5f, 0.5f);

Console.WriteLine($"Grass RGB: {sample.Color}, opacity: {sample.Opacity:0.00}");
```

### Minecraftonia.VoxelEngine

Install: `dotnet add package Minecraftonia.VoxelEngine`

```csharp
using System.Numerics;
using Minecraftonia.Core;
using Minecraftonia.VoxelEngine;

public sealed class FlatWorld : VoxelWorld<BlockType>
{
    public FlatWorld()
        : base(new ChunkDimensions(16, 16, 16), chunkCountX: 4, chunkCountY: 1, chunkCountZ: 4)
    {
    }

    protected override void PopulateChunk(VoxelChunk<BlockType> chunk)
    {
        for (int x = 0; x < chunk.Dimensions.SizeX; x++)
        {
            for (int z = 0; z < chunk.Dimensions.SizeZ; z++)
            {
                chunk.SetBlock(x, 0, z, BlockType.Grass, markDirty: false);
                chunk.SetBlock(x, 1, z, BlockType.Dirt, markDirty: false);
            }
        }
    }
}

var world = new FlatWorld();
world.EnsureChunksAround(new Vector3(8f, 0f, 8f), radius: 1);
```

### Minecraftonia.Rendering.Core

Install: `dotnet add package Minecraftonia.Rendering.Core`

```csharp
using System;
using Minecraftonia.Rendering.Core;

using var framebuffer = new VoxelFrameBuffer(new VoxelSize(320, 180));

Span<byte> pixels = framebuffer.Span;
for (int i = 0; i < pixels.Length; i += 4)
{
    pixels[i + 0] = 0x40; // B
    pixels[i + 1] = 0x90; // G
    pixels[i + 2] = 0xE0; // R
    pixels[i + 3] = 0xFF; // A
}
```

### Minecraftonia.Rendering.Pipelines

Install: `dotnet add package Minecraftonia.Rendering.Pipelines`

```csharp
using Minecraftonia.Core;
using Minecraftonia.Game;
using Minecraftonia.Rendering.Core;
using Minecraftonia.Rendering.Pipelines;

var game = new MinecraftoniaGame();

var options = new VoxelRendererOptions<BlockType>(
    renderSize: new VoxelSize(640, 360),
    fieldOfViewDegrees: 75f,
    isSolid: block => block.IsSolid(),
    isEmpty: block => block == BlockType.Air);

var renderer = new VoxelRayTracerFactory<BlockType>().Create(options);
var result = renderer.Render(game.World, game.Player, game.Textures);
```

### Minecraftonia.Rendering.Avalonia

Install: `dotnet add package Minecraftonia.Rendering.Avalonia`

```csharp
using Avalonia.Controls;
using Minecraftonia.Core;
using Minecraftonia.Game;
using Minecraftonia.Rendering.Avalonia.Controls;
using Minecraftonia.Rendering.Core;
using Minecraftonia.Rendering.Pipelines;

public sealed class GameView : UserControl
{
    public GameView()
    {
        var game = new MinecraftoniaGame();
        var renderer = new VoxelRayTracerFactory<BlockType>().Create(
            new VoxelRendererOptions<BlockType>(
                renderSize: new VoxelSize(640, 360),
                fieldOfViewDegrees: 75f,
                isSolid: block => block.IsSolid(),
                isEmpty: block => block == BlockType.Air));

        Content = new VoxelRenderControl<BlockType>
        {
            Renderer = renderer,
            Materials = game.Textures,
            World = game.World,
            Player = game.Player,
            RenderContinuously = true
        };
    }
}
```

### Minecraftonia.Hosting

Install: `dotnet add package Minecraftonia.Hosting`

```csharp
using System;
using Minecraftonia.Core;
using Minecraftonia.Game;
using Minecraftonia.Hosting;
using Minecraftonia.Rendering.Core;
using Minecraftonia.Rendering.Pipelines;
using Minecraftonia.VoxelEngine;

var game = new MinecraftoniaGame();
var renderer = new VoxelRayTracerFactory<BlockType>().Create(
    new VoxelRendererOptions<BlockType>(
        renderSize: new VoxelSize(320, 180),
        fieldOfViewDegrees: 70f,
        isSolid: block => block.IsSolid(),
        isEmpty: block => block == BlockType.Air));

var host = new GameHost<BlockType>(new Session(game), new Pipeline(renderer));
var frame = host.Step(TimeSpan.FromMilliseconds(16));

sealed class Session : IGameSession<BlockType>
{
    private readonly MinecraftoniaGame _game;
    public Session(MinecraftoniaGame game) => _game = game;
    public IVoxelWorld<BlockType> World => _game.World;
    public Player Player => _game.Player;
    public IVoxelMaterialProvider<BlockType> Materials => _game.Textures;

    public void Update(GameTime time) =>
        _game.Update(
            new GameInputState(
                MoveForward: false,
                MoveBackward: false,
                MoveLeft: false,
                MoveRight: false,
                Sprint: false,
                Jump: false,
                KeyboardYaw: 0f,
                KeyboardPitch: 0f,
                MouseYawDelta: 0f,
                MousePitchDelta: 0f,
                BreakBlock: false,
                PlaceBlock: false,
                HotbarSelection: null,
                HotbarScrollDelta: 0),
            deltaTime: (float)time.Elapsed.TotalSeconds);
}

sealed class Pipeline : IRenderPipeline<BlockType>
{
    private readonly IVoxelRenderer<BlockType> _renderer;
    public Pipeline(IVoxelRenderer<BlockType> renderer) => _renderer = renderer;

    public IVoxelRenderResult<BlockType> Render(IGameSession<BlockType> session, IVoxelFrameBuffer? framebuffer = null) =>
        _renderer.Render(session.World, session.Player, session.Materials, framebuffer);
}
```

### Minecraftonia.Hosting.Avalonia

Install: `dotnet add package Minecraftonia.Hosting.Avalonia`

Assumes `renderControl` is the Avalonia control you want to capture.

```csharp
using System;
using Avalonia;
using Avalonia.Controls;
using Minecraftonia.Hosting.Avalonia;

var topLevel = TopLevel.GetTopLevel(renderControl) ?? throw new InvalidOperationException("Control is not attached.");
var pointer = new PointerInputSource(topLevel, renderControl);
var keyboard = new KeyboardInputSource(topLevel);

pointer.EnableMouseLook();

void ProcessInput()
{
    var deltaYaw = pointer.DeltaX;
    var deltaPitch = pointer.DeltaY;

    pointer.NextFrame();
    keyboard.NextFrame();
}
```

### Minecraftonia.Game

Install: `dotnet add package Minecraftonia.Game`

```csharp
using Minecraftonia.Game;

var game = new MinecraftoniaGame();
var input = new GameInputState(
    MoveForward: false,
    MoveBackward: false,
    MoveLeft: false,
    MoveRight: false,
    Sprint: false,
    Jump: false,
    KeyboardYaw: 0f,
    KeyboardPitch: 0f,
    MouseYawDelta: 0f,
    MousePitchDelta: 0f,
    BreakBlock: false,
    PlaceBlock: false,
    HotbarSelection: null,
    HotbarScrollDelta: 0);

game.Update(input, deltaTime: 1f / 60f);
Console.WriteLine($"Player position: {game.Player.Position}");
```

### Minecraftonia.MarkovJunior

Install: `dotnet add package Minecraftonia.MarkovJunior`

```csharp
using Minecraftonia.MarkovJunior;
using Minecraftonia.MarkovJunior.Rules;

var empty = new MarkovSymbol("Empty", paletteIndex: 0);
var wall = new MarkovSymbol("Wall", paletteIndex: 1);

var state = new MarkovJuniorState(16, 4, 16, empty);

var layer = new MarkovLayer("Scatter")
    .AddRule(new NoiseFillRule("Walls", wall, threshold: 0.35, salt: 0x42));

var engine = new MarkovJuniorEngine(seed: 1234)
    .AddLayer(layer);

engine.Execute(state);
```

### Minecraftonia.MarkovJunior.Architecture

Install: `dotnet add package Minecraftonia.MarkovJunior.Architecture`

```csharp
using Minecraftonia.MarkovJunior.Architecture;
using Minecraftonia.WaveFunctionCollapse;
using Minecraftonia.WaveFunctionCollapse.Architecture;

var cluster = new SettlementCluster(
    id: 1,
    moduleType: ArchitectureModuleType.Market,
    cells: new[] { (0, 0), (1, 0), (0, 1), (1, 1) });

var context = new ArchitectureClusterContext(cluster, tileSizeX: 8, tileSizeZ: 8);

var layout = ArchitectureRuleSet.GenerateLayout(
    moduleType: ArchitectureModuleType.Market,
    context,
    seed: 2025);
```

### Minecraftonia.OpenStreetMap

Install: `dotnet add package Minecraftonia.OpenStreetMap`

```csharp
using System;
using Minecraftonia.OpenStreetMap;

if (OpenStreetMapBlueprintGenerator.TryCreate(
        width: 64,
        depth: 64,
        seed: Environment.TickCount,
        out var blueprint,
        out var source,
        out var link))
{
    Console.WriteLine($"Fetched blueprint for {source} ({link}) with {blueprint.Clusters.Count} clusters.");
}
else
{
    Console.WriteLine("No blueprint available this time. Try a different seed.");
}
```

### Minecraftonia.WaveFunctionCollapse

Install: `dotnet add package Minecraftonia.WaveFunctionCollapse`

```csharp
using System;
using Minecraftonia.Core;
using Minecraftonia.WaveFunctionCollapse;

var library = VoxelPatternLibraryFactory.CreateDefault(worldHeight: 64, waterLevel: 32);

var generator = new WaveFunctionCollapseGenerator3D(
    library,
    gridSizeX: 4,
    gridSizeZ: 4,
    random: new Random(1337));

BlockType[,,] blocks = generator.Generate();
Console.WriteLine($"Generated pattern volume: {blocks.GetLength(0)}×{blocks.GetLength(1)}×{blocks.GetLength(2)}");
```

## Features

- **Custom voxel engine** – handcrafted player physics, block interactions, and chunkless world representation tuned for experimentation.
- **Software ray tracing** – real-time voxel ray marcher that writes into a CPU framebuffer which can be presented either via the legacy Avalonia `WriteableBitmap` path or uploaded to a Skia `GRContext`-backed texture for crisp scaling without traditional GPU shaders.
- **Procedural terrain generation** – legacy layered noise with caves, shoreline dressing, and tree placement, plus a height-map importer for real-world terrain experiments.
- **Composable UI shell** – Avalonia-based menus with keyboard/mouse controls, full pointer capture, and configurable sensitivity/inversion.
- **Shared hosting infrastructure** – `Minecraftonia.Hosting` and `.Hosting.Avalonia` drive the update/render loop and pointer/keyboard capture used by the full game and sample apps alike.
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

### Samples

The repo also ships with a lightweight sample that demonstrates the reusable rendering stack without the full game shell:

```bash
dotnet run --project samples/Minecraftonia.Sample.BasicBlock
```

The window now hosts `SampleGameControl`, which composes `Minecraftonia.Hosting` and `.Hosting.Avalonia` to reuse the same render/input loop as the main game. Click or press `Tab` to capture the pointer, then fly around the sample block column with `WASD`, `Space`, and `Shift`.

### CLI Doom sample

Render a classic-inspired hanger to a Portable Pixmap image using only the shared rendering pipelines:

```bash
dotnet run --project samples/Minecraftonia.Sample.Doom
```

This produces `doom-frame.ppm` in the working directory. Open the file with any PPM-compatible image viewer to inspect the voxel scene.

### Avalonia Doom demo

Launch an interactive Avalonia window orbiting the same voxel hall with mouse-look controls:

```bash
dotnet run --project samples/Minecraftonia.Sample.Doom.Avalonia
```

Use `Tab`/click to capture the mouse, move with the mouse, and press `Esc` to release the pointer.

## Build & Publish

### Local release publish

Minecraftonia ships as a self-contained desktop app for each OS. To publish locally:

```bash
# Linux x64
dotnet publish src/Minecraftonia/Minecraftonia.csproj -c Release -r linux-x64 --self-contained true -o publish/linux-x64

# macOS Apple Silicon
dotnet publish src/Minecraftonia/Minecraftonia.csproj -c Release -r osx-arm64 --self-contained true -o publish/osx-arm64

# Windows x64
dotnet publish src/Minecraftonia/Minecraftonia.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
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
├── src/Minecraftonia/             # Avalonia desktop front-end
├── src/Minecraftonia.Game/        # Game orchestration, rendering bridge, save system
├── src/Minecraftonia.VoxelEngine/ # Core voxel world data structures & physics
├── src/Minecraftonia.Rendering.Pipelines/ # CPU ray tracer, GI/FXAA helpers, renderer interfaces
└── publish/                   # Optional self-contained publish output
```

## Architecture & Algorithms

### `src/Minecraftonia` (Desktop UI frontend)

- **Avalonia navigation shell** – Hosts the `GameControl` custom control and overlays menus rendered via XAML. Menu state is driven from `MainWindow.axaml.cs`, which responds to pause events and manages new/load/save flows.
- **Pointer capture management** – Relies on `GameControl` APIs to toggle raw mouse capture, warp the pointer to the window center, and react to `Esc`/`F1` toggles, keeping UI and camera motion in sync.
- **Compositor-synced render loop** – `GameControl` now advances simulation via `TopLevel.RequestAnimationFrame`. The loop self-reschedules only while attached to the visual tree, keeping update cadence tightly coupled to Avalonia’s compositor and avoiding timer drift.
- **SIMD-aware frame orchestration** – Input marshalling and render scheduling remain on the UI thread, but AA and post-processing rely on `System.Numerics.Vector<T>` operations, so we structure pixel buffers and deltas to maximise contiguous memory access for vectorised stages downstream.

### `src/Minecraftonia.Game`

- **`MinecraftoniaGame` orchestration** – Central façade coordinating player input, movement integration, voxel manipulation, and save/load. Capsule-vs-voxel sweeps handle collisions while the interaction system retains the closest ray hit for block breaking/placing. Chunk ranges are warmed before spawn to avoid hitching.
- **Wave Function Collapse terrain** – `MinecraftoniaVoxelWorld` provides both legacy layered-noise terrain and a 3D-pattern WFC pipeline. Patterns encode voxel microstructures with edge tags, letting the solver assemble rivers, shores, cliffs, plains, and forests while respecting block-level adjacency.
- **Save system** – `GameSaveData`/`PlayerSaveData` capture world dimensions, chunk parameters, flattened block arrays, palette index, and player kinematics. Loading reconstructs chunks via span hydration to minimise allocations.
- **Input pipeline** – `GameControl` converts keyboard/mouse/pointer events into `GameInputState` per frame and forwards it to `_game.Update`.

### `src/Minecraftonia.VoxelEngine`

- **Chunk-backed voxel store** – `VoxelWorld<TBlock>` maintains chunk dictionaries, but each chunk owns a contiguous block span. `BlockAccessCache` caches the current chunk/raw array so hot loops (ray tracing, serialisation) avoid repeated dictionary lookups.
- **Occupancy tracking** – Chunks count solid blocks when hydrated or mutated, providing hooks for future culling/streaming heuristics.
- **Player physics** – Movement solves planar wish directions, gravity, sprint, and jump. `MoveWithCollisions` performs axis-separated sweeps with step-up logic, delivering responsive first-person motion.

### `Minecraftonia.Rendering.Pipelines`

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

Minecraftonia is released under the [MIT](LICENSE).

## Credits

- **Creator**: Wiesław Šoltés (@wieslawsoltes)
- **Engine Foundations**: Custom voxel math, physics, and rendering authored specifically for this project
- **Libraries**: Avalonia UI, System.Numerics (SIMD), .NET runtime

Have fun exploring! Contributions, bug reports, and feature suggestions are always welcome.
