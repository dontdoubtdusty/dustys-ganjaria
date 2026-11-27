using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The rest of Art: drawing for the day (intro card, clock), the hand-over shot, the day results and the tip jar.
// Same rule as Art.cs: every placeholder drawing lives here, so it can be swapped for a sprite in one place.
static partial class Art
{
    static readonly Color Gold = new(232, 190, 80, 255);
    static readonly Color Silver = new(200, 205, 215, 255);
    static readonly Color Bad = new(225, 80, 70, 255);

    // A score's colour: green when good, orange when middling, red when poor.
    static Color ScoreColor(int score) => score >= 70 ? Accent : score >= 50 ? Warn : Bad;

    // ---------- Day ----------

    // Draws the card before a day starts: day number, how many customers to expect, rank and tip jar, the strains
    // on today's menu, and the next strain still to unlock (null when all are open).
    // date is today's date as text; notice (null for none) is a news line in orange at the bottom.
    public static void DrawDayIntro(Rectangle panel, int day, int expected, string rank, string jar, Strain[] menu, string? next,
                                    string date, string? notice)
    {
        Raylib.DrawRectangle(0, 0, Program.Width, Program.Height, new Color(0, 0, 0, 130));
        DrawPanel(panel);
        float cx = panel.X + panel.Width / 2;
        LabelCentered($"Day {day}", cx, panel.Y + 26, 64, Accent);
        LabelCentered($"{expected} customers expected", cx, panel.Y + 104, 30, Text);
        LabelCentered($"{date}. Doors open 10 AM, close 8 PM.", cx, panel.Y + 146, 22, Faint);
        if (notice != null) LabelCentered(notice, cx, panel.Y + 356, 20, Warn);
        LabelCentered("At closing, finish everyone still inside.", cx, panel.Y + 174, 22, Faint);
        LabelCentered($"{rank}   Tip jar {jar}", cx, panel.Y + 214, 22, Faint);

        // Today's strains as tags in a row.
        LabelCentered("On the menu today", cx, panel.Y + 256, 22, Text);
        const int size = 20;
        float gap = 12, total = menu.Sum(s => Measure(s.Name, size) + size) + gap * (menu.Length - 1);
        float x = cx - total / 2;
        foreach (var s in menu)
        {
            float w = Measure(s.Name, size) + size;
            DrawStrainTag(s, x + w / 2, panel.Y + 288, size);
            x += w + gap;
        }
        if (next != null) LabelCentered(next, cx, panel.Y + 330, 20, Faint);
    }

    // ---------- Pause menu ----------

    // Dims the game and draws the pause panel with its title and, under it, a line of small print (may be empty).
    public static void DrawPausePanel(Rectangle panel, string title, string note)
    {
        Raylib.DrawRectangle(0, 0, Program.Width, Program.Height, new Color(0, 0, 0, 150));
        DrawPanel(panel);
        float cx = panel.X + panel.Width / 2;
        LabelCentered(title, cx, panel.Y + 22, 48, Accent);
        if (note.Length > 0) LabelCentered(note, cx, panel.Y + 80, 20, Faint);
    }

    // Draws a volume slider: a label, the track, the filled part up to value (0-1), a knob and the percentage.
    public static void DrawSlider(Rectangle track, string label, float value, bool hot)
    {
        Label(label, track.X, track.Y - 30, 22, Text);
        LabelRight($"{value * 100:0}%", track.X + track.Width, track.Y - 30, 22, Faint);
        Raylib.DrawRectangleRounded(track, 1f, 8, new Color(40, 34, 48, 255));
        Raylib.DrawRectangleRounded(new Rectangle(track.X, track.Y, track.Width * value, track.Height), 1f, 8, Accent);
        var knob = new Vector2(track.X + track.Width * value, track.Y + track.Height / 2);
        Raylib.DrawCircleV(knob, hot ? 13 : 11, hot ? new Color(250, 248, 240, 255) : Text);
    }

    // Draws text ending at x.
    static void LabelRight(string text, float x, float y, int size, Color color) =>
        Label(text, x - Measure(text, size), y, size, color);

