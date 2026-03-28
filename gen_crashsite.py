#!/usr/bin/env python3
import json, os
from collections import Counter

W, H = 80, 100
EMPTY = 0
STONE = 1
DIRT = 2
GRASS = 3
PLATFORM_METAL = 24
BREAKABLE = 70
FIRE = 86
BROKEN_PIPE = 87
ELECTRIC_SHOCK = 88
METAL_FLOOR = 95
METAL_WALL = 96
DIRT_BG = 101
STONE_BG = 102
WOOD_BG = 103

grid = [EMPTY] * (W * H)

def s(col, row, tile):
    if 0 <= col < W and 0 <= row < H:
        grid[row * W + col] = tile

def fill(c1, c2, r1, r2, tile):
    for r in range(r1, r2 + 1):
        for c in range(c1, c2 + 1):
            s(c, r, tile)

# CAVE CEILING rows 0-12: solid stone
fill(0, 79, 0, 12, STONE)

# Rows 13-19: stone except cols 25-55 (ship hull breach)
fill(0, 79, 13, 19, STONE)
fill(25, 55, 13, 19, EMPTY)

# Stalactites at cols 10, 20, 60, 65
for col, length in [(10, 3), (20, 2), (60, 3), (65, 1)]:
    for dr in range(length):
        s(col, 13 + dr, STONE)

# COCKPIT BREACH rows 20-44
# Hull outline
fill(25, 55, 20, 20, METAL_WALL)  # top
fill(25, 26, 20, 38, METAL_WALL)  # left wall
fill(54, 55, 20, 38, METAL_WALL)  # right wall
fill(27, 53, 38, 38, METAL_FLOOR) # cockpit floor

# Broken console
fill(35, 37, 30, 32, METAL_WALL)

# Cracked windshield
fill(38, 42, 22, 24, WOOD_BG)

# Electric shock hazard
s(30, 42, ELECTRIC_SHOCK)

# LEFT CAVE WALL rows 40-84, cols 0-4
fill(0, 4, 40, 84, STONE)

# Middle zone left wall wider cols 0-6, rows 45-69
fill(5, 6, 45, 69, STONE)

# RIGHT CAVE WALL rows 50-84, cols 75-79
fill(75, 79, 50, 84, STONE)

# Middle zone right wall wider cols 72-79, rows 50-69
fill(72, 74, 50, 69, STONE)

# Cave walls around cockpit breach rows 20-44
fill(0, 24, 20, 44, STONE)
fill(56, 79, 20, 44, STONE)

# WRECKAGE PLATFORMS (MetalFloor)
for r, c1, c2 in [(75,18,24),(68,35,42),(61,15,22),(54,40,48),(48,25,30)]:
    fill(c1, c2, r, r, METAL_FLOOR)

# Pass-through platforms (PlatformMetal)
for r, c1, c2 in [(71,28,32),(58,30,35)]:
    fill(c1, c2, r, r, PLATFORM_METAL)

# Stone outcroppings
fill(5, 9, 65, 65, STONE)
fill(70, 74, 55, 55, STONE)

# BrokenPipe
fill(45, 46, 50, 50, BROKEN_PIPE)

# StoneBg behind wreckage climb
for r, c in [(52,30),(56,20),(63,50),(67,60)]:
    s(c, r, STONE_BG)

# DirtBg behind wreckage
for r, c in [(60,40),(55,25),(50,35)]:
    s(c, r, DIRT_BG)

# CAVE FLOOR rows 82-84, cols 5-70 (Dirt) with gaps
for r in range(82, 85):
    for c in range(5, 71):
        if c in range(30, 33) or c in range(55, 58):
            continue  # gaps
        s(c, r, DIRT)

# Stone mixed into dirt floor for variety
for c in [8, 15, 25, 40, 55, 62]:
    if grid[83 * W + c] == DIRT:
        s(c, 83, STONE)

# Row 84-85 stone floor (abyss barrier) - fill row 84-85 solid under the dirt
# Row 84 is already dirt where applicable; add stone at row 85
fill(5, 70, 85, 85, STONE)
# Gaps in row 85 too so player can look down
for c in range(30, 33):
    s(c, 85, EMPTY)
for c in range(55, 58):
    s(c, 85, EMPTY)

# Fire
s(15, 81, FIRE)
s(16, 81, FIRE)

# Breakable debris on floor
for c in [20, 35, 50]:
    s(c, 81, BREAKABLE)

# Grass (alien plants)
for c in range(68, 73):
    s(c, 81, GRASS)

# THE DEEP rows 85-99
# Row 89: scattered stone crumbling edge
for c in [10, 25, 45, 60, 70]:
    s(c, 89, STONE)

# Background tiles in abyss
for r, c, t in [(92,20,DIRT_BG),(95,50,STONE_BG),(97,35,STONE_BG)]:
    s(c, r, t)

# Count tiles
counts = Counter(grid)
names = {0:'Empty',1:'Stone',2:'Dirt',3:'Grass',24:'PlatformMetal',70:'Breakable',
         86:'Fire',87:'BrokenPipe',88:'ElectricShock',95:'MetalFloor',96:'MetalWall',
         101:'DirtBg',102:'StoneBg',103:'WoodBg'}
print("Tile counts:")
for tid, cnt in sorted(counts.items()):
    print(f"  {names.get(tid, f'Unknown({tid})')}: {cnt}")

level = {
    "name": "Crash Site",
    "author": "",
    "playerSpawn": {"x": 320, "y": 2496},
    "bounds": {"left": 0, "right": 2560, "top": 0, "bottom": 3200},
    "floor": {"y": 3100, "height": 50},
    "isUnderground": True,
    "tileGrid": {
        "width": W, "height": H, "tileSize": 32,
        "originX": 0, "originY": 0,
        "tiles": grid
    },
    "exits": [
        {"x": 1280, "y": 1184, "w": 64, "h": 32,
         "targetLevel": "ship-interior", "id": "exit-to-ship",
         "targetExitId": "exit-to-crashsite"}
    ],
    "items": [
        {"id": "heart-1", "type": "heart", "x": 1440, "y": 1696, "w": 20, "h": 20}
    ],
    "enemies": [],
    "shelters": [],
    "platforms": [],
    "ropes": [],
    "walls": [],
    "spikes": [],
    "ceilings": [],
    "solidFloors": [],
    "wallSpikes": [],
    "npcs": [],
    "objects": [],
    "switches": [],
    "labels": [],
    "retractableSpikes": [],
    "neighbors": {"left": "", "right": "", "up": "", "down": "",
                   "LeftZones": [], "RightZones": [], "UpZones": [], "DownZones": []},
    "envRegions": []
}

outpath = "/home/node/.openclaw/workspace/genesys/Content/levels/crashsite.json"
os.makedirs(os.path.dirname(outpath), exist_ok=True)
with open(outpath, 'w') as f:
    json.dump(level, f, indent=2)
print(f"\nWrote {outpath} ({os.path.getsize(outpath)} bytes)")
