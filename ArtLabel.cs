using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// Placeholder drawing for the label station: the rack, the label pads, the guide marks and the strain labels.
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

    // Draws corner marks where the label should go on the container front.
    public static void DrawLabelGuide(Vector2 center, Vector2 size)
    {
        var col = new Color(250, 250, 250, 120);
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
}
