# Draws the whole app icon in code: the scene from make_icon_scene.py plus every shape of Dusty (long hair, cap,
# face, fist, shirt) and the nug, as Grease Pencil strokes; then saves art/icon/dusty_icon.blend and renders
# art/icon/icon_2048.png.
# Run headless:  blender -b --python art/icon/draw_icon.py
# This rebuilds dusty_icon.blend from scratch every run: change the numbers here, not the .blend.
#
# Coordinates are written in reference-picture pixels (1024 x 1024, origin top left, y down) so shapes can be
# placed by reading them off the reference; px() turns them into scene units.
import bpy
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_icon_scene as base  # noqa: E402

PNG = os.path.join(base.HERE, "icon_2048.png")
UNIT = base.FRAME / 1024.0


# Reference pixel (x, y down) to scene (x, z up).
def px(x, y):
    return (x * UNIT - base.FRAME / 2, base.FRAME / 2 - y * UNIT)


# Smooth closed or open curve through the given points (Catmull-Rom), returned as a denser point list.
def smooth(points, closed=True, steps=6):
    n = len(points)
    if n < 3:
        return list(points)
    out = []
    count = n if closed else n - 1
    for i in range(count):
        p0 = points[(i - 1) % n] if closed or i > 0 else points[0]
        p1 = points[i]
        p2 = points[(i + 1) % n]
        p3 = points[(i + 2) % n] if closed or i + 2 < n else points[-1]
        for s in range(steps):
            t = s / steps
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * (2 * p1[k] + (-p0[k] + p2[k]) * t + (2 * p0[k] - 5 * p1[k] + 4 * p2[k] - p3[k]) * t2
                                    + (-p0[k] + 3 * p1[k] - 3 * p2[k] + p3[k]) * t3) for k in range(2)))
    if not closed:
        out.append(points[-1])
    return out


# Points round a circle or ellipse, optionally with bumps (for nug buds), in pixel space.
def circle(cx, cy, r, ry=None, n=40, bumps=0, amp=0.0, phase=0.0, jitter=0.0, rng=None):
    ry = r if ry is None else ry
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        k = 1 + amp * math.cos(bumps * a + phase) + (rng.uniform(-jitter, jitter) if rng else 0)
        pts.append((cx + r * k * math.cos(a), cy + ry * k * math.sin(a)))
    return pts


# Draws strokes into the icon object: layer groups, per-part layers, fills and lines.
class Painter:
    def __init__(self, gp):
        self.gp = gp
        self.names = [m.name for m in gp.materials]
        self.group = None

    # Starts a new layer group on top of the stack; parts made after this go in it.
    def begin_group(self, name):
        self.group = self.gp.layer_groups.new(name)

    # Makes one layer at the top of the current group, lights off, optionally clipped to a mask layer.
    def layer(self, name, mask=None):
        layer = self.gp.layers.new(name, set_active=True, layer_group=self.group)
        layer.use_lights = False
        layer.frames.new(1)
        self.gp.layers.move_top(layer)
        if mask is not None:
            layer.use_masks = True
            layer.mask_layers.add(mask)
        return layer

    # A part is three layers: Base (flat colour), Shade (clipped to Base) and Line (outlines on top).
    def part(self, name):
        b = self.layer(name + "_Base")
        s = self.layer(name + "_Shade", mask=b)
        l = self.layer(name + "_Line")
        return b, s, l

    # Adds one filled shape (no outline) from pixel points.
    def fill(self, layer, material, points, curve=True):
        pts = smooth(points) if curve else points
        base.add_shape(self.gp, layer, [px(*p) for p in pts], material)

    # Adds one line; its weight comes from the material's modifier, or from radius for unmodified materials.
    def line(self, layer, material, points, closed=False, curve=True, radius=3.0):
        pts = smooth(points, closed) if curve else list(points)
        drawing = layer.frames[0].drawing
        drawing.add_strokes([len(pts)])
        stroke = drawing.strokes[len(drawing.strokes) - 1]
        stroke.cyclic = closed
        stroke.material_index = self.names.index(material)
        for p, (x, y) in zip(stroke.points, pts):
            sx, sz = px(x, y)
            p.position = (sx, 0.0, sz)
            p.radius = radius * UNIT
            p.opacity = 1.0

    # Filled shape plus its outline in one go.
    def shape(self, fill_layer, line_layer, material, points, curve=True):
        self.fill(fill_layer, material, points, curve)
        self.line(line_layer, "Line_Dark", points, closed=True, curve=curve)


