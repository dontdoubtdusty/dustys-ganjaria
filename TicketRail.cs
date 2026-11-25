using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The ticket rail along the top of every screen: taken orders hang here, oldest on the left,
// until the player drags one onto a station. The shop decides what a drop does.
sealed class TicketRail
{
    public const float Height = 104;
    const float CardW = 140, CardH = 80, Gap = 14, Left = 20, Top = 16;
    const float FreshSeconds = 1.5f;

    readonly List<Ticket> tickets = new();
    Ticket? dragged;
    Vector2 grabOffset;

    public int Count => tickets.Count;
    public Ticket? Dragged => dragged;

    static Vector2 Slot(int i) => new(Left + i * (CardW + Gap), Top);
    static Rectangle CardRect(Ticket t) => new(t.Pos.X, t.Pos.Y, CardW, CardH);

    // Hangs a new ticket at the end of the rail; it flies up from the given point and glows for a moment.
    public void Add(Ticket t, Vector2 from)
    {
        t.Pos = from;
        t.Fresh = FreshSeconds;
        tickets.Add(t);
    }

    // Takes a ticket off the rail (it went onto a station, or its customer walked out).
    public void Remove(Ticket t)
    {
        tickets.Remove(t);
        if (dragged == t) dragged = null;
    }

    // How many tickets on the rail need a station next.
    public int Waiting(Stage stage) => tickets.Count(t => t.Next == stage);

    // The ticket for a customer, if one is on the rail.
    public Ticket? For(Customer c) => tickets.FirstOrDefault(t => t.Customer == c);

    // Picks up the ticket under the mouse, if any. True if one was picked up.
    public bool TryGrab(Vector2 mouse)
    {
        for (int i = tickets.Count - 1; i >= 0; i--)
        {
            if (!Raylib.CheckCollisionPointRec(mouse, CardRect(tickets[i]))) continue;
            dragged = tickets[i];
            grabOffset = mouse - dragged.Pos;
            return true;
        }
        return false;
    }

    // Moves the held ticket with the mouse. Returns it on the frame it is let go; it stays on the rail
    // (and slides back to its slot) unless the shop removes it.
    public Ticket? UpdateDrag(Vector2 mouse, bool release)
    {
        if (dragged == null) return null;
        dragged.Pos = mouse - grabOffset;
        if (!release) return null;
        var dropped = dragged;
        dragged = null;
        return dropped;
    }

    // Takes every ticket down (a new day).
    public void Clear()
    {
        tickets.Clear();
        dragged = null;
    }

    // Puts down a held ticket without dropping it anywhere (used when the shop pauses).
    public void LetGo() => dragged = null;

    // Slides every ticket that is not held toward its slot and fades the new-ticket glow.
    public void Update(float dt)
    {
        for (int i = 0; i < tickets.Count; i++)
        {
            var t = tickets[i];
            if (t.Fresh > 0) t.Fresh -= dt;
            if (t != dragged) t.Pos = Vector2.Lerp(t.Pos, Slot(i), MathF.Min(1f, dt * 10f));
        }
    }

    // Draws the rail and its tickets; the held ticket is drawn last, lifted.
    public void Draw(Vector2 mouse, bool canGrab)
    {
        Art.DrawRail(Height);
        foreach (var t in tickets)
        {
            if (t == dragged) continue;
            var r = CardRect(t);
            Art.DrawRailTicket(r, t, canGrab && dragged == null && Raylib.CheckCollisionPointRec(mouse, r), false);
        }
        if (dragged != null) Art.DrawRailTicket(CardRect(dragged), dragged, false, true);
    }
}
