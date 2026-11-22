using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The hand-over shot: the same over-the-shoulder view as the order shot. The finished order slides across the
// counter, the customer lifts it and looks it over (a neutral "hmm", no hint of the verdict), then the four scores
// land one by one on the score card. The customer reacts to the total, says so, and drops their tip in the jar. A click skips to the end; Next (or Enter) closes it.
sealed class HandOver
{
    // Seconds for the order to slide across, for the customer's bag check, between rows landing, for a row's bar
    // to fill, and for the coin's flight.
    const float SlideSeconds = 0.7f, CheckSeconds = 0.9f, RowGap = 0.4f, RowFill = 0.3f, CoinSeconds = 0.55f;
    public static readonly Rectangle NextButton = new(1010, 630, 220, 56);

    public Customer Customer { get; }
    public OrderResult Result { get; }
    public Ticket Ticket { get; }

    float t;
    int rowsTicked;
    bool lifted, chimed, reacted, tipped;
    // Set by a screenshot hold that must not move on.
    bool frozen;

    public HandOver(Ticket ticket, OrderResult result)
    {
        Ticket = ticket;
        Customer = ticket.Customer;
        Result = result;
        Customer.Mood = 0.4f;
    }

    float CheckStart => SlideSeconds;
    float RevealStart => SlideSeconds + CheckSeconds;
    float ReactAt => RevealStart + RowGap * (Scoring.MetricNames.Length - 1) + RowFill + 0.25f;
    float End => ReactAt + CoinSeconds + 0.2f;

    // True once everything has played; the shop then waits for Next.
    public bool Finished => t >= End;

    // Plays the timeline; a skip jumps to the end. Returns true when the player closes the shot.
    public bool Update(float dt, bool click, bool confirm, Vector2 mouse)
    {
        if (Finished)
        {
            bool next = confirm || (click && Raylib.CheckCollisionPointRec(mouse, NextButton));
            if (next) { Customer.Mood = null; Customer.Inspecting = false; }
            return next;
        }
        if (frozen) return false;

        t = click || confirm ? End : t + dt;
        bool skipped = click || confirm;

        // Sounds fire once each as the timeline passes them; a skip plays only the ending ones.
        if (!lifted && t >= CheckStart) { lifted = true; if (!skipped) Audio.Play(Audio.Crinkle, 0.7f); }
        Customer.Inspecting = t >= CheckStart && t < RevealStart;
        if (!chimed && t >= RevealStart) { chimed = true; if (!skipped) Audio.Play(Audio.Chime, 0.5f); }
        while (rowsTicked < Scoring.MetricNames.Length && t >= RevealStart + RowGap * rowsTicked)
        {
            if (!skipped && Day.Built(rowsTicked)) Audio.Play(Audio.Tick, 0.9f, 1.2f + 0.15f * rowsTicked);
            rowsTicked++;
        }
        if (!reacted && t >= ReactAt)
        {
            reacted = true;
            Customer.Mood = Result.Total >= 70 ? 1f : Result.Total >= 50 ? 0.4f : 0.1f;
            Audio.Play(Audio.Ding, 0.6f, Result.Total >= 70 ? 1f : 0.7f);
        }
        if (!tipped && t >= ReactAt + CoinSeconds)
        {
            tipped = true;
            if (Result.TipCents > 0) Audio.Play(Audio.Till, 0.7f);
        }
        return false;
    }

    // Holds the shot at a moment for the --shot-handover layout check: mid-reveal (false) or the end (true).
    public void HoldAt(bool end)
    {
        t = end ? End : RevealStart + RowGap * 1.5f;
        lifted = chimed = true;
        Customer.Inspecting = false;
        rowsTicked = Scoring.MetricNames.Length;
        if (end) { reacted = tipped = true; Customer.Mood = 1f; }
    }

    // Holds the shot half way through the customer's bag check, for the --shot-check layout check.
    public void HoldAtCheck()
    {
        t = CheckStart + CheckSeconds * 0.5f;
        lifted = frozen = true;
        Customer.Inspecting = true;
    }

    // Draws the shot: each row is drawn filled as far as the timeline has got.
    public void Draw(Vector2 mouse)
    {
        var shown = new float[Scoring.MetricNames.Length];
        for (int i = 0; i < shown.Length; i++)
            shown[i] = Math.Clamp((t - RevealStart - RowGap * i) / RowFill, 0f, 1f);

        float slide = Math.Clamp(t / SlideSeconds, 0f, 1f);
        float check = Math.Clamp((t - CheckStart) / CheckSeconds, 0f, 1f);
        float coin = t < ReactAt ? -1f : Math.Clamp((t - ReactAt) / CoinSeconds, 0f, 1f);
        string? said = t >= ReactAt ? Scoring.ReactionFor(Result.Total) : null;

        Art.DrawHandOverShot(Customer, Ticket, Result, slide, check, shown, t >= ReactAt, said, coin);
        if (Finished)
            Art.DrawButton(NextButton, "Next", Raylib.CheckCollisionPointRec(mouse, NextButton), true);
        else
            Art.LabelCentered("Click to skip", Program.Width / 2f, 680, 20, Art.Faint);
    }
}
