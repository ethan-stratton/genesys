#!/usr/bin/env python3
"""Generate surface-east.json — The Living Forest level."""
import json, random, os

random.seed(42)

W, H = 250, 80
EMPTY, DIRT, STONE, GRASS, WOOD = 0, 1, 2, 3, 4
PLATFORM_WOOD, PLATFORM_STONE, PLATFORM_METAL = 20, 21, 24
SLOPE_UP_RIGHT, SLOPE_UP_LEFT = 50, 51
BREAKABLE = 70
METAL_FLOOR = 95
STONE_BG, DIRT_BG, GRASS_BG, WOOD_BG = 102, 101, 103, 104

def idx(col, row):
    return row * W + col

tiles = [EMPTY] * (W * H)

def set_tile(c, r, v):
    if 0 <= c < W and 0 <= r < H:
        tiles[idx(c, r)] = v

def fill_rect(c1, r1, c2, r2, v):
    for r in range(r1, r2 + 1):
        for c in range(c1, c2 + 1):
            set_tile(c, r, v)

def fill_rect_ragged(c1, r1, c2, r2, v, edge_prob=0.3):
    """Fill rect but randomly skip edge tiles for organic look."""
    for r in range(r1, r2 + 1):
        for c in range(c1, c2 + 1):
            is_edge = (r == r1 or r == r2 or c == c1 or c == c2)
            if is_edge and random.random() < edge_prob:
                continue
            set_tile(c, r, v)

def tree_trunk(col1, col2, r_top, r_bot):
    """Draw a tree trunk with slight organic variation."""
    for r in range(r_top, r_bot + 1):
        for c in range(col1, col2 + 1):
            # Occasionally widen by 1 at random rows
            set_tile(c, r, WOOD)
        if random.random() < 0.15 and col1 > 0:
            set_tile(col1 - 1, r, WOOD)
        if random.random() < 0.15:
            set_tile(col2 + 1, r, WOOD)

# ============================================================
# UNDERGROUND: Stone layer rows 75-79 everywhere
# ============================================================
fill_rect(0, 75, W - 1, 79, STONE)

# ============================================================
# ZONE 1: HULL BREACH / FOREST EDGE (cols 0-30)
# ============================================================
# Ship hull wall
fill_rect(0, 20, 3, 70, STONE)
# Breach opening
fill_rect(2, 50, 3, 60, EMPTY)

# Ground
for c in range(4, 31):
    gr = 68
    if random.random() < 0.1:
        gr = 67  # occasional bump
    for r in range(gr, 71):
        set_tile(c, r, GRASS)
    for r in range(71, 75):
        set_tile(c, r, DIRT)

# Tree trunks
tree_trunk(12, 13, 40, 67)
tree_trunk(22, 23, 35, 67)
tree_trunk(28, 29, 45, 67)

# Canopy
fill_rect_ragged(8, 30, 18, 38, GRASS)
fill_rect_ragged(19, 28, 27, 36, GRASS)
fill_rect_ragged(25, 32, 32, 38, GRASS)

# Ship debris
for c in [6, 7, 8]:
    set_tile(c, 67, METAL_FLOOR)

# ============================================================
# ZONE 2: OBSERVATION CLEARING (cols 31-65)
# ============================================================
# Sloping ground from row 68 down to row 72
for c in range(31, 40):
    ground_row = 68 + int((c - 31) * 4 / 9)
    if random.random() < 0.1:
        ground_row += random.choice([-1, 0])
    # Place slope tile at transition, grass below
    set_tile(c, ground_row, SLOPE_UP_LEFT)
    for r in range(ground_row + 1, 75):
        set_tile(c, r, DIRT if r > ground_row + 2 else GRASS)

# Flat clearing
for c in range(40, 56):
    for r in range(72, 75):
        set_tile(c, r, GRASS if r == 72 else DIRT)

# Continue ground to col 65
for c in range(56, 66):
    gr = 72
    if random.random() < 0.1:
        gr = 71
    for r in range(gr, 75):
        set_tile(c, r, GRASS if r == gr else DIRT)

# Low plants
for c in [42, 45, 48, 52]:
    set_tile(c, 71, GRASS)

# Trees
tree_trunk(35, 36, 35, 71)
tree_trunk(58, 59, 30, 71)

# Canopy platforms
for c in range(38, 49):
    set_tile(c, 32, PLATFORM_WOOD)
for c in range(50, 59):
    set_tile(c, 28, PLATFORM_WOOD)

# Stone ledge (lock before key)
fill_rect(60, 62, 65, 65, STONE)

