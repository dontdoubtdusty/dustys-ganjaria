using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// Placeholder drawing for the pack station: the shelf, containers, lid, seal strip, mat, IN tray and the sealed bag.
static partial class Art
{
    static readonly Color Foil = new(54, 110, 78, 255);
    static readonly Color FoilLight = new(84, 146, 104, 255);
    static readonly Color LidColor = new(46, 44, 52, 255);
    static readonly Color Rubber = new(52, 58, 56, 255);

    // Draws the container shelf: a board for the small ones, the table for the large ones, and size labels.
    public static void DrawShelf(float left, float right, float shelfY, float floorY)
    {
        Raylib.DrawRectangleRec(new Rectangle(left, shelfY, right - left, 14), WoodLight);
        Raylib.DrawRectangleRec(new Rectangle(left + 8, shelfY + 14, 10, 30), Wood);
        Raylib.DrawRectangleRec(new Rectangle(right - 18, shelfY + 14, 10, 30), Wood);
        float cx = (left + right) / 2;
        LabelCentered("SMALL: 1 g or 3.5 g", cx, shelfY - 168, 20, Faint);
        LabelCentered("LARGE: 7 g", cx, shelfY + 22, 20, Faint);
    }

    // Draws the inside of a container, behind whatever is in it.
    public static void DrawContainerBack(Rectangle r, Container c)
    {
        if (c.Kind == ContainerKind.Jar) Raylib.DrawRectangleRec(r, new Color(150, 190, 210, 45));
        else Raylib.DrawRectangleRec(r, new Color(30, 50, 40, 255));
    }

    // Draws a container's walls in front of what is in it. A jar is glass with a threaded neck; a pouch is a foil
    // front that hides the lower part of the bag (closed, it covers it all). hover outlines it. bare leaves off
    // the kind and size printed on the front (where a strain label goes).
    public static void DrawContainerFront(Rectangle r, Container c, bool hover, bool closed, bool bare = false)
    {
        float l = r.X, rt = r.X + r.Width, t = r.Y, b = r.Y + r.Height;
        if (hover) Raylib.DrawRectangleRoundedLinesEx(Grow(r, 10), 0.15f, 8, 3, Accent);
        string size = c.Large ? "L" : "S";
        // The name shrinks to fit a small drawing (the hand-over).
        int text = (int)Math.Clamp(r.Width / 5.2f, 12f, 22f);

        if (c.Kind == ContainerKind.Jar)
        {
            Raylib.DrawLineEx(new(l, t), new(l, b), 6, Glass);
            Raylib.DrawLineEx(new(rt, t), new(rt, b), 6, Glass);
            Raylib.DrawLineEx(new(l - 3, b - 3), new(rt + 3, b - 3), 6, Glass);
            // The neck's threads.
            for (int i = 0; i < 2; i++)
                Raylib.DrawLineEx(new(l + 2, t + 6 + i * 7), new(rt - 2, t + 9 + i * 7), 2, new Color(200, 225, 235, 200));
            if (bare) return;
            var band = new Rectangle(l + 6, t + r.Height * 0.45f, r.Width - 12, text + 8);
            Raylib.DrawRectangleRec(band, Paper);
            LabelCentered($"JAR {size}", l + r.Width / 2, band.Y + 4, text, Ink);
            return;
        }

        float top = closed ? t : t + 34;
        var front = new Rectangle(l - 4, top, r.Width + 8, b - top);
        Raylib.DrawRectangleRounded(front, 0.08f, 6, Foil);
        Raylib.DrawRectangleRec(new Rectangle(front.X, front.Y, 8, front.Height), FoilLight);
        Raylib.DrawRectangleLinesEx(front, 2, new Color(30, 70, 46, 255));
        if (!bare) LabelCentered($"POUCH {size}", l + r.Width / 2, t + r.Height * 0.55f, text, new Color(220, 240, 220, 255));
        if (!closed)
        {
            // The open mouth: the back panel's top edge behind the bag.
            Raylib.DrawLineEx(new(l - 4, t), new(rt + 4, t), 3, FoilLight);
        }
    }

