#!/usr/bin/env python3
"""
Draws every sprite in the game and writes them to Assets/Resources/Art.

Run:  python3 Tools/art/generate_art.py            (needs Pillow: pip install pillow)
      python3 Tools/art/generate_art.py --preview  (also writes Tools/art/preview.png)

Nothing here is hand-painted: change the palette or a building recipe in code, re-run, and the
whole game restyles. See iso.py for the drawing primitives.
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

from iso import (Iso, mix, shade, lighten, RED, RED_DARK, NAVY, NAVY_2, SLATE, STEEL, CREAM, SAND, TEAL,
                 GOLD, WHITE, SHADOW_TINT, GROUND_A, GROUND_B, GROUND_LINE, TW, TH, ZU, SS, PAD_B)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "Art")
MAX_LEVEL = 5
GLASS = (140, 232, 222)


def octagon(cx, cy, r):
    k = r * 0.414
    return [(cx - k, cy - r), (cx + k, cy - r), (cx + r, cy - k), (cx + r, cy + k),
            (cx + k, cy + r), (cx - k, cy + r), (cx - r, cy + k), (cx - r, cy - k)]


def square(cx, cy, r):
    return [(cx - r, cy - r), (cx + r, cy - r), (cx + r, cy + r), (cx - r, cy + r)]


# ============================================================================ buildings
# Each recipe receives the level (1-5) and returns an Iso canvas.
# Parts are drawn back-to-front (smaller x + y first), like layering paper cut-outs.

def garage(L):
    g = Iso(1, 1.5)
    g.ground_shadow(0.06, 0.06, 0.98, 0.98)
    h = 0.40 + (0.12 if L >= 3 else 0) + (0.10 if L >= 5 else 0)
    if L >= 2:
        g.pole(0.24, 0.24, 0, h + 0.62, NAVY_2)
        g.box(0.19, 0.19, 0.29, 0.29, h + 0.62, h + 0.70, RED, rim=False)
    g.box(0.14, 0.16, 0.86, 0.86, 0, h, CREAM)
    g.decal_left(0.86, 0.24, 0.64, 0, 0.29, NAVY_2)                    # roller door
    for z in (0.08, 0.16, 0.23):
        g.decal_left(0.86, 0.24, 0.64, z, z + 0.015, SLATE)
    g.decal_right(0.86, 0.30, 0.70, 0.17, 0.30, TEAL, glow=True)      # side window
    if L >= 3:
        g.decal_left(0.86, 0.20, 0.80, h - 0.10, h - 0.05, TEAL, glow=True)
        g.decal_right(0.86, 0.22, 0.80, h - 0.10, h - 0.05, RED, lit=False)
    g.wedge(0.08, 0.10, 0.92, 0.92, h, h + 0.26, h + 0.06, GOLD if L >= 5 else RED)
    if L >= 4:
        g.box(0.66, 0.90, 0.92, 1.0, 0, 0.16, SLATE)                   # supply crates out front
        g.box(0.70, 0.91, 0.88, 0.99, 0.16, 0.28, RED_DARK)
    return g


def recon(L):
    g = Iso(1, 1.7)
    g.ground_shadow(0.04, 0.04, 0.98, 0.98)
    h = 0.52 + 0.07 * (L - 1)
    g.chamfer(0.08, 0.08, 0.92, 0.92, 0, 0.08, SLATE, c=0.2)
    g.chamfer(0.20, 0.20, 0.80, 0.80, 0.08, h, NAVY_2, c=0.13)
    rows = 1 + (L >= 3) + (L >= 5)
    for r in range(rows):
        z = 0.20 + r * 0.21
        g.decal_left(0.80, 0.36, 0.64, z, z + 0.15, TEAL, glow=True)
        g.decal_right(0.80, 0.36, 0.64, z, z + 0.15, TEAL, glow=True)
    g.chamfer(0.10, 0.10, 0.90, 0.90, h, h + 0.07, CREAM, c=0.2)
    g.decal_top([(0.3, 0.3), (0.7, 0.3), (0.7, 0.7), (0.3, 0.7)], h + 0.07, RED)
    if L >= 2:
        g.pole(0.5, 0.5, h + 0.07, h + 0.50, NAVY_2)
        # holographic panel hanging off the mast
        g.poly([(0.5, 0.5, h + 0.26), (0.5, 0.92, h + 0.30), (0.5, 0.92, h + 0.46), (0.5, 0.5, h + 0.48)],
               TEAL, alpha=230, glow=True)
    if L >= 4:
        g.gem(0.5, 0.5, h + 0.62, 0.09, 0.12, GOLD if L >= 5 else RED)
    return g


def drone(L):
    g = Iso(2, 1.7)
    g.ground_shadow(0.04, 0.04, 1.98, 1.98)
    g.chamfer(0.08, 0.08, 1.92, 1.92, 0, 0.12, SLATE, c=0.28)
    hh = 0.60 + (0.12 if L >= 4 else 0)
    g.box(0.22, 0.22, 1.05, 1.78, 0.12, hh, CREAM)                     # hangar
    g.decal_right(1.05, 0.42, 1.58, 0.12, hh - 0.16, NAVY_2)           # hangar mouth
    g.decal_right(1.05, 0.42, 1.58, hh - 0.13, hh - 0.08, TEAL, glow=True)
    g.decal_left(1.78, 0.34, 0.92, 0.30, 0.42, RED, lit=False)
    g.wedge(0.16, 0.16, 1.11, 1.84, hh, hh + 0.30, hh + 0.05, GOLD if L >= 5 else RED)
    if L >= 2:
        g.pole(0.36, 1.62, hh + 0.2, hh + 0.72, NAVY_2)
        g.gem(0.36, 1.62, hh + 0.80, 0.06, 0.08, TEAL)
    # landing pad with chevron markings
    g.decal_top([(1.18, 0.40), (1.80, 0.40), (1.80, 1.60), (1.18, 1.60)], 0.121, NAVY_2)
    for y0 in (0.55, 0.95):
        g.decal_top([(1.28, y0), (1.49, y0 + 0.14), (1.70, y0), (1.70, y0 + 0.12),
                     (1.49, y0 + 0.26), (1.28, y0 + 0.12)], 0.122, TEAL, glow=True)
    spots = [(1.49, 1.30, 0.62)] + ([(1.49, 0.70, 0.95)] if L >= 3 else []) + ([(1.0, 1.0, 1.35)] if L >= 5 else [])
    for (x, y, z) in spots:                                           # hovering drones
        g.chamfer(x - 0.17, y - 0.17, x + 0.17, y + 0.17, z, z + 0.05, NAVY_2, c=0.1)
        g.box(x - 0.06, y - 0.06, x + 0.06, y + 0.06, z + 0.05, z + 0.10, RED, rim=False)
        g.decal_left(y + 0.17, x - 0.05, x + 0.05, z + 0.01, z + 0.04, TEAL, glow=True)
    return g


def signal(L):
    g = Iso(1, 2.9)
    g.ground_shadow(0.08, 0.08, 0.96, 0.96)
    H = 1.05 + 0.26 * (L - 1)
    g.chamfer(0.14, 0.14, 0.86, 0.86, 0, 0.14, SLATE, c=0.18)
    split = 0.14 + (H - 0.14) * 0.55
    g.prism(square(0.5, 0.5, 0.22), 0.14, split, CREAM, top_scale=0.62)
    g.box(0.33, 0.33, 0.67, 0.67, split, split + 0.07, RED)
    g.prism(square(0.5, 0.5, 0.125), split + 0.07, H, NAVY_2, top_scale=0.5)
    fins = min(3, 1 + L // 2)
    for i in range(fins):                                             # angular relay fins
        z = split + 0.16 + i * (H - split - 0.2) / max(1, fins)
        g.poly([(0.5, 0.58, z), (0.5, 0.93, z + 0.10), (0.5, 0.58, z + 0.20)], shade(CREAM, 0.2))
        g.poly([(0.58, 0.5, z), (0.93, 0.5, z + 0.10), (0.58, 0.5, z + 0.20)], shade(CREAM, 0.46))
    g.pole(0.5, 0.5, H, H + 0.26, RED, w=0.02)
    g.gem(0.5, 0.5, H + 0.36, 0.07 + 0.01 * L, 0.10, GOLD if L >= 5 else TEAL)
    return g


def vault(L):
    g = Iso(2, 1.9)
    g.ground_shadow(0.04, 0.04, 1.98, 1.98)
    g.chamfer(0.06, 0.06, 1.94, 1.94, 0, 0.10, SLATE, c=0.22)
    count = 2 + (L >= 2) + (L >= 4)
    h = 0.72 + 0.10 * ((L >= 3) + (L >= 5)) + 0.04 * L
    x_start = 1.0 - count * 0.21 + 0.03
    for i in range(count):                                            # rows of server racks
        x0 = x_start + i * 0.42
        x1 = x0 + 0.30
        g.box(x0, 0.24, x1, 1.76, 0.10, h, NAVY_2, top_color=GOLD if L >= 5 else CREAM)
        z = 0.20
        k = 0
        while z < h - 0.08:                                           # blinking status lights
            col = RED if (k + i) % 4 == 3 else TEAL
            g.decal_right(x1, 0.34, 1.66, z, z + 0.028, col, glow=True)
            z += 0.085
            k += 1
        g.decal_left(1.76, x0 + 0.06, x1 - 0.06, 0.18, h - 0.10, SLATE)
        g.decal_left(1.76, x0 + 0.11, x1 - 0.11, h - 0.24, h - 0.14, TEAL, glow=True)
    return g


def lab(L):
    g = Iso(2, 2.3)
    g.ground_shadow(0.02, 0.02, 1.98, 1.98)
    g.chamfer(0.05, 0.05, 1.95, 1.95, 0, 0.08, SLATE, c=0.22)
    h = 0.66 + (0.14 if L >= 3 else 0)
    g.chamfer(0.20, 0.20, 1.48, 1.80, 0.08, h, CREAM, c=0.14)
    g.decal_left(1.80, 0.36, 1.32, h - 0.17, h - 0.10, RED, lit=False)
    for i in range(3):
        x = 0.42 + i * 0.30
        g.decal_left(1.80, x, x + 0.20, 0.22, 0.42, TEAL, glow=True)
    for i in range(3):
        y = 0.44 + i * 0.40
        g.decal_right(1.48, y, y + 0.26, 0.22, 0.42, TEAL, glow=True)
    # glass pyramid atrium
    g.prism([(0.42, 0.50), (1.26, 0.50), (1.26, 1.50), (0.42, 1.50)], h, h + 0.50 + 0.06 * L, GLASS,
            top_scale=0.0, glow=L >= 3)
    if L >= 5:
        g.gem(0.84, 1.0, h + 1.12, 0.13, 0.17, GOLD)
    # annex with exhaust stacks
    g.box(1.48, 0.84, 1.86, 1.72, 0.08, 0.40, NAVY_2)
    g.decal_right(1.86, 0.96, 1.60, 0.18, 0.28, RED, lit=False)
    if L >= 2:
        for y in ((1.05, 1.45) if L >= 4 else (1.25,)):
            g.box(1.60, y - 0.07, 1.74, y + 0.07, 0.40, 0.78, STEEL)
            g.box(1.60, y - 0.07, 1.74, y + 0.07, 0.78, 0.84, RED, rim=False)
            g.decal_top(square(1.67, y, 0.04), 0.841, TEAL, glow=True)
    return g


def academy(L):
    g = Iso(3, 2.6)
    g.ground_shadow(0.02, 0.02, 2.98, 2.98)
    g.chamfer(0.06, 0.06, 2.94, 2.94, 0, 0.10, SLATE, c=0.32)
    roof = GOLD if L >= 5 else RED
    # main hall along the back
    g.box(0.25, 0.30, 1.10, 2.70, 0.10, 0.86, CREAM)
    g.decal_right(1.10, 1.25, 1.75, 0.10, 0.55, NAVY_2)
    g.decal_right(1.10, 1.25, 1.75, 0.58, 0.63, RED, lit=False)
    for y in (0.48, 0.86, 1.94, 2.32):
        g.decal_right(1.10, y, y + 0.22, 0.34, 0.62, TEAL, glow=True)
    g.wedge(0.19, 0.24, 1.16, 2.76, 0.86, 1.24, 0.93, roof)
    if L >= 3:                                                        # watch tower
        g.box(0.30, 0.34, 0.80, 0.84, 0.9, 1.62, CREAM)
        g.decal_left(0.84, 0.40, 0.70, 1.30, 1.46, TEAL, glow=True)
        g.decal_right(0.80, 0.44, 0.74, 1.30, 1.46, TEAL, glow=True)
        g.prism(square(0.55, 0.59, 0.31), 1.62, 2.0, roof, top_scale=0.0)
        if L >= 5:
            g.gem(0.55, 0.59, 2.2, 0.10, 0.14, TEAL)
    # far wing
    g.box(1.10, 0.30, 2.62, 0.98, 0.10, 0.58, CREAM)
    for x in (1.30, 1.72, 2.14):
        g.decal_left(0.98, x, x + 0.24, 0.26, 0.44, TEAL, glow=True)
    g.decal_right(2.62, 0.40, 0.88, 0.40, 0.46, RED, lit=False)
    g.chamfer(1.06, 0.26, 2.66, 1.02, 0.58, 0.66, NAVY_2, c=0.10)
    # training yard
    g.decal_top([(1.18, 1.08), (2.60, 1.08), (2.60, 1.92), (1.18, 1.92)], 0.101, NAVY_2)
    g.decal_top([(1.70, 1.22), (2.10, 1.50), (1.70, 1.78), (1.82, 1.50)], 0.102, TEAL, glow=True)
    g.decal_top([(2.05, 1.22), (2.45, 1.50), (2.05, 1.78), (2.17, 1.50)], 0.102, RED)
    if L >= 2:
        for y in (1.25, 1.75):                                        # target dummies
            g.box(1.34, y - 0.05, 1.44, y + 0.05, 0.10, 0.34, RED, rim=False)
    # near wing
    g.box(1.10, 2.02, 2.62, 2.70, 0.10, 0.58, CREAM)
    for x in (1.30, 1.72, 2.14):
        g.decal_left(2.70, x, x + 0.24, 0.26, 0.44, TEAL, glow=True)
    g.decal_right(2.62, 2.12, 2.60, 0.26, 0.44, NAVY_2)
    g.chamfer(1.06, 1.98, 2.66, 2.74, 0.58, 0.66, NAVY_2, c=0.10)
    if L >= 4:                                                        # banners
        for (x, y) in ((2.74, 1.10), (2.74, 1.90)):
            g.pole(x, y, 0.10, 0.95, NAVY_2, w=0.025)
            g.poly([(x, y, 0.92), (x, y - 0.30, 0.86), (x, y - 0.30, 0.62), (x, y, 0.62)], RED)
    return g


def uplink(L):
    g = Iso(3, 3.6)
    g.ground_shadow(0.02, 0.02, 2.98, 2.98)
    g.chamfer(0.08, 0.08, 2.92, 2.92, 0, 0.12, SLATE, c=0.55)
    g.chamfer(0.45, 0.45, 2.55, 2.55, 0.12, 0.42, CREAM, c=0.50)
    g.decal_left(2.55, 1.05, 1.95, 0.22, 0.30, TEAL, glow=True)
    g.decal_right(2.55, 1.05, 1.95, 0.22, 0.30, TEAL, glow=True)
    if L >= 2:                                                        # back pylon
        g.prism(square(0.75, 0.75, 0.13), 0.42, 0.95, NAVY_2, top_scale=0.4)
        g.gem(0.75, 0.75, 1.06, 0.06, 0.08, TEAL)
    mast = 1.45 + 0.12 * L
    g.prism(square(1.5, 1.5, 0.34), 0.42, mast, NAVY_2, top_scale=0.42)
    g.decal_left(1.84, 1.38, 1.62, 0.50, 0.80, RED, lit=False)
    spread = 3.0 + 0.35 * L
    g.prism(octagon(1.5, 1.5, 0.26), mast, mast + 0.42, CREAM, top_scale=spread,
            top_color=GOLD if L >= 5 else NAVY_2)                     # the dish
    top = mast + 0.42
    g.decal_top(octagon(1.5, 1.5, 0.26 * spread * 0.62), top + 0.001, SLATE)
    g.decal_top(octagon(1.5, 1.5, 0.26 * spread * 0.30), top + 0.002, TEAL, glow=True)
    g.pole(1.5, 1.5, top, top + 0.50, RED, w=0.025)
    g.gem(1.5, 1.5, top + 0.62, 0.09, 0.12, TEAL)
    if L >= 4:                                                        # transmission beam
        g.line((1.5, 1.5, top + 0.72), (1.5, 1.5, top + 1.25), TEAL, width=3, glow=True)
    if L >= 2:
        for (x, y) in ((2.25, 0.75), (0.75, 2.25)):
            g.prism(square(x, y, 0.13), 0.42, 0.95, NAVY_2, top_scale=0.4)
            g.gem(x, y, 1.06, 0.06, 0.08, TEAL)
    if L >= 3:
        g.box(1.95, 1.95, 2.40, 2.40, 0.42, 0.70, NAVY_2)             # control hut
        g.decal_left(2.40, 2.02, 2.33, 0.50, 0.62, TEAL, glow=True)
        g.decal_right(2.40, 2.02, 2.33, 0.50, 0.62, RED, lit=False)
    return g


def core(L):
    g = Iso(3, 3.7)
    g.ground_shadow(0.0, 0.0, 3.0, 3.0, alpha=90)
    g.chamfer(0.05, 0.05, 2.95, 2.95, 0, 0.16, NAVY_2, c=0.60, top_color=SLATE)

    def pylon(x, y):
        g.prism(square(x, y, 0.15), 0.16, 0.80 + 0.06 * L, NAVY_2, top_scale=0.35)
        g.gem(x, y, 0.95 + 0.06 * L, 0.07, 0.10, GOLD if L >= 5 else TEAL)

    if L >= 3:
        pylon(0.62, 0.62)
    g.chamfer(0.40, 0.40, 2.60, 2.60, 0.16, 0.60, CREAM, c=0.52)
    g.decal_left(2.60, 1.00, 2.00, 0.30, 0.40, TEAL, glow=True)
    g.decal_right(2.60, 1.00, 2.00, 0.30, 0.40, TEAL, glow=True)
    g.decal_left(2.60, 1.25, 1.75, 0.16, 0.24, RED, lit=False)
    zt = 0.60
    if L >= 2:
        g.chamfer(0.80, 0.80, 2.20, 2.20, zt, zt + 0.34, NAVY_2, c=0.36, top_color=SLATE)
        g.decal_left(2.20, 1.22, 1.78, zt + 0.12, zt + 0.20, RED, lit=False)
        g.decal_right(2.20, 1.22, 1.78, zt + 0.12, zt + 0.20, RED, lit=False)
        zt += 0.34
    if L >= 4:
        g.chamfer(1.05, 1.05, 1.95, 1.95, zt, zt + 0.30, CREAM, c=0.24)
        g.decal_left(1.95, 1.32, 1.68, zt + 0.10, zt + 0.18, TEAL, glow=True)
        g.decal_right(1.95, 1.32, 1.68, zt + 0.10, zt + 0.18, TEAL, glow=True)
        zt += 0.30
    r = 0.30 + 0.035 * L
    hgt = 0.44 + 0.05 * L
    k = 0.36

    def prong(dx, dy):                                                # claws that cradle the crystal
        x, y = 1.5 + dx * k, 1.5 + dy * k
        g.prism(square(x, y, 0.07), zt, zt + 0.34 + 0.03 * L, RED_DARK, top_scale=0.2)

    prong(-1, -1)
    prong(1, -1)
    prong(-1, 1)
    g.decal_top(octagon(1.5, 1.5, 0.22), zt + 0.002, TEAL, glow=True)
    g.gem(1.5, 1.5, zt + 0.30 + hgt, r, hgt, GOLD if L >= 5 else RED)
    prong(1, 1)
    if L >= 3:
        pylon(2.38, 0.62)
        pylon(0.62, 2.38)
        pylon(2.38, 2.38)
    return g


BUILDINGS = {"garage": garage, "recon": recon, "drone": drone, "signal": signal, "vault": vault,
             "lab": lab, "academy": academy, "uplink": uplink, "core": core}


# ============================================================================ ground
def diamond_pts(w, h, inset=0.0):
    return [(w / 2, inset), (w - inset * 2, h / 2), (w / 2, h - inset), (inset * 2, h / 2)]


def tile(fill, line=None, mark=None):
    w, h = TW * SS, TH * SS
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon(diamond_pts(w, h), fill=fill + (255,))
    if line:
        pts = diamond_pts(w, h)
        d.line(pts + [pts[0]], fill=line + (255,), width=SS * 2)
    if mark:
        # small chevron decal pointing "up" the grid
        cx, cy = w / 2, h / 2
        s = w * 0.10
        d.polygon([(cx - s, cy), (cx, cy - s * 0.5), (cx + s, cy), (cx + s * 0.55, cy), (cx, cy - s * 0.22),
                   (cx - s * 0.55, cy)], fill=mark + (255,))
    return img.resize((TW, TH), Image.LANCZOS)


def tile_mask(outline_only):
    """White diamond (filled or outlined) that the game tints: placement ghost, selection, hover."""
    w, h = TW * SS, TH * SS
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pts = diamond_pts(w, h, inset=3 * SS)
    if outline_only:
        d.line(pts + [pts[0]], fill=(255, 255, 255, 255), width=SS * 7, joint="curve")
    else:
        d.polygon(pts, fill=(255, 255, 255, 255))
    return img.resize((TW, TH), Image.LANCZOS)


EDGE_DEPTH = 150


def edge(left):
    """The cliff face under the two front edges of the map. Half a tile wide."""
    w, h = (TW // 2) * SS, (TH // 2 + EDGE_DEPTH) * SS
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    slope = (TH // 2) * SS
    depth = EDGE_DEPTH * SS
    base = shade(GROUND_A, 0.30 if left else 0.55)
    if left:   # runs from top-left down to bottom-right
        quad = lambda a, b: [(0, a), (w, slope + a), (w, slope + b), (0, b)]
    else:      # mirrored: from top-right down to bottom-left
        quad = lambda a, b: [(w, a), (0, slope + a), (0, slope + b), (w, b)]
    d.polygon(quad(0, depth), fill=base + (255,))
    d.polygon(quad(0, 10 * SS), fill=lighten(base, 0.12) + (255,))
    d.polygon(quad(26 * SS, 32 * SS), fill=(RED if left else RED_DARK) + (255,))
    d.polygon(quad(depth * 0.55, depth), fill=shade(base, 0.25) + (255,))
    img = img.resize((TW // 2, TH // 2 + EDGE_DEPTH), Image.LANCZOS)
    # fade the bottom out so the island seems to float
    fade = Image.new("L", img.size, 255)
    fd = ImageDraw.Draw(fade)
    for y in range(img.size[1]):
        t = max(0.0, (y - img.size[1] * 0.45) / (img.size[1] * 0.55))
        fd.line([(0, y), (img.size[0], y)], fill=int(255 * (1 - t) ** 1.5))
    a = img.getchannel("A")
    img.putalpha(_mul(a, fade))
    return img


def _mul(a, b):
    from PIL import ImageChops
    return ImageChops.multiply(a, b)


def background():
    """Dark backdrop: vignette plus faint diagonal hatch lines."""
    n = 512
    img = Image.new("RGB", (n, n), NAVY)
    px = img.load()
    for y in range(n):
        for x in range(n):
            dx, dy = (x - n / 2) / (n / 2), (y - n / 2) / (n / 2)
            t = min(1.0, math.sqrt(dx * dx + dy * dy) / 1.25)
            c = mix((34, 52, 70), (10, 16, 24), t ** 1.3)
            if (x + y) % 32 < 2:
                c = lighten(c, 0.035)
            px[x, y] = c
    return img.convert("RGBA")


# ============================================================================ UI kit
def cut_rect(size, cuts, fill, border=None, bw=2):
    """Rectangle with selected corners cut at 45 degrees. cuts = (tl, tr, br, bl) in pixels."""
    w = h = size
    s = SS
    tl, tr, br, bl = [c * s for c in cuts]
    W, H = w * s, h * s

    def pts(i):
        return [(tl + i, i), (W - tr - i, i), (W - i, tr + i), (W - i, H - br - i), (W - br - i, H - i),
                (bl + i, H - i), (i, H - bl - i), (i, tl + i)]

    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if border:
        d.polygon(pts(0), fill=border)
        d.polygon(pts(bw * s), fill=fill)
    else:
        d.polygon(pts(0), fill=fill)
    return img.resize((w, h), Image.LANCZOS)


def ui_sprites():
    out = {}
    out["ui_panel"] = cut_rect(64, (0, 16, 0, 16), NAVY + (238,), SLATE + (255,), 2)
    out["ui_card"] = cut_rect(64, (0, 0, 12, 0), NAVY_2 + (255,), (44, 60, 78, 255), 2)
    out["ui_btn"] = cut_rect(64, (10, 0, 10, 0), (255, 255, 255, 255))
    out["ui_frame"] = cut_rect(64, (0, 0, 12, 0), (0, 0, 0, 0), (255, 255, 255, 255), 3)
    out["ui_tag"] = cut_rect(64, (0, 10, 0, 10), (255, 255, 255, 255))
    return out


def icon(draw_fn, size=128):
    img = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    u = size * SS / 100.0   # draw in a 100x100 space

    def P(pts, fill):
        d.polygon([(x * u, y * u) for x, y in pts], fill=fill + (255,) if len(fill) == 3 else fill)

    draw_fn(P)
    return img.resize((size, size), Image.LANCZOS)


def icon_credit(P):
    P([(50, 4), (90, 27), (90, 73), (50, 96), (10, 73), (10, 27)], shade(GOLD, 0.35))
    P([(50, 4), (90, 27), (90, 66), (50, 89), (10, 66), (10, 27)], GOLD)
    P([(50, 14), (80, 31), (80, 62), (50, 79), (20, 62), (20, 31)], lighten(GOLD, 0.35))
    P([(50, 26), (66, 46), (50, 67), (34, 46)], shade(GOLD, 0.45))
    P([(50, 26), (66, 46), (50, 50), (34, 46)], shade(GOLD, 0.25))


def icon_click(P):
    P([(22, 8), (80, 52), (54, 56), (68, 88), (54, 94), (40, 62), (22, 80)], WHITE)


def icon_global(P):
    for y in (10, 38, 66):
        P([(50, y), (88, y + 24), (88, y + 38), (50, y + 14), (12, y + 38), (12, y + 24)], WHITE)


def icon_up(P):
    P([(50, 8), (90, 50), (66, 50), (66, 92), (34, 92), (34, 50), (10, 50)], WHITE)


def icon_move(P):
    P([(50, 4), (68, 24), (56, 24), (56, 44), (76, 44), (76, 32), (96, 50), (76, 68), (76, 56), (56, 56),
       (56, 76), (68, 76), (50, 96), (32, 76), (44, 76), (44, 56), (24, 56), (24, 68), (4, 50), (24, 32),
       (24, 44), (44, 44), (44, 24), (32, 24)], WHITE)


def icon_sell(P):
    P([(18, 8), (50, 40), (82, 8), (92, 18), (60, 50), (92, 82), (82, 92), (50, 60), (18, 92), (8, 82),
       (40, 50), (8, 18)], WHITE)


def icon_lock(P):
    P([(28, 46), (28, 26), (40, 12), (60, 12), (72, 26), (72, 46), (60, 46), (60, 30), (56, 24), (44, 24),
       (40, 30), (40, 46)], WHITE)
    P([(18, 46), (82, 46), (82, 82), (72, 92), (18, 92)], WHITE)


def icon_bolt(P):
    P([(58, 4), (20, 56), (46, 56), (38, 96), (80, 40), (54, 40)], WHITE)


ICONS = {"icon_credit": icon_credit, "icon_click": icon_click, "icon_global": icon_global, "icon_up": icon_up,
         "icon_move": icon_move, "icon_sell": icon_sell, "icon_lock": icon_lock, "icon_bolt": icon_bolt}


# ============================================================================ effects
def fx_shard():
    img = Image.new("RGBA", (48 * SS, 48 * SS), (0, 0, 0, 0))
    ImageDraw.Draw(img).polygon([(6 * SS, 40 * SS), (22 * SS, 4 * SS), (44 * SS, 30 * SS)], fill=(255, 255, 255, 255))
    return img.resize((48, 48), Image.LANCZOS)


def fx_glow():
    n = 128
    img = Image.new("RGBA", (n, n), (255, 255, 255, 0))
    px = img.load()
    for y in range(n):
        for x in range(n):
            d = math.hypot(x - n / 2 + 0.5, y - n / 2 + 0.5) / (n / 2)
            px[x, y] = (255, 255, 255, int(255 * max(0.0, 1 - d) ** 2))
    return img


def supply_drop():
    g = Iso(1, 1.6)
    g.ground_shadow(0.15, 0.15, 0.9, 0.9, alpha=90)
    g.decal_top(octagon(0.5, 0.5, 0.42), 0.001, TEAL, glow=True)
    g.decal_top(octagon(0.5, 0.5, 0.34), 0.002, NAVY_2)
    g.chamfer(0.24, 0.24, 0.76, 0.76, 0.0, 0.42, CREAM, c=0.08)
    g.decal_left(0.76, 0.34, 0.66, 0.14, 0.28, RED, lit=False)
    g.decal_right(0.76, 0.34, 0.66, 0.14, 0.28, RED, lit=False)
    g.decal_top([(0.34, 0.44), (0.66, 0.44), (0.66, 0.56), (0.34, 0.56)], 0.421, RED)
    g.decal_top([(0.44, 0.34), (0.56, 0.34), (0.56, 0.66), (0.44, 0.66)], 0.421, RED)
    g.line((0.5, 0.5, 0.42), (0.5, 0.5, 1.35), GOLD, width=3, glow=True)
    g.gem(0.5, 0.5, 1.0, 0.10, 0.14, GOLD)
    return g


# ============================================================================ main
def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"), optimize=True)


def generate_all():
    for name, fn in BUILDINGS.items():
        for level in range(1, MAX_LEVEL + 1):
            save(fn(level).render(), "b_%s_%d" % (name, level))
    save(supply_drop().render(), "drop")
    save(tile(GROUND_A, GROUND_LINE), "tile_a")
    save(tile(GROUND_B, GROUND_LINE), "tile_b")
    save(tile(GROUND_B, GROUND_LINE, mark=GROUND_LINE), "tile_c")
    save(tile_mask(False), "tile_fill")
    save(tile_mask(True), "tile_outline")
    save(edge(True), "edge_l")
    save(edge(False), "edge_r")
    save(background(), "bg")
    for name, img in ui_sprites().items():
        save(img, name)
    for name, fn in ICONS.items():
        save(icon(fn), name)
    save(fx_shard(), "fx_shard")
    save(fx_glow(), "fx_glow")


def preview(path):
    """Compose a mock scene from the generated sprites, to eyeball the style without opening Unity."""
    n = 11
    W, H = 2200, 1500
    img = background().resize((W, H), Image.BILINEAR)
    ox, oy = W // 2, 330

    def to_px(x, y):
        return ox + (x - y) * TW / 2, oy + (x + y) * TH / 2

    def load(name):
        return Image.open(os.path.join(OUT, name + ".png"))

    for y in range(n):
        for x in range(n):
            px, py = to_px(x + 0.5, y + 0.5)
            name = "tile_c" if (x * 7 + y * 13) % 11 == 0 else ("tile_a" if (x + y) % 2 == 0 else "tile_b")
            t = load(name)
            img.alpha_composite(t, (int(px - TW / 2), int(py - TH / 2)))
    el, er = load("edge_l"), load("edge_r")
    for i in range(n):
        px, py = to_px(i, n)
        img.alpha_composite(el, (int(px), int(py)))
        px, py = to_px(n, i)
        img.alpha_composite(er, (int(px - TW / 2), int(py)))
    placed = [("core", 3, 4, 4, 3), ("garage", 1, 1, 1, 1), ("garage", 1, 2, 1, 3), ("garage", 1, 3, 1, 5),
              ("recon", 1, 1, 3, 1), ("recon", 1, 2, 3, 3), ("recon", 1, 1, 4, 5), ("signal", 1, 5, 1, 1),
              ("signal", 1, 6, 1, 3), ("signal", 1, 7, 1, 5), ("drone", 2, 8, 1, 2), ("drone", 2, 8, 4, 5),
              ("vault", 2, 1, 6, 1), ("vault", 2, 1, 9, 5), ("lab", 2, 4, 8, 3), ("academy", 3, 8, 8, 3)]
    placed.sort(key=lambda p: p[2] + p[3] + p[1])
    for name, s, x, y, lvl in placed:
        spr = load("b_%s_%d" % (name, lvl))
        px, py = to_px(x + s, y + s)
        img.alpha_composite(spr, (int(px - spr.size[0] / 2), int(py - spr.size[1] + PAD_B)))
    img.convert("RGB").save(path, quality=90)


def sheet(path):
    """Contact sheet: every building at every level."""
    names = list(BUILDINGS.keys())
    cell_w, cell_h = 420, 520
    img = Image.new("RGBA", (cell_w * MAX_LEVEL, cell_h * len(names)), GROUND_A + (255,))
    for r, name in enumerate(names):
        for lvl in range(1, MAX_LEVEL + 1):
            spr = Image.open(os.path.join(OUT, "b_%s_%d.png" % (name, lvl)))
            k = min(cell_w / spr.size[0], cell_h / spr.size[1], 1.0) * 0.95
            spr = spr.resize((int(spr.size[0] * k), int(spr.size[1] * k)), Image.LANCZOS)
            img.alpha_composite(spr, ((lvl - 1) * cell_w + (cell_w - spr.size[0]) // 2,
                                      r * cell_h + cell_h - spr.size[1] - 6))
    img.convert("RGB").save(path, quality=88)


if __name__ == "__main__":
    generate_all()
    print("wrote sprites to", OUT)
    if "--preview" in sys.argv:
        here = os.path.dirname(os.path.abspath(__file__))
        preview(os.path.join(here, "preview.jpg"))
        sheet(os.path.join(here, "sheet.jpg"))
        print("wrote preview.jpg and sheet.jpg")
