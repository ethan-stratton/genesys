#!/usr/bin/env python3
"""
Convert existing Genesys level JSON files to an LDtk project file.
Reads Content/levels/*.json and outputs Content/levels/genesys.ldtk
"""

import json
import os
import sys
import uuid
from pathlib import Path

GRID_SIZE = 32
LDTK_VERSION = "1.5.3"

# IntGrid value definitions matching TileType enum
INTGRID_VALUES = [
    (1, "Dirt", "6D4321"),
    (2, "Stone", "787878"),
    (3, "Grass", "4C9900"),
    (4, "Wood", "8B5A2B"),
    (5, "Sand", "C2B280"),
    (20, "PlatformWood", "8B5A2B"),
    (21, "PlatformStone", "646464"),
    (22, "PlatformTop", "646464"),
    (23, "PlatformBottom", "646464"),
    (24, "PlatformMetal", "828C9B"),
    (40, "Spikes", "C81E1E"),
    (41, "SpikesDown", "C81E1E"),
    (42, "SpikesLeft", "C81E1E"),
    (43, "SpikesRight", "C81E1E"),
    (44, "HalfSpikesUp", "B41919"),
    (45, "HalfSpikesDown", "B41919"),
    (46, "HalfSpikesLeft", "B41919"),
    (47, "HalfSpikesRight", "B41919"),
    (48, "RetractSpikesUp", "C8781E"),
    (49, "RetractSpikesDown", "C8781E"),
    (50, "SlopeUpRight", "5A3C1E"),
    (51, "SlopeUpLeft", "5A3C1E"),
    (52, "SlopeCeilRight", "463219"),
    (53, "SlopeCeilLeft", "463219"),
    (54, "GentleUpRight", "5F4123"),
    (55, "GentleUpLeft", "5F4123"),
    (56, "ShavedRight", "55371C"),
    (57, "ShavedLeft", "55371C"),
    (58, "GentleCeilRight", "412D16"),
    (59, "GentleCeilLeft", "412D16"),
    (60, "ShavedCeilRight", "412D16"),
    (61, "ShavedCeilLeft", "412D16"),
    (62, "Gentle4UpRightA", "5F4123"),
    (63, "Gentle4UpRightB", "5F4123"),
    (64, "Gentle4UpRightC", "5F4123"),
    (65, "Gentle4UpRightD", "5F4123"),
    (66, "Gentle4UpLeftA", "5F4123"),
    (67, "Gentle4UpLeftB", "5F4123"),
    (68, "Gentle4UpLeftC", "5F4123"),
    (69, "Gentle4UpLeftD", "5F4123"),
    (70, "Breakable", "A08C50"),
    (71, "DamageTile", "962896"),
    (72, "KnockbackTile", "2878C8"),
    (73, "SpeedBoostTile", "28C850"),
    (74, "FloatTile", "B4B4FF"),
    (75, "Gentle4CeilRightA", "412D16"),
    (76, "Gentle4CeilRightB", "412D16"),
    (77, "Gentle4CeilRightC", "412D16"),
    (78, "Gentle4CeilRightD", "412D16"),
    (79, "Gentle4CeilLeftA", "412D16"),
    (80, "Gentle4CeilLeftB", "412D16"),
    (81, "Gentle4CeilLeftC", "412D16"),
    (82, "Gentle4CeilLeftD", "412D16"),
    (83, "DamageNoKBTile", "781414"),
    (84, "DamageFloorTile", "640F0F"),
    (86, "Fire", "FF6414"),
    (87, "BrokenPipe", "505A64"),
    (88, "ElectricShock", "FFFF3C"),
    (89, "Puddle", "3C78A0"),
    (90, "Water", "1E5AB4"),
    (91, "Lava", "C83C14"),
    (92, "Acid", "3CC828"),
    (93, "BreakableGlass", "B4DCF0"),
    (94, "BreakableBattery", "DCC83C"),
    (95, "MetalFloor", "8C96A5"),
    (96, "MetalWall", "646E7D"),
    (101, "DirtBg", "32210C"),
    (102, "StoneBg", "3C3C3C"),
    (103, "GrassBg", "264C00"),
    (104, "WoodBg", "452D15"),
    (105, "SandBg", "615940"),
    (110, "RetractSpikesLeft", "C8781E"),
    (111, "RetractSpikesRight", "C8781E"),
    (112, "RetractHalfSpikesUp", "B46E19"),
    (113, "RetractHalfSpikesDown", "B46E19"),
    (114, "RetractHalfSpikesLeft", "B46E19"),
    (115, "RetractHalfSpikesRight", "B46E19"),
]