    // Draws a jar lid at its centre. slide is how far it has turned, in pixels of mouse travel: the ridges slide
    // exactly that far, so they move with the mouse. done (0-1) sinks it a little as it tightens. hot lightens it
    // while held; shake (0-1) is how close it is to cross-threading: it reddens. arrow (0-1, 0 for none) is the
    // opacity of a right-pointing arrow above it, so it reads as "drag sideways".
    public static void DrawLid(Vector2 center, float width, float slide, float done, bool hot, float shake, float arrow)
    {
        center.Y += 6f * Math.Clamp(done, 0f, 1f);
        var r = new Rectangle(center.X - width / 2, center.Y - 12, width, 24);
        var fill = hot ? new Color(80, 78, 90, 255) : LidColor;
        fill = Raylib.ColorLerp(fill, new Color(200, 70, 60, 255), shake);
        Raylib.DrawRectangleRounded(r, 0.4f, 6, fill);
        const float gap = 12f;
        float shift = slide % gap;
        for (float x = r.X + 6 + shift - gap; x < r.X + r.Width - 4; x += gap)
        {
            if (x < r.X + 4) continue;
            Raylib.DrawLineEx(new(x, r.Y + 4), new(x, r.Y + r.Height - 4), 2, new Color(110, 108, 120, 255));
        }
        if (arrow <= 0) return;
        var col = new Color((byte)160, (byte)150, (byte)170, (byte)(170 * arrow));
        float y = r.Y - 22, half = MathF.Min(50f, width / 2 - 10);
        Raylib.DrawLineEx(new(center.X - half, y), new(center.X + half - 4, y), 4, col);
        var tip = new Vector2(center.X + half + 12, y);
        Raylib.DrawTriangle(tip + new Vector2(-16, -10), tip + new Vector2(-16, 10), tip, col);
    }

    static readonly Color SealCold = new(170, 170, 176, 255);
    static readonly Color SealHot = new(250, 150, 60, 255);
    static readonly Color SealDone = new(120, 84, 52, 255);
    static readonly Color SealBurnt = new(48, 36, 32, 255);
    static readonly Color GaugeGreen = new(90, 190, 100, 255);