# Long hair mass behind the head: spills from under the cap down both sides to the shoulders in pointed locks.
def draw_hair_back(p):
    b, s, l = p.part("HairBack")
    mass = [(175, 230), (120, 300), (88, 400), (76, 500), (68, 590), (42, 690), (98, 640), (112, 725),
            (150, 650), (188, 712), (205, 635), (300, 655), (450, 690), (560, 745), (590, 680), (650, 750),
            (668, 672), (728, 708), (708, 600), (722, 500), (708, 390), (672, 300), (600, 215), (450, 170),
            (300, 170)]
    p.shape(b, l, "Fill_Hair", mass)
    p.fill(s, "Fill_Hair_Shade", [(150, 250), (185, 250), (172, 420), (178, 600), (160, 660), (128, 600), (138, 420)])
    p.fill(s, "Fill_Hair_Shade", [(60, 560), (95, 600), (98, 650), (60, 690), (40, 640)])
    p.fill(s, "Fill_Hair_Hi", [(104, 340), (120, 332), (112, 470), (100, 560), (94, 470)])
    p.fill(s, "Fill_Hair_Hi", [(610, 300), (640, 320), (665, 450), (650, 560), (628, 440)])
    for strand in ([(108, 420), (98, 540), (72, 640)], [(140, 470), (136, 580), (128, 680)],
                   [(660, 480), (662, 600), (650, 700)]):
        p.line(l, "Line_Detail", strand)


# Red polo shirt, the raised arm and its sleeve; dark cast shadow where the hair lies on the shoulders.
def draw_shirt(p):
    b, s, l = p.part("Shirt")
    p.shape(b, l, "Fill_Shirt", [(30, 1060), (50, 860), (100, 760), (200, 708), (300, 684), (420, 692),
                                 (520, 722), (610, 800), (660, 1060)], curve=True)
    p.fill(s, "Fill_Shirt_Shade", [(380, 690), (520, 720), (610, 800), (660, 1060), (430, 1060), (470, 850)])
    p.fill(s, "Fill_Shirt_Dark", [(60, 700), (210, 690), (215, 760), (150, 745), (90, 790)])
    p.fill(s, "Fill_Shirt_Dark", [(430, 690), (600, 740), (610, 800), (520, 780)])


# Neck between collar and chin, shaded under the jaw.
def draw_neck(p):
    b, s, l = p.part("Neck")
    p.fill(b, "Fill_Skin", [(292, 590), (400, 590), (410, 740), (285, 740)], curve=False)
    p.fill(s, "Fill_Skin_Shade", [(280, 580), (420, 580), (420, 680), (350, 660), (280, 670)], curve=False)
    p.line(l, "Line_Dark", [(296, 600), (294, 700)], curve=False)
    p.line(l, "Line_Dark", [(398, 600), (404, 700)], curve=False)


# Polo collar flaps either side of the neck.
def draw_collar(p):
    b, s, l = p.part("Collar")
    p.shape(b, l, "Fill_Shirt", [(205, 705), (296, 688), (318, 700), (290, 795)], curve=False)
    p.shape(b, l, "Fill_Shirt", [(372, 700), (404, 688), (470, 705), (380, 785)], curve=False)
    p.fill(s, "Fill_Shirt_Shade", [(380, 690), (480, 690), (480, 800), (390, 760)], curve=False)


# Raised forearm in a red sleeve, coming up from the bottom-left corner.
def draw_arm(p):
    b, s, l = p.part("Arm")
    p.shape(b, l, "Fill_Shirt", [(-30, 1060), (-30, 790), (60, 770), (160, 770), (215, 880), (205, 1060)])
    p.fill(s, "Fill_Shirt_Shade", [(130, 770), (170, 770), (220, 880), (210, 1060), (150, 1060), (165, 880)])
    p.line(l, "Line_Detail", [(30, 830), (90, 860), (150, 850)])