_uid_counter = 100
def uid():
    """Generate a unique sequential integer ID for LDtk."""
    global _uid_counter
    _uid_counter += 1
    return _uid_counter


FIELD_TYPE_MAP = {
    "String": "F_String",
    "Int": "F_Int",
    "Float": "F_Float",
    "Bool": "F_Bool",
}


def make_field_def(identifier, field_type, default_val=None, can_be_null=True, is_array=False):
    """Create an LDtk field definition."""
    ldtk_type = FIELD_TYPE_MAP.get(field_type, field_type)
    fd = {
        "identifier": identifier,
        "doc": None,
        "uid": uid(),
        "__type": f"Array<{field_type}>" if is_array else field_type,
        "type": ldtk_type if not is_array else ldtk_type,
        "isArray": is_array,
        "canBeNull": can_be_null,
        "arrayMinLength": None,
        "arrayMaxLength": None,
        "editorDisplayMode": "Hidden",
        "editorDisplayScale": 1,
        "editorDisplayPos": "Above",
        "editorLinkStyle": "StraightArrow",
        "editorDisplayColor": None,
        "editorAlwaysShow": False,
        "editorShowInWorld": True,
        "editorCutLongValues": True,
        "editorTextSuffix": None,
        "editorTextPrefix": None,
        "useForSmartColor": False,
        "exportToToc": False,
        "searchable": False,
        "min": None,
        "max": None,
        "regex": None,
        "acceptFileTypes": None,
        "defaultOverride": None,
        "textLanguageMode": None,
        "symmetricalRef": False,
        "autoChainRef": True,
        "allowOutOfLevelRef": True,
        "allowedRefs": "Any",
        "allowedRefsEntityUid": None,
        "allowedRefTags": [],
        "tilesetUid": None,
    }
    if default_val is not None:
        fd["defaultOverride"] = {"id": "V_String", "params": [str(default_val)]} if isinstance(default_val, str) else \
                                {"id": "V_Int", "params": [default_val]} if isinstance(default_val, int) else \
                                {"id": "V_Float", "params": [default_val]} if isinstance(default_val, float) else \
                                {"id": "V_Bool", "params": [default_val]} if isinstance(default_val, bool) else None
    return fd


def make_entity_def(name, color, width, height, fields):
    return {
        "identifier": name,
        "uid": uid(),
        "tags": [],
        "exportToToc": False,
        "allowOutOfBounds": False,
        "doc": None,
        "width": width,
        "height": height,
        "resizableX": False,
        "resizableY": False,
        "minWidth": None,
        "maxWidth": None,
        "minHeight": None,
        "maxHeight": None,
        "keepAspectRatio": False,
        "tileOpacity": 1,
        "fillOpacity": 0.08,
        "lineOpacity": 0.64,
        "hollow": False,
        "color": color,
        "renderMode": "Rectangle",
        "showName": True,
        "tilesetId": None,
        "tileRenderMode": "FitInside",
        "tileRect": None,
        "uiTileRect": None,
        "nineSliceBorders": [],
        "maxCount": 0,
        "limitScope": "PerLevel",
        "limitBehavior": "MoveLastOne",
        "pivotX": 0,
        "pivotY": 0,
        "fieldDefs": fields,
    }