    // Draws a pouch's heat-seal strip. While heating, the sealer's jaws clamp it and it glows with the heat. Once
    // sealed it shows how it went: under the green some segments stay open (gaps), in the green it is evenly crimped,
    // over the green it is scorched dark. heat is 0-1 of the gauge; low and high are the green band.
    public static void DrawHeatSeal(float left, float right, float y, float heat, bool heating, bool done)
    {
        const int n = 8;
        float w = (right - left) / n;
        float low = Scoring.SealGreenLow, high = Scoring.SealGreenHigh;
        for (int i = 0; i < n; i++)
        {
            var seg = new Rectangle(left + i * w, y - 7, w, 14);
            Color fill;
            bool crimp = false;
            if (!done)
                fill = heat <= high ? Raylib.ColorLerp(SealCold, SealHot, Math.Clamp(heat / low, 0f, 1f))
                                    : Raylib.ColorLerp(SealHot, new Color(220, 60, 40, 255), (heat - high) / (1f - high));
            else if (heat < low)
            {
                // A scattered share of the segments, as many as the heat reached, sealed; the rest are gaps.
                crimp = i * 0.618f % 1f < heat / low;
                fill = crimp ? SealDone : SealCold;
            }
            else
            {
                crimp = true;
                fill = heat > high ? Raylib.ColorLerp(SealDone, SealBurnt, Math.Clamp((heat - high) / 0.12f, 0f, 1f)) : SealDone;
            }
            Raylib.DrawRectangleRec(seg, fill);
            if (crimp)
                for (float x = seg.X + 3; x < seg.X + seg.Width; x += 5)
                    Raylib.DrawLineEx(new(x, seg.Y + 2), new(x, seg.Y + seg.Height - 2), 1, new Color(70, 48, 30, 255));
        }
        if (!heating) return;
        // The sealer's chrome jaws, top and bottom, pressed onto the strip, their heating bars glowing with the heat.
        var hot = heat <= high ? Raylib.ColorLerp(SealCold, SealHot, Math.Clamp(heat / low, 0f, 1f))
                               : Raylib.ColorLerp(SealHot, GaugeRed, (heat - high) / (1f - high));
        float jl = left - 14, jw = right - left + 28;
        Raylib.DrawRectangleRounded(new Rectangle(jl + 3, y - 20, jw, 44), 0.3f, 6, new Color(0, 0, 0, 60));
        foreach (float top in new[] { y - 25f, y + 10f })
        {
            Raylib.DrawRectangleRounded(new Rectangle(jl, top, jw, 15), 0.5f, 6, ChromeShade);
            Raylib.DrawRectangleRounded(new Rectangle(jl, top, jw, 11), 0.5f, 6, Chrome);
            Raylib.DrawLineEx(new(jl + 8, top + 2.5f), new(jl + jw - 8, top + 2.5f), 1.5f, new Color(255, 255, 255, 150));
        }
        Raylib.DrawRectangleRec(new Rectangle(left - 10, y - 11, right - left + 20, 3), hot);
        Raylib.DrawRectangleRec(new Rectangle(left - 10, y + 8, right - left + 20, 3), hot);
        // The heat's glow, added on top so it brightens what is under it.
        Raylib.BeginBlendMode(BlendMode.Additive);
        Raylib.DrawRectangleRounded(new Rectangle(left - 12, y - 14, right - left + 24, 28), 0.6f, 8,
                                    Raylib.ColorAlpha(hot, 0.3f * Math.Clamp(heat / low, 0f, 1f)));
        Raylib.EndBlendMode();
    }

    static readonly Color Chrome = new(212, 216, 224, 255);
    static readonly Color ChromeShade = new(128, 132, 144, 255);
    static readonly Color Enamel = new(240, 233, 214, 255);
    static readonly Color GaugeRed = new(214, 70, 52, 255);
    static readonly Color NotchRed = new(218, 88, 48, 255);
    static readonly Color Mustard = new(222, 172, 64, 255);
    static readonly Color Cherry = new(186, 70, 60, 255);
    static readonly Color GaugeInk = new(50, 45, 40, 160);

    // Draws the heat gauge beside a pouch, a thermometer in a chrome case: a glass tube rising from a bulb on a cream
    // face, with the green band painted behind it (a dark line marks its perfect middle, a red zone runs above it),
    // ticks up both sides, and a pointer outside at the heat (0-1). The liquid is orange below the band, green in it
    // and red over it. hot puts a pulsing warm glow round the case while the sealer is held.
    public static void DrawHeatGauge(Rectangle r, float heat, float low, float high, bool hot)
    {
        float h = Math.Clamp(heat, 0f, 1f);
        bool inBand = h >= low && h <= high;
        LabelCentered("HEAT", r.X + r.Width / 2, r.Y - 30, 20, Faint);
        if (hot)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin((float)Raylib.GetTime() * 10f);
            Raylib.BeginBlendMode(BlendMode.Additive);
            Raylib.DrawRectangleRounded(Grow(r, 12), 0.7f, 12, Raylib.ColorAlpha(SealHot, 0.12f + 0.08f * pulse));
            Raylib.DrawRectangleRounded(Grow(r, 6), 0.7f, 12, Raylib.ColorAlpha(SealHot, 0.18f + 0.1f * pulse));
            Raylib.EndBlendMode();
        }
        // The case: shadow, chrome rim lit from the top left, cream face.
        Raylib.DrawRectangleRounded(new Rectangle(r.X + 4, r.Y + 6, r.Width, r.Height), 0.6f, 12, new Color(0, 0, 0, 80));
        Raylib.DrawRectangleRounded(r, 0.6f, 12, ChromeShade);
        Raylib.DrawRectangleRounded(new Rectangle(r.X, r.Y, r.Width - 3, r.Height - 3), 0.6f, 12, Chrome);
        var face = Grow(r, -6);
        Raylib.DrawRectangleRounded(face, 0.6f, 12, Enamel);

