#!/usr/bin/env python3
"""Generate redesigned ship-interior level for Genesis."""
import json

W, H = 200, 80
TILE_SIZE = 32

# Tile IDs
EMPTY = 0
DIRT = 1
STONE = 2
PLATFORM_METAL = 24
METAL_FLOOR = 95
METAL_WALL = 96
DIRT_BG = 101

def make_grid():
    return [STONE] * (W * H)

def s(tiles, x, y, v):
    if 0 <= x < W and 0 <= y < H:
        tiles[y * W + x] = v

def g(tiles, x, y):
    if 0 <= x < W and 0 <= y < H:
        return tiles[y * W + x]
    return -1

def fill_rect(tiles, x1, y1, x2, y2, v):
    for y in range(max(0, y1), min(H, y2 + 1)):
        for x in range(max(0, x1), min(W, x2 + 1)):
            tiles[y * W + x] = v

def hline(tiles, x1, x2, y, v):
    for x in range(max(0, x1), min(W, x2 + 1)):
        s(tiles, x, y, v)

def vline(tiles, x, y1, y2, v):
    for y in range(max(0, y1), min(H, y2 + 1)):
        s(tiles, x, y, v)

tiles = make_grid()

# === SHIP HULL OUTLINE ===
# The ship spans roughly x=2..197, with two decks
# Upper deck: y=24..42 (ceiling at 24, floor at 42)
# Lower deck: y=44..72 (ceiling at 44, floor at 72)
# Ship hull walls surround everything

# -- COCKPIT / BRIDGE (x=3..12, upper deck) --
cockpit_l, cockpit_r = 3, 12
cockpit_top, cockpit_bot = 28, 42
# Carve interior
fill_rect(tiles, cockpit_l, cockpit_top, cockpit_r, cockpit_bot, EMPTY)
# Walls
hline(tiles, cockpit_l, cockpit_r, cockpit_top - 1, METAL_WALL)  # ceiling
hline(tiles, cockpit_l, cockpit_r, cockpit_bot + 1, METAL_FLOOR)  # floor
vline(tiles, cockpit_l - 1, cockpit_top - 1, cockpit_bot + 1, METAL_WALL)  # left wall
# Right wall with doorway (opening at y=37..41)
for y in range(cockpit_top - 1, cockpit_bot + 2):
    if y < 37 or y > 41:
        s(tiles, cockpit_r + 1, y, METAL_WALL)

# Exit to crashsite on LEFT wall at y=33 (pixel: 33*32=1056)
# Opening in left wall for exit
for y in range(31, 34):
    s(tiles, cockpit_l - 1, y, EMPTY)
    s(tiles, cockpit_l - 2, y, EMPTY)

# Console decorations
s(tiles, 5, 41, PLATFORM_METAL)
s(tiles, 6, 41, PLATFORM_METAL)
s(tiles, 7, 41, PLATFORM_METAL)
# Scavenger in cockpit
s(tiles, 8, 39, PLATFORM_METAL)  # elevated console

# -- UPPER CORRIDOR (x=13..58) --
corr_top = 33
corr_bot = 42
fill_rect(tiles, 13, corr_top, 58, corr_bot, EMPTY)
hline(tiles, 13, 58, corr_top - 1, METAL_WALL)  # ceiling
hline(tiles, 13, 58, corr_bot + 1, METAL_FLOOR)  # floor

# Collapsed beam - low ceiling at x=35..42, y=38 (forces crouch, 2-tile gap: y=40,41 open, floor at 42)
hline(tiles, 35, 42, 38, METAL_WALL)
hline(tiles, 35, 42, 37, METAL_WALL)
# Fill above beam
fill_rect(tiles, 35, corr_top, 42, 36, METAL_WALL)
# Clear the crouch gap
fill_rect(tiles, 35, 39, 42, 42, EMPTY)
# Restore floor under crouch section
hline(tiles, 35, 42, corr_bot + 1, METAL_FLOOR)

