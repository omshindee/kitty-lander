"""
Kitty Lander - procedural art generator.

Generates the cartoon-style sprites/textures used by the generated levels:
terrain fill + edge textures for each world theme, recoloured backgrounds,
hazards, pickups and UI icons. Style follows the existing hand-drawn assets:
flat fills, soft highlights and thick dark outlines.

Usage (from the project root):
    python Tools/generate_art.py

Output goes to Assets/Art/Generated/. Re-running overwrites the PNGs but keeps
the .meta files Unity created, so references stay intact.
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Generated")
SRC_BG = os.path.join(ROOT, "Assets", "Textures and Materials", "Background.png")

OUTLINE = (43, 29, 20, 255)
SS = 4  # supersampling factor for smooth anti-aliased edges


def out(*parts):
    p = os.path.join(OUT, *parts)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    return p


def finish(img, size):
    """Downsample a supersampled canvas to its final size."""
    return img.resize(size, Image.LANCZOS)


# ---------------------------------------------------------------------------
# Terrain fill: tileable cobble/rock pattern (Voronoi cells with dark seams)
# ---------------------------------------------------------------------------
def tileable_cobble(size, palette, seam, n_cells, seed, craters=0, specks=0.0):
    rng = np.random.default_rng(seed)
    pts = rng.random((n_cells, 2)) * size
    cols = [palette[i % len(palette)] for i in rng.permutation(n_cells)]

    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    best = np.full((size, size), 1e9, np.float32)
    second = np.full((size, size), 1e9, np.float32)
    idx = np.zeros((size, size), np.int32)
    for i, (px, py) in enumerate(pts):
        dx = np.abs(xx - px)
        dx = np.minimum(dx, size - dx)  # wrap -> tileable
        dy = np.abs(yy - py)
        dy = np.minimum(dy, size - dy)
        d = np.sqrt(dx * dx + dy * dy)
        closer = d < best
        second = np.where(closer, best, np.minimum(second, d))
        idx = np.where(closer, i, idx)
        best = np.where(closer, d, best)

    rgb = np.array(cols, np.float32)[idx]
    edge = second - best
    # bevel: lighten towards cell centre, darken near seams
    shade = np.clip(edge / (size / math.sqrt(n_cells) * 0.55), 0, 1)
    rgb = rgb * (0.78 + 0.32 * shade[..., None])
    # top-left highlight per stone
    rgb = np.clip(rgb, 0, 255)
    seam_mask = edge < size * 0.012
    rgb[seam_mask] = seam
    if specks:
        m = rng.random((size, size)) < specks
        rgb[m] = rgb[m] * 0.75
    img = Image.fromarray(rgb.astype(np.uint8), "RGB").convert("RGBA")

    if craters:
        d = ImageDraw.Draw(img)
        r = random.Random(seed)
        for _ in range(craters):
            cx, cy, rad = r.uniform(0, size), r.uniform(0, size), r.uniform(size * 0.03, size * 0.08)
            for ox in (-size, 0, size):
                for oy in (-size, 0, size):
                    x, y = cx + ox, cy + oy
                    d.ellipse([x - rad, y - rad, x + rad, y + rad], fill=tuple(int(c * 0.7) for c in seam[:3]) + (255,))
                    d.ellipse([x - rad * 0.8, y - rad * 0.75, x + rad * 0.8, y + rad * 0.85],
                              fill=tuple(min(255, int(c * 1.0)) for c in palette[0][:3]) + (255,))
    return img.filter(ImageFilter.SMOOTH)


# ---------------------------------------------------------------------------
# Terrain edge strip: horizontally tileable, edge line sits at 45% height
# v=0 (bottom) is inside the ground, v=1 (top) is the sky side.
# ---------------------------------------------------------------------------
def edge_strip(kind, seed):
    W, H = 512 * SS, 128 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rng = random.Random(seed)
    base_y = int(H * 0.55)  # image y (top-down) of the surface line
    lw = 5 * SS

    def wavy(y0, amp, freq, phase):
        # periodic over W so the strip tiles
        return [(x, y0 + amp * math.sin(2 * math.pi * freq * x / W + phase)) for x in range(-SS * 8, W + SS * 9, SS * 8)]

    if kind == "grass":
        dark, mid, light = (52, 120, 38, 255), (86, 168, 52, 255), (140, 205, 80, 255)
        bottom = wavy(base_y + 26 * SS, 7 * SS, 6, 0.7)
        poly = [(-SS * 8, base_y - 4 * SS)] + [(x, y) for x, y in bottom][::-1]
        poly = [(-SS * 8, base_y - 4 * SS), (W + SS * 8, base_y - 4 * SS)] + bottom[::-1]
        d.polygon(poly, fill=mid)
        d.line(bottom, fill=OUTLINE, width=lw, joint="curve")
        # blades (periodic placement)
        n = 70
        for i in range(n):
            x = (i + rng.random() * 0.8) * W / n
            h = rng.uniform(18, 40) * SS
            lean = rng.uniform(-10, 10) * SS
            w = rng.uniform(5, 9) * SS
            col = rng.choice([dark, mid, light])
            for ox in (-W, 0, W):
                tri = [(x + ox - w, base_y + 2 * SS), (x + ox + w, base_y + 2 * SS), (x + ox + lean, base_y - h)]
                d.polygon(tri, fill=col, outline=OUTLINE)
        # tiny flowers
        for i in range(6):
            x = (i + 0.5) * W / 6 + rng.uniform(-30, 30) * SS
            y = base_y - rng.uniform(12, 26) * SS
            c = rng.choice([(255, 236, 120, 255), (255, 170, 200, 255), (200, 170, 255, 255)])
            for k in range(5):
                a = k * 2 * math.pi / 5
                px, py = x + math.cos(a) * 5 * SS, y + math.sin(a) * 5 * SS
                d.ellipse([px - 4 * SS, py - 4 * SS, px + 4 * SS, py + 4 * SS], fill=c, outline=OUTLINE)
            d.ellipse([x - 3 * SS, y - 3 * SS, x + 3 * SS, y + 3 * SS], fill=(255, 200, 60, 255))
    elif kind == "sand":
        sand, light = (236, 190, 110, 255), (250, 222, 160, 255)
        bottom = wavy(base_y + 18 * SS, 5 * SS, 4, 1.3)
        top = wavy(base_y - 6 * SS, 4 * SS, 8, 0.2)
        d.polygon(top + bottom[::-1], fill=sand)
        d.line(wavy(base_y - 1 * SS, 3 * SS, 8, 0.2), fill=light, width=6 * SS)
        d.line(top, fill=OUTLINE, width=lw)
        d.line(bottom, fill=(150, 100, 50, 255), width=3 * SS)
        for _ in range(60):
            x, y = rng.uniform(0, W), rng.uniform(base_y - 2 * SS, base_y + 14 * SS)
            d.ellipse([x - 2 * SS, y - 2 * SS, x + 2 * SS, y + 2 * SS], fill=(190, 140, 80, 255))
    elif kind == "snow":
        snow, shade = (250, 252, 255, 255), (196, 222, 245, 255)
        top = wavy(base_y - 10 * SS, 5 * SS, 5, 0.4)
        # drips hanging below the surface
        bottom = []
        n = 16
        for i in range(n + 1):
            x0 = i * W / n
            depth = (16 + 14 * (0.5 + 0.5 * math.sin(i * 2.1))) * SS
            bottom.append((x0, base_y + 8 * SS))
            bottom.append((x0 + W / n * 0.5, base_y + depth))
        d.polygon(top + bottom[::-1], fill=snow)
        d.line(wavy(base_y + 2 * SS, 3 * SS, 5, 0.4), fill=shade, width=8 * SS)
        d.line(top, fill=OUTLINE, width=lw)
        d.line(bottom, fill=OUTLINE, width=3 * SS)
    elif kind == "moon":
        crust, light = (190, 178, 220, 255), (225, 218, 245, 255)
        top = wavy(base_y - 5 * SS, 3 * SS, 10, 0.9)
        bottom = wavy(base_y + 16 * SS, 6 * SS, 7, 2.0)
        d.polygon(top + bottom[::-1], fill=crust)
        d.line(wavy(base_y - 1 * SS, 2 * SS, 10, 0.9), fill=light, width=5 * SS)
        d.line(top, fill=OUTLINE, width=lw)
        d.line(bottom, fill=(90, 70, 130, 255), width=3 * SS)
        for _ in range(14):
            x, y = rng.uniform(0, W), base_y + rng.uniform(2, 10) * SS
            r = rng.uniform(3, 6) * SS
            d.ellipse([x - r, y - r * 0.6, x + r, y + r * 0.6], fill=(140, 125, 180, 255), outline=(90, 70, 130, 255))
    return finish(img, (512, 128))


# ---------------------------------------------------------------------------
# Backgrounds: recolour the original cat-skyline background
# ---------------------------------------------------------------------------
def recolor_background(src, top_col, bottom_col, sil_dark, sil_light, stars=0, planets=False, seed=0):
    im = np.asarray(Image.open(src).convert("RGBA")).astype(np.float32)
    h, w = im.shape[:2]
    lum = im[..., :3].mean(axis=2) / 255.0  # original is pale blue; silhouettes darker
    lum_n = (lum - lum.min()) / max(1e-5, lum.max() - lum.min())
    grad = np.linspace(0, 1, h, dtype=np.float32)[:, None, None]
    sky = np.array(top_col, np.float32) * (1 - grad) + np.array(bottom_col, np.float32) * grad
    sil = np.array(sil_dark, np.float32) * (1 - lum_n[..., None]) + np.array(sil_light, np.float32) * lum_n[..., None]
    t = np.clip((lum_n[..., None] - 0.55) / 0.35, 0, 1)  # bright areas -> sky gradient
    rgb = sil * (1 - t) + sky * t
    img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    d = ImageDraw.Draw(img)
    rng = random.Random(seed)
    for _ in range(stars):
        x, y = rng.uniform(0, w), rng.uniform(0, h * 0.55)
        r = rng.choice([1.5, 2, 2.5, 3.5])
        c = rng.choice([(255, 255, 255, 255), (255, 245, 200, 255), (210, 225, 255, 255)])
        if r > 3:
            d.polygon([(x, y - r * 3), (x + r * 0.7, y), (x, y + r * 3), (x - r * 0.7, y)], fill=c)
            d.polygon([(x - r * 3, y), (x, y + r * 0.7), (x + r * 3, y), (x, y - r * 0.7)], fill=c)
        else:
            d.ellipse([x - r, y - r, x + r, y + r], fill=c)
    if planets:
        for (px, py, pr, col) in [(w * 0.82, h * 0.18, 70, (255, 190, 120)), (w * 0.12, h * 0.3, 38, (150, 220, 210))]:
            d.ellipse([px - pr, py - pr, px + pr, py + pr], fill=col + (255,), outline=OUTLINE, width=6)
            d.arc([px - pr * 1.7, py - pr * 0.45, px + pr * 1.7, py + pr * 0.45], 200, 340, fill=(255, 240, 210, 255), width=7)
    return img


# ---------------------------------------------------------------------------
# Hazards / pickups / UI
# ---------------------------------------------------------------------------
def spikes():
    W, H = 256 * SS, 128 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    base = H - 18 * SS
    d.rectangle([0, base, W, H - 2 * SS], fill=(110, 110, 125, 255))
    d.line([(0, base), (W, base)], fill=OUTLINE, width=5 * SS)
    d.line([(0, H - 3 * SS), (W, H - 3 * SS)], fill=OUTLINE, width=5 * SS)
    n = 4
    for i in range(n):
        x0, x1 = i * W / n + 2 * SS, (i + 1) * W / n - 2 * SS
        xm = (x0 + x1) / 2
        d.polygon([(x0, base), (x1, base), (xm, 8 * SS)], fill=(200, 205, 215, 255))
        d.polygon([(xm, base), (x1, base), (xm, 8 * SS)], fill=(150, 155, 170, 255))
        d.line([(x0, base), (xm, 8 * SS), (x1, base)], fill=OUTLINE, width=5 * SS, joint="curve")
    return finish(img, (256, 128))


def yarn_ball():
    S = 256 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c, r = S / 2, S * 0.42
    d.ellipse([c - r, c - r, c + r, c + r], fill=(232, 82, 104, 255))
    # strands
    for k in range(7):
        a = k * math.pi / 7
        bbox = [c - r * 0.95, c - r * (0.2 + 0.11 * k), c + r * 0.95, c + r * (0.2 + 0.11 * k)]
        img2 = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        d2 = ImageDraw.Draw(img2)
        d2.arc(bbox, 0, 180, fill=(170, 40, 70, 255), width=4 * SS)
        img2 = img2.rotate(math.degrees(a) * 1.3 + 15, center=(c, c))
        img.alpha_composite(img2)
    d = ImageDraw.Draw(img)
    d.ellipse([c - r * 0.55, c - r * 0.7, c - r * 0.15, c - r * 0.35], fill=(255, 170, 185, 200))
    d.ellipse([c - r, c - r, c + r, c + r], outline=OUTLINE, width=7 * SS)
    # loose thread
    pts = [(c + r * 0.7, c + r * 0.7)]
    for i in range(1, 8):
        pts.append((c + r * 0.7 + i * 5 * SS, c + r * 0.7 + math.sin(i) * 8 * SS + i * 3 * SS))
    d.line(pts, fill=(170, 40, 70, 255), width=5 * SS, joint="curve")
    return finish(img, (256, 256))


def cactus():
    W, H = 256 * SS, 384 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    g, gl = (80, 160, 80, 255), (130, 200, 110, 255)
    lw = 7 * SS

    def capsule(x0, y0, x1, y1):
        rr = (x1 - x0) / 2
        d.rounded_rectangle([x0, y0, x1, y1], radius=rr, fill=g, outline=OUTLINE, width=lw)
        d.rounded_rectangle([x0 + rr * 0.35, y0 + rr * 0.4, x0 + rr * 0.75, y1 - rr * 0.6], radius=rr * 0.2, fill=gl)

    # arms first so the trunk outline overlaps them
    capsule(W * 0.08, H * 0.28, W * 0.30, H * 0.58)
    d.rectangle([W * 0.15, H * 0.50, W * 0.42, H * 0.58], fill=g)
    d.line([(W * 0.15, H * 0.58), (W * 0.40, H * 0.58)], fill=OUTLINE, width=lw)
    capsule(W * 0.70, H * 0.18, W * 0.92, H * 0.48)
    d.rectangle([W * 0.58, H * 0.40, W * 0.85, H * 0.48], fill=g)
    d.line([(W * 0.60, H * 0.48), (W * 0.85, H * 0.48)], fill=OUTLINE, width=lw)
    capsule(W * 0.34, H * 0.04, W * 0.66, H * 0.99)
    for y in np.linspace(H * 0.1, H * 0.92, 9):
        for x in (W * 0.42, W * 0.58):
            d.line([(x, y), (x + (x - W * 0.5) * 0.6, y - 6 * SS)], fill=(250, 245, 210, 255), width=3 * SS)
    return finish(img, (256, 384))


def asteroid(seed=3):
    S = 256 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rng = random.Random(seed)
    c = S / 2
    pts = []
    for k in range(18):
        a = k * 2 * math.pi / 18
        r = S * 0.40 * rng.uniform(0.82, 1.0)
        pts.append((c + math.cos(a) * r, c + math.sin(a) * r))
    d.polygon(pts, fill=(150, 135, 170, 255))
    inner = [(c + (x - c) * 0.8 - 10 * SS, c + (y - c) * 0.8 - 10 * SS) for x, y in pts]
    d.polygon(inner, fill=(178, 165, 198, 255))
    for (ox, oy, rr) in [(-0.15, -0.1, 0.10), (0.18, 0.12, 0.08), (-0.05, 0.22, 0.06), (0.15, -0.2, 0.05)]:
        x, y, r = c + ox * S, c + oy * S, rr * S
        d.ellipse([x - r, y - r, x + r, y + r], fill=(110, 95, 135, 255), outline=OUTLINE, width=3 * SS)
        d.ellipse([x - r * 0.7, y - r * 0.5, x + r * 0.8, y + r * 0.9], fill=(135, 120, 160, 255))
    d.line(pts + [pts[0]], fill=OUTLINE, width=7 * SS, joint="curve")
    return finish(img, (256, 256))


def icicle():
    W, H = 128 * SS, 256 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    tri = [(W * 0.08, 6 * SS), (W * 0.92, 6 * SS), (W * 0.5, H - 6 * SS)]
    d.polygon(tri, fill=(170, 220, 250, 255))
    d.polygon([(W * 0.5, 6 * SS), (W * 0.92, 6 * SS), (W * 0.5, H - 6 * SS)], fill=(130, 190, 235, 255))
    d.line([(W * 0.3, 20 * SS), (W * 0.45, H * 0.6)], fill=(240, 250, 255, 255), width=6 * SS)
    d.line(tri + [tri[0]], fill=OUTLINE, width=6 * SS, joint="curve")
    return finish(img, (128, 256))


def milk():
    W, H = 192 * SS, 256 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    lw = 6 * SS
    body = [(W * 0.18, H * 0.38), (W * 0.32, H * 0.22), (W * 0.68, H * 0.22), (W * 0.82, H * 0.38),
            (W * 0.82, H * 0.94), (W * 0.18, H * 0.94)]
    d.polygon(body, fill=(250, 250, 255, 255))
    d.rectangle([W * 0.32, H * 0.08, W * 0.68, H * 0.22], fill=(80, 150, 230, 255), outline=OUTLINE, width=lw)
    d.rectangle([W * 0.18, H * 0.52, W * 0.82, H * 0.80], fill=(90, 160, 235, 255))
    # paw print on label
    cx, cy = W * 0.5, H * 0.68
    d.ellipse([cx - 16 * SS, cy - 8 * SS, cx + 16 * SS, cy + 16 * SS], fill=(255, 255, 255, 255))
    for ox, oy in [(-18, -16), (-6, -24), (6, -24), (18, -16)]:
        d.ellipse([cx + (ox - 6) * SS, cy + (oy - 6) * SS, cx + (ox + 6) * SS, cy + (oy + 6) * SS], fill=(255, 255, 255, 255))
    d.rectangle([W * 0.24, H * 0.40, W * 0.32, H * 0.88], fill=(225, 230, 245, 255))
    d.line(body + [body[0]], fill=OUTLINE, width=lw, joint="curve")
    return finish(img, (192, 256))


def star(filled=True):
    S = 256 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = S / 2
    pts = []
    for k in range(10):
        a = -math.pi / 2 + k * math.pi / 5
        r = S * (0.46 if k % 2 == 0 else 0.20)
        pts.append((c + math.cos(a) * r, c + math.sin(a) * r + S * 0.03))
    fill = (255, 205, 50, 255) if filled else (120, 120, 135, 255)
    d.polygon(pts, fill=fill)
    if filled:
        hl = [(c + (x - c) * 0.55 - 10 * SS, c + (y - c) * 0.55 - 12 * SS) for x, y in pts]
        d.polygon(hl, fill=(255, 235, 140, 255))
    d.line(pts + [pts[0]], fill=OUTLINE, width=12 * SS, joint="curve")
    return finish(img, (256, 256))


def rounded_panel(fill, size=128, radius=36, border=8):
    S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([border * SS / 2, border * SS / 2, S - border * SS / 2, S - border * SS / 2],
                        radius=radius * SS, fill=fill, outline=OUTLINE, width=border * SS)
    return finish(img, (size, size))


def paw_icon():
    S = 128 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = S / 2
    col = (255, 255, 255, 255)
    d.ellipse([c - 30 * SS, c - 8 * SS, c + 30 * SS, c + 40 * SS], fill=col)
    for ox, oy in [(-34, -20), (-12, -40), (12, -40), (34, -20)]:
        d.ellipse([c + (ox - 13) * SS, c + (oy - 15) * SS, c + (ox + 13) * SS, c + (oy + 15) * SS], fill=col)
    return finish(img, (128, 128))


def main():
    random.seed(1)
    themes = {
        "Meadow": dict(pal=[(120, 72, 40), (138, 86, 48), (104, 62, 34), (150, 98, 58)], seam=(58, 34, 18), kind="grass"),
        "Desert": dict(pal=[(206, 140, 72), (220, 158, 88), (190, 124, 62), (230, 172, 104)], seam=(120, 70, 34), kind="sand"),
        "Snow": dict(pal=[(120, 140, 170), (136, 158, 188), (108, 126, 156), (150, 172, 200)], seam=(52, 62, 88), kind="snow"),
        "Moon": dict(pal=[(104, 88, 140), (118, 102, 156), (92, 78, 126), (132, 118, 170)], seam=(48, 36, 74), kind="moon"),
    }
    for i, (name, t) in enumerate(themes.items()):
        fill = tileable_cobble(512, t["pal"], t["seam"], 24, seed=10 + i,
                               craters=10 if name == "Moon" else 0, specks=0.02)
        fill.save(out("Terrain", f"Fill_{name}.png"))
        edge_strip(t["kind"], seed=20 + i).save(out("Terrain", f"Edge_{name}.png"))

    recolor_background(SRC_BG, (255, 196, 140), (255, 236, 200), (176, 98, 104), (236, 150, 130), seed=1) \
        .save(out("Backgrounds", "BG_Desert.png"))
    recolor_background(SRC_BG, (24, 40, 92), (88, 120, 178), (20, 30, 66), (60, 86, 140), stars=160, seed=2) \
        .save(out("Backgrounds", "BG_Snow.png"))
    recolor_background(SRC_BG, (18, 10, 40), (64, 38, 110), (30, 18, 58), (70, 46, 120), stars=320, planets=True, seed=3) \
        .save(out("Backgrounds", "BG_Moon.png"))

    spikes().save(out("Hazards", "Spikes.png"))
    yarn_ball().save(out("Hazards", "YarnBall.png"))
    cactus().save(out("Hazards", "Cactus.png"))
    asteroid().save(out("Hazards", "Asteroid.png"))
    icicle().save(out("Hazards", "Icicle.png"))

    milk().save(out("Pickups", "Milk.png"))

    star(True).save(out("UI", "Star.png"))
    star(False).save(out("UI", "StarEmpty.png"))
    rounded_panel((255, 248, 232, 255)).save(out("UI", "Panel.png"))
    rounded_panel((255, 255, 255, 255), radius=28, border=7).save(out("UI", "Button.png"))
    paw_icon().save(out("UI", "Paw.png"))
    generate_sfx()
    print("Art written to", OUT)


# ---------------------------------------------------------------------------
# Sound effects (simple synthesized 16-bit mono WAVs)
# ---------------------------------------------------------------------------
SR = 44100


def _write_wav(name, samples):
    import wave
    p = out("Audio", name)
    data = np.clip(samples, -1, 1)
    pcm = (data * 32000).astype("<i2").tobytes()
    with wave.open(p, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm)


def _env(n, attack=0.01, release=0.2):
    t = np.arange(n) / SR
    dur = n / SR
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    r = np.clip((dur - t) / max(release, 1e-4), 0, 1)
    return a * r


def _tone(freqs, dur, wave="sine", vol=0.5):
    n = int(SR * dur)
    t = np.arange(n) / SR
    f = np.interp(np.arange(n), np.linspace(0, n, len(freqs)), freqs)
    phase = 2 * np.pi * np.cumsum(f) / SR
    if wave == "square":
        s = np.sign(np.sin(phase)) * 0.6
    elif wave == "tri":
        s = 2 / np.pi * np.arcsin(np.sin(phase))
    else:
        s = np.sin(phase)
    return s * vol


def generate_sfx():
    rng = np.random.default_rng(7)
    # fish pickup: quick rising double blip
    a = _tone([660, 990], 0.08, "tri") * _env(int(SR * 0.08), 0.005, 0.04)
    b = _tone([990, 1320], 0.12, "tri") * _env(int(SR * 0.12), 0.005, 0.08)
    _write_wav("Pickup.wav", np.concatenate([a, b]))
    # fuel pickup: bubbly glug
    parts = [_tone([300 + i * 80, 500 + i * 80], 0.07, "sine", 0.6) * _env(int(SR * 0.07), 0.005, 0.05) for i in range(4)]
    _write_wav("Refuel.wav", np.concatenate(parts))
    # crash: filtered noise burst + low thump
    n = int(SR * 0.9)
    noise = rng.uniform(-1, 1, n)
    k = np.exp(-np.arange(200) / 30.0)
    noise = np.convolve(noise, k / k.sum(), mode="same")
    env = np.exp(-np.arange(n) / (SR * 0.22))
    thump = _tone([120, 40], 0.9, "sine", 0.8) * np.exp(-np.arange(n) / (SR * 0.15))
    _write_wav("Crash.wav", (noise * 2.2 + thump) * env)
    # win: happy arpeggio
    notes = [523, 659, 784, 1047]
    seq = [_tone([f], 0.13, "tri", 0.5) * _env(int(SR * 0.13), 0.005, 0.08) for f in notes]
    seq.append(_tone([1047], 0.4, "tri", 0.5) * _env(int(SR * 0.4), 0.005, 0.35))
    _write_wav("Win.wav", np.concatenate(seq))
    # pad opens: shimmering chime
    ch = sum(_tone([f], 0.7, "sine", 0.25) for f in (784, 988, 1175)) * _env(int(SR * 0.7), 0.01, 0.6)
    _write_wav("PadOpen.wav", ch)
    # ui click
    _write_wav("Click.wav", _tone([1200, 800], 0.05, "square", 0.25) * _env(int(SR * 0.05), 0.002, 0.04))
    # thruster loop: low rumbling noise, seamless (crossfaded ends)
    n = SR * 1
    noise = rng.uniform(-1, 1, n + 2000)
    k = np.exp(-np.arange(400) / 60.0)
    rumble = np.convolve(noise, k / k.sum(), mode="same")[:n + 2000] * 4.0
    fade = np.linspace(0, 1, 2000)
    loop = rumble[:n].copy()
    loop[:2000] = rumble[:2000] * fade + rumble[n:n + 2000] * (1 - fade)
    _write_wav("Thrust.wav", np.clip(loop, -1, 1) * 0.6)


if __name__ == "__main__":
    main()
