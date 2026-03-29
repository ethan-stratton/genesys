#!/usr/bin/env python3
"""Generate redesigned crashsite level."""
import json

W, H = 80, 100
tiles = [0] * (W * H)

def set_tile(x, y, t):
    if 0 <= x < W and 0 <= y < H:
        tiles[y * W + x] = t

def fill_rect(x1, y1, x2, y2, t):
    for y in range(y1, y2+1):
        for x in range(x1, x2+1):
            set_tile(x, y, t)

def get_tile(x, y):
    if 0 <= x < W and 0 <= y < H:
        return tiles[y * W + x]
    return -1

# === CAVE SHELL ===
# Top wall
fill_rect(0, 0, 79, 9, 2)  # Stone ceiling

# Left wall - thick, narrows cave at top, widens at bottom
for y in range(H):
    if y < 25:
        wall_right = 18  # narrow at top
    elif y < 55:
        wall_right = 14 - (y - 25) // 6  # gradually widens
    elif y < 70:
        wall_right = 8
    else:
        wall_right = 6
    fill_rect(0, y, wall_right, y, 2)

# Right wall - solid at top, opens up at bottom for exit
for y in range(H):
    if y < 70:
        if y < 25:
            wall_left = 62
        elif y < 55:
            wall_left = 66 + (y - 25) // 6
        else:
            wall_left = 72
        fill_rect(wall_left, y, 79, y, 2)
    elif y < 82:
        # Cave mouth opening on right for exit
        wall_left = 72 + (y - 70) // 3  # wall recedes
        if wall_left < 80:
            fill_rect(wall_left, y, 79, y, 2)
    else:
        # Exit area - right side open, but floor below
        pass  # no right wall here - exit area

# Bottom - NO floor (chasm), but walls on sides
for y in range(85, 100):
    fill_rect(0, y, 6, y, 2)  # left wall continues
    # Right side: some wall far right at very bottom
    if y > 90:
        fill_rect(76, y, 79, y, 2)

# === SHIP HULL (MetalWall 96) embedded upper-left ===
fill_rect(20, 10, 50, 32, 96)

# Hollow out interior of ship (it's embedded, so only exterior shell visible)
# The cave side of the ship is open - carve out the right/bottom edge
# Actually the ship is IN the wall, so the metal replaces stone in that area
# Add some stone back around it to embed it
fill_rect(0, 10, 19, 32, 2)  # stone left of ship

# Ship hatch area - clear space outside the hatch
# Hatch at approximately x=37, y=33 (just below ship hull)
fill_rect(30, 33, 45, 35, 0)  # clear space below hatch

# Metal platform outside hatch (PlatformMetal = 24)
fill_rect(34, 35, 42, 35, 24)

# === DESCENT PLATFORMS (PlatformStone = 21) ===
# Zigzag down from hatch area
platforms = [
    # (x_start, y, length) - zigzagging left-right
    (34, 35, 9),   # hatch ledge (already metal, skip)
    (42, 39, 10),  # right
    (25, 43, 10),  # left
    (40, 47, 12),  # right
    (20, 51, 10),  # left
    (38, 55, 12),  # right - entering dead creature area
]

for px, py, plen in platforms:
    fill_rect(px, py, px + plen - 1, py, 21)

# Grass on some ledges
grass_spots = [(44, 38), (45, 38), (27, 42), (28, 42), (42, 46), (22, 50), (23, 50)]
for gx, gy in grass_spots:
    set_tile(gx, gy, 3)

# === SPORE CLOUD AREA (y=35-40 area, let's put it around y=43-47) ===
# Grass on walls representing spore plants
spore_plants = [
    (19, 44), (19, 45), (19, 46),  # left wall spores
    (20, 44), (20, 45),
    (62, 44), (62, 45), (62, 46),  # right wall spores  
    (61, 45), (61, 46),
]
for sx, sy in spore_plants:
    if get_tile(sx, sy) == 2:
        set_tile(sx, sy, 3)

# The main 3-tile opening is the normal path (spore cloud fills it narratively)
# 2-tile crouch gap below: ensure clear at y=48-49 between walls
# Wind gap above: ensure clear at y=42-43

# === DEAD CREATURE AREA (y=55-70) ===
# Wider flat area with dirt mound
fill_rect(15, 65, 55, 67, 1)   # Dirt ground
fill_rect(20, 63, 45, 64, 1)   # Dirt mound upper part
fill_rect(25, 61, 40, 62, 1)   # Dirt mound peak
fill_rect(22, 64, 48, 65, 21)  # PlatformStone at base

# === StoneBg (102) in open cave areas ===
for y in range(10, H):
    for x in range(W):
        if tiles[y * W + x] == 0:
            # Check if this is interior cave space
            if y >= 70:
                set_tile(x, y, 102)  # chasm background
            elif y >= 33:
                # General cave interior background
                set_tile(x, y, 102)

# === CAVE EXIT (right side, near bottom) ===
# DirtBg (101) behind exit area  
for y in range(78, 90):
    for x in range(65, 80):
        if get_tile(x, y) == 102 or get_tile(x, y) == 0:
            set_tile(x, y, 101)

# Grass near exit
exit_grass = [(65, 82), (66, 82), (67, 82), (68, 83), (70, 81), (72, 80)]
for gx, gy in exit_grass:
    if get_tile(gx, gy) in (101, 102, 0):
        set_tile(gx, gy, 3)

# === BUILD LEVEL JSON ===
# Load existing to preserve structure
with open('Content/levels/crashsite.json') as f:
    level = json.load(f)

level['playerSpawn'] = {"x": 1200, "y": 1050}
level['bounds'] = {"left": 0, "right": 2560, "top": 0, "bottom": 3200}

level['enemies'] = [
    {"id": "forager-1", "type": "forager", "x": 800, "y": 1300, "w": 32, "h": 32},
    {"id": "forager-2", "type": "forager", "x": 1600, "y": 1400, "w": 32, "h": 32},
    {"id": "scavenger-1", "type": "scavenger", "x": 1200, "y": 2400, "w": 32, "h": 32},
]

level['items'] = [
    {"id": "battery-1", "type": "battery", "x": 1800, "y": 2200, "w": 20, "h": 20},
]

level['exits'] = [
    {
        "x": 1200, "y": 1100, "w": 128, "h": 64,
        "targetLevel": "ship-interior",
        "id": "exit-to-ship",
        "targetExitId": "exit-to-crashsite"
    },
    {
        "x": 2400, "y": 2700, "w": 128, "h": 64,
        "targetLevel": "surface-east",
        "id": "exit-to-surface",
        "targetExitId": "exit-from-cave"
    },
]

level['neighbors'] = {
    "left": "ship-interior",
    "right": "surface-east",
    "up": "",
    "down": "",
    "LeftZones": [],
    "RightZones": [],
    "UpZones": [],
    "DownZones": []
}

level['tileGrid'] = {
    "width": W,
    "height": H,
    "tileSize": 32,
    "originX": 0,
    "originY": 0,
    "tiles": tiles
}

level['isUnderground'] = True

# Clear old geometry that's now in tiles
level['platforms'] = []
level['ropes'] = []
level['walls'] = []
level['solidFloors'] = []
level['floor'] = {"y": 3200, "height": 0}

with open('Content/levels/crashsite.json', 'w') as f:
    json.dump(level, f, indent=2)

print(f"Generated crashsite: {W}x{H}, {len(level['enemies'])} enemies, {len(level['items'])} items, {len(level['exits'])} exits")
