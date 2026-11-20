# Builds the empty working scene for the app icon: camera, locked reference, layer stack, materials, modifiers and the
# sunburst background. The drawing itself is done by hand in Blender afterwards.
# Run headless:  blender -b --python art/icon/make_icon_scene.py -- [--force]
# Writes art/icon/dusty_icon.blend next to this script. It refuses to overwrite an existing file (that file holds hand
# drawing) unless --force is given.
import bpy
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
BLEND = os.path.join(HERE, "dusty_icon.blend")
REFERENCE = os.path.join(REPO, "resources", "art-reference", "icon-reference.jpg")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

FRAME = 10.0           # orthographic scale: the camera sees -5..5 on X and Z
LINE_RADIUS = 0.030    # main outline radius in scene units (about 12 px wide at 2048)
DETAIL_RADIUS = 0.013  # stubble, inner nug lines, lock partings
RAYS = 16              # light rays in the sunburst

# Materials: name -> (stroke colour or None, fill colour or None), sRGB hex. Picked from the reference, with the
# hair pushed to copper and the shirt to crimson so the two stay apart at small sizes.
PALETTE = {
    "Line_Dark":        ("3A1F12", None),
    "Line_Detail":      ("7A4A2A", None),
    "Line_NugDetail":   ("2F4A0E", None),
    "Line_Pistil":      ("E08A1E", None),
    "Fill_BG_Dark":     (None, "5E8C2A"),
    "Fill_BG_Light":    (None, "79A63A"),
    "Fill_Skin":        (None, "F3C498"),
    "Fill_Skin_Shade":  (None, "D9966A"),
    "Fill_Eye_White":   (None, "FBEFDD"),
    "Fill_Eye_Pupil":   (None, "452B1A"),
    "Fill_Mouth":       (None, "6B2A22"),
    "Fill_Tongue":      (None, "D9786A"),
    "Fill_Hair":        (None, "D2642A"),
    "Fill_Hair_Shade":  (None, "A2441A"),
    "Fill_Hair_Hi":     (None, "F08A45"),
    "Fill_Shirt":       (None, "B3172A"),
    "Fill_Shirt_Shade": (None, "861020"),
    "Fill_Shirt_Dark":  (None, "5C0914"),
    "Fill_Cap_Red":     (None, "D4202A"),
    "Fill_Cap_Red_Shade": (None, "8E1218"),
    "Fill_Cap_Hi":      (None, "F2655A"),
    "Fill_Cap_Blue":    (None, "1A6FD0"),
    "Fill_Cap_Blue_Shade": (None, "0E4A93"),
    "Fill_Cap_Blue_Hi": (None, "4F9BEA"),
    "Fill_Nug_1":       (None, "A8D04A"),
    "Fill_Nug_2":       (None, "82AE33"),
    "Fill_Nug_3":       (None, "5E8420"),
    "Fill_Nug_4":       (None, "3F5A12"),
    "Fill_Pistil":      (None, "E08A1E"),
    "Fill_Trich":       (None, "E8E27A"),
}

# Layer stack, back to front: (group, [layers back to front]).
STACK = [
    ("Background", ["BG_Dark", "BG_Rays"]),
    ("Character", ["Hair_Back_Fill", "Hair_Back_Shade", "Hair_Back_Line",
                   "Char_Fill", "Char_Shade", "Char_Line", "Char_Detail"]),
    ("Nug", ["Nug_Fill", "Nug_Shade", "Nug_Line", "Nug_Detail"]),
]

# Shade layers are clipped to the fill under them, so shadows can be drawn loosely past the edges.
MASKS = {"Hair_Back_Shade": "Hair_Back_Fill", "Char_Shade": "Char_Fill", "Nug_Shade": "Nug_Fill"}

# Detail layers get layer pass 2 (handy for filters); line weight itself follows the material (see tag_line_materials).
DETAIL_LAYERS = {"Char_Detail", "Nug_Detail"}


# Converts an sRGB hex colour to Blender's linear RGBA.
def linear(hex_colour, alpha=1.0):
    def channel(v):
        v /= 255.0
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    return (channel(int(hex_colour[0:2], 16)), channel(int(hex_colour[2:4], 16)),
            channel(int(hex_colour[4:6], 16)), alpha)