def build_entity_defs():
    return [
        make_entity_def("PlayerSpawn", "#00FF00", 24, 48, []),
        make_entity_def("Enemy", "#FF0000", 32, 32, [
            make_field_def("id", "String"),
            make_field_def("type", "String"),
            make_field_def("count", "Int", 1),
            make_field_def("scale", "Float", 1.0),
            make_field_def("scaleX", "Float", 0.0),
            make_field_def("scaleY", "Float", 0.0),
            make_field_def("frozen", "Bool", False),
            make_field_def("passive", "Bool", False),
        ]),
        make_entity_def("Exit", "#0088FF", 64, 96, [
            make_field_def("id", "String"),
            make_field_def("targetLevel", "String"),
            make_field_def("targetExitId", "String"),
            make_field_def("w", "Int", 64),
            make_field_def("h", "Int", 96),
        ]),
        make_entity_def("Npc", "#AA00FF", 24, 48, [
            make_field_def("id", "String"),
            make_field_def("name", "String", "NPC"),
            make_field_def("w", "Int", 24),
            make_field_def("h", "Int", 48),
            make_field_def("color", "String", "Purple"),
            make_field_def("dialogue", "String", is_array=True),
            make_field_def("dialogueSpeakers", "String", is_array=True),
        ]),
        make_entity_def("Item", "#FFFF00", 20, 20, [
            make_field_def("id", "String"),
            make_field_def("type", "String"),
            make_field_def("w", "Int", 20),
            make_field_def("h", "Int", 20),
        ]),
        make_entity_def("Switch", "#FF8800", 16, 24, [
            make_field_def("id", "String"),
            make_field_def("action", "String"),
            make_field_def("label", "String"),
            make_field_def("w", "Int", 16),
            make_field_def("h", "Int", 24),
        ]),
        make_entity_def("Shelter", "#00FFAA", 32, 48, [
            make_field_def("id", "String"),
            make_field_def("name", "String", "Shelter"),
        ]),
        make_entity_def("EnvObject", "#888888", 40, 80, [
            make_field_def("id", "String"),
            make_field_def("type", "String"),
            make_field_def("w", "Int", 40),
            make_field_def("h", "Int", 80),
        ]),
        make_entity_def("Label", "#FFFFFF", 32, 16, [
            make_field_def("text", "String"),
            make_field_def("color", "String", "White"),
            make_field_def("size", "String", "small"),
        ]),
    ]


def build_layer_defs(entity_defs):
    entities_uid = uid()
    collision_uid = uid()

    intgrid_values = []
    for val, name, color in INTGRID_VALUES:
        intgrid_values.append({
            "value": val,
            "identifier": name,
            "color": f"#{color}",
            "tile": None,
            "groupUid": 0,
        })

    return [
        {
            "identifier": "Entities",
            "type": "Entities",
            "uid": entities_uid,
            "__type": "Entities",
            "doc": None,
            "uiColor": None,
            "gridSize": GRID_SIZE,
            "guideGridWid": 0,
            "guideGridHei": 0,
            "displayOpacity": 1,
            "inactiveOpacity": 0.6,
            "hideInList": False,
            "hideFieldsWhenInactive": True,
            "canSelectWhenInactive": True,
            "renderInWorldView": True,
            "pxOffsetX": 0,
            "pxOffsetY": 0,
            "parallaxFactorX": 0,
            "parallaxFactorY": 0,
            "parallaxScaling": True,
            "requiredTags": [],
            "excludedTags": [],
            "autoTilesKilledByOtherLayerUid": None,
            "uiFilterTags": [],
            "useAsyncRender": False,
            "intGridValues": [],
            "intGridValuesGroups": [],
            "autoRuleGroups": [],
            "autoSourceLayerDefUid": None,
            "tilesetDefUid": None,
            "tilePivotX": 0,
            "tilePivotY": 0,
            "biomeFieldUid": None,
        },
        {
            "identifier": "Collision",
            "type": "IntGrid",
            "uid": collision_uid,
            "__type": "IntGrid",
            "doc": None,
            "uiColor": None,
            "gridSize": GRID_SIZE,
            "guideGridWid": 0,
            "guideGridHei": 0,
            "displayOpacity": 1,
            "inactiveOpacity": 0.6,
            "hideInList": False,
            "hideFieldsWhenInactive": False,
            "canSelectWhenInactive": True,
            "renderInWorldView": True,
            "pxOffsetX": 0,
            "pxOffsetY": 0,
            "parallaxFactorX": 0,
            "parallaxFactorY": 0,
            "parallaxScaling": True,
            "requiredTags": [],
            "excludedTags": [],
            "autoTilesKilledByOtherLayerUid": None,
            "uiFilterTags": [],
            "useAsyncRender": False,
            "intGridValues": intgrid_values,
            "intGridValuesGroups": [],
            "autoRuleGroups": [],
            "autoSourceLayerDefUid": None,
            "tilesetDefUid": None,
            "tilePivotX": 0,
            "tilePivotY": 0,
            "biomeFieldUid": None,
        },
    ], entities_uid, collision_uid