# Vertical shaft down to crew quarters at x=55..58
fill_rect(tiles, 55, corr_bot + 1, 58, 44, EMPTY)  # remove floor section

# -- CREW QUARTERS (x=35..55, lower deck y=45..65) --
cq_l, cq_r = 35, 55
cq_top, cq_bot = 45, 65
fill_rect(tiles, cq_l, cq_top, cq_r, cq_bot, EMPTY)
hline(tiles, cq_l, cq_r, cq_top - 1, METAL_WALL)  # ceiling
hline(tiles, cq_l, cq_r, cq_bot + 1, METAL_FLOOR)  # floor
vline(tiles, cq_l - 1, cq_top - 1, cq_bot + 1, METAL_WALL)  # left wall
# Right wall with doorway to medical bay (y=58..62)
for y in range(cq_top - 1, cq_bot + 2):
    if y < 58 or y > 62:
        s(tiles, cq_r + 1, y, METAL_WALL)

# Shaft from upper corridor connects at x=55..58, y=43..45
fill_rect(tiles, 55, 43, 58, 45, EMPTY)
# Shaft walls
vline(tiles, 54, 43, 45, METAL_WALL)

# Bunk platforms
hline(tiles, 38, 42, 52, PLATFORM_METAL)  # upper bunk
hline(tiles, 38, 42, 58, PLATFORM_METAL)  # lower bunk
hline(tiles, 45, 49, 48, PLATFORM_METAL)  # high shelf (battery here)
hline(tiles, 45, 49, 55, PLATFORM_METAL)  # mid shelf

# -- MEDICAL BAY (x=56..78, split between upper and lower) --
# Upper portion connects from corridor
med_top_u = 33
med_bot_u = 42
fill_rect(tiles, 59, med_top_u, 78, med_bot_u, EMPTY)
hline(tiles, 59, 78, med_top_u - 1, METAL_WALL)
hline(tiles, 59, 78, med_bot_u + 1, METAL_FLOOR)

# Lower medical bay (connects from crew quarters)
med_top_l = 45
med_bot_l = 65
fill_rect(tiles, 56, med_top_l, 78, med_bot_l, EMPTY)
hline(tiles, 56, 78, med_top_l - 1, METAL_WALL)  # will overlap with upper floor
hline(tiles, 56, 78, med_bot_l + 1, METAL_FLOOR)
vline(tiles, 79, med_top_u - 1, med_bot_l + 1, METAL_WALL)  # right wall with doorway

# Doorway in right wall (upper level, y=37..41)
for y in range(37, 42):
    s(tiles, 79, y, EMPTY)
# Doorway in right wall (lower level, y=58..62)
for y in range(58, 63):
    s(tiles, 79, y, EMPTY)

# Vertical connection between upper and lower medical bay at x=65..68
fill_rect(tiles, 65, med_bot_u + 1, 68, med_top_l - 1, EMPTY)

# Medical shelf with heart
hline(tiles, 60, 64, 40, PLATFORM_METAL)
hline(tiles, 70, 74, 52, PLATFORM_METAL)

# -- MID CORRIDOR (x=80..109) --
# Upper corridor
mc_top = 33
mc_bot = 42
fill_rect(tiles, 80, mc_top, 109, mc_bot, EMPTY)
hline(tiles, 80, 109, mc_top - 1, METAL_WALL)
hline(tiles, 80, 109, mc_bot + 1, METAL_FLOOR)

# Lower corridor  
fill_rect(tiles, 80, 45, 109, 65, EMPTY)
hline(tiles, 80, 109, 44, METAL_WALL)
hline(tiles, 80, 109, 66, METAL_FLOOR)

# Floor gaps showing dirt below (in lower corridor)
for x in range(88, 93):
    s(tiles, x, 66, EMPTY)
    s(tiles, x, 67, DIRT)
    s(tiles, x, 68, DIRT)

for x in range(100, 104):
    s(tiles, x, 66, EMPTY)
    s(tiles, x, 67, DIRT)
    s(tiles, x, 68, DIRT)

