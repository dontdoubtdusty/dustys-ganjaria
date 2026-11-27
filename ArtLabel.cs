using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// Placeholder drawing for the label station: the rack, the label pads, the guide marks, the strain labels, the
// label's backing, curl and air bubbles, and the hand that slaps it on.
static partial class Art
{
    // Draws the board the label pads hang on, with its title.
    public static void DrawLabelRack(Rectangle r)
    {
        Raylib.DrawRectangleRounded(r, 0.06f, 8, new Color(84, 64, 50, 255));
        Raylib.DrawRectangleRoundedLinesEx(r, 0.06f, 8, 3, WoodLight);
        LabelCentered("LABELS", r.X + r.Width / 2, r.Y - 34, 28, Faint);
    }

    // Draws a pad of one strain's labels: a few stacked labels, the top one face up. hover outlines it.
    public static void DrawLabelPad(Rectangle r, Strain s, bool hover)
    {
        for (int i = 2; i >= 1; i--)
            Raylib.DrawRectangleRec(new Rectangle(r.X + i * 4, r.Y + i * 4, r.Width, r.Height), new Color(200, 194, 178, 255));
        DrawStrainLabel(new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2), new Vector2(r.Width, r.Height), 0, s, false, false, 0, false);
        if (hover) Raylib.DrawRectangleLinesEx(Grow(r, 5), 3, Accent);
    }

    // Draws corner marks where the label should go on the container front (white), or, given a colour, where the
    // held label would land.
    public static void DrawLabelGuide(Vector2 center, Vector2 size, Color? color = null)
    {
        var col = color ?? new Color(250, 250, 250, 120);
        float l = center.X - size.X / 2, r = center.X + size.X / 2, t = center.Y - size.Y / 2, b = center.Y + size.Y / 2;
        const float arm = 14;
        foreach (var (x, y, dx, dy) in new[] { (l, t, 1f, 1f), (r, t, -1f, 1f), (l, b, 1f, -1f), (r, b, -1f, -1f) })
        {
            Raylib.DrawLineEx(new(x, y), new(x + arm * dx, y), 3, col);
            Raylib.DrawLineEx(new(x, y), new(x, y + arm * dy), 3, col);
        }
    }

    // Draws a strain label centred on a point, turned by degrees (clockwise): a paper label with a band in the
    // strain's colour and its name. lifted (carried) casts a deep shadow and loose (stuck on, not pressed) a thin
    // one; hover outlines it. press (0-1) fills a ring while it is being pressed down. net, when given, is the
    // weight printed at the bottom (deli labels carry the weight the scale showed).
    public static void DrawStrainLabel(Vector2 center, Vector2 size, float degrees, Strain s, bool lifted, bool loose,
                                       float press, bool hover, string? net = null)
    {
        float a = degrees * MathF.PI / 180f;
        Vector2 Turn(Vector2 v) => new(v.X * MathF.Cos(a) - v.Y * MathF.Sin(a), v.X * MathF.Sin(a) + v.Y * MathF.Cos(a));
        void Box(Vector2 offset, Vector2 dims, Color c) =>
            Raylib.DrawRectanglePro(new Rectangle(center.X + Turn(offset).X, center.Y + Turn(offset).Y, dims.X, dims.Y),
                                    dims / 2, degrees, c);

        if (lifted || loose)
        {
            var shadow = lifted ? new Vector2(6, 9) : new Vector2(2, 3);
            Raylib.DrawRectanglePro(new Rectangle(center.X + shadow.X, center.Y + shadow.Y, size.X, size.Y), size / 2, degrees,
                                    new Color(0, 0, 0, lifted ? 80 : 50));
        }
        Box(Vector2.Zero, size, Paper);
        float band = size.Y * 0.32f;
        Box(new Vector2(0, -size.Y / 2 + band / 2), new Vector2(size.X, band), s.Tag);

        int small = (int)Math.Clamp(band * 0.62f, 7f, 20f);
        LabelTurned("DUSTY'S GANJARIA", center, new Vector2(0, -size.Y / 2 + band / 2), degrees, small, new Color(250, 248, 240, 255));
        int big = (int)Math.Clamp(size.Y * (net == null ? 0.36f : 0.3f), 8f, 30f);
        while (big > 8 && Measure(s.Name, big) > size.X * 0.9f) big--;
        float nameY = net == null ? band / 2 : band / 2 - size.Y * 0.1f;
        LabelTurned(s.Name, center, new Vector2(0, nameY), degrees, big, Ink);
        if (net != null)
        {
            int tiny = (int)Math.Clamp(size.Y * 0.2f, 7f, 18f);
            LabelTurned($"NET WT {net}", center, new Vector2(0, size.Y / 2 - size.Y * 0.15f), degrees, tiny, Ink);
        }

        if (hover || press > 0)
        {
            var half = size / 2;
            var corners = new[] { new Vector2(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y), new(-half.X, half.Y) }
                .Select(v => center + Turn(v)).ToArray();
            for (int i = 0; i < 4; i++) Raylib.DrawLineEx(corners[i], corners[(i + 1) % 4], 3, Accent);
        }
        if (press > 0)
        {
            Raylib.DrawRing(center, 14, 21, 0, 360, 24, new Color(0, 0, 0, 90));
            Raylib.DrawRing(center, 14, 21, -90, -90 + 360 * Math.Clamp(press, 0f, 1f), 24, Accent);
        }
    }

    static readonly Color BackingColor = new(232, 234, 236, 255);
    static readonly Color LabelBack = new(226, 218, 200, 255);
    static readonly Color HandLine = new(120, 82, 60, 255);

    // Draws a filled triangle whichever way its corners wind (Raylib only fills counter-clockwise ones).
    static void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
    {
        Raylib.DrawTriangle(a, b, c, col);
        Raylib.DrawTriangle(a, c, b, col);
    }

    // Draws a label's glossy backing sheet centred on a point, turned by degrees, with its peel tab sticking out of
    // the lower left corner (ringed when hovered).
    public static void DrawLabelBacking(Vector2 center, Vector2 size, float degrees, bool hover)
    {
        float a = degrees * MathF.PI / 180f;
        Vector2 Turn(Vector2 v) => new(v.X * MathF.Cos(a) - v.Y * MathF.Sin(a), v.X * MathF.Sin(a) + v.Y * MathF.Cos(a));
        Raylib.DrawRectanglePro(new Rectangle(center.X, center.Y, size.X, size.Y), size / 2, degrees, BackingColor);
        var tab = center + Turn(new Vector2(-size.X / 2 - 4, size.Y / 2 - 2));
        var tabSize = new Vector2(40, 24);
        Raylib.DrawRectanglePro(new Rectangle(tab.X, tab.Y, tabSize.X, tabSize.Y), tabSize / 2, degrees - 20,
                                new Color(205, 208, 214, 255));
        Raylib.DrawRectanglePro(new Rectangle(tab.X, tab.Y, tabSize.X - 12, 3), new Vector2((tabSize.X - 12) / 2, 1.5f),
                                degrees - 20, new Color(140, 140, 152, 255));
        if (hover) Raylib.DrawRing(tab, 24, 27, 0, 360, 32, Accent);
    }

    // Draws a peeled label curling up: its top left and bottom right corners fold toward the viewer, more as curl
    // goes from 0 to 1.
    public static void DrawLabelCurl(Vector2 center, Vector2 size, float curl)
    {
        if (curl <= 0) return;
        float leg = curl * size.Y * 0.4f;
        DrawFold(center + new Vector2(-size.X / 2, -size.Y / 2), new Vector2(1, 0), new Vector2(0, 1), leg);
        DrawFold(center + size / 2, new Vector2(-1, 0), new Vector2(0, -1), leg);
    }

    // Draws a stuck-on label's lifted bottom right corner, flatter as left goes from 1 to 0.
    public static void DrawLabelCorner(Vector2 center, Vector2 size, float left) =>
        DrawFold(center + size / 2, new Vector2(-1, 0), new Vector2(0, -1), size.Y * 0.38f * left);

    // Draws one folded corner: the gap it leaves in shadow and the flap of plain label back folded over it. ux and
    // uy point from the corner along the label's two edges.
    static void DrawFold(Vector2 corner, Vector2 ux, Vector2 uy, float leg)
    {
        if (leg < 1) return;
        var a = corner + ux * leg;
        var b = corner + uy * leg;
        var d = corner + (ux + uy) * leg;
        Tri(corner, a, b, new Color(0, 0, 0, 110));
        var drop = new Vector2(2, 3);
        Tri(a + drop, b + drop, d + drop, new Color(0, 0, 0, 60));
        Tri(a, b, d, LabelBack);
        Raylib.DrawLineEx(a, b, 1.5f, new Color(0, 0, 0, 50));
    }

    // Draws an air bubble under a label: a pale raised spot with a glint, shrinking as left goes from 1 to 0.
    public static void DrawLabelBubble(Vector2 at, float radius, float left)
    {
        float r = radius * (0.45f + 0.55f * left);
        Raylib.DrawCircleV(at + new Vector2(1.5f, 2.5f), r, new Color(0, 0, 0, 70));
        Raylib.DrawCircleV(at, r, new Color(255, 255, 252, 255));
        Raylib.DrawRing(at, r - 1.5f, r, 0, 360, 24, new Color(150, 140, 120, 200));
        Raylib.DrawCircleV(at - new Vector2(r * 0.35f), r * 0.3f, new Color(255, 255, 255, 255));
    }

    // Draws the back of an open right hand, fingers up and thumb out to the left, its palm centred on a point.
    // scale 1 is about 50 px across the palm; alpha (0-1) fades it.
    public static void DrawSlapHand(Vector2 palm, float scale, float alpha)
    {
        if (alpha <= 0) return;
        float s = scale;
        // Fingers and thumb as (base, tip, width) in palm units.
        var digits = new (Vector2 Base, Vector2 Tip, float W)[]
        {
            (new(-17, -14), new(-21, -50), 11f), (new(-6, -16), new(-7, -56), 11.5f),
            (new(6, -16), new(7, -54), 11.5f), (new(17, -14), new(21, -44), 10f),
            (new(-20, 10), new(-42, -10), 13f),
        };
        alpha = Math.Clamp(alpha, 0f, 1f);
        var line = Raylib.ColorAlpha(HandLine, alpha);
        var skin = Raylib.ColorAlpha(Skin, alpha);
        // The outline first (everything a little fatter in the line colour), then the skin over it.
        for (int pass = 0; pass < 2; pass++)
        {
            var col = pass == 0 ? line : skin;
            float grow = pass == 0 ? 2.5f : 0f;
            var body = new Rectangle(palm.X - 25 * s - grow, palm.Y - 23 * s - grow, 50 * s + 2 * grow, 46 * s + 2 * grow);
            Raylib.DrawRectangleRounded(body, 0.45f, 8, col);
            foreach (var (b, t, w) in digits)
            {
                var from = palm + b * s;
                var to = palm + t * s;
                float width = w * s + 2 * grow;
                Raylib.DrawLineEx(from, to, width, col);
                Raylib.DrawCircleV(from, width / 2, col);
                Raylib.DrawCircleV(to, width / 2, col);
            }
        }
        // Knuckle creases.
        var crease = Raylib.ColorAlpha(HandLine, alpha * 0.45f);
        foreach (float x in new[] { -17f, -6f, 6f, 17f })
            Raylib.DrawLineEx(palm + new Vector2(x - 3, -12) * s, palm + new Vector2(x + 3, -12) * s, 1.5f, crease);
    }
}
