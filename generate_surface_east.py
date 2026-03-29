#!/usr/bin/env python3
"""Generate redesigned surface-east.json with 4-zone layout."""
import json, math

W, H = 250, 80
TILE_SIZE = 32

# Tile types
EMPTY, DIRT, STONE, GRASS, WOOD, SAND = 0, 1, 2, 3, 4, 5
PLAT_WOOD, PLAT_STONE, PLAT_METAL = 20, 21, 24
METAL_FLOOR, METAL_WALL = 95, 96
DIRT_BG, STONE_BG = 101, 102

def make_grid():
    return [EMPTY] * (W * H)

def set_tile(grid, x, y, t):
    if 0 <= x < W and 0 <= y < H:
        grid[y * W + x] = t

def get_tile(grid, x, y):
    if 0 <= x < W and 0 <= y < H:
        return grid[y * W + x]
    return EMPTY

def fill_rect(grid, x1, y1, x2, y2, t):
    for y in range(max(0,y1), min(H,y2+1)):
        for x in range(max(0,x1), min(W,x2+1)):
            grid[y * W + x] = t

def ground_height(tx):
    """Returns the y tile coordinate of the ground surface for each x tile."""
    # Zone A: 0-60, flat around y=70
    if tx < 10:
        return 70
    elif tx < 60:
        # Slight variation, mostly flat at 70
        h = 70 + int(math.sin(tx * 0.15) * 1.5)
        # Rock outcrop at x=40-48, elevated platform
        return h
    # Zone B: 60-100, climbing from 70 to 25, then descending
    elif tx < 65:
        return 70  # transition
    elif tx < 90:
        # Linear climb from 70 to 25
        t = (tx - 65) / 25.0
        return int(70 - 45 * t)
    elif tx < 95:
        return 25  # Vista plateau
    elif tx < 100:
        # Descent from 25 to 65
        t = (tx - 95) / 5.0
        return int(25 + 40 * t)
    # Zone C: 100-180, ground at 65-70
    elif tx < 120:
        return 67
    elif tx < 140:
        # Clearing area, flat at 68
        return 68
    elif tx < 170:
        return 68 + int(math.sin((tx - 140) * 0.1) * 2)
    elif tx < 180:
        return 67
    # Zone D: 180-250, ground at 67
    elif tx < 230:
        return 67
    else:
        return 67