        float cx = r.X + r.Width / 2;
        float bulbY = face.Y + face.Height - 22, bottom = bulbY - 12, top = face.Y + 18;
        float Y(float v) => bottom - (bottom - top) * v;

        // The zones painted on the face: red over the band, the band itself, its perfect middle.
        float zl = face.X + 5, zw = face.Width - 10;
        Raylib.DrawRectangleRec(new Rectangle(zl, Y(1f), zw, Y(high) - Y(1f)), new Color(214, 70, 52, 60));
        var band = inBand ? new Color(120, 214, 112, 255) : new Color(140, 200, 128, 230);
        Raylib.DrawRectangleRec(new Rectangle(zl, Y(high), zw, Y(low) - Y(high)), band);
        float mid = Y((low + high) / 2);
        Raylib.DrawLineEx(new(zl, mid), new(zl + zw, mid), 2, new Color(40, 110, 50, 255));
        for (int i = 0; i <= 10; i++)
        {
            float ty = Y(i / 10f), len = i % 5 == 0 ? 9 : 5;
            Raylib.DrawLineEx(new(zl, ty), new(zl + len, ty), 2, GaugeInk);
            Raylib.DrawLineEx(new(zl + zw - len, ty), new(zl + zw, ty), 2, GaugeInk);
        }

        // The glass tube, its liquid, the bulb and the glass's shine.
        var tube = new Rectangle(cx - 5, top - 6, 10, bottom - top + 14);
        Raylib.DrawRectangleRounded(Grow(tube, 2), 1f, 8, new Color(150, 150, 160, 255));
        Raylib.DrawRectangleRounded(tube, 1f, 8, new Color(250, 250, 252, 255));
        var liquid = h > high ? GaugeRed : inBand ? GaugeGreen : SealHot;
        if (h > 0) Raylib.DrawRectangleRec(new Rectangle(cx - 3, Y(h), 6, bulbY - Y(h)), liquid);
        Raylib.DrawCircleV(new(cx, bulbY), 13, ChromeShade);
        Raylib.DrawCircleV(new(cx, bulbY), 11, h > 0 ? liquid : new Color(205, 150, 110, 255));
        Raylib.DrawCircleV(new(cx - 4, bulbY - 4), 3.5f, new Color(255, 255, 255, 150));
        Raylib.DrawLineEx(new(cx - 3.5f, top - 2), new(cx - 3.5f, bottom), 1.5f, new Color(255, 255, 255, 150));

