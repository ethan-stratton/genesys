# Genesys

A 2D side-scrolling action RPG built with MonoGame. Explore interconnected levels, fight enemies, discover weapons, and uncover the story of a crashed ship on an alien world.

## How to Run

**Requirements:** [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

```bash
git clone https://github.com/ethan-stratton/genesys.git
cd genesys
dotnet run
```

## Controls

| Action | Input |
|--------|-------|
| Move | **A / D** or **Left / Right arrows** |
| Jump | **Space** (double jump available) |
| Attack (right hand) | **J** or **Left Click** |
| Attack (left hand) | **K** or **Right Click** |
| Shield / Block | Hold **J** or **K** (with shield equipped) |
| Interact / Talk | **E** |
| Inventory | **Tab** or **I** |
| Map | **M** |
| Swap weapon slot 1 | **1** |
| Swap weapon slot 2 | **2** |
| Pause / Menu | **Escape** |
| Cycle NPC names | **N** |
| Bestiary | **B** |

### Menu Navigation
| Action | Input |
|--------|-------|
| Navigate | **W / S** or **Up / Down** |
| Confirm | **Enter** or **Space** |
| Back | **Escape** |

## Settings

Access the settings menu from the title screen or pause menu. Options include:

- CRT filter (off by default)
- Fullscreen toggle
- Debug overlays (F9)

## Project Structure

```
genesys/
├── Game1.cs           # Main game loop
├── Player.cs          # Player physics & combat
├── Enemy.cs           # Enemy types
├── Camera.cs          # Camera system with trauma shake
├── Easing.cs          # Animation easing utilities
├── LevelData.cs       # Level loading & tile system
├── Content/
│   ├── levels/        # Level data (JSON + LDtk)
│   ├── tilesets/      # Tileset images
│   └── Music/         # Soundtrack
└── tools/             # Dev utilities
```

## Tech

- **Engine:** MonoGame / C# / .NET 9
- **Rendering:** Procedural fallback graphics (runs without sprite sheets)
- **Platforms:** Windows, macOS, Linux
