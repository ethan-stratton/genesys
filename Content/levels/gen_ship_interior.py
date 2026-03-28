#!/usr/bin/env python3
import json, random, os
from collections import Counter

W, H = 200, 80
EMPTY=0; STONE=2; METAL_FLOOR=95; METAL_WALL=96
PLAT_WOOD=20; PLAT_STONE=21; SLOPE_UP_LEFT=51
BROKEN_PIPE=87; ELECTRIC_SHOCK=88; PUDDLE=89; FIRE=86
DAMAGE=71; DAMAGE_NOKB=83; ACID=92
BREAKABLE=70; BREAKABLE_GLASS=93; BREAKABLE_BATTERY=94
WOOD_BG=104; STONE_BG=102; DIRT_BG=101

grid = [EMPTY]*(W*H)

def s(r,c,v):
    if 0<=r<H and 0<=c<W: grid[r*W+c]=v

def fill(r1,r2,c1,c2,v):
    for r in range(r1,r2+1):
        for c in range(c1,c2+1): s(r,c,v)

# CAVE SHELL
fill(0,8,0,199,STONE)
fill(76,79,0,199,STONE)
fill(0,79,0,1,STONE)
fill(0,79,198,199,STONE)

# UPPER DECK hull
fill(11,11,4,130,METAL_WALL)  # ceiling
fill(36,36,4,130,METAL_WALL)  # floor structure
fill(35,35,4,130,METAL_FLOOR) # walkable
# Left hull wall
fill(11,36,4,4,METAL_WALL)

# Room 1: COCKPIT (cols 5-24)
fill(11,32,24,24,METAL_WALL)  # right wall (door gap 33-35)
fill(15,17,8,12,WOOD_BG)      # dead consoles
s(12,10,BROKEN_PIPE); s(12,15,BROKEN_PIPE)
fill(16,17,18,20,METAL_WALL)  # nav console

# Room 2: UPPER CORRIDOR (cols 25-42)
fill(30,31,32,35,METAL_WALL)  # crouch beam
s(12,35,ELECTRIC_SHOCK)
s(12,28,BROKEN_PIPE); s(12,38,BROKEN_PIPE)

# Room 3: CREW QUARTERS (cols 43-68)
fill(11,32,42,42,METAL_WALL)  # left wall
fill(11,32,69,69,METAL_WALL)  # right wall
fill(25,25,45,50,PLAT_WOOD)   # bunk shelf
fill(29,29,55,60,PLAT_WOOD)   # desk
fill(28,30,65,65,BREAKABLE_GLASS) # glass cabinet
fill(30,33,50,55,WOOD_BG)     # furniture

# Room 4: MEDICAL BAY (cols 70-100)
fill(11,32,101,101,METAL_WALL) # right wall
fill(20,26,75,78,METAL_WALL)   # cryo pod
fill(21,25,76,77,WOOD_BG)      # cryo interior
fill(35,35,80,85,PUDDLE)       # leaked fluids
s(28,90,BREAKABLE_BATTERY)

# SHAFT (cols 97-107)
# Remove upper floor
fill(35,36,99,104,EMPTY)
# Remove lower ceiling at shaft
fill(44,45,99,104,EMPTY)
# Stepping stones
fill(40,40,100,102,PLAT_STONE)
fill(48,48,102,104,PLAT_STONE)
s(36,98,SLOPE_UP_LEFT)

# LOWER DECK hull
fill(44,44,25,196,METAL_WALL)  # ceiling
fill(73,73,25,196,METAL_WALL)  # bottom hull
fill(72,72,25,196,METAL_FLOOR) # walkable
# Left hull wall
fill(44,73,25,25,METAL_WALL)
# Right hull wall
fill(44,73,196,196,METAL_WALL)

# Room 5: MID CORRIDOR / TOXIC (cols 26-54)
fill(44,69,55,55,METAL_WALL)   # right wall (door gap 70-72)
fill(72,72,38,43,DAMAGE)       # toxic fungus
fill(65,65,39,42,PLAT_STONE)   # overhead route
fill(60,60,35,37,PLAT_WOOD)    # stepping stone
fill(65,67,48,49,BREAKABLE)    # breakable shortcut

# Room 6: CARGO BAY (cols 56-125)
fill(44,69,126,126,METAL_WALL) # right wall (door gap 70-72)
# Container stacks
fill(66,72,62,65,METAL_WALL)   # A
fill(62,72,75,77,METAL_WALL)   # B
fill(68,72,88,92,METAL_WALL)   # C
fill(58,72,100,103,METAL_WALL) # D
fill(64,72,112,116,METAL_WALL) # E
# Platforms
fill(63,63,66,74,PLAT_WOOD)
fill(58,58,78,87,PLAT_WOOD)
fill(54,54,93,99,PLAT_WOOD)
fill(67,67,104,111,PLAT_WOOD)
# Breakable glass shortcut
fill(68,70,85,86,BREAKABLE_GLASS)