        // The pointer outside the case, in the liquid's colour.
        var tip = new Vector2(r.X + r.Width + 3, Y(h));
        Raylib.DrawTriangle(tip, tip + new Vector2(16, 10), tip + new Vector2(16, -10), Text);
        Raylib.DrawTriangle(tip + new Vector2(4, 0), tip + new Vector2(14, 6), tip + new Vector2(14, -6), liquid);
    }

    // Draws the overhead rail the sealed bag slides along at the pack station: an I-beam hung from two rods, with
    // bolts along it and bumpers at the ends. When the bag hangs from it (hook, the top of the bag), the trolley rides
    // the beam on two wheels that turn as it moves, with a cable down to the bag's clip.
    public static void DrawDropRail(float left, float right, float y, Vector2? hook)
    {
        foreach (float x in new[] { left + 34, right - 34 })
        {
            Raylib.DrawRectangleRec(new Rectangle(x - 2, y - 42, 4, 34), ChromeShade);
            Raylib.DrawRectangleRounded(new Rectangle(x - 15, y - 48, 30, 8), 0.5f, 4, ChromeShade);
        }
        Raylib.DrawRectangleRec(new Rectangle(left + 2, y + 2, right - left, 12), new Color(0, 0, 0, 50));
        Raylib.DrawRectangleRec(new Rectangle(left, y - 9, right - left, 4), Chrome);
        Raylib.DrawRectangleRec(new Rectangle(left + 3, y - 5, right - left - 6, 9), ChromeShade);
        Raylib.DrawRectangleRec(new Rectangle(left, y + 4, right - left, 5), Chrome);
        for (float x = left + 26; x < right - 20; x += 46) Raylib.DrawCircleV(new(x, y - 0.5f), 2f, new Color(84, 86, 96, 255));
        Raylib.DrawRectangleRounded(new Rectangle(left - 5, y - 13, 11, 26), 0.4f, 4, Mustard);
        Raylib.DrawRectangleRounded(new Rectangle(right - 6, y - 13, 11, 26), 0.4f, 4, Mustard);
        if (hook is not { } h) return;

        // The wheels on top of the beam, their spokes turned by how far the trolley has run.
        float spin = h.X / 9f;
        foreach (float wx in new[] { h.X - 13, h.X + 13 })
        {
            var w = new Vector2(wx, y - 17);
            Raylib.DrawCircleV(w, 8, new Color(70, 72, 82, 255));
            Raylib.DrawCircleV(w, 6, Chrome);
            for (int i = 0; i < 3; i++)
            {
                float a = spin + i * MathF.PI / 3f;
                var d = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 5.5f;
                Raylib.DrawLineEx(w - d, w + d, 1.5f, ChromeShade);
            }
            Raylib.DrawCircleV(w, 2, new Color(70, 72, 82, 255));
        }
        // The trolley's side plate over the beam, then the cable and the clip.
        var plate = new Rectangle(h.X - 23, y - 12, 46, 28);
        Raylib.DrawRectangleRounded(plate, 0.35f, 6, Cherry);
        Raylib.DrawLineEx(new(plate.X + 6, plate.Y + 2.5f), new(plate.X + plate.Width - 6, plate.Y + 2.5f), 2, new Color(230, 130, 115, 255));
        Raylib.DrawCircleV(new(h.X - 14, y + 8), 2.5f, new Color(110, 40, 34, 255));
        Raylib.DrawCircleV(new(h.X + 14, y + 8), 2.5f, new Color(110, 40, 34, 255));
        Raylib.DrawLineEx(new(h.X, y + 16), new(h.X, h.Y - 10), 2.5f, new Color(200, 196, 210, 255));
        Raylib.DrawRectangleRounded(new Rectangle(h.X - 6, h.Y - 13, 12, 9), 0.4f, 4, ChromeShade);
        Raylib.DrawRing(h + new Vector2(0, 1), 3.5f, 6f, 0, 360, 20, Chrome);
    }

    // Draws the jar lid's notch dial above the lid, like a safe dial: a chrome bezel with the mark fixed at the top
    // (its window either side in green), a cream face that turns with the notch (angle, degrees clockwise from the
    // mark) and its ticks, and a dark hub with a lamp per turn made. flash (0-1) lights the window and the lamps after
    // a good click.
    public static void DrawNotchDial(Vector2 c, float radius, float angle, float window, int turns, int total, float flash)
    {
        // Shadow and bezel, its upper half lit. Raylib measures ring angles from 3 o'clock, clockwise on screen, so
        // 180-360 is the top half and the mark at 12 o'clock is -90.
        Raylib.DrawCircleV(c + new Vector2(4, 6), radius + 13, new Color(0, 0, 0, 80));
        Raylib.DrawCircleV(c, radius + 13, ChromeShade);
        for (int s = 0; s < 24; s++)
        {
            float a0 = s * 15f, lightness = 0.5f - 0.5f * MathF.Sin((a0 + 7.5f) * MathF.PI / 180f);
            Raylib.DrawRing(c, radius + 3, radius + 13, a0, a0 + 15.5f, 4, Raylib.ColorLerp(ChromeShade, Chrome, lightness));
        }
        Raylib.DrawRing(c, radius + 10, radius + 12, 205, 335, 32, new Color(255, 255, 255, 140));
        var green = Raylib.ColorLerp(GaugeGreen, new Color(230, 255, 210, 255), flash);
        Raylib.DrawRing(c, radius + 2, radius + 13, -90 - window, -90 + window, 16, green);

        // The face and its ticks, turning with the notch.
        Raylib.DrawCircleV(c, radius + 3, new Color(70, 68, 78, 255));
        Raylib.DrawCircleV(c, radius + 1, Enamel);
        for (int i = 1; i < 24; i++)
        {
            float ta = (angle + i * 15f) * MathF.PI / 180f;
            var td = new Vector2(MathF.Sin(ta), -MathF.Cos(ta));
            bool major = i % 6 == 0;
            Raylib.DrawLineEx(c + td * (radius - (major ? 11 : 7)), c + td * (radius - 2), major ? 2.5f : 1.5f, GaugeInk);
        }
        float a = angle * MathF.PI / 180f;
        var dir = new Vector2(MathF.Sin(a), -MathF.Cos(a));
        Raylib.DrawLineEx(c + dir * (radius - 20), c + dir * (radius - 1), 7, NotchRed);
        Raylib.DrawCircleV(c + dir * (radius - 20), 3.5f, NotchRed);

        // The hub and its lamps.
        Raylib.DrawCircleV(c, 21, ChromeShade);
        Raylib.DrawCircleV(c, 19, LidColor);
        var lit = Raylib.ColorLerp(GaugeGreen, new Color(230, 255, 210, 255), flash);
        for (int i = 0; i < total; i++)
        {
            var pip = c + new Vector2((i - (total - 1) / 2f) * 11, 0);
            if (i < turns) Raylib.DrawCircleV(pip, 6.5f, Raylib.ColorAlpha(lit, 0.3f));
            Raylib.DrawCircleV(pip, 4, i < turns ? lit : new Color(84, 82, 94, 255));
        }

        // The fixed mark at the top, over the face's edge.
        var m = c + new Vector2(0, -radius + 5);
        Raylib.DrawTriangle(m, m + new Vector2(-8, -16), m + new Vector2(8, -16), Ink);
    }

    // Draws the packing mat; it glows while a container is carried over it, and says what it is while empty.
    public static void DrawMat(float centerX, float floorY, bool glow, bool empty, string emptyText = "PACK HERE")
    {
        var r = new Rectangle(centerX - 130, floorY - 6, 260, 10);
        if (glow) Raylib.DrawRectangleRounded(Grow(r, 10), 0.4f, 8, new Color(120, 200, 110, 80));
        Raylib.DrawRectangleRounded(r, 0.5f, 6, Rubber);
        if (empty) LabelCentered(emptyText, centerX, floorY - 40, 22, Faint);
    }

    // Draws the IN tray's floor behind the bag, and its low sides and label in front.
    public static void DrawTrayBack(Rectangle r) => Raylib.DrawRectangleRec(r, new Color(70, 64, 80, 255));

    public static void DrawTrayFront(Rectangle r)
    {
        var edge = new Color(120, 112, 136, 255);
        Raylib.DrawLineEx(new(r.X, r.Y), new(r.X, r.Y + r.Height), 6, edge);
        Raylib.DrawLineEx(new(r.X + r.Width, r.Y), new(r.X + r.Width, r.Y + r.Height), 6, edge);
        LabelCentered("IN", r.X + r.Width / 2, r.Y - 34, 28, Faint);
    }

    // Draws a sealed, pressed-flat bag of flower; lifted casts a shadow. With a fill it shows the very nugs sealed at
    // the weigh station, as they lay, the bag scaled to fit r (sitting on its bottom edge, centred); without one, a
    // few stand-in buds in the strain's colours.
    public static void DrawSealedBag(Rectangle r, bool lifted, Strain strain, BagFill? fill = null)
    {
        if (fill != null)
        {
            float s = MathF.Min(r.Width / fill.Size.X, r.Height / fill.Size.Y);
            var b = new Rectangle(r.X + (r.Width - fill.Size.X * s) / 2, r.Y + r.Height - fill.Size.Y * s,
                                  fill.Size.X * s, fill.Size.Y * s);
            if (lifted) Raylib.DrawRectangleRec(new Rectangle(b.X + 6, b.Y + 8, b.Width, b.Height), new Color(0, 0, 0, 70));
            Raylib.DrawRectangleRec(b, new Color(230, 236, 242, 150));
            foreach (var (bud, offset) in fill.Nugs)
                DrawBudAt(bud, new Vector2(b.X, b.Y) + offset * s, Bag.SealedScale * s);
            Raylib.DrawRectangleLinesEx(b, MathF.Max(1.5f, 3 * s), Plastic);
            Raylib.DrawLineEx(new(b.X, b.Y + 3 * s), new(b.X + b.Width, b.Y + 3 * s), MathF.Max(2f, 6 * s), new Color(110, 150, 210, 255));
            return;
        }
        if (lifted) Raylib.DrawRectangleRec(new Rectangle(r.X + 6, r.Y + 8, r.Width, r.Height), new Color(0, 0, 0, 70));
        Raylib.DrawRectangleRec(r, new Color(230, 236, 242, 150));
        var shades = strain.Shades;
        int buds = Math.Max(2, (int)(r.Width / 28));
        for (int i = 0; i < buds; i++)
        {
            float x = r.X + r.Width * (i + 0.5f) / buds;
            float rad = MathF.Min(r.Width / buds * 0.5f, 16);
            Raylib.DrawCircleV(new(x, r.Y + r.Height - rad - 4), rad, shades[i % shades.Length]);
            if (r.Height > 80) Raylib.DrawCircleV(new(x + rad * 0.5f, r.Y + r.Height - rad * 2.6f - 4), rad * 0.8f, shades[(i + 1) % shades.Length]);
        }
        Raylib.DrawRectangleLinesEx(r, 3, Plastic);
        Raylib.DrawLineEx(new(r.X, r.Y + 3), new(r.X + r.Width, r.Y + 3), 6, new Color(110, 150, 210, 255));
    }

    // Draws a closed, packed container (label station and hand-over): the container, the flower showing through a
    // jar's glass (the order's own nugs when its fill is known), and the lid on a jar. bare leaves its front
    // unprinted for a strain label.
    public static void DrawPackedContainer(Rectangle r, Container c, Strain strain, bool bare, BagFill? fill = null)
    {
        DrawContainerBack(r, c);
        if (c.Kind == ContainerKind.Jar)
            DrawSealedBag(new Rectangle(r.X + 6, r.Y + r.Height * 0.35f, r.Width - 12, r.Height * 0.65f - 4), false, strain, fill);
        DrawContainerFront(r, c, false, true, bare);
        float scale = r.Width / 116f;
        if (c.Kind == ContainerKind.Jar) DrawLid(new Vector2(r.X + r.Width / 2, r.Y - 10 * scale), r.Width + 14 * scale, 0, 0, false, 0, 0);
        else Raylib.DrawRectangleRec(new Rectangle(r.X - 4, r.Y, r.Width + 8, 10 * scale), new Color(120, 84, 52, 255));
    }
}