    // Draws the day and wall clock in the bottom bar; after closing it says so, and >> shows while time is sped up.
    public static void DrawClock(float x, float y, int day, string clock, bool closed, bool fast)
    {
        var face = new Vector2(x + 14, y + 13);
        Raylib.DrawCircleV(face, 13, Paper);
        Raylib.DrawLineEx(face, face + new Vector2(0, -9), 2, Ink);
        Raylib.DrawLineEx(face, face + new Vector2(6, 0), 2, Ink);
        string text = closed ? $"Day {day}   Closed" : $"Day {day}   {clock}";
        Label(text, x + 36, y + 2, 22, closed ? Warn : Text);
        if (fast) Label(">>", x + 48 + Measure(text, 22), y + 2, 22, Accent);
    }

    // The OPEN or CLOSED sign hanging in the door's window.
    public static void DrawDoorSign(Vector2 feet, bool open)
    {
        var sign = new Rectangle(feet.X - 40, feet.Y - 200, 80, 30);
        Raylib.DrawRectangleRounded(sign, 0.3f, 6, open ? Accent : Bad);
        LabelCentered(open ? "OPEN" : "CLOSED", feet.X, sign.Y + 5, 20, new Color(25, 25, 30, 255));
    }

    // ---------- Metric icons ----------

    // Draws a round icon: 0-3 are the four metrics (clock, scale dial, bag, label tag), 4 a customer, 5 a medal.
    public static void DrawMetricIcon(int which, Vector2 c, float r)
    {
        Color[] rims = { new(90, 160, 220, 255), Accent, new(200, 140, 220, 255), Warn, Silver, Gold };
        Raylib.DrawCircleV(c, r, rims[which]);
        Raylib.DrawCircleV(c, r - 4, new Color(40, 36, 46, 255));
        var ink = rims[which];
        float k = r / 20f;
        switch (which)
        {
            case 0:
                Raylib.DrawCircleLinesV(c, 10 * k, ink);
                Raylib.DrawLineEx(c, c + new Vector2(0, -8) * k, 2, ink);
                Raylib.DrawLineEx(c, c + new Vector2(6, 2) * k, 2, ink);
                break;
            case 1:
                Raylib.DrawRing(c + new Vector2(0, 5) * k, 8 * k, 11 * k, 180, 360, 12, ink);
                Raylib.DrawLineEx(c + new Vector2(0, 5) * k, c + new Vector2(6, -3) * k, 2, ink);
                break;
            case 2:
                Raylib.DrawRectangleLinesEx(new Rectangle(c.X - 8 * k, c.Y - 9 * k, 16 * k, 18 * k), 2, ink);
                Raylib.DrawLineEx(c + new Vector2(-8, -5) * k, c + new Vector2(8, -5) * k, 3, ink);
                break;
            case 3:
                Raylib.DrawTriangle(c + new Vector2(-11, 0) * k, c + new Vector2(-4, 7) * k, c + new Vector2(-4, -7) * k, ink);
                Raylib.DrawRectangleRec(new Rectangle(c.X - 4 * k, c.Y - 7 * k, 14 * k, 14 * k), ink);
                Raylib.DrawCircleV(c + new Vector2(-4, 0) * k, 2 * k, new Color(40, 36, 46, 255));
                break;
            case 4:
                Raylib.DrawCircleV(c + new Vector2(0, -4) * k, 6 * k, ink);
                Raylib.DrawRing(c + new Vector2(0, 12) * k, 7 * k, 10 * k, 180, 360, 12, ink);
                break;
            default:
                Raylib.DrawCircleV(c, 8 * k, ink);
                Raylib.DrawCircleV(c, 4 * k, new Color(40, 36, 46, 255));
                break;
        }
    }

    // A score bar: dark track, filled to a share in the score's colour.
    static void DrawScoreBar(Rectangle r, int score, float shown)
    {
        Raylib.DrawRectangleRounded(r, 0.5f, 6, new Color(0, 0, 0, 90));
        float w = r.Width * score / 100f * shown;
        if (w > 1) Raylib.DrawRectangleRounded(new Rectangle(r.X, r.Y, w, r.Height), 0.5f, 6, ScoreColor(score));
    }

    // ---------- Hand-over shot ----------