# Fist with knuckle lines and the thumb wrapped over the top.
def draw_fist(p):
    b, s, l = p.part("Fist")
    p.shape(b, l, "Fill_Skin", [(28, 640), (60, 602), (120, 598), (152, 632), (158, 700), (152, 782),
                                (112, 818), (52, 812), (24, 762), (18, 700)])
    p.fill(s, "Fill_Skin_Shade", [(125, 600), (165, 630), (165, 790), (112, 820), (138, 700)])
    for y in (668, 718, 766):
        p.line(l, "Line_Dark", [(20, y - 2), (55, y + 3), (92, y + 2)])
    p.shape(b, l, "Fill_Skin", [(72, 612), (98, 566), (138, 560), (164, 592), (156, 640), (114, 652), (82, 642)])
    p.fill(s, "Fill_Skin_Shade", [(140, 600), (168, 600), (160, 650), (130, 650)], curve=False)


# Big round head, shaded on the far side and under the cap brim.
def draw_head(p):
    b, s, l = p.part("Head")
    p.shape(b, l, "Fill_Skin", [(172, 296), (150, 420), (168, 530), (228, 610), (320, 648), (420, 628),
                                (500, 560), (540, 460), (530, 340), (470, 262), (330, 232), (222, 250)])
    p.fill(s, "Fill_Skin_Shade", [(470, 262), (530, 340), (540, 460), (500, 560), (420, 628), (330, 648),
                                  (410, 598), (472, 520), (500, 420), (494, 322)])
    p.fill(s, "Fill_Skin_Shade", [(130, 200), (260, 205), (380, 245), (480, 305), (580, 390), (580, 455),
                                  (478, 372), (372, 308), (258, 278), (130, 276)])
    p.fill(s, "Fill_Skin_Shade", [(240, 600), (320, 618), (420, 600), (420, 660), (240, 660)])


# Heavy-lidded eyes, angled brows, open mouth with teeth and tongue, stubble.
def draw_face(p):
    b, s, l = p.part("Face")
    for cx, cy, look in ((232, 372, -8), (326, 394, -8)):
        p.fill(b, "Fill_Eye_Pupil", circle(cx, cy, 50))
        p.fill(b, "Fill_Eye_White", circle(cx, cy, 39))
        p.fill(b, "Fill_Eye_Pupil", circle(cx + look, cy + 8, 11, n=24))
        h = -4  # lid edge, a little above the eye centre
        lid = [(cx + 41 * math.cos(math.radians(a)), cy - 41 * math.sin(math.radians(a))) for a in range(8, 173, 8)]
        edge = math.sqrt(41 * 41 - h * h)
        p.fill(b, "Fill_Skin_Shade", [(cx + edge, cy + h)] + lid + [(cx - edge, cy + h)], curve=False)
        p.line(b, "Line_Dark", [(cx - 44, cy + h + 2), (cx, cy + h - 1), (cx + 44, cy + h + 2)])
    p.shape(b, l, "Fill_Hair", [(160, 312), (205, 288), (270, 296), (266, 316), (212, 310), (166, 330)])
    p.shape(b, l, "Fill_Hair", [(296, 322), (348, 318), (392, 344), (382, 362), (342, 340), (300, 340)])

    mouth = [(208, 506), (275, 490), (346, 512), (326, 560), (268, 578), (228, 552)]
    p.fill(b, "Fill_Mouth", mouth)
    p.fill(b, "Fill_Eye_White", [(214, 506), (275, 492), (342, 512), (336, 526), (276, 510), (222, 520)])
    p.fill(b, "Fill_Tongue", [(242, 556), (282, 536), (322, 552), (302, 570), (264, 576)])
    p.line(b, "Line_Dark", mouth, closed=True)

    rng = random.Random(7)
    spots = [(rng.uniform(215, 440), rng.uniform(585, 632)) for _ in range(16)]
    spots += [(rng.uniform(410, 490), rng.uniform(470, 570)) for _ in range(8)]
    spots += [(rng.uniform(180, 210), rng.uniform(470, 540)) for _ in range(4)]
    for x, y in spots:
        if 200 < x < 355 and 480 < y < 590:
            continue
        a = math.radians(rng.uniform(55, 80))
        p.line(l, "Line_Detail", [(x, y), (x + 9 * math.cos(a), y + 9 * math.sin(a))], curve=False)


