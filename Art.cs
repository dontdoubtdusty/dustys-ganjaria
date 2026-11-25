using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// Every placeholder drawing and all text live here, so each one can be swapped for a PNG sprite in one place.
static partial class Art
{
    public static readonly Color Background = new(38, 34, 44, 255);
    public static readonly Color Panel = new(58, 52, 66, 255);
    public static readonly Color PanelEdge = new(92, 84, 104, 255);
    public static readonly Color Text = new(236, 230, 240, 255);
    public static readonly Color Faint = new(160, 150, 170, 255);
    public static readonly Color Accent = new(120, 200, 110, 255);
    public static readonly Color Warn = new(240, 160, 80, 255);

    static readonly Color Glass = new(170, 205, 220, 220);
    static readonly Color Plastic = new(225, 232, 240, 230);

    // ---------- Text ----------

    // The game font, loaded once at a large size and scaled down, so text stays smooth at every size.
    const string FontPath = "resources/fonts/LilitaOne-Regular.ttf";
    const int FontBakeSize = 64;
    static Font font;
    static bool fontLoaded;

    // Loads the game font; falls back to the Raylib default if the file is missing.
    public static void LoadFont()
    {
        string path = Path.Combine(AppContext.BaseDirectory, FontPath);
        if (!File.Exists(path)) { font = Raylib.GetFontDefault(); return; }
        font = Raylib.LoadFontEx(path, FontBakeSize, null, 0);
        // Bilinear filtering: smooth edges when the baked glyphs are scaled.
        Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);
        fontLoaded = true;
    }

    // Frees the game font.
    public static void UnloadFont()
    {
        if (fontLoaded) Raylib.UnloadFont(font);
    }

    // Spacing between letters, scaled with the text size.
    static float Spacing(int size) => size / 20f;

    // Width of a text at a size, in the game font.
    public static float Measure(string text, int size) =>
        Raylib.MeasureTextEx(font, text, size, Spacing(size)).X;

    // Draws text at a float position.
    public static void Label(string text, float x, float y, int size, Color color) =>
        Raylib.DrawTextEx(font, text, new Vector2(MathF.Round(x), MathF.Round(y)), size, Spacing(size), color);

    // Draws text centred on x.
    public static void LabelCentered(string text, float centerX, float y, int size, Color color) =>
        Label(text, centerX - Measure(text, size) / 2f, y, size, color);

    // Draws text turned with a shape: centred on a point offset from pivot (in the shape's own, unturned
    // coordinates), the whole thing turned by degrees (clockwise) about pivot.
    public static void LabelTurned(string text, Vector2 pivot, Vector2 offset, float degrees, int size, Color color)
    {
        var dims = Raylib.MeasureTextEx(font, text, size, Spacing(size));
        var origin = new Vector2(dims.X / 2 - offset.X, dims.Y / 2 - offset.Y);
        Raylib.DrawTextPro(font, text, pivot, origin, degrees, size, Spacing(size), color);
    }

    // ---------- Station pieces ----------

    // Draws the table top the station stands on.
    public static void DrawTable(float floorY)
    {
        Raylib.DrawRectangle(0, (int)floorY, Program.Width, Program.Height - (int)floorY, new Color(110, 80, 58, 255));
        Raylib.DrawRectangle(0, (int)floorY, Program.Width, 4, new Color(140, 104, 76, 255));
    }

    // Draws a bud from its lobes, turned by its angle. Hovered, it lifts a little with an outline; held, it casts a
    // shadow; after a hard landing it squashes flat and wide for a moment.
    public static void DrawBud(Bud bud, bool highlight, bool lifted)
    {
        float k = bud.Scale * (highlight ? 1.08f : 1f);
        if (lifted || highlight)
            Raylib.DrawCircleV(bud.Pos + new Vector2(5, 7), bud.Radius * k, new Color(0, 0, 0, lifted ? 70 : 45));

        float cos = MathF.Cos(bud.Angle), sin = MathF.Sin(bud.Angle);
        float q = bud.Squash * bud.Squash;
        var squash = new Vector2(1f + 0.25f * q, 1f - 0.3f * q);
        // Squash about the bottom of the bud, so it flattens onto what it hit.
        var center = bud.Pos + new Vector2(0, bud.Radius * k * 0.3f * q);
        Vector2 Turn(Vector2 v) => new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos) * k * squash;

        foreach (var lobe in bud.Lobes)
            Raylib.DrawCircleV(center + Turn(lobe.Offset), lobe.Radius * k * (1f + 0.1f * q), bud.Strain.Shades[lobe.Shade]);
        if (!bud.IsShake)
        {
            // A few orange specks so it reads as flower rather than a pea.
            foreach (var lobe in bud.Lobes)
                Raylib.DrawCircleV(center + Turn(lobe.Offset * 1.3f), 1.6f, bud.Strain.Hair);
        }

        if (highlight)
            Raylib.DrawCircleLines((int)bud.Pos.X, (int)bud.Pos.Y, bud.Radius * k + 3, Text);
    }

    // Draws the glass behind the jar's contents.
    public static void DrawJarBack(Rectangle rect) =>
        Raylib.DrawRectangleRec(rect, new Color(150, 190, 210, 35));

    // Draws the jar's walls in front of its contents, with its strain's name on a tag above it.
    public static void DrawJarFront(Rectangle rect, Strain strain)
    {
        float l = rect.X, r = rect.X + rect.Width, t = rect.Y, b = rect.Y + rect.Height;
        Raylib.DrawLineEx(new(l, t), new(l, b), 5, Glass);
        Raylib.DrawLineEx(new(l - 2, b), new(r + 2, b), 5, Glass);
        Raylib.DrawLineEx(new(r, t), new(r, b), 5, Glass);
        DrawStrainTag(strain, l + rect.Width / 2, t - 36, 20);
    }

    // Draws a strain's name on a pill in its label colour, centred on x with its top at y.
    public static void DrawStrainTag(Strain strain, float centerX, float y, int size)
    {
        float w = Measure(strain.Name, size) + size;
        var pill = new Rectangle(centerX - w / 2, y, w, size + 8);
        Raylib.DrawRectangleRounded(pill, 0.5f, 8, strain.Tag);
        LabelCentered(strain.Name, centerX, y + 4, size, new Color(250, 248, 240, 255));
    }

    // Draws the scale: body, plate, and a dial with a green band around the target and a needle.
    // glow (0-1) brightens the band and lights a halo around the dial while the needle sits in the green.
    public static void DrawScale(Rectangle body, Rectangle plate, Vector2 dial, float radius,
                                 float grams, float target, float band, float max, float glow)
    {
        Raylib.DrawRectangleRounded(body, 0.06f, 8, new Color(200, 200, 205, 255));
        Raylib.DrawRectangleRec(plate, new Color(150, 152, 160, 255));

        if (glow > 0.01f)
            Raylib.DrawCircleSector(dial, radius + 22, 180, 360, 40, new Color(120, 220, 110, (int)(110 * glow)));
        // Dial face: the top half of a circle, left = empty, right = full scale.
        Raylib.DrawCircleSector(dial, radius + 8, 180, 360, 40, new Color(245, 242, 232, 255));
        float Deg(float g) => 180f + 180f * Math.Clamp(g / max, 0f, 1f);
        // No ticket: no green band and no target label.
        if (target > 0)
        {
            var bandColor = new Color(90 + (int)(40 * glow), 200 + (int)(40 * glow), 90 + (int)(30 * glow), 255);
            float grow = 4f * glow;
            Raylib.DrawRing(dial, radius - 34 - grow, radius + grow, Deg(target - band), Deg(target + band), 16, bandColor);
        }

        for (int i = 0; i <= 8; i++)
        {
            float a = (180f + 22.5f * i) * MathF.PI / 180f;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            Raylib.DrawLineEx(dial + dir * (radius - 6), dial + dir * (radius + 4), 2, new Color(90, 90, 95, 255));
        }

        float n = Deg(grams) * MathF.PI / 180f;
        var tip = dial + new Vector2(MathF.Cos(n), MathF.Sin(n)) * (radius - 4);
        Raylib.DrawLineEx(dial, tip, 4, new Color(200, 50, 50, 255));
        Raylib.DrawCircleV(dial, 7, new Color(60, 60, 65, 255));

        if (target > 0)
            LabelCentered($"{target:0.0#} g", dial.X, dial.Y - radius - 42, 26, new Color(60, 60, 65, 255));
    }

    // Draws the stack of empty bags; it glows when it is the next thing to grab.
    public static void DrawBagStack(Rectangle rect, bool glow)
    {
        if (glow) Raylib.DrawRectangleRounded(Grow(rect, 6), 0.2f, 8, new Color(120, 200, 110, 60));
        for (int i = 2; i >= 0; i--)
        {
            var r = new Rectangle(rect.X + i * 6, rect.Y + i * 6, rect.Width - 12, rect.Height - 12);
            Raylib.DrawRectangleRec(r, new Color(230, 236, 242, 120));
            Raylib.DrawRectangleLinesEx(r, 2, Plastic);
            Raylib.DrawLineEx(new(r.X, r.Y + 6), new(r.X + r.Width, r.Y + 6), 3, new Color(110, 150, 210, 255));
        }
        LabelCentered("BAGS", rect.X + rect.Width / 2, rect.Y - 26, 20, Faint);
    }

    // Draws the see-through back of the bag, behind its contents.
    public static void DrawBagBack(Rectangle rect) =>
        Raylib.DrawRectangleRec(rect, new Color(230, 236, 242, 60));

    // Draws the bag's edges and its zip in front of the contents. Open teeth show ahead of the tab, closed ones
    // behind it are a solid strip. shake (0-1) is how close the zip is to slipping: the tab jitters by jitter and
    // turns red.
    public static void DrawBagFront(Rectangle rect, float zip, bool sealedBag, bool zipHot, float shake,
                                    Vector2 jitter, float toothGap)
    {
        float l = rect.X, r = rect.X + rect.Width, t = rect.Y, b = rect.Y + rect.Height;
        Raylib.DrawLineEx(new(l, t), new(l, b), 3, Plastic);
        Raylib.DrawLineEx(new(l, b), new(r, b), 3, Plastic);
        Raylib.DrawLineEx(new(r, t), new(r, b), 3, Plastic);

        var zipColor = new Color(110, 150, 210, 255);
        float zx = l + rect.Width * zip;
        Raylib.DrawLineEx(new(zx, t), new(r, t), 3, new Color(110, 150, 210, 120));
        if (!sealedBag)
        {
            // The open teeth, one tick each.
            for (float x = l + toothGap * MathF.Ceiling((zx - l) / toothGap); x < r - 2; x += toothGap)
                Raylib.DrawLineEx(new(x, t - 3), new(x, t + 3), 2, new Color(110, 150, 210, 170));
        }
        if (zip > 0) Raylib.DrawLineEx(new(l, t), new(zx, t), 7, zipColor);
        if (sealedBag) return;

        var tab = new Rectangle(zx - 9 + jitter.X, t - 12 + jitter.Y, 18, 24);
        var fill = zipHot ? new Color(160, 200, 255, 255) : zipColor;
        fill = Raylib.ColorLerp(fill, new Color(235, 90, 70, 255), shake);
        Raylib.DrawRectangleRounded(tab, 0.4f, 6, fill);
        Raylib.DrawRectangleRoundedLinesEx(tab, 0.4f, 6, 2, new Color(40, 60, 100, 255));
    }

    // Draws the inside of the OUT bin, behind a bag thrown into it; it glows when a bag is ready to throw.
    public static void DrawBinBack(Rectangle rect, bool glow)
    {
        if (glow) Raylib.DrawRectangleRec(Grow(rect, 6), new Color(120, 200, 110, 50));
        Raylib.DrawRectangleRec(rect, new Color(70, 64, 80, 255));
    }

    // Draws the OUT bin's walls and label in front of anything inside it.
    public static void DrawBinFront(Rectangle rect)
    {
        float l = rect.X, r = rect.X + rect.Width, t = rect.Y, b = rect.Y + rect.Height;
        var edge = new Color(120, 112, 136, 255);
        Raylib.DrawLineEx(new(l, t), new(l, b), 6, edge);
        Raylib.DrawLineEx(new(r, t), new(r, b), 6, edge);
        LabelCentered("OUT", l + rect.Width / 2, t - 34, 28, Faint);
    }

    // Draws the station's ticket holder: the ticket being worked on (number, name, weight, patience), or an empty
    // slot that glows while a ticket is being dragged toward it.
    public static void DrawTicket(Rectangle rect, Customer? customer, int number, bool dropGlow)
    {
        if (customer == null)
        {
            if (dropGlow) Raylib.DrawRectangleRec(Grow(rect, 6), new Color(120, 200, 110, 70));
            Raylib.DrawRectangleRec(rect, new Color(245, 240, 225, 40));
            Raylib.DrawRectangleLinesEx(rect, 2, dropGlow ? Accent : PanelEdge);
            LabelCentered("TICKET HERE", rect.X + rect.Width / 2, rect.Y + rect.Height / 2 - 12, 24, dropGlow ? Accent : Faint);
            return;
        }

        Raylib.DrawRectangleRec(rect, Paper);
        Raylib.DrawRectangleLinesEx(rect, 2, new Color(190, 180, 160, 255));
        string title = number > 0 ? $"#{number:000}  {customer.Name.ToUpperInvariant()}" : customer.Name.ToUpperInvariant();
        Label(title, rect.X + 16, rect.Y + 10, 22, Ink);
        Label(customer.ContainerText, rect.X + rect.Width - 16 - Measure(customer.ContainerText, 22), rect.Y + 10, 22,
              new Color(60, 70, 120, 255));
        Label(customer.OrderText, rect.X + 16, rect.Y + 40, 34, Ink);
        DrawStrainTag(customer.Strain, rect.X + 112 + (Measure(customer.Strain.Name, 22) + 22) / 2, rect.Y + 44, 22);
        DrawPatienceBar(new Rectangle(rect.X + 16, rect.Y + rect.Height - 20, rect.Width - 32, 8), customer.PatienceFraction);
    }

    // ---------- Ticket rail ----------

    static readonly Color Paper = new(245, 240, 225, 255);
    static readonly Color Ink = new(50, 45, 40, 255);

    // Draws the rail strip across the top of the screen, with the bar the tickets hang from.
    public static void DrawRail(float height)
    {
        Raylib.DrawRectangle(0, 0, Program.Width, (int)height, new Color(44, 38, 50, 255));
        Raylib.DrawRectangle(0, (int)height - 3, Program.Width, 3, PanelEdge);
        Raylib.DrawRectangle(0, 8, Program.Width, 6, new Color(170, 170, 180, 255));
        Label("TICKETS", Program.Width - 110, height - 30, 18, Faint);
    }

    // Draws one ticket on the rail: number, name, amount and strain, container and the customer's patience; a
    // weighed order gets a green WEIGHED stamp (it goes to Pack next), a packed one a purple PACKED stamp (Label
    // next). A new ticket glows green, a hovered one is outlined, a held one casts a shadow.
    public static void DrawRailTicket(Rectangle r, Ticket t, bool hover, bool lifted)
    {
        if (lifted) Raylib.DrawRectangleRec(new Rectangle(r.X + 6, r.Y + 8, r.Width, r.Height), new Color(0, 0, 0, 90));
        if (t.Fresh > 0) Raylib.DrawRectangleRec(Grow(r, 5), new Color(120, 200, 110, (int)(200 * Math.Clamp(t.Fresh, 0f, 1f))));
        Raylib.DrawRectangleRec(r, Paper);
        Raylib.DrawRectangleLinesEx(r, 2, hover ? Accent : new Color(190, 180, 160, 255));
        // The clip it hangs from.
        Raylib.DrawRectangleRec(new Rectangle(r.X + r.Width / 2 - 12, r.Y - 8, 24, 12), new Color(120, 120, 130, 255));

        Label($"{t.Label}  {t.Customer.Name.ToUpperInvariant()}", r.X + 8, r.Y + 8, 18, Ink);
        Label(t.Customer.OrderText, r.X + 8, r.Y + 30, 20, Ink);
        var strain = t.Customer.Strain;
        float sw = Measure(strain.Short, 14) + 12;
        var chip = new Rectangle(r.X + r.Width - 8 - sw, r.Y + 31, sw, 18);
        Raylib.DrawRectangleRounded(chip, 0.4f, 6, strain.Tag);
        LabelCentered(strain.Short, chip.X + sw / 2, chip.Y + 2, 14, new Color(250, 248, 240, 255));
        Label(t.Customer.ContainerText, r.X + 8, r.Y + 50, 16, new Color(60, 70, 120, 255));
        if (t.Next != Stage.Weigh)
        {
            var (word, ink) = t.Next == Stage.Pack ? ("WEIGHED", new Color(60, 150, 70, 255)) : ("PACKED", new Color(130, 80, 170, 255));
            var stamp = new Rectangle(r.X + r.Width - 74, r.Y + 51, 66, 18);
            Raylib.DrawRectangleRoundedLinesEx(stamp, 0.3f, 4, 2, ink);
            LabelCentered(word, stamp.X + stamp.Width / 2, stamp.Y + 2, 14, ink);
        }
        DrawPatienceBar(new Rectangle(r.X + 8, r.Y + r.Height - 10, r.Width - 16, 5), t.Customer.PatienceFraction);
    }

    // Draws a short message on a dark pill, centred on x.
    public static void DrawBanner(string text, float centerX, float y)
    {
        int size = 24;
        float w = Measure(text, size) + 32;
        Raylib.DrawRectangleRounded(new Rectangle(centerX - w / 2, y - 4, w, size + 10), 0.5f, 8, new Color(30, 26, 34, 230));
        LabelCentered(text, centerX, y, size, Warn);
    }

    // ---------- Shop floor ----------

    // Patience colours: green while there is plenty, orange when it runs low, red when they are about to leave.
    static Color PatienceColor(float fraction) =>
        fraction > 0.5f ? new Color(110, 200, 100, 255) : fraction > 0.25f ? Warn : new Color(225, 80, 70, 255);

    // Draws a patience bar that empties from right to left.
    public static void DrawPatienceBar(Rectangle rect, float fraction)
    {
        Raylib.DrawRectangleRec(rect, new Color(0, 0, 0, 90));
        Raylib.DrawRectangleRec(new Rectangle(rect.X, rect.Y, rect.Width * fraction, rect.Height), PatienceColor(fraction));
    }

    static readonly Color Skin = new(222, 186, 150, 255);
    static readonly Color Wood = new(110, 80, 58, 255);
    static readonly Color WoodLight = new(150, 112, 82, 255);
    static readonly Color Apron = new(60, 120, 70, 255);
    static readonly Color StaffHair = new(70, 50, 40, 255);

    // A person's height in the shop view at scale 1, feet to the centre of the head, and the head radius.
    const float HeadHeight = 232, HeadRadius = 32;

    // The centre of the head of someone standing at feet, at a scale.
    public static Vector2 HeadOf(Vector2 feet, float scale) => feet - new Vector2(0, HeadHeight * scale);

    // Draws the shop room side-on: the back wall from under the rail down to the back row, then the floor.
    public static void DrawShopRoom(float top, float floorY)
    {
        Raylib.DrawRectangle(0, (int)top, Program.Width, (int)(floorY - top), new Color(66, 58, 76, 255));
        Raylib.DrawRectangle(0, (int)floorY - 12, Program.Width, 12, new Color(54, 46, 62, 255));
        Raylib.DrawRectangle(0, (int)floorY, Program.Width, Program.Height - (int)floorY, new Color(96, 78, 66, 255));
        // Floor boards.
        for (float y = floorY + 40; y < Program.Height; y += 52)
            Raylib.DrawLineEx(new(0, y), new(Program.Width, y), 2, new Color(84, 68, 58, 255));
    }

    // Draws the front door on the back wall; customers appear in it and leave through it.
    public static void DrawDoor(Vector2 feet)
    {
        var frame = new Rectangle(feet.X - 60, feet.Y - 250, 120, 250);
        Raylib.DrawRectangleRec(Grow(frame, 8), WoodLight);
        Raylib.DrawRectangleRec(frame, new Color(40, 34, 44, 255));
        Raylib.DrawRectangleRec(new Rectangle(frame.X + 18, frame.Y + 20, frame.Width - 36, 100), new Color(110, 150, 170, 120));
        LabelCentered("DOOR", feet.X, frame.Y - 34, 20, Faint);
    }

    // Draws the waiting bench against the back wall, with its sign.
    public static void DrawBench(float left, float right, float floorY)
    {
        float seatY = floorY - 70;
        Raylib.DrawRectangleRec(new Rectangle(left, seatY - 60, right - left, 14), WoodLight);
        Raylib.DrawRectangleRec(new Rectangle(left, seatY, right - left, 14), WoodLight);
        Raylib.DrawRectangleRec(new Rectangle(left + 10, seatY + 14, 10, 56), Wood);
        Raylib.DrawRectangleRec(new Rectangle(right - 20, seatY + 14, 10, 56), Wood);
        LabelCentered("WAITING", (left + right) / 2, floorY - 300, 22, Faint);
    }

    // Draws the budtender standing behind the counter, side-on, facing the line (the counter hides the legs).
    public static void DrawBudtender(Vector2 feet)
    {
        var head = HeadOf(feet, 1f);
        Raylib.DrawRectangleRounded(new Rectangle(feet.X - 40, feet.Y - 205, 80, 140), 0.35f, 8, new Color(70, 70, 80, 255));
        Raylib.DrawRectangleRounded(new Rectangle(feet.X - 36, feet.Y - 175, 60, 110), 0.25f, 8, Apron);
        Raylib.DrawCircleV(head, HeadRadius, Skin);
        Raylib.DrawRing(head, HeadRadius - 6, HeadRadius + 2, 170, 360, 16, StaffHair);
        Raylib.DrawCircleV(head + new Vector2(-14, -4), 4, new Color(40, 34, 30, 255));
        LabelCentered("YOU", head.X, head.Y - 72, 18, Faint);
    }

    // Draws the counter side-on in front of the budtender, with the till on top.
    public static void DrawCounterSide(Rectangle r)
    {
        Raylib.DrawRectangleRec(r, Wood);
        Raylib.DrawRectangleRec(new Rectangle(r.X - 10, r.Y, r.Width + 20, 12), WoodLight);
        var till = new Rectangle(r.X + 60, r.Y - 50, 90, 50);
        Raylib.DrawRectangleRec(till, new Color(70, 70, 80, 255));
        Raylib.DrawRectangleRec(new Rectangle(till.X + 10, till.Y + 8, 46, 18), new Color(120, 200, 110, 255));
    }

    // Draws a placeholder customer standing at feet: shoes, trousers, shirt, a head with a face that follows their
    // patience, and (with showBar) their name and a patience bar above the head.
    public static void DrawCustomer(Customer c, Vector2 feet, float s, bool showBar)
    {
        var head = HeadOf(feet, s);
        float mood = c.Mood ?? c.Now switch
        {
            Customer.State.Leaving => c.Served ? 1f : 0f,
            Customer.State.Ordering => 1f,
            _ => c.PatienceFraction,
        };

        var shoe = new Color(40, 36, 40, 255);
        Raylib.DrawRectangleRounded(new Rectangle(feet.X - 26 * s, feet.Y - 10 * s, 24 * s, 10 * s), 0.5f, 6, shoe);
        Raylib.DrawRectangleRounded(new Rectangle(feet.X + 2 * s, feet.Y - 10 * s, 24 * s, 10 * s), 0.5f, 6, shoe);
        Raylib.DrawRectangleRec(new Rectangle(feet.X - 24 * s, feet.Y - 95 * s, 22 * s, 86 * s), c.Trousers);
        Raylib.DrawRectangleRec(new Rectangle(feet.X + 2 * s, feet.Y - 95 * s, 22 * s, 86 * s), c.Trousers);
        Raylib.DrawRectangleRounded(new Rectangle(feet.X - 40 * s, feet.Y - 205 * s, 80 * s, 120 * s), 0.35f, 8, c.Shirt);

        Raylib.DrawCircleV(head, HeadRadius * s, Skin);
        var eye = new Color(40, 34, 30, 255);
        // Inspecting: eyes turned down toward the bag in their hand, one brow raised, a small sideways "hmm" mouth.
        var look = c.Inspecting ? new Vector2(3, 3) : Vector2.Zero;
        Raylib.DrawCircleV(head + (new Vector2(-11, -6) + look) * s, 4 * s, eye);
        Raylib.DrawCircleV(head + (new Vector2(11, -6) + look) * s, 4 * s, eye);

        var mouth = new Color(90, 40, 40, 255);
        if (c.Inspecting)
        {
            Raylib.DrawLineEx(head + new Vector2(5, -19) * s, head + new Vector2(17, -23) * s, 2.5f * s, eye);
            Raylib.DrawLineEx(head + new Vector2(-4, 15) * s, head + new Vector2(9, 13) * s, 3 * s, mouth);
        }
        else if (mood > 0.5f) Raylib.DrawRing(head + new Vector2(0, 2) * s, 12 * s, 15 * s, 30, 150, 12, mouth);
        else if (mood > 0.25f) Raylib.DrawLineEx(head + new Vector2(-10, 14) * s, head + new Vector2(10, 14) * s, 3 * s, mouth);
        else Raylib.DrawRing(head + new Vector2(0, 28) * s, 12 * s, 15 * s, 210, 330, 12, mouth);

        if (!showBar) return;
        float top = head.Y - HeadRadius * s;
        DrawPatienceBar(new Rectangle(head.X - 34, top - 16, 68, 8), c.PatienceFraction);
        LabelCentered(c.Name, head.X, top - 42, 20, Text);
    }

    // Draws a small speech bubble above a head.
    public static void DrawSpeech(Vector2 head, string text)
    {
        int size = 22;
        float w = Measure(text, size) + 28;
        var rect = new Rectangle(head.X - w / 2, head.Y - 128, w, 40);
        Raylib.DrawRectangleRounded(rect, 0.4f, 8, Paper);
        Raylib.DrawTriangle(new(head.X - 9, rect.Y + rect.Height), new(head.X, rect.Y + rect.Height + 14),
                            new(head.X + 9, rect.Y + rect.Height), Paper);
        LabelCentered(text, head.X, rect.Y + 9, size, Ink);
    }

    // ---------- Order shot ----------

    // Draws the over-the-shoulder order shot: the customer across the counter facing the camera, what they are
    // saying in a bubble, and in the foreground the budtender from behind, writing the ticket in a notepad.
    // written is what is on the ticket so far, out of itemCount lines.
    public static void DrawOrderShot(Customer c, Ticket t, string? said, string[] written, int itemCount)
    {
        const float counterY = 540;
        Raylib.DrawRectangle(0, 0, Program.Width, Program.Height, new Color(66, 58, 76, 255));
        // A shelf of jars on the back wall, out of focus.
        Raylib.DrawRectangle(960, 420, 280, 10, WoodLight);
        for (int i = 0; i < 5; i++)
            Raylib.DrawRectangleRounded(new Rectangle(975 + i * 52, 366, 38, 54), 0.3f, 6, new Color(150, 190, 210, 70));

        // The customer, big; the counter hides everything below the chest.
        var feet = new Vector2(780, counterY + 300);
        float s = 2.3f;
        DrawCustomer(c, feet, s, false);
        var head = HeadOf(feet, s);

        Raylib.DrawRectangle(0, (int)counterY, Program.Width, Program.Height - (int)counterY, Wood);
        Raylib.DrawRectangle(0, (int)counterY, Program.Width, 14, WoodLight);
        LabelCentered(c.Name, head.X, counterY + 26, 28, Text);

        if (said != null)
        {
            int size = 34;
            float w = Measure(said, size) + 48;
            var bubble = new Rectangle(MathF.Min(head.X + 110, Program.Width - 16 - w), head.Y - 140, w, 72);
            Raylib.DrawRectangleRounded(bubble, 0.4f, 10, Paper);
            Raylib.DrawTriangle(new(bubble.X + 20, bubble.Y + bubble.Height - 4), new(head.X + 70, head.Y - 30),
                                new(bubble.X + 60, bubble.Y + bubble.Height - 4), Paper);
            Label(said, bubble.X + 24, bubble.Y + 18, size, Ink);
        }

        // The budtender from behind: shoulders, back of the head, the arm out to the notepad.
        Raylib.DrawRectangleRounded(new Rectangle(-80, 500, 420, 320), 0.3f, 10, new Color(70, 70, 80, 255));
        Raylib.DrawLineEx(new(250, 590), new(420, 560), 54, new Color(70, 70, 80, 255));
        Raylib.DrawCircleV(new(170, 440), 112, Skin);
        Raylib.DrawCircleV(new(170, 424), 112, StaffHair);

        var pad = new Rectangle(400, 330, 280, 300);
        Raylib.DrawRectangleRec(new Rectangle(pad.X + 8, pad.Y + 10, pad.Width, pad.Height), new Color(0, 0, 0, 70));
        Raylib.DrawRectangleRec(pad, Paper);
        Raylib.DrawRectangleRec(new Rectangle(pad.X, pad.Y, pad.Width, 22), new Color(200, 80, 70, 255));
        Label($"{t.Label}  {c.Name.ToUpperInvariant()}", pad.X + 18, pad.Y + 36, 26, Ink);
        for (int i = 0; i < Math.Max(itemCount, 3); i++)
        {
            float y = pad.Y + 120 + i * 52;
            Raylib.DrawLineEx(new(pad.X + 16, y), new(pad.X + pad.Width - 16, y), 2, new Color(170, 190, 215, 255));
            if (i < written.Length) Label(written[i], pad.X + 22, y - 38, 34, new Color(60, 70, 120, 255));
        }
        Raylib.DrawCircleV(new(pad.X + 20, pad.Y + pad.Height - 30), 30, Skin);

        LabelCentered("Click to hurry them along", Program.Width / 2f, 640, 20, Faint);
    }

    // Draws one screen tab along the bottom; warn lights it orange when something there needs the player.
    public static void DrawTab(Rectangle rect, string text, bool active, bool hover, bool warn)
    {
        var fill = active ? Panel : hover ? new Color(80, 72, 90, 255) : new Color(60, 46, 36, 255);
        Raylib.DrawRectangleRounded(rect, 0.3f, 8, fill);
        if (warn) Raylib.DrawRectangleRoundedLinesEx(rect, 0.3f, 8, 3, Warn);
        else if (active) Raylib.DrawRectangleRoundedLinesEx(rect, 0.3f, 8, 2, PanelEdge);
        LabelCentered(text, rect.X + rect.Width / 2, rect.Y + (rect.Height - 22) / 2, 22, active ? Text : Faint);
    }

    // ---------- Interface ----------

    // Draws a clickable button; the caller tests the click.
    public static void DrawButton(Rectangle rect, string text, bool hover, bool enabled)
    {
        var fill = !enabled ? Panel : hover ? new Color(140, 215, 125, 255) : Accent;
        Raylib.DrawRectangleRounded(rect, 0.25f, 8, fill);
        int size = 28;
        var ink = enabled ? new Color(25, 35, 25, 255) : Faint;
        LabelCentered(text, rect.X + rect.Width / 2, rect.Y + (rect.Height - size) / 2, size, ink);
    }

    // Draws a dark panel with an edge, for overlays.
    public static void DrawPanel(Rectangle rect)
    {
        Raylib.DrawRectangleRounded(rect, 0.05f, 8, Panel);
        Raylib.DrawRectangleLinesEx(rect, 2, PanelEdge);
    }

    // Draws the F1 tuning panel in the top right, under the rail: a title, then one line per value, warnings orange.
    public static void DrawTuning(string title, IReadOnlyList<TuningLine> lines)
    {
        const int size = 18, gap = 22;
        const float width = 540, x = Program.Width - width - 12, y = 112;
        var rect = new Rectangle(x, y, width, 40 + gap * lines.Count);
        Raylib.DrawRectangleRounded(rect, 0.06f, 8, new Color(20, 18, 26, 225));
        Raylib.DrawRectangleRoundedLinesEx(rect, 0.06f, 8, 2, PanelEdge);
        Label($"{title}   (F1 hides)", x + 12, y + 8, size, Faint);
        for (int i = 0; i < lines.Count; i++)
            Label(lines[i].Text, x + 12, y + 32 + gap * i, size, lines[i].Warn ? Warn : Text);
    }

    static Rectangle Grow(Rectangle r, float by) => new(r.X - by, r.Y - by, r.Width + 2 * by, r.Height + 2 * by);
}

// One line of the F1 tuning panel; Warn draws it orange (over par, close to a slip, outside the green).
readonly record struct TuningLine(string Text, bool Warn = false);