# Vertical shaft connecting upper to lower at x=105..108
fill_rect(tiles, 105, mc_bot + 1, 108, 44, EMPTY)

# Walls
vline(tiles, 110, mc_top - 1, 66, METAL_WALL)
# Doorway at upper level
for y in range(37, 42):
    s(tiles, 110, y, EMPTY)
# Doorway at lower level
for y in range(58, 63):
    s(tiles, 110, y, EMPTY)

# -- CARGO BAY (x=111..144, y=25..72 — tall room) --
cargo_l, cargo_r = 111, 144
cargo_top, cargo_bot = 25, 72
fill_rect(tiles, cargo_l, cargo_top, cargo_r, cargo_bot, EMPTY)
hline(tiles, cargo_l, cargo_r, cargo_top - 1, METAL_WALL)  # ceiling
hline(tiles, cargo_l, cargo_r, cargo_bot + 1, METAL_FLOOR)  # floor
# Right wall with doorway to armory
vline(tiles, cargo_r + 1, cargo_top - 1, cargo_bot + 1, METAL_WALL)
for y in range(58, 63):
    s(tiles, cargo_r + 1, y, EMPTY)

# Platform/crate levels
hline(tiles, 115, 120, 55, PLATFORM_METAL)  # lower crates
hline(tiles, 123, 128, 45, PLATFORM_METAL)  # mid crates — KNIFE here
hline(tiles, 131, 136, 35, PLATFORM_METAL)  # high crates — heart here
hline(tiles, 118, 122, 65, PLATFORM_METAL)  # stepping stone
hline(tiles, 130, 135, 55, PLATFORM_METAL)  # another mid platform
hline(tiles, 138, 142, 62, PLATFORM_METAL)  # low right platform

# Main floor walkway at bottom
hline(tiles, cargo_l, cargo_r, 70, METAL_FLOOR)

# -- ARMORY (x=145..163, lower area y=50..72) --
arm_l, arm_r = 145, 163
arm_top, arm_bot = 50, 72
fill_rect(tiles, arm_l, arm_top, arm_r, arm_bot, EMPTY)
hline(tiles, arm_l, arm_r, arm_top - 1, METAL_WALL)  # ceiling
hline(tiles, arm_l, arm_r, arm_bot + 1, METAL_FLOOR)  # floor
vline(tiles, arm_r + 1, arm_top - 1, arm_bot + 1, METAL_WALL)

# Jump chain platforms to gun
hline(tiles, 148, 151, 68, PLATFORM_METAL)
hline(tiles, 154, 157, 63, PLATFORM_METAL)
hline(tiles, 149, 152, 57, PLATFORM_METAL)  # GUN here at top
hline(tiles, 157, 160, 55, PLATFORM_METAL)

# -- ENGINE ROOM (x=165..197, y=25..72) --
eng_l, eng_r = 165, 197
eng_top, eng_bot = 25, 72
fill_rect(tiles, eng_l, eng_top, eng_r, eng_bot, EMPTY)
hline(tiles, eng_l, eng_r, eng_top - 1, METAL_WALL)  # ceiling
hline(tiles, eng_l, eng_r, eng_bot + 1, METAL_FLOOR)  # floor
vline(tiles, eng_l - 1, eng_top - 1, eng_bot + 1, METAL_WALL)
# Doorway from armory/cargo
for y in range(58, 63):
    s(tiles, eng_l - 1, y, EMPTY)

# Machinery platforms
hline(tiles, 168, 173, 55, PLATFORM_METAL)
hline(tiles, 176, 181, 45, PLATFORM_METAL)
hline(tiles, 170, 175, 38, PLATFORM_METAL)
hline(tiles, 183, 188, 60, PLATFORM_METAL)

# Terminal area
hline(tiles, 185, 190, 50, PLATFORM_METAL)
s(tiles, 187, 49, PLATFORM_METAL)  # terminal decoration

# Main floor
hline(tiles, eng_l, eng_r, 70, METAL_FLOOR)