# ============================================================
# ZONE 3: SCAVENGER PATH (cols 66-95)
# ============================================================
for c in range(66, 96):
    gr = 72
    if random.random() < 0.12:
        gr = 71
    for r in range(gr, 75):
        set_tile(c, r, GRASS if r == gr else DIRT)

# Rocky hill
fill_rect(85, 65, 92, 71, STONE)
# Ragged top edge
for c in range(85, 93):
    if random.random() < 0.3:
        set_tile(c, 65, EMPTY)
    if random.random() < 0.2:
        set_tile(c, 64, STONE)

# Breakable debris
set_tile(75, 71, BREAKABLE)
set_tile(82, 71, BREAKABLE)

# Trees
tree_trunk(70, 71, 38, 71)
tree_trunk(88, 89, 42, 71)

# Canopy
fill_rect_ragged(66, 34, 76, 40, GRASS)
fill_rect_ragged(84, 36, 94, 42, GRASS)

# ============================================================
# ZONE 4: PREDATOR TERRITORY (cols 96-150)
# ============================================================
for c in range(96, 151):
    # Uneven ground
    if 100 <= c <= 110:
        gr = 68
    elif random.random() < 0.15:
        gr = 71
    else:
        gr = 70 + (random.randint(0, 1))
    
    for r in range(gr, 75):
        set_tile(c, r, GRASS if r == gr else DIRT)
    
    # Undergrowth scatter
    if random.random() < 0.2:
        set_tile(c, gr - 1, GRASS)

# Dense tree trunks
for cols in [(100, 101), (112, 113), (125, 126), (138, 139), (145, 146)]:
    top = 35 + random.randint(-2, 3)
    tree_trunk(cols[0], cols[1], top, 69)

# Rock outcrop
fill_rect(118, 55, 125, 62, STONE)
# Ragged edges
for c in range(118, 126):
    if random.random() < 0.25:
        set_tile(c, 55, EMPTY)
    if random.random() < 0.2:
        set_tile(c, 54, STONE)

# Canopy platforms
for c in range(105, 116):
    set_tile(c, 40, PLATFORM_WOOD)
for c in range(128, 141):
    set_tile(c, 38, PLATFORM_WOOD)

# Underground tunnel
fill_rect(130, 73, 145, 73, STONE)  # ceiling
fill_rect(130, 74, 145, 74, EMPTY)  # tunnel space
# Actually make tunnel: rows 74-78 empty, surrounded by dirt
for c in range(130, 146):
    for r in range(74, 79):
        set_tile(c, r, EMPTY)
    set_tile(c, 73, STONE)  # ceiling

# ============================================================
# ZONE 5: THE RAVINE (cols 151-175)
# ============================================================
# Ground before ravine (cols 151-154)
for c in range(151, 155):
    for r in range(70, 75):
        set_tile(c, r, GRASS if r == 70 else DIRT)

# Grapple ledge
fill_rect(152, 48, 157, 50, STONE)

# Ravine walls
fill_rect(155, 50, 157, 79, STONE)
fill_rect(169, 50, 171, 79, STONE)

# Ravine gap - ensure empty
fill_rect(158, 50, 168, 79, EMPTY)

# Grapple points
for c in range(160, 163):
    set_tile(c, 45, PLATFORM_STONE)
for c in range(165, 168):
    set_tile(c, 42, PLATFORM_STONE)

# Ground after ravine
for c in range(172, 176):
    for r in range(70, 75):
        set_tile(c, r, GRASS if r == 70 else DIRT)

# ============================================================
# ZONE 6: CANOPY VISTA + SHELTER (cols 176-220)
# ============================================================
for c in range(176, 221):
    gr = 66
    if random.random() < 0.1:
        gr = 65
    for r in range(gr, 69):
        set_tile(c, r, GRASS if r == gr else DIRT)
    for r in range(69, 75):
        set_tile(c, r, DIRT)

# Great tree
fill_rect(185, 20, 188, 65, WOOD)
# Add some width variation
for r in range(20, 66):
    if random.random() < 0.12:
        set_tile(184, r, WOOD)
    if random.random() < 0.12:
        set_tile(189, r, WOOD)

# Massive canopy
fill_rect_ragged(178, 10, 200, 25, GRASS, edge_prob=0.35)

# Branch platforms
for c in range(180, 193):
    set_tile(c, 35, PLATFORM_WOOD)
for c in range(183, 196):
    set_tile(c, 25, PLATFORM_WOOD)

# WoodBg vista backdrop
for r in range(15, 25):
    for c in range(200, 218):
        if random.random() < 0.3:
            set_tile(c, r, WOOD_BG)

# Cairn (stone stack)
fill_rect(195, 63, 196, 65, STONE)