# Room 7: ARMORY (cols 127-142)
fill(44,67,126,126,METAL_WALL) # left wall (gap 50-52 for high entrance) - already set, now clear gap
for r in range(50,53): s(r,126,EMPTY)
fill(44,69,143,143,METAL_WALL) # right wall (door gap 70-72)
fill(60,60,130,140,METAL_WALL) # weapons rack
fill(57,59,131,139,WOOD_BG)    # behind rack

# Room 8: LOWER CORRIDOR (cols 143-158)
fill(72,72,150,152,FIRE)       # burning debris
s(45,148,ELECTRIC_SHOCK)
fill(50,60,155,158,STONE_BG)   # cave visible
# Hull breach gap
fill(55,65,158,158,EMPTY)

# Room 9: ENGINE ROOM (cols 159-196)
fill(55,68,170,178,METAL_WALL) # reactor core
fill(69,69,168,180,DAMAGE_NOKB) # reactor base
fill(52,52,162,185,PLAT_STONE) # catwalks
fill(72,72,165,168,ACID)       # coolant
fill(72,72,190,193,FIRE)       # fire near exit

# Cave irregularity: stone poking through where cave meets hull
random.seed(42)
# Along upper hull top
for c in range(4,131):
    if random.random()<0.08: s(10,c,STONE)
# Along upper hull bottom  
for c in range(4,131):
    if random.random()<0.05: s(37,c,STONE)
# Along lower hull
for c in range(25,197):
    if random.random()<0.06: s(43,c,STONE)
    if random.random()<0.06: s(74,c,STONE)
# Some stone in the gap between decks
fill(9,10,2,3,STONE)
fill(9,10,197,199,STONE)
fill(74,75,2,3,STONE)
fill(74,75,197,199,STONE)
# Fill cave areas between hull and cave shell with some scattered stone
for r in range(9,11):
    for c in range(2,198):
        if grid[r*W+c]==EMPTY and random.random()<0.3: s(r,c,STONE)
for r in range(74,76):
    for c in range(2,198):
        if grid[r*W+c]==EMPTY and random.random()<0.3: s(r,c,STONE)

# Assemble JSON
level = {
    "name": "Ship Interior",
    "author": "",
    "width": W,
    "height": H,
    "tileWidth": 32,
    "tileHeight": 32,
    "bounds": {"left":0,"right":6400,"top":0,"bottom":2560},
    "playerSpawn": {"x":192,"y":1056},
    "floor": {"y":2400,"height":50},
    "isUnderground": True,
    "neighbors": {"left":"","right":"","up":"","down":"","LeftZones":[],"RightZones":[],"UpZones":[],"DownZones":[]},
    "envRegions": [],
    "tiles": grid,
    "exits": [
        {"x":128,"y":992,"w":32,"h":96,"targetLevel":"crashsite","id":"exit-to-crashsite","targetExitId":"exit-to-ship"},
        {"x":6304,"y":2208,"w":32,"h":96,"targetLevel":"surface-east","id":"exit-to-surface-east","targetExitId":"exit-to-ship"}
    ],
    "enemies": [
        {"id":"scav-1","type":"scavenger","x":2048,"y":2240,"count":0,"scale":1,"scaleX":0,"scaleY":0,"frozen":False,"passive":False},
        {"id":"scav-2","type":"scavenger","x":2560,"y":2240,"count":0,"scale":1,"scaleX":0,"scaleY":0,"frozen":False,"passive":False},
        {"id":"scav-3","type":"scavenger","x":3200,"y":2240,"count":0,"scale":1,"scaleX":0,"scaleY":0,"frozen":False,"passive":False},
        {"id":"scav-4","type":"scavenger","x":5600,"y":2240,"count":0,"scale":1,"scaleX":0,"scaleY":0,"frozen":False,"passive":False},
        {"id":"scav-5","type":"scavenger","x":5920,"y":2240,"count":0,"scale":1,"scaleX":0,"scaleY":0,"frozen":False,"passive":False},
        {"id":"skitter-1","type":"skitter","x":3840,"y":2240,"count":0,"scale":1,"scaleX":0,"scaleY":0,"frozen":False,"passive":False}
    ],
    "items": [
        {"id":"knife-1","type":"knife","x":3232,"y":1792,"w":20,"h":20},
        {"id":"gun-1","type":"gun","x":4320,"y":2208,"w":20,"h":20},
        {"id":"battery-1","type":"battery","x":1600,"y":768,"w":20,"h":20},
        {"id":"battery-2","type":"battery","x":5760,"y":1600,"w":20,"h":20},
        {"id":"heart-1","type":"heart","x":2112,"y":928,"w":20,"h":20},
        {"id":"heart-2","type":"heart","x":2880,"y":960,"w":20,"h":20}
    ],
    "shelters": [{"x":2720,"y":1056,"w":32,"h":64}],
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
    "retractableSpikes": []
}

out = os.path.join(os.path.dirname(__file__), "ship-interior.json")
with open(out, "w") as f:
    json.dump(level, f, separators=(",",":"))

counts = Counter(grid)
print(f"Written to {out}")
print(f"Total tiles: {len(grid)}")
for tid, cnt in sorted(counts.items(), key=lambda x:-x[1]):
    print(f"  Tile {tid:3d}: {cnt}")
