"""
Tiny isometric drawing kit used by generate_art.py.

How it works
------------
Every building is described in "grid space": x and y run along the two ground axes of the
isometric grid (1.0 = one tile) and z is height. `Iso.pt()` projects a grid-space point to a
pixel, and every shape is just a flat-coloured polygon built from projected points.

The art style comes from three rules applied everywhere:
  * flat shading only - each face gets ONE colour (top lightest, left mid, right darkest);
  * shadows are tinted towards navy instead of plain black, which keeps colours rich;
  * no curves - everything is boxes, wedges, chamfers and tapered prisms.

Shapes are drawn at 3x size and scaled down at the end, which gives smooth anti-aliased edges.
"""
from PIL import Image, ImageDraw, ImageFilter

# ---------------------------------------------------------------- palette (single source of truth)
RED = (238, 66, 82)
RED_DARK = (176, 40, 62)
NAVY = (15, 25, 35)
NAVY_2 = (27, 40, 54)
SLATE = (58, 76, 96)
STEEL = (112, 130, 150)
CREAM = (236, 232, 225)
SAND = (214, 203, 186)
TEAL = (84, 226, 206)
GOLD = (255, 198, 84)
WHITE = (255, 255, 255)
SHADOW_TINT = (18, 22, 48)

GROUND_A = (52, 80, 96)
GROUND_B = (56, 86, 102)
GROUND_LINE = (70, 102, 118)

# ---------------------------------------------------------------- geometry constants
TW = 256          # tile width in final pixels (= 1 world unit in Unity at 256 PPU)
TH = 128          # tile height (2:1 isometric diamond)
ZU = 140          # pixels per unit of height
SS = 3            # supersampling factor
PAD_B = 16        # empty pixels under the footprint's front corner (Unity pivot relies on this)
PAD_X = 16

# How much each face direction is pulled towards the shadow tint.
SHADE_TOP = 0.0
SHADE_LEFT = 0.20
SHADE_RIGHT = 0.46


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def shade(color, amount):
    return mix(color, SHADOW_TINT, amount)


def lighten(color, amount):
    return mix(color, WHITE, amount)