def build_tiles():
    tiles = make_grid()
    bg = make_grid()
    
    # Build terrain for each column
    for tx in range(W):
        gy = ground_height(tx)
        
        # Surface layer = Grass (Zone A uses some metal debris)
        if tx < 60:
            surface = GRASS
        elif tx < 100:
            surface = STONE if gy < 50 else GRASS
        elif tx < 180:
            surface = GRASS
        else:
            surface = GRASS
        
        set_tile(tiles, tx, gy, surface)
        
        # Fill dirt below surface
        for y in range(gy + 1, min(H, gy + 4)):
            set_tile(tiles, tx, y, DIRT)
        for y in range(gy + 4, H):
            set_tile(tiles, tx, y, STONE)
        
        # Background tiles above ground for depth
        for y in range(max(0, gy - 8), gy):
            if tx < 60:
                set_tile(bg, tx, y, DIRT_BG)
            elif tx < 100:
                set_tile(bg, tx, y, STONE_BG)
            else:
                set_tile(bg, tx, y, DIRT_BG)
    
    # === ZONE A: Debris Field (0-60) ===
    # Metal hull plates as platforms
    fill_rect(tiles, 8, 65, 14, 65, PLAT_METAL)   # Floating hull plate
    fill_rect(tiles, 20, 62, 26, 62, PLAT_METAL)   # Another hull section
    fill_rect(tiles, 35, 58, 40, 58, PLAT_METAL)   # Higher debris
    
    # Rock outcrop at x=40-48, y=55 (leaper perch)
    fill_rect(tiles, 40, 55, 48, 55, STONE)
    fill_rect(tiles, 41, 56, 47, 57, STONE)
    
    # Metal wall remnants (left wall near ship exit)
    fill_rect(tiles, 0, 50, 1, 70, METAL_WALL)
    # Ship exit area - clear a passage at y=53 (exit at y=1700px = tile 53)
    fill_rect(tiles, 0, 52, 2, 54, EMPTY)
    set_tile(tiles, 0, 52, METAL_FLOOR)
    set_tile(tiles, 0, 54, METAL_FLOOR)
    
    # Cargo container area at x=48-52 (where battery goes)
    fill_rect(tiles, 48, 68, 52, 68, METAL_FLOOR)
    fill_rect(tiles, 48, 65, 48, 68, METAL_WALL)
    fill_rect(tiles, 52, 65, 52, 68, METAL_WALL)
    
    # === ZONE B: Vista Climb (60-100) ===
    # Metal platform stepping stones for climbing
    fill_rect(tiles, 62, 66, 65, 66, PLAT_METAL)
    fill_rect(tiles, 67, 60, 70, 60, PLAT_METAL)
    fill_rect(tiles, 72, 54, 75, 54, PLAT_METAL)
    fill_rect(tiles, 77, 47, 80, 47, PLAT_METAL)
    fill_rect(tiles, 82, 40, 85, 40, PLAT_METAL)
    fill_rect(tiles, 87, 33, 89, 33, PLAT_METAL)
    
    # Bird alcove at x=75, y=37 (a small platform)
    fill_rect(tiles, 74, 37, 76, 37, PLAT_STONE)
    fill_rect(tiles, 73, 38, 73, 40, STONE)  # Cliff wall behind
    
    # Vista point - wide flat platform at top
    fill_rect(tiles, 88, 25, 95, 25, STONE)
    fill_rect(tiles, 87, 26, 96, 27, STONE)
    
    # Cliff face on the left side of zone B
    for y in range(25, 70):
        gy_local = ground_height(60)
        cliff_x = 60
        if y > 30:
            set_tile(tiles, cliff_x, y, STONE)
            if y > 40:
                set_tile(tiles, cliff_x + 1, y, STONE)
    
    # === ZONE C: Forest Edge (100-180) ===
    # Tree trunk platforms (wood)
    for tx_base in [106, 115, 125, 135, 148, 160, 168]:
        # Vertical trunk
        trunk_y = ground_height(tx_base)
        for y in range(trunk_y - 15, trunk_y):
            set_tile(tiles, tx_base, y, WOOD)
        # Branch platforms
        fill_rect(tiles, tx_base - 2, trunk_y - 12, tx_base + 2, trunk_y - 12, PLAT_WOOD)
        fill_rect(tiles, tx_base - 3, trunk_y - 8, tx_base + 1, trunk_y - 8, PLAT_WOOD)
    
    # Hopper clearing at x=119-131 - make it flat
    for tx in range(119, 132):
        gy = 68
        set_tile(tiles, tx, gy, GRASS)
        for y in range(gy + 1, min(H, gy + 4)):
            set_tile(tiles, tx, y, DIRT)
    
    # Path split at x=137
    # Upper path: wooden platforms y=38-45
    fill_rect(tiles, 138, 42, 142, 42, PLAT_WOOD)
    fill_rect(tiles, 145, 40, 149, 40, PLAT_WOOD)
    fill_rect(tiles, 152, 38, 156, 38, PLAT_WOOD)
    fill_rect(tiles, 159, 40, 163, 40, PLAT_WOOD)
    fill_rect(tiles, 166, 42, 169, 42, PLAT_WOOD)
    
    # Lower path stays on ground (already exists), add some fungal terrain
    for tx in range(140, 170):
        gy = ground_height(tx)
        # Make lower path slightly darker with wood tiles as fungal platforms
        if tx % 7 == 0:
            set_tile(tiles, tx, gy - 1, WOOD)
    
    # Stalker ceiling perch at x=150, y=40
    fill_rect(tiles, 148, 35, 153, 35, STONE)  # Ceiling for stalker
    fill_rect(tiles, 148, 36, 153, 36, STONE)
    
    # Paths converge at x=169
    fill_rect(tiles, 168, 45, 172, 45, PLAT_WOOD)  # Ramp down from upper
    
    # === ZONE D: Observation Clearing (180-250) ===
    # Stone cairn at center x=200
    fill_rect(tiles, 199, 65, 201, 65, PLAT_STONE)
    fill_rect(tiles, 200, 64, 200, 64, STONE)
    
    # Rocky outcrop / shelter cave at x=225-237
    fill_rect(tiles, 225, 50, 237, 50, STONE)  # Roof
    fill_rect(tiles, 225, 51, 225, 58, STONE)  # Left wall
    fill_rect(tiles, 237, 51, 237, 58, STONE)  # Right wall
    fill_rect(tiles, 225, 58, 237, 58, STONE)  # Floor
    fill_rect(tiles, 226, 57, 236, 57, PLAT_STONE)  # Platform inside
    # Entrance on left side - clear some wall
    fill_rect(tiles, 225, 54, 225, 56, EMPTY)
    
    # Raised ground leading to shelter
    for tx in range(220, 226):
        for y in range(55, 67):
            set_tile(tiles, tx, y, STONE)
        set_tile(tiles, tx, 54, GRASS)
    
    # Right wall boundary
    fill_rect(tiles, 249, 0, 249, 79, STONE)
    
    return tiles, bg

def make_enemy(eid, etype, x, y, passive=False, frozen=False):
    return {
        "id": eid,
        "type": etype,
        "x": x,
        "y": y,
        "count": 0,
        "scale": 1,
        "scaleX": 0,
        "scaleY": 0,
        "frozen": frozen,
        "passive": passive
    }