# HULL BREACH — right wall is OPEN, with DirtBg behind
# Remove right wall entirely from y=30..72
for y in range(30, 73):
    for x in range(195, 200):
        s(tiles, x, y, DIRT_BG)

# Ground/planet visible below ship
fill_rect(tiles, 0, 73, 199, 79, DIRT)
fill_rect(tiles, 0, 75, 199, 79, STONE)

# DirtBg behind some floor gaps
for x in range(88, 93):
    s(tiles, x, 69, DIRT_BG)
for x in range(100, 104):
    s(tiles, x, 69, DIRT_BG)

# === BUILD LEVEL JSON ===
level = {
    "bounds": {"left": 0, "right": 6400, "top": 0, "bottom": 2560},
    "playerStart": {"x": 192, "y": 1024},  # near cockpit entry
    "enemies": [
        {"id": "scav-1", "type": "scavenger", "x": 288, "y": 1280, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},  # cockpit (x=9*32, y=40*32)
        {"id": "scav-2", "type": "scavenger", "x": 1408, "y": 1888, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},  # crew quarters (x=44*32, y=59*32)
        {"id": "scav-3", "type": "scavenger", "x": 3008, "y": 2016, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},  # mid corridor (x=94*32, y=63*32)
        {"id": "scav-4", "type": "scavenger", "x": 3872, "y": 2208, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},  # cargo bay (x=121*32, y=69*32)
        {"id": "skitter-1", "type": "skitter", "x": 5696, "y": 1600, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},  # engine room wall (x=178*32, y=50*32)
        {"id": "forager-1", "type": "forager", "x": 6272, "y": 1920, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},  # hull breach exterior (x=196*32, y=60*32)
        {"id": "forager-2", "type": "forager", "x": 6336, "y": 2176, "count": 0, "scale": 1, "scaleX": 0, "scaleY": 0, "frozen": False, "passive": False},  # hull breach exterior
    ],
    "items": [
        {"id": "knife-1", "type": "knife", "x": 4000, "y": 1408, "w": 20, "h": 20},   # cargo bay crate (x=125*32, y=44*32)
        {"id": "gun-1", "type": "gun", "x": 4816, "y": 1792, "w": 20, "h": 20},        # armory high platform (x=150.5*32, y=56*32)
        {"id": "battery-1", "type": "battery", "x": 1504, "y": 1504, "w": 20, "h": 20}, # crew quarters high shelf (x=47*32, y=47*32)
        {"id": "battery-2", "type": "battery", "x": 5984, "y": 1568, "w": 20, "h": 20}, # engine room terminal (x=187*32, y=49*32)
        {"id": "heart-1", "type": "heart", "x": 1984, "y": 1248, "w": 20, "h": 20},     # medical bay shelf (x=62*32, y=39*32)
        {"id": "heart-2", "type": "heart", "x": 4256, "y": 1088, "w": 20, "h": 20},     # cargo bay high crate (x=133*32, y=34*32)
    ],
    "exits": [
        {"x": 64, "y": 1008, "w": 32, "h": 96, "targetLevel": "crashsite", "id": "exit-to-crashsite", "targetExitId": "exit-to-ship"},
        {"x": 6304, "y": 2208, "w": 32, "h": 96, "targetLevel": "surface-east", "id": "exit-to-surface-east", "targetExitId": "exit-to-ship"},
    ],
    "neighbors": {"left": "", "right": "", "up": "", "down": "", "LeftZones": [], "RightZones": [], "UpZones": [], "DownZones": []},
    "tileGrid": {
        "width": W,
        "height": H,
        "tileSize": TILE_SIZE,
        "originX": 0,
        "originY": 0,
        "tiles": tiles
    }
}

with open("Content/levels/ship-interior.json", "w") as f:
    json.dump(level, f, separators=(",", ":"))

print("Written ship-interior.json")
print(f"Tiles: {W}x{H} = {len(tiles)}")
print(f"Enemies: {len(level['enemies'])}")
print(f"Items: {len(level['items'])}")