def make_field_instance(identifier, ftype, value):
    return {
        "__identifier": identifier,
        "__type": ftype,
        "__value": value,
        "__tile": None,
        "defUid": 0,
        "realEditorValues": [],
    }


def convert_entity(ent_type, data, entity_defs_map):
    """Convert a game entity dict into an LDtk entity instance."""
    x = int(data.get("x", 0))
    y = int(data.get("y", 0))
    grid_x = x // GRID_SIZE
    grid_y = y // GRID_SIZE

    fields = []

    if ent_type == "Enemy":
        for key, ft, default in [
            ("id", "String", ""), ("type", "String", ""),
            ("count", "Int", 1), ("scale", "Float", 1.0),
            ("scaleX", "Float", 0.0), ("scaleY", "Float", 0.0),
            ("frozen", "Bool", False), ("passive", "Bool", False),
        ]:
            val = data.get(key, default)
            fields.append(make_field_instance(key, ft, val))

    elif ent_type == "Exit":
        for key, ft, default in [
            ("id", "String", ""), ("targetLevel", "String", ""),
            ("targetExitId", "String", ""),
            ("w", "Int", 64), ("h", "Int", 96),
        ]:
            fields.append(make_field_instance(key, ft, data.get(key, default)))

    elif ent_type == "Npc":
        for key, ft, default in [
            ("id", "String", ""), ("name", "String", "NPC"),
            ("w", "Int", 24), ("h", "Int", 48),
            ("color", "String", "Purple"),
        ]:
            fields.append(make_field_instance(key, ft, data.get(key, default)))
        fields.append(make_field_instance("dialogue", "Array<String>", data.get("dialogue", [])))
        fields.append(make_field_instance("dialogueSpeakers", "Array<String>", data.get("dialogueSpeakers", [])))

    elif ent_type == "Item":
        for key, ft, default in [
            ("id", "String", ""), ("type", "String", ""),
            ("w", "Int", 20), ("h", "Int", 20),
        ]:
            fields.append(make_field_instance(key, ft, data.get(key, default)))

    elif ent_type == "Switch":
        for key, ft, default in [
            ("id", "String", ""), ("action", "String", ""),
            ("label", "String", ""), ("w", "Int", 16), ("h", "Int", 24),
        ]:
            fields.append(make_field_instance(key, ft, data.get(key, default)))

    elif ent_type == "Shelter":
        for key, ft, default in [("id", "String", ""), ("name", "String", "Shelter")]:
            fields.append(make_field_instance(key, ft, data.get(key, default)))

    elif ent_type == "EnvObject":
        for key, ft, default in [
            ("id", "String", ""), ("type", "String", ""),
            ("w", "Int", 40), ("h", "Int", 80),
        ]:
            fields.append(make_field_instance(key, ft, data.get(key, default)))

    elif ent_type == "Label":
        for key, ft, default in [
            ("text", "String", ""), ("color", "String", "White"),
            ("size", "String", "small"),
        ]:
            fields.append(make_field_instance(key, ft, data.get(key, default)))

    return {
        "__identifier": ent_type,
        "__grid": [grid_x, grid_y],
        "__pivot": [0, 0],
        "__tags": [],
        "__tile": None,
        "__smartColor": "#FF0000",
        "__worldX": x,
        "__worldY": y,
        "iid": str(uuid.uuid4()),
        "width": entity_defs_map.get(ent_type, {}).get("width", 32),
        "height": entity_defs_map.get(ent_type, {}).get("height", 32),
        "defUid": entity_defs_map.get(ent_type, {}).get("uid", 0),
        "px": [x, y],
        "fieldInstances": fields,
    }