    // Draws the hand-over: the customer across the counter, the bag sliding over to them, the score card with
    // each metric as far as it has landed (shown, 0-1 per row), the total and their reaction once it is in, and
    // the tip coin flying to the jar (coin 0-1, below 0 not yet thrown). check (0-1) is the customer's bag check:
    // the bag lifts up beside their face, sways while they look it over, and goes back down on the counter.
    public static void DrawHandOverShot(Customer c, Ticket t, OrderResult r, float slide, float check,
                                        float[] shown, bool showTotal, string? said, float coin)
    {
        const float counterY = 540;
        Raylib.DrawRectangle(0, 0, Program.Width, Program.Height, new Color(66, 58, 76, 255));
        Raylib.DrawRectangle(960, 420, 280, 10, WoodLight);
        for (int i = 0; i < 5; i++)
            Raylib.DrawRectangleRounded(new Rectangle(975 + i * 52, 366, 38, 54), 0.3f, 6, new Color(150, 190, 210, 70));

        var feet = new Vector2(900, counterY + 300);
        float s = 2.3f;
        DrawCustomer(c, feet, s, false, faceLeft: true, inShot: true);
        var head = HeadOf(feet, s);

        Raylib.DrawRectangle(0, (int)counterY, Program.Width, Program.Height - (int)counterY, Wood);
        Raylib.DrawRectangle(0, (int)counterY, Program.Width, 14, WoodLight);
        LabelCentered(c.Name, head.X, counterY + 26, 28, Text);

        // The tip jar on the counter, and the bag sliding across to the customer.
        var jar = new Rectangle(1130, 452, 80, 90);
        Raylib.DrawRectangleRec(jar, new Color(150, 190, 210, 60));
        Raylib.DrawRectangleLinesEx(jar, 3, Glass);
        LabelCentered("TIPS", jar.X + jar.Width / 2, jar.Y + 34, 18, Faint);
        float eased = 1f - (1f - slide) * (1f - slide);
        var bag = new Rectangle(500 + 300 * eased, counterY - 90, 76, 90);
        static float Ease(float x) => x * x * (3f - 2f * x);   // smoothstep: slow start and end
        float lift = check < 0.25f ? Ease(check / 0.25f) : check < 0.8f ? 1f : 1f - Ease((check - 0.8f) / 0.2f);
        float sway = check > 0.25f && check < 0.8f ? MathF.Sin((check - 0.25f) / 0.55f * MathF.PI * 3f) * 7f : 0f;
        var lifted = new Vector2(head.X + 145 - (bag.X + bag.Width / 2), head.Y + 60 - (bag.Y + bag.Height / 2));
        bag.X += lifted.X * lift + sway;
        bag.Y += lifted.Y * lift;
        DrawPackedContainer(bag, r.Pack.Used, c.Strain, true, t.Fill);
        // The label where it actually went: as far off the guide as it was slapped on.
        var (labelAt, labelSize) = LabelStation.LabelOn(bag, r.Label.At);
        DrawStrainLabel(labelAt, labelSize, 0, r.Label.Used, false, false, 0, false, $"{r.Weigh.Weight:0.00} g");

        // The budtender from behind, arm out across the counter; it draws back once the customer has the bag.
        float reach = 300 + 200 * eased * (1f - MathF.Min(1f, check * 3f));
        Raylib.DrawRectangleRounded(new Rectangle(-80, 500, 420, 320), 0.3f, 10, new Color(70, 70, 80, 255));
        Raylib.DrawLineEx(new(250, 590), new(reach, 560), 54, new Color(70, 70, 80, 255));
        Raylib.DrawCircleV(new(reach, 556), 26, Skin);
        Raylib.DrawCircleV(new(170, 440), 112, Skin);
        Raylib.DrawCircleV(new(170, 424), 112, StaffHair);

        // The score card.
        var card = new Rectangle(330, 90, 420, 360);
        Raylib.DrawRectangleRec(new Rectangle(card.X + 8, card.Y + 10, card.Width, card.Height), new Color(0, 0, 0, 70));
        Raylib.DrawRectangleRec(card, Paper);
        Raylib.DrawRectangleRec(new Rectangle(card.X, card.Y, card.Width, 22), new Color(200, 80, 70, 255));
        Label($"{t.Label}  {c.Name.ToUpperInvariant()}", card.X + 18, card.Y + 32, 24, Ink);
        Label(c.FullOrderText, card.X + card.Width - 18 - Measure(c.FullOrderText, 20), card.Y + 36, 20, new Color(110, 100, 90, 255));

        var metrics = r.Metrics;
        for (int i = 0; i < metrics.Length; i++)
        {
            float y = card.Y + 78 + i * 56;
            DrawMetricIcon(i, new Vector2(card.X + 36, y + 18), 18);
            Label(Scoring.MetricNames[i], card.X + 64, y + 4, 26, Ink);
            if (metrics[i] == null)
            {
                if (shown[i] > 0) Label("not built yet", card.X + 230, y + 8, 20, new Color(150, 140, 130, 255));
                continue;
            }
            if (shown[i] <= 0)
            {
                Label("...", card.X + 230, y, 30, new Color(150, 140, 130, 255));
                continue;
            }
            int value = (int)MathF.Round(metrics[i]!.Value * shown[i]);
            DrawScoreBar(new Rectangle(card.X + 200, y + 10, 130, 16), metrics[i]!.Value, shown[i]);
            Label($"{value}%", card.X + 342, y + 2, 28, Ink);
        }

        if (showTotal)
        {
            float y = card.Y + card.Height - 56;
            Raylib.DrawLineEx(new(card.X + 16, y - 8), new(card.X + card.Width - 16, y - 8), 2, new Color(190, 180, 160, 255));
            Label($"{r.Grade}  {r.Total}%", card.X + 20, y, 38, ScoreColor(r.Total));
            string tip = r.TipCents > 0 ? $"Tip {Scoring.Money(r.TipCents)}" : "No tip";
            Label(tip, card.X + card.Width - 20 - Measure(tip, 26), y + 8, 26, Ink);

            // An accepted bad ID: the fine, under the card.
            if (r.FineCents > 0)
            {
                string fine = $"Bad ID ({Ids.Why(r.Flaw)}): fined {Scoring.Money(r.FineCents)}";
                float fw = Measure(fine, 24) + 32;
                var pill = new Rectangle(card.X + card.Width / 2 - fw / 2, card.Y + card.Height + 16, fw, 36);
                Raylib.DrawRectangleRounded(pill, 0.5f, 8, new Color(30, 26, 34, 235));
                LabelCentered(fine, pill.X + fw / 2, pill.Y + 6, 24, Bad);
            }
        }

        if (said != null)
        {
            int size = 32;
            float w = Measure(said, size) + 48;
            float bx = MathF.Min(head.X - 60, Program.Width - 20 - w);
            var bubble = new Rectangle(bx, head.Y - 190, w, 68);
            Raylib.DrawRectangleRounded(bubble, 0.4f, 10, Paper);
            Raylib.DrawTriangle(new(head.X - 20, bubble.Y + bubble.Height - 4), new(head.X, head.Y - 70),
                                new(head.X + 20, bubble.Y + bubble.Height - 4), Paper);
            Label(said, bubble.X + 24, bubble.Y + 17, size, Ink);
        }

        // The tip: a coin arcs from the customer's hand into the jar, then the amount pops up over it.
        if (coin >= 0 && r.TipCents > 0)
        {
            var from = new Vector2(head.X + 90, counterY - 40);
            var to = new Vector2(jar.X + jar.Width / 2, jar.Y + jar.Height - 20);
            var at = Vector2.Lerp(from, to, coin) + new Vector2(0, -160 * 4 * coin * (1 - coin));
            if (coin < 1) Raylib.DrawCircleV(at, 12, Gold);
            else
            {
                Raylib.DrawEllipse((int)to.X, (int)to.Y + 8, 18, 6, Gold);
                LabelCentered($"+{Scoring.Money(r.TipCents)}", to.X, jar.Y - 44, 30, Gold);
            }
        }
    }