# Spiky fringe poking out under the brim over the forehead.
def draw_fringe(p):
    b, s, l = p.part("Fringe")
    p.shape(b, l, "Fill_Hair", [(118, 222), (285, 236), (268, 284), (242, 260), (224, 316), (202, 266),
                                (178, 306), (166, 264), (136, 296), (140, 252)], curve=False)
    p.fill(s, "Fill_Hair_Shade", [(110, 215), (290, 225), (290, 252), (110, 245)], curve=False)


# Blue cap crown with the leaf badge.
def draw_crown(p):
    b, s, l = p.part("Crown")
    p.shape(b, l, "Fill_Cap_Blue", [(205, 255), (235, 170), (310, 100), (420, 60), (545, 68), (645, 130),
                                    (705, 230), (715, 345), (690, 440), (645, 482), (560, 400), (440, 320),
                                    (320, 262)])
    p.fill(s, "Fill_Cap_Blue_Shade", [(560, 95), (650, 130), (710, 230), (720, 345), (692, 445), (645, 485),
                                      (600, 440), (652, 330), (632, 210)])
    p.fill(s, "Fill_Cap_Blue_Hi", [(285, 130), (380, 82), (480, 70), (420, 96), (315, 150)])

    cx, cy = 462, 175
    p.shape(l, l, "Fill_Eye_White", circle(cx, cy, 44))
    for angle, length in ((-78, 20), (-52, 27), (-26, 33), (0, 38), (26, 33), (52, 27), (78, 20)):
        a = math.radians(angle - 90)
        d = (math.cos(a), math.sin(a))
        n = (-d[1], d[0])
        root = (cx, cy + 18)
        tip = (root[0] + d[0] * length, root[1] + d[1] * length)
        mid = (root[0] + d[0] * length * 0.5, root[1] + d[1] * length * 0.5)
        w = 2.4 + length * 0.12
        p.fill(l, "Fill_Nug_3", [root, (mid[0] + n[0] * w, mid[1] + n[1] * w), tip,
                                 (mid[0] - n[0] * w, mid[1] - n[1] * w)])
    p.line(l, "Line_NugDetail", [(cx, cy + 18), (cx, cy + 32)], curve=False)


# Red brim sweeping across the front, with a hard highlight on top and shade along the underside.
def draw_brim(p):
    b, s, l = p.part("Brim")
    brim = [(8, 172), (90, 130), (200, 115), (305, 132), (410, 185), (505, 255), (585, 330), (645, 420),
            (664, 478), (620, 490), (560, 415), (470, 335), (370, 275), (260, 240), (150, 235), (60, 250),
            (12, 230)]
    p.shape(b, l, "Fill_Cap_Red", brim)
    p.fill(s, "Fill_Cap_Red_Shade", [(0, 210), (60, 228), (150, 214), (260, 218), (370, 252), (470, 312),
                                     (560, 392), (628, 466), (640, 520), (560, 450), (460, 370), (360, 300),
                                     (255, 270), (150, 266), (60, 280), (0, 260)], curve=False)
    p.fill(s, "Fill_Cap_Hi", [(60, 152), (150, 130), (260, 130), (360, 165), (460, 222), (550, 295),
                              (612, 372), (560, 322), (452, 246), (350, 192), (250, 154), (150, 152)])
    p.line(l, "Line_Detail", [(40, 200), (150, 180), (265, 186), (380, 228), (480, 296), (566, 375),
                              (630, 455)])