def convert_level(name, data, layer_defs, entities_layer_uid, collision_layer_uid, entity_defs_map, world_x=0, world_y=0):
    """Convert a single game level to LDtk level format."""
    bounds = data.get("bounds", {})
    left = bounds.get("left", 0)
    right = bounds.get("right", 800)
    top = bounds.get("top", 0)
    bottom = bounds.get("bottom", 600)
    px_wid = right - left
    px_hei = bottom - top

    tg = data.get("tileGrid")
    if tg:
        c_wid = tg["width"]
        c_hei = tg["height"]
        intgrid_csv = list(tg.get("tiles", []))
    else:
        c_wid = px_wid // GRID_SIZE
        c_hei = px_hei // GRID_SIZE
        intgrid_csv = [0] * (c_wid * c_hei)

    # Build entity instances
    entity_instances = []

    # Player spawn
    ps = data.get("playerSpawn", {})
    if ps:
        entity_instances.append(convert_entity("PlayerSpawn", ps, entity_defs_map))

    for enemy in data.get("enemies", []):
        entity_instances.append(convert_entity("Enemy", enemy, entity_defs_map))
    for exit_d in data.get("exits", []):
        entity_instances.append(convert_entity("Exit", exit_d, entity_defs_map))
    for npc in data.get("npcs", []):
        entity_instances.append(convert_entity("Npc", npc, entity_defs_map))
    for item in data.get("items", []):
        entity_instances.append(convert_entity("Item", item, entity_defs_map))
    for sw in data.get("switches", []):
        entity_instances.append(convert_entity("Switch", sw, entity_defs_map))
    for sh in data.get("shelters", []):
        entity_instances.append(convert_entity("Shelter", sh, entity_defs_map))
    for obj in data.get("objects", []):
        entity_instances.append(convert_entity("EnvObject", obj, entity_defs_map))
    for lbl in data.get("labels", []):
        entity_instances.append(convert_entity("Label", lbl, entity_defs_map))

    # Level custom fields
    floor = data.get("floor", {})
    neighbors = data.get("neighbors", {})

    # Resolve neighbor strings (handle both simple string and zone-based)
    def resolve_neighbor(val):
        if isinstance(val, str):
            return val
        if isinstance(val, list) and len(val) > 0:
            return val[0].get("target", "")
        return ""

    # For NeighborData, the JSON may have both "left" and "LeftZones" etc.
    neighbor_left = resolve_neighbor(neighbors.get("left", ""))
    neighbor_right = resolve_neighbor(neighbors.get("right", ""))
    neighbor_up = resolve_neighbor(neighbors.get("up", ""))
    neighbor_down = resolve_neighbor(neighbors.get("down", ""))

    field_instances = [
        make_field_instance("floorY", "Int", floor.get("y", px_hei)),
        make_field_instance("floorHeight", "Int", floor.get("height", 0)),
        make_field_instance("isUnderground", "Bool", data.get("isUnderground", False)),
        make_field_instance("neighborLeft", "String", neighbor_left),
        make_field_instance("neighborRight", "String", neighbor_right),
        make_field_instance("neighborUp", "String", neighbor_up),
        make_field_instance("neighborDown", "String", neighbor_down),
    ]

    layer_instances = [
        {
            "__identifier": "Entities",
            "__type": "Entities",
            "__cWid": c_wid,
            "__cHei": c_hei,
            "__gridSize": GRID_SIZE,
            "__opacity": 1,
            "__pxTotalOffsetX": 0,
            "__pxTotalOffsetY": 0,
            "__tilesetDefUid": None,
            "__tilesetRelPath": None,
            "iid": str(uuid.uuid4()),
            "levelId": 0,  # filled later
            "layerDefUid": entities_layer_uid,
            "pxOffsetX": 0,
            "pxOffsetY": 0,
            "visible": True,
            "optionalRules": [],
            "intGridCsv": [],
            "autoLayerTiles": [],
            "seed": 0,
            "overrideTilesetUid": None,
            "gridTiles": [],
            "entityInstances": entity_instances,
        },
        {
            "__identifier": "Collision",
            "__type": "IntGrid",
            "__cWid": c_wid,
            "__cHei": c_hei,
            "__gridSize": GRID_SIZE,
            "__opacity": 1,
            "__pxTotalOffsetX": 0,
            "__pxTotalOffsetY": 0,
            "__tilesetDefUid": None,
            "__tilesetRelPath": None,
            "iid": str(uuid.uuid4()),
            "levelId": 0,
            "layerDefUid": collision_layer_uid,
            "pxOffsetX": 0,
            "pxOffsetY": 0,
            "visible": True,
            "optionalRules": [],
            "intGridCsv": intgrid_csv,
            "autoLayerTiles": [],
            "seed": 0,
            "overrideTilesetUid": None,
            "gridTiles": [],
            "entityInstances": [],
        },
    ]

    level_uid = uid()
    for li in layer_instances:
        li["levelId"] = level_uid

    return {
        "identifier": name,
        "iid": str(uuid.uuid4()),
        "uid": level_uid,
        "worldX": world_x,
        "worldY": world_y,
        "worldDepth": 0,
        "pxWid": px_wid,
        "pxHei": px_hei,
        "__bgColor": "#40465B",
        "bgColor": None,
        "useAutoIdentifier": False,
        "bgRelPath": None,
        "bgPos": None,
        "bgPivotX": 0.5,
        "bgPivotY": 0.5,
        "__smartColor": "#ADADB5",
        "__bgPos": None,
        "externalRelPath": None,
        "fieldInstances": field_instances,
        "layerInstances": layer_instances,
        "__neighbours": [],
    }


