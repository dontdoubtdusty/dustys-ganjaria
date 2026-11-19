using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// Every placeholder drawing and all text live here, so each one can be swapped for a PNG sprite in one place.
static class Art
{
    public static readonly Color Background = new(38, 34, 44, 255);
    public static readonly Color Panel = new(58, 52, 66, 255);
    public static readonly Color PanelEdge = new(92, 84, 104, 255);
    public static readonly Color Text = new(236, 230, 240, 255);
    public static readonly Color Faint = new(160, 150, 170, 255);
    public static readonly Color Accent = new(120, 200, 110, 255);
    public static readonly Color Warn = new(240, 160, 80, 255);

    static readonly Color[] BudShades =
    {
        new(96, 150, 70, 255),
        new(120, 170, 80, 255),
        new(140, 160, 90, 255),
    };
    static readonly Color Hair = new(214, 140, 70, 255);
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

    // ---------- Station pieces ----------

    // Draws the table top the station stands on.
    public static void DrawTable(float floorY)
    {
        Raylib.DrawRectangle(0, (int)floorY, Program.Width, Program.Height - (int)floorY, new Color(110, 80, 58, 255));
        Raylib.DrawRectangle(0, (int)floorY, Program.Width, 4, new Color(140, 104, 76, 255));
    }

    // Draws a bud from its lobes, turned by its angle, with an outline when the mouse is over it.
    public static void DrawBud(Bud bud, bool highlight, bool lifted)
    {
        float k = bud.Scale;
        if (lifted)
            Raylib.DrawCircleV(bud.Pos + new Vector2(6, 8), bud.Radius * k, new Color(0, 0, 0, 70));

        float cos = MathF.Cos(bud.Angle), sin = MathF.Sin(bud.Angle);
        Vector2 Turn(Vector2 v) => new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos) * k;

        foreach (var lobe in bud.Lobes)
            Raylib.DrawCircleV(bud.Pos + Turn(lobe.Offset), lobe.Radius * k, BudShades[lobe.Shade]);
        if (!bud.IsShake)
        {
            // A few orange specks so it reads as flower rather than a pea.
            foreach (var lobe in bud.Lobes)
                Raylib.DrawCircleV(bud.Pos + Turn(lobe.Offset * 1.3f), 1.6f, Hair);
        }

