using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The ID check's pieces: hair on the placeholder heads, the ID card with its photo, the date sign by the counter,
// and the Accept and Refuse buttons. Same rule as Art.cs: every placeholder drawing lives in Art.
static partial class Art
{
    static readonly Color CardBlue = new(70, 110, 160, 255);
    static readonly Color CardPaper = new(232, 238, 244, 255);
    static readonly Color RefuseRed = new(210, 90, 80, 255);

    // Draws a placeholder hair cap over the top of a head (none for bald).
    static void DrawHair(Vector2 head, float s, Color? hair)
    {
        if (hair is not { } h) return;
        Raylib.DrawRing(head, (HeadRadius - 7) * s, (HeadRadius + 3) * s, 180, 360, 20, h);
    }

    // Draws the ID card held up at the counter: a header band, the photo (head and shoulders in the card's look),
    // and the name, birth date and expiry date.
    public static void DrawIdCard(Rectangle r, IdCard id)
    {
        Raylib.DrawRectangleRounded(new Rectangle(r.X + 6, r.Y + 8, r.Width, r.Height), 0.08f, 8, new Color(0, 0, 0, 80));
        Raylib.DrawRectangleRounded(r, 0.08f, 8, CardPaper);
        var band = new Rectangle(r.X, r.Y, r.Width, 40);
        Raylib.DrawRectangleRounded(band, 0.3f, 8, CardBlue);
        Raylib.DrawRectangleRec(new Rectangle(r.X, r.Y + 24, r.Width, 16), CardBlue);
        Label("IDENTIFICATION CARD", r.X + 16, r.Y + 9, 22, new Color(240, 244, 250, 255));

        // The photo: a head and shoulders in the card's shirt and hair.
        var photo = new Rectangle(r.X + 16, r.Y + 54, 110, 136);
        Raylib.DrawRectangleRec(photo, new Color(190, 205, 220, 255));
        Raylib.BeginScissorMode((int)photo.X, (int)photo.Y, (int)photo.Width, (int)photo.Height);
        var head = new Vector2(photo.X + photo.Width / 2, photo.Y + 58);
        Raylib.DrawRectangleRounded(new Rectangle(head.X - 46, head.Y + 30, 92, 90), 0.35f, 8, id.PhotoShirt);
        Raylib.DrawCircleV(head, HeadRadius * 1.1f, Skin);
        DrawHair(head, 1.1f, id.PhotoHair);
        var eye = new Color(40, 34, 30, 255);
        Raylib.DrawCircleV(head + new Vector2(-11, -5), 4, eye);
        Raylib.DrawCircleV(head + new Vector2(11, -5), 4, eye);
        Raylib.DrawLineEx(head + new Vector2(-9, 15), head + new Vector2(9, 15), 3, new Color(90, 40, 40, 255));
        Raylib.EndScissorMode();
        Raylib.DrawRectangleLinesEx(photo, 2, new Color(150, 165, 185, 255));

        float x = photo.X + photo.Width + 18, y = r.Y + 52;
        var small = new Color(110, 120, 140, 255);
        void Field(string name, string value, float at)
        {
            Label(name, x, at, 16, small);
            Label(value, x, at + 16, 26, Ink);
        }
        Field("NAME", id.Name.ToUpperInvariant(), y);
        Field("BORN", Ids.Text(id.Born), y + 48);
        Field("EXPIRES", Ids.Text(id.Expires), y + 96);
    }

    // Draws the sign by the counter: today's date and the latest birth date that is old enough.
    public static void DrawDateSign(Rectangle r, DateOnly today)
    {
        Raylib.DrawRectangleRounded(r, 0.12f, 8, Paper);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.12f, 8, 3, new Color(200, 80, 70, 255));
        Label($"TODAY  {Ids.LongText(today)}", r.X + 16, r.Y + 12, 22, Ink);
        Label($"{Ids.LegalAge}+ if born on or before", r.X + 16, r.Y + 44, 20, new Color(110, 100, 90, 255));
        Label(Ids.Text(Ids.Cutoff(today)), r.X + 16, r.Y + 66, 30, new Color(200, 80, 70, 255));
    }

    // Draws the Refuse button: like DrawButton, but red.
    public static void DrawRefuseButton(Rectangle rect, string text, bool hover)
    {
        var fill = hover ? new Color(232, 120, 108, 255) : RefuseRed;
        Raylib.DrawRectangleRounded(rect, 0.25f, 8, fill);
        LabelCentered(text, rect.X + rect.Width / 2, rect.Y + (rect.Height - 28) / 2, 28, new Color(40, 20, 20, 255));
    }
}