# Clears the startup file down to an empty scene.
def empty_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene


# Square 2048 output, Standard colour (AgX would wash out the saturated reds and greens), PNG out.
def setup_render(scene):
    r = scene.render
    r.resolution_x = r.resolution_y = 2048
    r.resolution_percentage = 100
    r.engine = "BLENDER_EEVEE"
    r.film_transparent = False
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGBA"
    r.image_settings.color_depth = "8"
    r.filepath = "//icon_2048.png"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.display_settings.display_device = "sRGB"
    scene.grease_pencil_settings.antialias_threshold_render = 0.5
    scene.frame_start = scene.frame_end = scene.frame_current = 1


# Orthographic camera at -Y looking at the X-Z drawing plane.
def add_camera(scene):
    cam = bpy.data.cameras.new("Camera")
    cam.type = "ORTHO"
    cam.ortho_scale = FRAME
    ob = bpy.data.objects.new("Camera", cam)
    ob.location = (0.0, -10.0, 0.0)
    ob.rotation_euler = (math.radians(90), 0.0, 0.0)
    scene.collection.objects.link(ob)
    scene.camera = ob


# The reference picture as an image Empty just behind the drawing plane: faded, unselectable, never rendered.
def add_reference(scene):
    if not os.path.exists(REFERENCE):
        print("No reference at", REFERENCE, "- skipped")
        return
    img = bpy.data.images.load(REFERENCE)
    ob = bpy.data.objects.new("Reference", None)
    ob.empty_display_type = "IMAGE"
    ob.data = img
    ob.empty_display_size = FRAME
    ob.empty_image_offset = (-0.5, -0.5)
    ob.empty_image_depth = "BACK"
    ob.use_empty_image_alpha = True
    ob.color[3] = 0.4
    ob.location = (0.0, 1.0, 0.0)
    ob.rotation_euler = (math.radians(90), 0.0, 0.0)
    ob.hide_select = True
    ob.hide_render = True
    scene.collection.objects.link(ob)


# One Grease Pencil material per palette entry, in palette order. The unused half (line of a fill material, fill of
# a line material) is also made fully transparent: in 5.3 the Stroke/Fill checkboxes alone did not stop a fill
# material from drawing a black line.
def make_materials(gp):
    for name, (stroke, fill) in PALETTE.items():
        mat = bpy.data.materials.new(name)
        bpy.data.materials.create_gpencil_data(mat)
        g = mat.grease_pencil
        g.show_stroke = stroke is not None
        g.show_fill = fill is not None
        g.color = linear(stroke) if stroke else linear(fill, 0.0)
        g.fill_color = linear(fill) if fill else linear(stroke, 0.0)
        gp.materials.append(mat)


# Builds the layer groups and layers back to front, lights off, masks on the shade layers.
def make_layers(gp):
    layers = {}
    for group_name, names in STACK:
        group = gp.layer_groups.new(group_name)
        for name in names:
            layer = gp.layers.new(name, set_active=True, layer_group=group)
            layer.use_lights = False
            layer.pass_index = 2 if name in DETAIL_LAYERS else 1
            layer.frames.new(1)
            layers[name] = layer
    # Put each group's layers in the listed order regardless of where new() inserted them.
    for _, names in STACK:
        for name in names:
            gp.layers.move_top(layers[name])
    for shade, fill in MASKS.items():
        layers[shade].use_masks = True
        layers[shade].mask_layers.add(layers[fill])
    return layers


# Index of a material by name on the Grease Pencil data.
def mat_index(gp, name):
    return [m.name for m in gp.materials].index(name)


# Adds one closed fill-only shape from (x, z) points to a layer's drawing.
# Blender 5.x only fills strokes with a non-zero fill_id (strokes sharing an id form one fill), so each shape gets
# its own id.
def add_shape(gp, layer, points, material):
    drawing = layer.frames[0].drawing
    drawing.add_strokes([len(points)])
    stroke = drawing.strokes[len(drawing.strokes) - 1]
    stroke.cyclic = True
    add_shape.next_id = getattr(add_shape, "next_id", 0) + 1
    stroke.fill_id = add_shape.next_id
    stroke.hide_stroke = True
    stroke.material_index = mat_index(gp, material)
    for p, (x, z) in zip(stroke.points, points):
        p.position = (x, 0.0, z)
        p.radius = 0.01


