# Minecraftonia

Minecraftonia is a work-in-progress voxel sandbox built with C# 13/.NET 9 and Avalonia. The project experiments with custom voxel ray tracing, procedural terrain, and responsive desktop UI while staying fully cross-platform.

## Features

- **Custom voxel engine** – handcrafted player physics, block interactions, and chunkless world representation tuned for experimentation.
- **Software ray tracing** – real-time voxel ray marcher that renders the world to a `WriteableBitmap`, producing a distinct stylised look without GPU shaders.
- **Procedural terrain generation** – layered noise, biome-inspired surface treatments, tree placement, and underground cave carving.
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

## Gameplay Controls

| Action | Default | Notes |
| --- | --- | --- |
| Move | `W A S D` | Arrow keys also rotate the camera in keyboard-only mode |
| Sprint | `Shift` | Works with both left and right shift |
| Jump | `Space` | Requires ground contact |
| Look | Mouse | `F1` toggles relative pointer capture |
| Break Block | Left mouse button | Ray traced hit detection |
| Place Block | Right mouse button | Places currently selected block |
| Toggle Mouse Capture | `F1` / `Esc` | `Esc` releases pointer when captured |
| Invert X / Y | `F2` / `F3` | Toggles independently |
| Sensitivity | `+` / `-` | Adjusts mouse look multiplier |
| Hotbar | `1`-`7`, `Tab`, scroll wheel | Scroll cycles palette |

Pause the game with `Esc`, then resume, save/exit, or return to the main menu.

## Project Layout

```text
Minecraftonia.sln
├── Minecraftonia/             # Avalonia desktop front-end
├── Minecraftonia.Game/        # Game orchestration, rendering bridge, save system
├── Minecraftonia.VoxelEngine/ # Core voxel world data structures & physics
├── Minecraftonia.VoxelRendering/ # Ray tracer, texture atlas, overlay helpers
└── publish/                   # Optional self-contained publish output
```

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