# The nug: bumpy silhouette, many shaded buds clipped inside it, orange pistils and trichome dots on top.
def draw_nug(p):
    rng = random.Random(11)
    cx, cy, rx, ry = 698, 768, 322, 282
    b, s, l = p.part("Nug")
    outline = circle(cx, cy, rx, ry, n=168, bumps=14, amp=0.055, phase=0.4, jitter=0.012, rng=rng)
    p.fill(b, "Fill_Nug_4", outline, curve=False)

    # Buds on a jittered grid so the whole nug is covered, drawn top row first so lower buds overlap upper ones.
    buds = []
    y = cy - ry * 0.95
    row = 0
    while y < cy + ry * 1.05:
        half = rx * math.sqrt(max(0.0, 1 - ((y - cy) / ry) ** 2))
        x = cx - half + (row % 2) * 45 + rng.uniform(-20, 10)
        while x < cx + half + 10:
            buds.append((x + rng.uniform(-22, 22), y + rng.uniform(-16, 16), rng.uniform(56, 98)))
            x += rng.uniform(80, 110)
        y += 72
        row += 1
    for bx, by, r in buds:
        p.fill(s, "Fill_Nug_3", circle(bx, by, r, n=40, bumps=rng.choice((5, 6, 7)), amp=0.09,
                                       phase=rng.uniform(0, 6), jitter=0.03, rng=rng), curve=False)
        p.fill(s, "Fill_Nug_2", circle(bx - 0.12 * r, by - 0.16 * r, 0.78 * r, n=36, bumps=5, amp=0.08,
                                       phase=rng.uniform(0, 6), jitter=0.03, rng=rng), curve=False)
        if rng.random() < 0.6:
            p.fill(s, "Fill_Nug_1", circle(bx - 0.36 * r, by - 0.4 * r, 0.26 * r, 0.16 * r, n=20), curve=False)
        arc = [(bx + 0.97 * r * math.cos(math.radians(a)), by + 0.97 * r * math.sin(math.radians(a)))
               for a in range(-10, 131, 14)]
        p.line(s, "Line_NugDetail", arc, curve=False)
    p.line(l, "Line_Dark", outline, closed=True, curve=False)

    d = p.layer("Nug_Detail", mask=b)
    for _ in range(16):
        a = rng.uniform(0, 2 * math.pi)
        k = math.sqrt(rng.uniform(0, 1)) * 0.8
        tx, ty = cx + rx * k * math.cos(a), cy + ry * k * math.sin(a)
        for _ in range(9):
            p.fill(d, "Fill_Trich", circle(tx + rng.gauss(0, 14), ty + rng.gauss(0, 10), rng.uniform(2.5, 4.2), n=10),
                   curve=False)
    for _ in range(22):
        a = rng.uniform(0, 2 * math.pi)
        k = math.sqrt(rng.uniform(0, 1)) * 0.9
        x0, y0 = cx + rx * k * math.cos(a), cy + ry * k * math.sin(a)
        h = rng.uniform(0, 2 * math.pi)
        bend = rng.choice((-1, 1)) * 0.6
        pts = [(x0 + math.cos(h + bend * t) * 34 * t, y0 + math.sin(h + bend * t) * 34 * t) for t in (0, 0.35, 0.7, 1)]
        p.line(d, "Line_Pistil", pts, radius=4.2)


def main():
    scene = base.empty_scene()
    base.setup_render(scene)
    base.add_camera(scene)
    base.add_reference(scene)
    base.STACK = [("Background", ["BG_Dark", "BG_Rays"])]
    base.MASKS = {}
    ob = base.make_icon(scene)
    p = Painter(ob.data)

    p.begin_group("Character")
    draw_shirt(p)
    draw_hair_back(p)
    draw_neck(p)
    draw_collar(p)
    draw_arm(p)
    draw_fist(p)
    draw_head(p)
    draw_face(p)
    draw_fringe(p)
    draw_crown(p)
    draw_brim(p)
    p.begin_group("Nug")
    draw_nug(p)

    base.report(ob)
    bpy.ops.wm.save_as_mainfile(filepath=base.BLEND, relative_remap=True)
    scene.render.filepath = PNG
    pct = next((int(a.split("=")[1]) for a in sys.argv if a.startswith("--pct=")), 100)
    scene.render.resolution_percentage = pct
    bpy.ops.render.render(write_still=True)
    print("RENDERED", PNG)


main()