# Dark full-frame square plus one light wedge that an Array modifier spins around the frame centre.
def make_sunburst(scene, gp, ob, layers):
    big = 12.0
    add_shape(gp, layers["BG_Dark"], [(-big, -big), (big, -big), (big, big), (-big, big)], "Fill_BG_Dark")
    half = math.radians(360.0 / RAYS / 4.0)  # light ray = half of one ray pitch
    wedge = [(0.0, 0.0), (big * math.sin(-half), big * math.cos(-half)), (big * math.sin(half), big * math.cos(half))]
    add_shape(gp, layers["BG_Rays"], wedge, "Fill_BG_Light")

    pivot = bpy.data.objects.new("Ray_Pivot", None)
    pivot.empty_display_type = "SPHERE"
    pivot.empty_display_size = 0.3
    pivot.rotation_euler = (0.0, math.radians(360.0 / RAYS), 0.0)
    pivot.hide_render = True
    scene.collection.objects.link(pivot)

    arr = ob.modifiers.new("Sunburst", "GREASE_PENCIL_ARRAY")
    arr.count = RAYS
    arr.use_relative_offset = False
    arr.use_object_offset = True
    arr.offset_object = pivot
    arr.tree_node_filter = "BG_Rays"


# Line weight follows the material: main outlines (material pass 1) get one uniform weight, detail lines (pass 2)
# a thinner one. Anything else (fills, Line_Pistil) keeps the radius it was drawn with.
def make_line_modifiers(ob):
    main = ob.modifiers.new("Outline_Weight", "GREASE_PENCIL_THICKNESS")
    main.use_uniform_thickness = True
    main.thickness = LINE_RADIUS
    main.use_material_pass_filter = True
    main.material_pass_filter = 1

    detail = ob.modifiers.new("Detail_Weight", "GREASE_PENCIL_THICKNESS")
    detail.use_uniform_thickness = True
    detail.thickness = DETAIL_RADIUS
    detail.use_material_pass_filter = True
    detail.material_pass_filter = 2

    smooth = ob.modifiers.new("Line_Smooth", "GREASE_PENCIL_SMOOTH")
    smooth.factor = 0.4
    smooth.step = 2
    smooth.use_material_pass_filter = True
    smooth.material_pass_filter = 1


# Material pass index picks the line weight: 1 main outline, 2 detail, 0 untouched.
def tag_line_materials(gp):
    for m in gp.materials:
        m.pass_index = {"Line_Dark": 1, "Line_Detail": 2, "Line_NugDetail": 2}.get(m.name, 0)


# Creates the Grease Pencil object and everything on it.
def make_icon(scene):
    gp = bpy.data.grease_pencils.new("Icon")
    ob = bpy.data.objects.new("Icon", gp)
    scene.collection.objects.link(ob)
    make_materials(gp)
    tag_line_materials(gp)
    layers = make_layers(gp)
    make_sunburst(scene, gp, ob, layers)
    make_line_modifiers(ob)
    if "Char_Line" in layers:
        gp.layers.active = layers["Char_Line"]
    ob.active_material_index = 0  # Line_Dark
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    return ob


# Prints the final layer order so a run can be checked without opening Blender.
def report(ob):
    print("LAYERS (front first):", [l.name for l in reversed(ob.data.layers)])
    print("MODIFIERS:", [m.name for m in ob.modifiers])


def main():
    if os.path.exists(BLEND) and "--force" not in ARGS:
        print("REFUSED:", BLEND, "exists (it may hold hand drawing). Pass --force to rebuild it.")
        return
    scene = empty_scene()
    setup_render(scene)
    add_camera(scene)
    add_reference(scene)
    ob = make_icon(scene)
    report(ob)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND, relative_remap=True)
    print("SAVED", BLEND)


# Only build when run as the script; draw_icon.py imports this file for its helpers and settings.
if __name__ == "__main__":
    main()