        if (highlight)
            Raylib.DrawCircleLines((int)bud.Pos.X, (int)bud.Pos.Y, bud.Radius + 3, Text);
    }

    // Draws the glass behind the jar's contents.
    public static void DrawJarBack(Rectangle rect) =>
        Raylib.DrawRectangleRec(rect, new Color(150, 190, 210, 35));

    // Draws the jar's walls in front of its contents.
    public static void DrawJarFront(Rectangle rect)
    {
        float l = rect.X, r = rect.X + rect.Width, t = rect.Y, b = rect.Y + rect.Height;
        Raylib.DrawLineEx(new(l, t), new(l, b), 5, Glass);
        Raylib.DrawLineEx(new(l - 2, b), new(r + 2, b), 5, Glass);
        Raylib.DrawLineEx(new(r, t), new(r, b), 5, Glass);
        LabelCentered("JAR", l + rect.Width / 2, t - 30, 22, Faint);
    }

    // Draws the scale: body, plate, and a dial with a green band around the target and a needle.
    public static void DrawScale(Rectangle body, Rectangle plate, Vector2 dial, float radius,
                                 float grams, float target, float band, float max)
    {
        Raylib.DrawRectangleRounded(body, 0.06f, 8, new Color(200, 200, 205, 255));
        Raylib.DrawRectangleRec(plate, new Color(150, 152, 160, 255));

        // Dial face: the top half of a circle, left = empty, right = full scale.
        Raylib.DrawCircleSector(dial, radius + 8, 180, 360, 40, new Color(245, 242, 232, 255));
        float Deg(float g) => 180f + 180f * Math.Clamp(g / max, 0f, 1f);
        // No ticket: no green band and no target label.
        if (target > 0)
            Raylib.DrawRing(dial, radius - 34, radius, Deg(target - band), Deg(target + band), 16, new Color(90, 200, 90, 255));

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

    // Draws the bag's edges and its zip in front of the contents; the tab shows how far it is zipped.
    public static void DrawBagFront(Rectangle rect, float zip, bool sealedBag, bool zipHot)
    {
        float l = rect.X, r = rect.X + rect.Width, t = rect.Y, b = rect.Y + rect.Height;
        Raylib.DrawLineEx(new(l, t), new(l, b), 3, Plastic);
        Raylib.DrawLineEx(new(l, b), new(r, b), 3, Plastic);
        Raylib.DrawLineEx(new(r, t), new(r, b), 3, Plastic);

        var zipColor = new Color(110, 150, 210, 255);
        Raylib.DrawLineEx(new(l, t), new(r, t), 3, new Color(110, 150, 210, 120));
        float zx = l + rect.Width * zip;
        if (zip > 0) Raylib.DrawLineEx(new(l, t), new(zx, t), 7, zipColor);
        if (sealedBag) return;

        var tab = new Rectangle(zx - 9, t - 12, 18, 24);
        Raylib.DrawRectangleRounded(tab, 0.4f, 6, zipHot ? new Color(160, 200, 255, 255) : zipColor);
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

    // Draws the order ticket: whose order, the weight to bag, their patience, and how many tickets wait behind it.
    // With no customer it shows an empty ticket.
    public static void DrawTicket(Rectangle rect, Customer? customer, int moreTickets)
    {
        Raylib.DrawRectangleRec(rect, new Color(245, 240, 225, customer == null ? 90 : 255));
        Raylib.DrawRectangleLinesEx(rect, 2, new Color(190, 180, 160, 255));
        var ink = new Color(50, 45, 40, 255);
        if (customer == null)
        {
            Label("NO TICKET", rect.X + 16, rect.Y + 10, 22, ink);
            return;
        }

        Label($"ORDER FOR {customer.Name.ToUpperInvariant()}", rect.X + 16, rect.Y + 10, 22, ink);
        Label(customer.OrderText, rect.X + 16, rect.Y + 38, 40, ink);
        DrawPatienceBar(new Rectangle(rect.X + 16, rect.Y + rect.Height - 20, rect.Width - 32, 8), customer.PatienceFraction);
        if (moreTickets > 0)
            Label($"+{moreTickets} more ticket{(moreTickets == 1 ? "" : "s")} waiting", rect.X, rect.Y + rect.Height + 8, 20, Faint);
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

    // Draws the shop wall with the signs over the waiting area and the line (behind the customers).
    public static void DrawShopWall(float counterY, float waitingX, float lineX)
    {
        Raylib.DrawRectangle(0, 0, Program.Width, (int)counterY, new Color(52, 46, 60, 255));
        LabelCentered("WAITING FOR ORDERS", waitingX, 120, 22, Faint);
        LabelCentered("LINE", lineX, 120, 22, Faint);
    }

    // Draws the counter in front of the customers, hiding their legs.
    public static void DrawCounterTop(float counterY)
    {
        Raylib.DrawRectangle(0, (int)counterY, Program.Width, Program.Height - (int)counterY, new Color(110, 80, 58, 255));
        Raylib.DrawRectangle(0, (int)counterY, Program.Width, 8, new Color(150, 112, 82, 255));
    }

    // Draws a placeholder customer: a shirt, a head, a face that follows their patience, their name and a patience bar.
    // pos is the centre of the head; the counter drawn later hides the legs.
    public static void DrawCustomer(Customer c, bool showBar)
    {
        var head = c.Pos;
        float mood = c.Now == Customer.State.Leaving ? (c.Served ? 1f : 0f) : c.PatienceFraction;

        Raylib.DrawRectangleRounded(new Rectangle(head.X - 42, head.Y + 30, 84, 200), 0.35f, 8, c.Shirt);
        Raylib.DrawCircleV(head, 34, new Color(222, 186, 150, 255));
        Raylib.DrawCircleV(head + new Vector2(-12, -6), 4, new Color(40, 34, 30, 255));
        Raylib.DrawCircleV(head + new Vector2(12, -6), 4, new Color(40, 34, 30, 255));

        var mouth = new Color(90, 40, 40, 255);
        if (mood > 0.5f) Raylib.DrawRing(head + new Vector2(0, 2), 13, 16, 30, 150, 12, mouth);
        else if (mood > 0.25f) Raylib.DrawLineEx(head + new Vector2(-10, 14), head + new Vector2(10, 14), 3, mouth);
        else Raylib.DrawRing(head + new Vector2(0, 28), 13, 16, 210, 330, 12, mouth);

        LabelCentered(c.Name, head.X, head.Y + 44, 20, Text);
        if (showBar)
            DrawPatienceBar(new Rectangle(head.X - 36, head.Y - 50, 72, 8), c.PatienceFraction);
    }

    // Draws a speech bubble above a customer's head with their order.
    public static void DrawSpeech(Vector2 head, string text)
    {
        int size = 26;
        float w = Measure(text, size) + 32;
        var rect = new Rectangle(head.X - w / 2, head.Y - 128, w, 52);
        Raylib.DrawRectangleRounded(rect, 0.4f, 8, new Color(245, 240, 225, 255));
        Raylib.DrawTriangle(new(head.X - 10, rect.Y + rect.Height), new(head.X, rect.Y + rect.Height + 16),
                            new(head.X + 10, rect.Y + rect.Height), new Color(245, 240, 225, 255));
        LabelCentered(text, head.X, rect.Y + 12, size, new Color(50, 45, 40, 255));
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

    static Rectangle Grow(Rectangle r, float by) => new(r.X - by, r.Y - by, r.Width + 2 * by, r.Height + 2 * by);
}
