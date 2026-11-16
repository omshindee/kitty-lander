"""
Kitty Lander - level layouts.

Each level is described in world units. This script writes
Assets/Editor/LevelData/levels.json, which the Unity menu item
"Kitty Lander > Rebuild Levels" turns into scenes (Assets/Scenes/Levels/).

Level 1 ("First Flight") is the original hand-made SampleScene and is not
listed here; these are levels 2..N.

Run:  python Tools/level_design.py [--preview OUT_DIR]

Size reference: the lander is about 3.1 wide x 2.5 tall, its centre sits
~1.85 above the ground when standing on a landing pad, and the pad is ~3 wide.
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_JSON = os.path.join(ROOT, "Assets", "Editor", "LevelData", "levels.json")


def ground(surface, bottom):
    """Solid ground below a left-to-right surface line."""
    return surface + [(surface[-1][0], bottom), (surface[0][0], bottom)]


def ceiling(underside, top):
    """Solid rock above a left-to-right underside line."""
    return underside + [(underside[-1][0], top), (underside[0][0], top)]


def hz(kind, x, y, scale=1.0, rot=0.0, width=1, mx=0.0, my=0.0, period=4.0, phase=0.0, spin=0.0):
    return dict(type=kind, x=x, y=y, scale=scale, rot=rot, width=width, mx=mx, my=my,
                period=period, phase=phase, spin=spin)


LEVELS = []

# ---------------------------------------------------------------- MEADOW
LEVELS.append(dict(
    name="Meadow Hop", theme="Meadow", ortho=7.5, fuel=30, gravity=0.7,
    hint="Collect every fish to open the landing pad.",
    bounds=(-18, -11, 18, 11), start=(-13, 4),
    terrain=[ground([(-22, 14), (-17, 14), (-17, -3), (-13, -3), (-10, -4.5), (-7, -4), (-4, -1.5), (-1, -1),
                     (1, -2), (3, -5), (6, -6.5), (14, -6.5), (16, -4), (17, -2), (17, 14), (22, 14)], -16)],
    fish=[(-9, 0.5), (-1, 2.8), (4.5, -2.2)],
    food=[(0, 7)], milk=[],
    pad=(10.5, -6.5),
    hazards=[],
))

LEVELS.append(dict(
    name="Pillar Perch", theme="Meadow", ortho=8, fuel=32, gravity=0.7,
    hint="The pad is on top of the pillar. Gentle now!",
    bounds=(-20, -10, 20, 13), start=(-14, 6),
    terrain=[ground([(-24, 16), (-19, 16), (-19, -6), (-12, -6), (-9, -7), (-4, -7), (-3, 1), (3, 1), (4, -7),
                     (9, -7), (12, -6), (19, -6), (19, 16), (24, 16)], -14)],
    fish=[(-12, -2.5), (12, -2.5), (0, 8)],
    food=[(14, 7)], milk=[],
    pad=(0, 1),
    hazards=[],
))

LEVELS.append(dict(
    name="Tunnel Toast", theme="Meadow", ortho=8, fuel=34, gravity=0.7,
    hint="Some fish are hiding in the tunnel. Mind the ceiling!",
    bounds=(-22, -10, 22, 12), start=(-17, 3),
    terrain=[
        ground([(-26, 15), (-21, 15), (-21, -1), (-13, -1), (-11, -6), (9, -6), (11, -4), (21, -4), (21, 15), (26, 15)], -14),
        [(-7, 0.5), (-3, -0.3), (2, 0.3), (7, -0.3), (11, 1), (10, 4), (4, 4.6), (-3, 4.2), (-7, 3)],
    ],
    fish=[(-3, -3.5), (5, -3.5), (2, 8.5)],
    food=[(-15, 7)], milk=[],
    pad=(16, -4),
    hazards=[],
))

# ---------------------------------------------------------------- DESERT
LEVELS.append(dict(
    name="Cactus Canyon", theme="Desert", ortho=8, fuel=34, gravity=0.7,
    hint="Cacti are prickly. Don't touch them!",
    bounds=(-22, -10, 22, 12), start=(-17, 3),
    terrain=[ground([(-26, 15), (-21, 15), (-21, -1), (-13, -1), (-11, -7), (9, -7), (11, -1), (21, -1),
                     (21, 15), (26, 15)], -14)],
    fish=[(-8.5, -4.5), (-1, -4.5), (6.8, -4.5)],
    food=[(-1, 6)], milk=[],
    pad=(16, -1),
    hazards=[hz("Cactus", -5, -5.6), hz("Cactus", 3, -5.6), hz("Cactus", -14.2, 0.2, scale=0.8)],
))

LEVELS.append(dict(
    name="Yarn Storm", theme="Desert", ortho=8, fuel=36, gravity=0.7,
    hint="Giant yarn balls bounce around. Time your moves!",
    bounds=(-22, -10, 22, 13), start=(-17, 4),
    terrain=[ground([(-26, 16), (-21, 16), (-21, -4), (-15, -4), (-12, -6), (-7, -5), (-3, -7), (1, -7), (5, -5),
                     (9, -6), (12, -5), (21, -5), (21, 16), (26, 16)], -14)],
    fish=[(-10, 0), (-1, -4), (8, 1), (-2, 9)],
    food=[(-14, 9)], milk=[],
    pad=(16.5, -5),
    hazards=[
        hz("YarnBall", -6, 2, scale=1.2, my=4, period=4, spin=90),
        hz("YarnBall", 4, 4, scale=1.2, my=4.5, period=5, phase=0.5, spin=-90),
        hz("YarnBall", 10, 7, scale=1.0, mx=4, period=6, spin=120),
    ],
))

LEVELS.append(dict(
    name="Sandstone Shafts", theme="Desert", ortho=9, fuel=40, gravity=0.7,
    hint="Zig-zag down the shafts and keep your speed low.",
    bounds=(-16, -26, 16, 10), start=(-9, 6),
    terrain=[[
        (-21, 14), (-15, 14), (-15, 1), (4, 1), (5, 0), (4, -1), (-15, -1), (-15, -20), (-8, -20), (-5, -21),
        (2, -21), (5, -20), (15, -20), (15, -11), (-4, -11), (-5, -10), (-4, -9), (15, -9), (15, 14), (21, 14),
        (21, -30), (-21, -30),
    ]],
    fish=[(10, 4), (10, -4), (-10, -5), (-10, -15), (0, -17)],
    food=[(0, -5)], milk=[],
    pad=(10, -20),
    hazards=[hz("Spikes", -12, -19.5, width=2), hz("Spikes", 2, -8.5, width=2)],
))

# ---------------------------------------------------------------- SNOW
LEVELS.append(dict(
    name="Icicle Grotto", theme="Snow", ortho=8, fuel=36, gravity=0.7,
    hint="Icicles hang low in the grotto. Fly under them carefully.",
    bounds=(-22, -10, 22, 10), start=(-17, 0),
    terrain=[
        ground([(-26, 14), (-21, 14), (-21, -6), (-14, -6), (-11, -4.5), (-8, -6), (-2, -7), (3, -7), (7, -5.5),
                (11, -6), (14, -6), (21, -6), (21, 14), (26, 14)], -14),
        ceiling([(-21, 5), (-15, 4), (-8, 5), (0, 3.5), (8, 5), (15, 4), (21, 5)], 14),
    ],
    fish=[(-14, -2), (-4, -3.5), (4, -3), (10.5, -2.5)],
    food=[(0, -5)], milk=[],
    pad=(17.5, -6),
    hazards=[
        hz("Icicle", -11, 3.3, scale=1.4), hz("Icicle", -4, 3.0, scale=1.4),
        hz("Icicle", 0.2, 1.4, scale=1.4, my=0.9, period=3),
        hz("Icicle", 4, 3.0, scale=1.4), hz("Icicle", 11, 3.3, scale=1.4),
    ],
))

LEVELS.append(dict(
    name="Frosty Floaters", theme="Snow", ortho=8.5, fuel=42, gravity=0.7,
    hint="The pad sits on a floating iceberg. Avoid the spikes below!",
    bounds=(-24, -12, 24, 14), start=(-18, -3),
    terrain=[
        ground([(-28, 17), (-23, 17), (-23, -8), (-16, -8), (-12, -9), (8, -9), (12, -8), (23, -8), (23, 17), (28, 17)], -16),
        [(-14, -1.5), (-13, -1), (-5, -1), (-4, -1.5), (-5, -3), (-9, -3.6), (-13, -3)],
        [(5, 3.5), (6, 4), (13, 4), (14, 3.5), (13, 2), (9.5, 1.4), (6, 2)],
        [(-6, 8.5), (-5, 9), (1, 9), (2, 8.5), (1, 7.3), (-2, 6.8), (-5, 7.3)],
    ],
    fish=[(-9, -6), (-2, 12), (18, -4), (-18, 7), (9.5, 8.5)],
    food=[(18, 8)], milk=[],
    pad=(9.5, 4),
    hazards=[
        hz("Spikes", -2, -8.5, width=4),
        hz("YarnBall", -12, 5, scale=1.1, mx=5, period=5, spin=100),
    ],
))

# ---------------------------------------------------------------- MOON
LEVELS.append(dict(
    name="Asteroid Alley", theme="Moon", ortho=8.5, fuel=40, gravity=0.45,
    hint="Low gravity on the moon! Dodge the drifting asteroids.",
    bounds=(-24, -10, 24, 14), start=(-19, 6),
    terrain=[ground([(-28, 17), (-23, 17), (-23, -4), (-18, -4), (-15, -6), (-12, -5), (-8, -7), (-4, -7), (-1, -5),
                     (2, -7), (6, -7), (9, -4), (13, -4), (16, -6), (23, -6), (23, 17), (28, 17)], -14)],
    fish=[(-15, 0), (-6, -3.5), (0, 0), (4, -3.5), (4, 10), (12, 1)],
    food=[], milk=[(-2, 11)],
    pad=(19.5, -6),
    hazards=[
        hz("Asteroid", -10, 2, scale=1.3, my=3.5, period=5, spin=40),
        hz("Asteroid", 0, 5, scale=1.3, mx=6, period=7, spin=-30),
        hz("Asteroid", 8, 0, scale=1.3, my=3.5, period=4.5, phase=0.3, spin=60),
        hz("Asteroid", 12, 8, scale=1.6, spin=20),
    ],
))

LEVELS.append(dict(
    name="Moonbase Maze", theme="Moon", ortho=9, fuel=55, gravity=0.5,
    hint="Final mission! Weave through the moonbase maze to the pad.",
    bounds=(-24, -22, 24, 14), start=(-17, 7),
    terrain=[
        [(-28, 18), (-22, 18), (-22, 2), (13, 2), (14, 1), (13, 0), (-22, 0), (-22, -18), (22, -18), (22, -10),
         (-13, -10), (-14, -9), (-13, -8), (22, -8), (22, 18), (28, 18), (28, -26), (-28, -26)],
        ceiling([(-22, 12), (-12, 11), (-2, 12), (8, 11), (22, 12)], 18),
    ],
    fish=[(-9, 6), (3, 5), (18, -4), (-6, -4), (-18, -14), (-2, -14)],
    food=[(18, 8), (-18, -4)], milk=[(12, -3)],
    pad=(17, -18),
    hazards=[
        hz("Asteroid", -2, 7, scale=1.3, my=3, period=4.5, spin=50),
        hz("Asteroid", 8, 7, scale=1.3, my=3, period=4.5, phase=0.5, spin=-50),
        hz("Asteroid", 2, -4, scale=1.3, my=2.8, period=4, spin=-60),
        hz("Asteroid", 8, -14, scale=1.3, my=2.2, period=3.5, spin=70),
        hz("Spikes", 7, -7.5, width=2),
        hz("Spikes", -2, -17.5, width=4),
    ],
))


def to_json():
    out = []
    for L in LEVELS:
        out.append(dict(
            name=L["name"], hint=L["hint"], theme=L["theme"], ortho=L["ortho"], fuel=L["fuel"], gravity=L["gravity"],
            bounds=list(L["bounds"]), start=list(L["start"]), pad=list(L["pad"]),
            fish=[c for p in L["fish"] for c in p],
            food=[c for p in L["food"] for c in p],
            milk=[c for p in L["milk"] for c in p],
            terrain=[dict(pts=[c for p in poly for c in p]) for poly in L["terrain"]],
            hazards=L["hazards"],
        ))
    os.makedirs(os.path.dirname(OUT_JSON), exist_ok=True)
    with open(OUT_JSON, "w") as f:
        json.dump(dict(levels=out), f, indent=1)
    print(f"Wrote {len(out)} levels to {OUT_JSON}")


def preview(out_dir):
    from PIL import Image, ImageDraw
    os.makedirs(out_dir, exist_ok=True)
    S = 20  # pixels per unit
    for i, L in enumerate(LEVELS):
        x0, y0, x1, y1 = L["bounds"]
        W, H = int((x1 - x0 + 4) * S), int((y1 - y0 + 4) * S)
        img = Image.new("RGB", (W, H), (200, 225, 245))
        d = ImageDraw.Draw(img)

        def P(x, y):
            return ((x - x0 + 2) * S, (y1 + 2 - y) * S)

        for poly in L["terrain"]:
            d.polygon([P(*p) for p in poly], fill=(140, 100, 60), outline=(40, 25, 10))
        d.rectangle([P(x0, y1), P(x1, y0)], outline=(255, 0, 0), width=2)
        for h in L["hazards"]:
            r = {"YarnBall": 1.0, "Asteroid": 0.75, "Cactus": 1.0, "Icicle": 0.5, "Spikes": 0.5}[h["type"]] * h["scale"]
            if h["type"] == "Spikes":
                w = h["width"]
                d.rectangle([P(h["x"] - w, h["y"] + 0.5), P(h["x"] + w, h["y"] - 0.5)], fill=(150, 150, 160))
                continue
            for s in (-1, 0, 1):
                cx, cy = h["x"] + h["mx"] * s, h["y"] + h["my"] * s
                ry = r * (1.5 if h["type"] in ("Cactus", "Icicle") else 1)
                d.ellipse([P(cx - r, cy + ry), P(cx + r, cy - ry)], outline=(220, 30, 60), width=2 if s == 0 else 1)
        for (fx, fy) in L["fish"]:
            d.ellipse([P(fx - 0.6, fy + 0.3), P(fx + 0.6, fy - 0.3)], fill=(80, 120, 230))
        for (fx, fy) in L["food"] + L["milk"]:
            d.rectangle([P(fx - 0.5, fy + 0.5), P(fx + 0.5, fy - 0.5)], fill=(250, 200, 40))
        px, py = L["pad"]
        d.rectangle([P(px - 1.5, py + 0.56), P(px + 1.5, py)], fill=(240, 140, 60))
        # lander outline at start and resting on pad
        for cx, cy in [L["start"], (px, py + 0.56 + 1.27)]:
            d.rectangle([P(cx - 1.62, cy + 1.19), P(cx + 1.46, cy - 1.27)], outline=(0, 140, 0), width=2)
        d.text((8, 8), f"{i + 2}: {L['name']}", fill=(0, 0, 0))
        img.save(os.path.join(out_dir, f"level_{i + 2:02d}.png"))
    print("Previews in", out_dir)


if __name__ == "__main__":
    to_json()
    if "--preview" in sys.argv:
        preview(sys.argv[sys.argv.index("--preview") + 1])