class Iso:
    """A canvas for one sprite whose footprint is `size` x `size` tiles and up to `height` units tall."""

    def __init__(self, size, height):
        self.size = size
        self.w = size * TW + PAD_X * 2
        self.h = int(PAD_B + size * TH + height * ZU + 24)
        self.img = Image.new("RGBA", (self.w * SS, self.h * SS), (0, 0, 0, 0))
        self.glow = Image.new("RGBA", (self.w * SS, self.h * SS), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)
        self.gd = ImageDraw.Draw(self.glow)

    # -- projection ------------------------------------------------------------------------
    def pt(self, x, y, z=0.0):
        s = self.size
        sx = self.w / 2 + (x - y) * TW / 2
        sy = (self.h - PAD_B) - (2 * s - x - y) * TH / 2 - z * ZU
        return (sx * SS, sy * SS)

    def poly(self, pts3, color, alpha=255, glow=False):
        """Fill a polygon given as grid-space (x, y, z) points."""
        pts = [self.pt(*p) for p in pts3]
        self.d.polygon(pts, fill=color + (alpha,))
        if glow:
            self.gd.polygon(pts, fill=color + (255,))

    def line(self, a, b, color, width=2, glow=False):
        pts = [self.pt(*a), self.pt(*b)]
        self.d.line(pts, fill=color + (255,), width=int(width * SS))
        if glow:
            self.gd.line(pts, fill=color + (255,), width=int(width * SS * 2))

    # -- solids ----------------------------------------------------------------------------
    def prism(self, base, z0, z1, color, top_scale=1.0, top_color=None, rim=True, glow=False):
        """
        Extrude a convex ground polygon `base` [(x, y), ...] from z0 up to z1.
        top_scale < 1 tapers it towards its centre (pyramids, tapered towers).
        Only faces pointing at the camera are drawn.
        """
        cx = sum(p[0] for p in base) / len(base)
        cy = sum(p[1] for p in base) / len(base)
        top = [(cx + (p[0] - cx) * top_scale, cy + (p[1] - cy) * top_scale) for p in base]
        n = len(base)
        for i in range(n):
            a, b = base[i], base[(i + 1) % n]
            ta, tb = top[i], top[(i + 1) % n]
            # outward normal of this edge (pointing away from the centre)
            nx, ny = (b[1] - a[1]), -(b[0] - a[0])
            mx, my = (a[0] + b[0]) / 2 - cx, (a[1] + b[1]) / 2 - cy
            if nx * mx + ny * my < 0:
                nx, ny = -nx, -ny
            if nx + ny <= 1e-6:
                continue  # faces away from the camera
            t = max(0.0, min(1.0, nx / (abs(nx) + abs(ny))))
            amount = SHADE_LEFT + (SHADE_RIGHT - SHADE_LEFT) * t
            self.poly([(a[0], a[1], z0), (b[0], b[1], z0), (tb[0], tb[1], z1), (ta[0], ta[1], z1)],
                      shade(color, amount), glow=glow)
        if top_scale > 0.02:
            tc = top_color if top_color else color
            tp = [(p[0], p[1], z1) for p in top]
            self.poly(tp, tc, glow=glow)
            if rim:
                # thin bright edge on the top face - makes silhouettes read crisply
                pts = [self.pt(*p) for p in tp]
                self.d.line(pts + [pts[0]], fill=lighten(tc, 0.35) + (255,), width=SS * 2)

    def box(self, x0, y0, x1, y1, z0, z1, color, **kw):
        self.prism([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], z0, z1, color, **kw)

    def chamfer(self, x0, y0, x1, y1, z0, z1, color, c=0.12, **kw):
        """A box with its four vertical corners cut off - the signature angular shape."""
        self.prism([(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c),
                    (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)], z0, z1, color, **kw)

    def wedge(self, x0, y0, x1, y1, z0, z_high, z_low, color):
        """Shed roof: high along the x0 edge, sloping down to the x1 edge."""
        self.poly([(x0, y1, z0), (x1, y1, z0), (x1, y1, z_low), (x0, y1, z_high)], shade(color, SHADE_LEFT))
        if z_low > z0:
            self.poly([(x1, y0, z0), (x1, y1, z0), (x1, y1, z_low), (x1, y0, z_low)], shade(color, SHADE_RIGHT))
        top = [(x0, y0, z_high), (x1, y0, z_low), (x1, y1, z_low), (x0, y1, z_high)]
        self.poly(top, shade(color, 0.08))
        pts = [self.pt(*p) for p in top]
        self.d.line(pts + [pts[0]], fill=lighten(color, 0.3) + (255,), width=SS * 2)

    def gem(self, cx, cy, zc, r, h, color, glow=True):
        """An eight-faced crystal (octahedron) floating at height zc."""
        top, bot = (cx, cy, zc + h), (cx, cy, zc - h)
        e, n_, w, s = (cx + r, cy, zc), (cx, cy + r, zc), (cx - r, cy, zc), (cx, cy - r, zc)
        faces = [
            (top, s, w, 0.30), (bot, s, w, 0.70),      # back pair (mostly hidden)
            (top, w, n_, -0.30), (top, e, s, 0.10),    # upper sides
            (bot, w, n_, 0.25), (bot, e, s, 0.55),     # lower sides
            (top, n_, e, -0.10), (bot, n_, e, 0.40),   # front pair
        ]
        for a, b, c, amt in faces:
            col = lighten(color, -amt) if amt < 0 else shade(color, amt)
            self.poly([a, b, c], col, glow=glow)

    # -- decals (flat details painted onto a face) --------------------------------------------
    def decal_left(self, y, x0, x1, z0, z1, color, glow=False, lit=True):
        """Rectangle on a face that looks towards the bottom-left of the screen (plane y = const)."""
        c = color if (glow or not lit) else shade(color, SHADE_LEFT)
        self.poly([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], c, glow=glow)

    def decal_right(self, x, y0, y1, z0, z1, color, glow=False, lit=True):
        """Rectangle on a face that looks towards the bottom-right of the screen (plane x = const)."""
        c = color if (glow or not lit) else shade(color, SHADE_RIGHT)
        self.poly([(x, y0, z0), (x, y1, z0), (x, y1, z1), (x, y0, z1)], c, glow=glow)

    def decal_top(self, pts2, z, color, glow=False):
        self.poly([(p[0], p[1], z) for p in pts2], color, glow=glow)

    def pole(self, x, y, z0, z1, color, w=0.035):
        self.box(x - w, y - w, x + w, y + w, z0, z1, color, rim=False)

    def ground_shadow(self, x0, y0, x1, y1, alpha=70):
        """Soft dark patch under a part, so it feels attached to the ground."""
        layer = Image.new("RGBA", self.img.size, (0, 0, 0, 0))
        pts = [self.pt(x0, y0), self.pt(x1, y0), self.pt(x1, y1), self.pt(x0, y1)]
        ImageDraw.Draw(layer).polygon(pts, fill=SHADOW_TINT + (alpha,))
        layer = layer.filter(ImageFilter.GaussianBlur(4 * SS))
        self.img.alpha_composite(layer)
        self.d = ImageDraw.Draw(self.img)

    # -- output ----------------------------------------------------------------------------
    def render(self, glow_strength=0.75, glow_radius=9):
        """Downscale, then add a soft bloom around everything that was drawn with glow=True."""
        out = self.img.resize((self.w, self.h), Image.LANCZOS)
        g = self.glow.resize((self.w, self.h), Image.LANCZOS).filter(ImageFilter.GaussianBlur(glow_radius))
        g.putalpha(g.getchannel("A").point(lambda a: int(a * glow_strength)))
        halo = Image.new("RGBA", out.size, (0, 0, 0, 0))
        halo.alpha_composite(g)
        halo.alpha_composite(out)
        # glow also brightens the surfaces it spills onto
        halo.alpha_composite(_fade(g, 0.35))
        return halo


def _fade(img, k):
    out = img.copy()
    out.putalpha(img.getchannel("A").point(lambda a: int(a * k)))
    return out
