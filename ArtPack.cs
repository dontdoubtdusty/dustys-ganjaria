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

    // Draws a pouch's heat-seal strip, one segment each: cold grey, glowing orange as it heats, dark and crimped
    // once sealed. hot outlines it while it is being pressed.
    public static void DrawSealStrip(float left, float right, float y, float[] heat, bool[] sealedSegs, float hold, bool hot)
    {
        int n = heat.Length;
        float w = (right - left) / n;
        for (int i = 0; i < n; i++)
        {
            var seg = new Rectangle(left + i * w + 1, y - 7, w - 2, 14);
            Color fill;
            if (sealedSegs[i]) fill = new Color(120, 84, 52, 255);
            else fill = Raylib.ColorLerp(new Color(170, 170, 176, 255), new Color(250, 150, 60, 255), Math.Clamp(heat[i] / hold, 0f, 1f));
            Raylib.DrawRectangleRec(seg, fill);
            if (sealedSegs[i])
                for (float x = seg.X + 3; x < seg.X + seg.Width; x += 5)
                    Raylib.DrawLineEx(new(x, seg.Y + 2), new(x, seg.Y + seg.Height - 2), 1, new Color(80, 54, 34, 255));
        }
        if (hot) Raylib.DrawRectangleLinesEx(new Rectangle(left - 3, y - 10, right - left + 6, 20), 2, Accent);
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

    // Draws a sealed, pressed-flat bag of flower in a strain's colours; lifted casts a shadow.
    public static void DrawSealedBag(Rectangle r, bool lifted, Strain strain)
    {
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
    // jar's glass, and the lid on a jar. bare leaves its front unprinted for a strain label.
    public static void DrawPackedContainer(Rectangle r, Container c, Strain strain, bool bare)
    {
        DrawContainerBack(r, c);
        if (c.Kind == ContainerKind.Jar)
            DrawSealedBag(new Rectangle(r.X + 6, r.Y + r.Height * 0.35f, r.Width - 12, r.Height * 0.65f - 4), false, strain);
        DrawContainerFront(r, c, false, true, bare);
        float scale = r.Width / 116f;
        if (c.Kind == ContainerKind.Jar) DrawLid(new Vector2(r.X + r.Width / 2, r.Y - 10 * scale), r.Width + 14 * scale, 0, 0, false, 0, 0);
        else Raylib.DrawRectangleRec(new Rectangle(r.X - 4, r.Y, r.Width + 8, 10 * scale), new Color(120, 84, 52, 255));
    }
}
