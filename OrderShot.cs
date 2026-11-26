using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The over-the-shoulder order shot at the counter. The customer says hi and holds out their ID; the player checks
// it against the date sign and the customer and clicks Accept or Refuse (no timer: the shop is paused). Accepted,
// they say their order one item at a time, each item writes itself onto the ticket in the budtender's notepad, and
// the ticket goes up on the rail. Refused, they say a line and leave (Refused is then true). A click (or Enter, or
// Space) skips to the next line, but never past the ID.
sealed class OrderShot
{
    // Seconds each line stays in the speech bubble, the pause after the last one, and how long a refused customer
    // takes to answer before leaving.
    const float Beat = 1.0f, EndPause = 0.5f, RefusedSeconds = 1.6f;

    static readonly Rectangle CardRect = new(850, 250, 390, 210);
    static readonly Rectangle SignRect = new(20, 120, 360, 112);
    static readonly Rectangle AcceptButton = new(850, 580, 185, 56);
    static readonly Rectangle RefuseButton = new(1055, 580, 185, 56);

    enum Stage { Greet, Id, Order, Refused }

    public Customer Customer { get; }
    public Ticket Ticket { get; }
    // True once the player refused the ID; the shop sends the customer out instead of taking the order.
    public bool Refused => stage == Stage.Refused;

    readonly DateOnly today;
    readonly string[] speech;
    readonly string[] items;
    Stage stage = Stage.Greet;
    // While ordering: 1..items = saying item step-1; after that the ticket is done.
    int step;
    float timer;

    public OrderShot(Ticket ticket, DateOnly today)
    {
        Ticket = ticket;
        Customer = ticket.Customer;
        this.today = today;
        string container = Customer.Wants == ContainerKind.Jar ? "In a jar, thanks." : "In a pouch, thanks.";
        speech = new[] { "Hi there!", $"{Customer.Grams:0.0#} grams...", $"...of {Customer.Strain.Name}, please.", container };
        items = new[] { $"{Customer.Grams:0.0#} g", Customer.Strain.Name, Customer.ContainerText };
    }

    // True once the order is written and the closing pause is over, or a refused customer has said their piece.
    public bool Done => stage switch
    {
        Stage.Order => step > items.Length && timer >= EndPause,
        Stage.Refused => timer >= RefusedSeconds,
        _ => false,
    };

    // Advances the shot. skip moves to the next line; press and mouse work the Accept and Refuse buttons.
    public void Update(float dt, bool skip, bool press, Vector2 mouse)
    {
        timer += dt;
        switch (stage)
        {
            case Stage.Greet:
                if (skip || timer >= Beat) { stage = Stage.Id; timer = 0; }
                break;
            case Stage.Id:
                if (press && Raylib.CheckCollisionPointRec(mouse, AcceptButton))
                {
                    stage = Stage.Order;
                    step = 1;
                    timer = 0;
                    Audio.Play(Audio.Tick, 0.8f);
                }
                else if (press && Raylib.CheckCollisionPointRec(mouse, RefuseButton))
                {
                    stage = Stage.Refused;
                    timer = 0;
                    Audio.Play(Audio.Slip, 0.5f, 0.8f);
                }
                break;
            case Stage.Order:
                bool talking = step <= items.Length;
                if (talking && (skip || timer >= Beat)) { step++; timer = 0; }
                else if (!talking && skip) timer = EndPause;
                break;
            case Stage.Refused:
                if (skip && timer > 0.3f) timer = RefusedSeconds;
                break;
        }
    }

    // Holds the shot on an order line for the --shot-order layout check (the ID already accepted).
    public void HoldAt(int line)
    {
        stage = Stage.Order;
        step = line;
        timer = float.NegativeInfinity;
    }

    // Holds the shot on the ID check, for the --shot-id layout check.
    public void HoldAtId() => stage = Stage.Id;

    // Draws the shot: the customer across the counter, the speech bubble, and the notepad with the items written
    // so far; while checking, the ID card, the date sign and the two buttons on top.
    public void Draw()
    {
        string? said = stage switch
        {
            Stage.Greet => speech[0],
            Stage.Id => "Here's my ID.",
            Stage.Refused => Customer.Id.Flaw == IdFlaw.None ? "What? That's me!" : "Worth a try...",
            _ => step < speech.Length ? speech[step] : null,
        };
        // Each item is written as soon as the customer starts saying it.
        int written = stage == Stage.Order ? Math.Min(step, items.Length) : 0;
        Art.DrawOrderShot(Customer, Ticket, said, items.Take(written).ToArray(), items.Length, stage != Stage.Id);
        if (stage != Stage.Id) return;

        var mouse = Raylib.GetMousePosition();
        Art.DrawDateSign(SignRect, today);
        Art.DrawIdCard(CardRect, Customer.Id);
        Art.DrawButton(AcceptButton, "Accept", Raylib.CheckCollisionPointRec(mouse, AcceptButton), true);
        Art.DrawRefuseButton(RefuseButton, "Refuse", Raylib.CheckCollisionPointRec(mouse, RefuseButton));
        Art.LabelCentered("Check the date of birth, the expiry and the photo", Program.Width / 2f, 640, 20, Art.Faint);
    }
}