# ============================================================
# ZONE 7: FOREST EXIT (cols 221-249)
# ============================================================
for c in range(221, 250):
    gr = 66 + (1 if random.random() < 0.1 else 0)
    for r in range(gr, 69):
        set_tile(c, r, GRASS if r == gr else DIRT)
    for r in range(69, 75):
        set_tile(c, r, DIRT)

# One tree
tree_trunk(230, 231, 40, 65)

# Sparse canopy
fill_rect_ragged(226, 30, 236, 36, GRASS, edge_prob=0.4)

# Lookout platform
fill_rect(240, 60, 249, 62, STONE)

# ============================================================
# Assemble JSON
# ============================================================
level = {
    "name": "The Living Forest",
    "author": "",
    "playerSpawn": {"x": 160, "y": 2144},
    "bounds": {"left": 0, "right": 8000, "top": 0, "bottom": 2560},
    "floor": {"y": 2500, "height": 50},
    "platforms": [],
    "ropes": [],
    "walls": [],
    "spikes": [],
    "ceilings": [],
    "solidFloors": [],
    "wallSpikes": [],
    "exits": [
        {
            "x": 64, "y": 1700, "w": 64, "h": 96,
            "targetLevel": "ship-interior",
            "id": "exit-to-ship",
            "targetExitId": "exit-to-surface-east"
        }
    ],
    "npcs": [],
    "items": [
        {"id": "battery-1", "type": "battery", "x": 2560, "y": 2240, "w": 20, "h": 20},
        {"id": "grapple-1", "type": "grapple", "x": 4960, "y": 1500, "w": 20, "h": 20}
    ],
    "objects": [],
    "enemies": [
        {"id": "forager-1", "type": "forager", "x": 1350, "y": 2240, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "forager-2", "type": "forager", "x": 1500, "y": 2240, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "hopper-1", "type": "hopper", "x": 1700, "y": 2200, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "hopper-2", "type": "hopper", "x": 1800, "y": 2200, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "bird-1", "type": "bird", "x": 1600, "y": 2150, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "scav-1", "type": "scavenger", "x": 2400, "y": 2240, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "scav-2", "type": "scavenger", "x": 2500, "y": 2240, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "scav-3", "type": "scavenger", "x": 2650, "y": 2240, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "scav-4", "type": "scavenger", "x": 2700, "y": 2240, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "leaper-1", "type": "leaper", "x": 3400, "y": 2150, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "leaper-2", "type": "leaper", "x": 3700, "y": 2150, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "leaper-3", "type": "leaper", "x": 4200, "y": 2150, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "skitter-1", "type": "skitter", "x": 3200, "y": 2240, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "skitter-2", "type": "skitter", "x": 3900, "y": 2240, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "forager-3", "type": "forager", "x": 3500, "y": 2240, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "wingbeater-1", "type": "wingbeater", "x": 4500, "y": 1200, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "thornback-1", "type": "thornback", "x": 6200, "y": 2050, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "bird-2", "type": "bird", "x": 5800, "y": 2050, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},
        {"id": "hopper-3", "type": "hopper", "x": 6000, "y": 2050, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False}
    ],
    "shelters": [{"x": 6400, "y": 2080, "w": 32, "h": 64}],
    "switches": [],
    "labels": [],
    "retractableSpikes": [],
    "isUnderground": False,
    "neighbors": {
        "left": "", "right": "", "up": "", "down": "",
        "LeftZones": [], "RightZones": [], "UpZones": [], "DownZones": []
    },
    "envRegions": [],
    "tileGrid": {
        "width": W,
        "height": H,
        "tileSize": 32,
        "originX": 0,
        "originY": 0,
        "tiles": tiles
    }
}

out_path = "/home/node/.openclaw/workspace/genesys/Content/levels/surface-east.json"
os.makedirs(os.path.dirname(out_path), exist_ok=True)
with open(out_path, "w") as f:
    json.dump(level, f, indent=2)

# Stats
from collections import Counter
counts = Counter(tiles)
names = {0:"Empty",1:"Dirt",2:"Stone",3:"Grass",4:"Wood",20:"PlatformWood",
         21:"PlatformStone",24:"PlatformMetal",50:"SlopeUpRight",51:"SlopeUpLeft",
         70:"Breakable",95:"MetalFloor",101:"DirtBg",102:"StoneBg",103:"GrassBg",104:"WoodBg"}
print(f"Total tiles: {len(tiles)} ({W}x{H})")
print("\nTile counts:")
for tid, cnt in sorted(counts.items(), key=lambda x: -x[1]):
    print(f"  {names.get(tid, f'Unknown({tid})')}: {cnt}")
print(f"\nWrote {out_path} ({os.path.getsize(out_path)} bytes)")