    // ---------- Day results ----------

    // Draws the day results panel: customers (with walkouts), service quality, and the four metric averages.
    // Each row's shown (0-1) counts its number up from zero; a row at 0 is not drawn yet. ids is a line about the
    // day's bad IDs under the rows (null for none), orange when one got through.
    public static void DrawDayResults(Rectangle panel, int day, int served, int walkouts, float shownServed,
                                      int quality, float shownQuality,
                                      (string Name, int? Value, bool Built, float Shown)[] metrics,
                                      string? ids = null, bool idsWarn = false, string? giveaway = null)
    {
        Raylib.DrawRectangle(0, 0, Program.Width, Program.Height, new Color(0, 0, 0, 150));
        DrawPanel(panel);
        float cx = panel.X + panel.Width / 2;
        LabelCentered($"Day {day} results", cx, panel.Y + 22, 48, Accent);

        float x = panel.X + 30, w = panel.Width - 60;
        float rowY = panel.Y + 96;
        void Stripe(int i, float y) =>
            Raylib.DrawRectangleRounded(new Rectangle(x, y, w, 54), 0.2f, 6, new Color(255, 255, 255, i % 2 == 0 ? 14 : 6));

        if (shownServed > 0)
        {
            Stripe(0, rowY);
            DrawMetricIcon(4, new Vector2(x + 32, rowY + 27), 20);
            Label("Customers today", x + 66, rowY + 12, 30, Text);
            Label($"{(int)MathF.Round(served * shownServed)}", x + w - 140, rowY + 8, 38, Text);
            if (walkouts > 0) Label($"{walkouts} walked out", x + w - 140 + 46, rowY + 18, 18, Warn);
        }
        rowY += 62;
        if (shownQuality > 0)
        {
            Stripe(1, rowY);
            DrawMetricIcon(5, new Vector2(x + 32, rowY + 27), 20);
            Label("Service quality", x + 66, rowY + 12, 30, Text);
            Label($"{(int)MathF.Round(quality * shownQuality)}%", x + w - 140, rowY + 8, 38, ScoreColor(quality));
        }
        rowY += 70;

        for (int i = 0; i < metrics.Length; i++)
        {
            var m = metrics[i];
            float y = rowY + i * 58;
            if (m.Shown <= 0) continue;
            Stripe(i, y);
            DrawMetricIcon(i, new Vector2(x + 64, y + 27), 18);
            Label(m.Name, x + 96, y + 14, 26, Text);
            if (!m.Built) { Label("not built yet", x + 260, y + 18, 20, Faint); continue; }
            if (m.Value == null) { Label("nobody scored", x + 260, y + 18, 20, Faint); continue; }
            int v = m.Value.Value;
            DrawScoreBar(new Rectangle(x + 260, y + 19, 200, 16), v, m.Shown);
            Label($"{(int)MathF.Round(v * m.Shown)}%", x + 480, y + 10, 32, Text);
        }
        // The flower given away (heavy bags and crumbs), then the ID line under it.
        if (giveaway != null) LabelCentered(giveaway, cx, panel.Y + 462, 22, Faint);
        if (ids != null) LabelCentered(ids, cx, panel.Y + (giveaway != null ? 488 : 466), 22, idsWarn ? Warn : Accent);
    }