def build_enemies():
    enemies = []
    n = [0]
    def add(etype, x, y, **kw):
        n[0] += 1
        enemies.append(make_enemy(f"{etype}-{n[0]}", etype, x, y, **kw))
    
    # Zone A: Debris Field
    add("forager", 400, 2200)
    add("forager", 700, 2200)
    add("hopper", 1000, 2180, passive=True)
    add("leaper", 1350, 1760)   # On outcrop (visible)
    add("leaper", 1600, 2200)   # Hidden behind terrain
    add("leaper", 1700, 2200)   # Hidden behind terrain
    add("scavenger", 1500, 2200)
    
    # Zone B: Vista
    add("bird", 2400, 1200, passive=True)
    add("wingbeater", 3100, 400, passive=True)
    
    # Zone C: Forest Edge
    # Hopper clearing
    add("hopper", 3850, 2140, passive=True)
    add("hopper", 3950, 2140, passive=True)
    add("hopper", 4050, 2140, passive=True)
    add("hopper", 4150, 2140, passive=True)
    # Foragers on tree trunks
    add("forager", 3400, 1760)
    add("forager", 4000, 1760)
    add("forager", 5100, 1760)
    # Lower path enemies
    add("skitter", 4600, 2140)
    add("stalker", 4800, 1120)  # On ceiling
    
    # Zone D: Observation Clearing
    # Storm leapers
    add("leaper", 6800, 2140)
    add("leaper", 6900, 2140)
    add("leaper", 7000, 2140)
    add("leaper", 7100, 2140)
    # Birds (flee during storm)
    add("bird", 6200, 800, passive=True)
    add("bird", 6300, 900, passive=True)
    # Scavenger in shelter
    add("scavenger", 7400, 1750)
    
    # Extra enemies to reach ~45-50 total
    # Zone A extras
    add("forager", 300, 2200)
    add("hopper", 1200, 2180, passive=True)
    # Zone C extras - more hoppers on upper path
    add("hopper", 4500, 1280, passive=True)
    add("hopper", 4700, 1280, passive=True)
    add("hopper", 5000, 1280, passive=True)
    # More foragers in forest
    add("forager", 3600, 1900)
    add("forager", 4400, 1900)
    add("forager", 5300, 1900)
    # Zone D extras
    add("leaper", 6500, 2140)
    add("leaper", 6600, 2140)
    add("forager", 6000, 2140)
    add("forager", 6100, 2140)
    add("scavenger", 5900, 2140)
    add("skitter", 7200, 2140)
    # A few thornbacks in zone C/D
    add("thornback", 5500, 2140)
    add("thornback", 6400, 2140)
    # Zone B extras
    add("bird", 2800, 600, passive=True)
    add("hopper", 2200, 2100, passive=True)
    # Zone A/B transition
    add("leaper", 1900, 2200)
    add("forager", 1800, 2200)
    
    return enemies

def build_items():
    return [
        {"id": "battery-1", "type": "battery", "x": 1600, "y": 2176, "w": 20, "h": 20},
        {"id": "grapple-1", "type": "grapple", "x": 5000, "y": 2140, "w": 20, "h": 20},
        {"id": "heart-1", "type": "heart", "x": 7400, "y": 1792, "w": 20, "h": 20},
    ]

def build_exits():
    return [
        {
            "x": 64, "y": 1700, "w": 64, "h": 96,
            "targetLevel": "ship-interior",
            "id": "exit-to-ship",
            "targetExitId": "exit-to-surface-east"
        },
        {
            "x": 7900, "y": 2100, "w": 64, "h": 96,
            "targetLevel": "",
            "id": "exit-to-east-locked",
            "targetExitId": ""
        }
    ]

def main():
    tiles, bg = build_tiles()
    
    # Load original to preserve structure
    with open("Content/levels/surface-east.json") as f:
        data = json.load(f)
    
    # Update fields
    data["playerSpawn"] = {"x": 160, "y": 1664}
    data["tileGrid"]["tiles"] = tiles
    data["enemies"] = build_enemies()
    data["items"] = build_items()
    data["exits"] = build_exits()
    data["shelters"] = [
        {"id": "shelter-cave", "x": 7300, "y": 1760, "name": "Shelter Cave"}
    ]
    
    # Update bg visual layer
    for layer in data.get("visualLayers", []):
        if layer["name"] == "bg":
            layer["tiles"] = bg
    
    with open("Content/levels/surface-east.json", "w") as f:
        json.dump(data, f)
    
    print(f"Generated: {len(data['enemies'])} enemies, {len(data['items'])} items, {len(data['exits'])} exits")

if __name__ == "__main__":
    main()