def layout_levels(level_data_map):
    """Position levels in a GridVania layout based on neighbor relationships."""
    if not level_data_map:
        return {}

    positions = {}
    placed = set()

    # Start with first level at (0,0)
    first = next(iter(level_data_map))
    queue = [(first, 0, 0)]

    while queue:
        name, gx, gy = queue.pop(0)
        if name in placed:
            continue
        if name not in level_data_map:
            continue
        placed.add(name)

        data = level_data_map[name]
        bounds = data.get("bounds", {})
        px_wid = bounds.get("right", 800) - bounds.get("left", 0)
        px_hei = bounds.get("bottom", 600) - bounds.get("top", 0)

        positions[name] = (gx, gy, px_wid, px_hei)

        neighbors = data.get("neighbors", {})

        def resolve(val):
            if isinstance(val, str) and val:
                return val
            if isinstance(val, list) and val:
                return val[0].get("target", "")
            return ""

        r = resolve(neighbors.get("right", ""))
        l = resolve(neighbors.get("left", ""))
        u = resolve(neighbors.get("up", ""))
        d = resolve(neighbors.get("down", ""))

        if r and r not in placed:
            queue.append((r, gx + px_wid, gy))
        if l and l not in placed:
            # Get left neighbor's width
            lb = level_data_map.get(l, {}).get("bounds", {})
            lw = lb.get("right", 800) - lb.get("left", 0)
            queue.append((l, gx - lw, gy))
        if d and d not in placed:
            queue.append((d, gx, gy + px_hei))
        if u and u not in placed:
            ub = level_data_map.get(u, {}).get("bounds", {})
            uh = ub.get("bottom", 600) - ub.get("top", 0)
            queue.append((u, gx, gy - uh))

    # Place any unplaced levels
    max_x = max((p[0] + p[2] for p in positions.values()), default=0)
    for name in level_data_map:
        if name not in positions:
            bounds = level_data_map[name].get("bounds", {})
            px_wid = bounds.get("right", 800) - bounds.get("left", 0)
            px_hei = bounds.get("bottom", 600) - bounds.get("top", 0)
            positions[name] = (max_x + 256, 0, px_wid, px_hei)
            max_x += px_wid + 256

    return positions