    // ---------- Tip jar ----------

    // Draws the tip jar screen: the budtender behind the counter, the jar with its coins (positions given),
    // the money counted so far and the jar's running total, and the rank badge with its meter at a number of points.
    public static void DrawTipJarScreen(Rectangle jar, int day, List<Vector2> coins, int countedCents, int bankCents,
                                        int points, int gained, bool rankedUp)
    {
        const float counterY = 522;
        Raylib.DrawRectangle(0, 0, Program.Width, Program.Height, new Color(66, 58, 76, 255));
        DrawBudtenderFront(new Vector2(230, 690), 1.25f);
        Raylib.DrawRectangle(0, (int)counterY, Program.Width, Program.Height - (int)counterY, Wood);
        Raylib.DrawRectangle(0, (int)counterY, Program.Width, 14, WoodLight);

        // The jar: glass behind, coins, glass edges and its paper label in front.
        Raylib.DrawRectangleRec(jar, new Color(150, 190, 210, 40));
        for (int i = 0; i < coins.Count; i++)
        {
            var p = coins[i];
            Raylib.DrawEllipse((int)p.X, (int)p.Y + 2, 10, 5, new Color(0, 0, 0, 60));
            Raylib.DrawEllipse((int)p.X, (int)p.Y, 10, 5, i % 3 == 0 ? Silver : Gold);
        }
        float l = jar.X, r = jar.X + jar.Width, b = jar.Y + jar.Height;
        Raylib.DrawLineEx(new(l, jar.Y), new(l, b), 5, Glass);
        Raylib.DrawLineEx(new(l - 2, b), new(r + 2, b), 5, Glass);
        Raylib.DrawLineEx(new(r, jar.Y), new(r, b), 5, Glass);
        Raylib.DrawRectangleRec(new Rectangle(l - 6, jar.Y - 6, jar.Width + 12, 12), Glass);
        var tag = new Rectangle(l + 50, jar.Y + 60, 100, 34);
        Raylib.DrawRectangleRec(tag, Paper);
        LabelCentered("TIPS", tag.X + tag.Width / 2, tag.Y + 6, 22, Ink);

        // The money.
        float cx = jar.X + jar.Width / 2;
        LabelCentered($"Day {day} tips", cx, 40, 30, Faint);
        LabelCentered(Scoring.Money(countedCents), cx, 76, 80, Gold);
        LabelCentered($"Tip jar total {Scoring.Money(bankCents)}", cx, 168, 26, Text);

        // The rank badge and its meter toward the next rank.
        int rank = Progress.RankOf(points);
        var badge = new Vector2(1010, 250);
        Raylib.DrawCircleV(badge, 128, rankedUp ? new Color(120, 200, 110, 70) : new Color(255, 255, 255, 18));
        Raylib.DrawCircleV(badge, 112, Panel);
        Raylib.DrawRing(badge, 106, 114, 0, 360, 48, rankedUp ? Accent : PanelEdge);
        LabelCentered("RANK", badge.X, badge.Y - 82, 26, Faint);
        LabelCentered($"{rank}", badge.X, badge.Y - 56, 100, Gold);
        LabelCentered(Progress.RankName(rank), badge.X, badge.Y + 136, 30, Text);

        int from = Progress.PointsFor(rank), to = Progress.PointsFor(rank + 1);
        var bar = new Rectangle(badge.X - 150, badge.Y + 182, 300, 18);
        Raylib.DrawRectangleRounded(bar, 0.5f, 6, new Color(0, 0, 0, 90));
        float share = Math.Clamp((points - from) / (float)(to - from), 0f, 1f);
        if (share > 0) Raylib.DrawRectangleRounded(new Rectangle(bar.X, bar.Y, bar.Width * share, bar.Height), 0.5f, 6, Accent);
        LabelCentered($"{to - points} points to {Progress.RankName(rank + 1)}", badge.X, bar.Y + 26, 20, Faint);
        LabelCentered($"+{gained} points today", badge.X, badge.Y - 170, 22, Accent);
        if (rankedUp) LabelCentered("Rank up!", badge.X + 120, badge.Y - 130, 36, Accent);
    }

    // Draws the budtender facing the camera, smiling, behind the counter (the counter hides the legs).
    static void DrawBudtenderFront(Vector2 feet, float s)
    {
        var head = HeadOf(feet, s);
        Raylib.DrawRectangleRounded(new Rectangle(feet.X - 50 * s, feet.Y - 210 * s, 100 * s, 150 * s), 0.35f, 8, new Color(70, 70, 80, 255));
        Raylib.DrawRectangleRounded(new Rectangle(feet.X - 38 * s, feet.Y - 180 * s, 76 * s, 120 * s), 0.25f, 8, Apron);
        Raylib.DrawCircleV(head, HeadRadius * s, Skin);
        Raylib.DrawRing(head, (HeadRadius - 6) * s, (HeadRadius + 2) * s, 180, 360, 16, StaffHair);
        var eye = new Color(40, 34, 30, 255);
        Raylib.DrawCircleV(head + new Vector2(-11, -4) * s, 4 * s, eye);
        Raylib.DrawCircleV(head + new Vector2(11, -4) * s, 4 * s, eye);
        Raylib.DrawRing(head + new Vector2(0, 2) * s, 12 * s, 15 * s, 30, 150, 12, new Color(90, 40, 40, 255));
    }
}