def main():
    script_dir = Path(__file__).parent
    project_dir = script_dir.parent
    levels_dir = project_dir / "Content" / "levels"

    if not levels_dir.exists():
        print(f"Error: {levels_dir} not found")
        sys.exit(1)

    # Load all level JSONs
    level_data_map = {}
    for f in sorted(levels_dir.glob("*.json")):
        try:
            with open(f) as fp:
                data = json.load(fp)
            # Skip if it looks like an LDtk file
            if "jsonVersion" in data:
                continue
            # Use file stem as identifier (neighbor refs use file stems)
            name = f.stem
            level_data_map[name] = data
            print(f"  Loaded: {f.name} -> {name}")
        except Exception as e:
            print(f"  Warning: skipping {f.name}: {e}")

    if not level_data_map:
        print("No levels found!")
        sys.exit(1)

    print(f"\nLoaded {len(level_data_map)} levels")

    # Build definitions
    entity_defs = build_entity_defs()
    entity_defs_map = {ed["identifier"]: ed for ed in entity_defs}
    layer_defs, entities_uid, collision_uid = build_layer_defs(entity_defs)

    # Layout levels
    positions = layout_levels(level_data_map)

    # Convert each level
    ldtk_levels = []
    for name, data in level_data_map.items():
        wx, wy, _, _ = positions[name]
        ldtk_level = convert_level(name, data, layer_defs, entities_uid, collision_uid, entity_defs_map, wx, wy)
        ldtk_levels.append(ldtk_level)
        print(f"  Converted: {name} at ({wx}, {wy})")

    # Build LDtk project
    level_fields_defs = [
        make_field_def("floorY", "Int", 0),
        make_field_def("floorHeight", "Int", 0),
        make_field_def("isUnderground", "Bool", False),
        make_field_def("neighborLeft", "String", ""),
        make_field_def("neighborRight", "String", ""),
        make_field_def("neighborUp", "String", ""),
        make_field_def("neighborDown", "String", ""),
    ]

    dummy_world_iid = str(uuid.uuid4())

    ldtk_project = {
        "__header__": {
            "fileType": "LDtk Project JSON",
            "app": "LDtk",
            "doc": "https://ldtk.io/json",
            "schema": "https://ldtk.io/files/JSON_SCHEMA.json",
            "appAuthor": "Sebastien 'deepnight' Benard",
            "appVersion": LDTK_VERSION,
            "url": "https://ldtk.io"
        },
        "iid": str(uuid.uuid4()),
        "jsonVersion": LDTK_VERSION,
        "appBuildId": 473702,
        "nextUid": uid(),
        "identifierStyle": "Capitalize",
        "toc": [],
        "worldLayout": "GridVania",
        "worldGridWidth": 256,
        "worldGridHeight": 256,
        "defaultLevelWidth": 256,
        "defaultLevelHeight": 256,
        "defaultGridSize": GRID_SIZE,
        "defaultEntityWidth": 16,
        "defaultEntityHeight": 16,
        "defaultPivotX": 0,
        "defaultPivotY": 0,
        "bgColor": "#40465B",
        "defaultLevelBgColor": "#696A79",
        "minifyJson": False,
        "externalLevels": False,
        "exportTiled": False,
        "simplifiedExport": False,
        "imageExportMode": "None",
        "exportLevelBg": True,
        "exportLevelBg": True,
        "pngFilePattern": None,
        "backupOnSave": False,
        "backupLimit": 10,
        "backupRelPath": None,
        "levelNamePattern": "Level_%idx",
        "tutorialDesc": None,
        "customCommands": [],
        "flags": [],
        "dummyWorldIid": dummy_world_iid,
        "defs": {
            "layers": layer_defs,
            "entities": entity_defs,
            "tilesets": [],
            "enums": [],
            "externalEnums": [],
            "levelFields": level_fields_defs,
        },
        "levels": ldtk_levels,
        "worlds": [],
    }

    output_path = levels_dir / "genesys.ldtk"
    with open(output_path, "w") as fp:
        json.dump(ldtk_project, fp, indent=2)

    print(f"\nWrote {output_path} ({os.path.getsize(output_path)} bytes)")
    print(f"Levels: {len(ldtk_levels)}")


if __name__ == "__main__":
    main()
