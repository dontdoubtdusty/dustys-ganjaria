namespace DustysGanjaria;

// The over-the-shoulder order shot at the counter: the customer says their order one item at a time,
// each item writes itself onto the ticket in the budtender's notepad, then the ticket goes up on the rail.
// A click (or Enter, or Space) skips to the next line.
sealed class OrderShot
{
    // Seconds each line stays in the speech bubble, and the pause after the last one.
    const float Beat = 1.0f, EndPause = 0.5f;

    public Customer Customer { get; }
    public Ticket Ticket { get; }

    readonly string[] speech;
    readonly string[] items;
    // 0 = greeting; 1..items = saying item step-1; after that the ticket is done.
    int step;
    float timer;

    public OrderShot(Ticket ticket)
    {
        Ticket = ticket;
        Customer = ticket.Customer;
        speech = new[] { "Hi there!", $"{Customer.Grams:0.0#} grams...", "...of flower, please." };
        items = new[] { $"{Customer.Grams:0.0#} g", "Flower" };
    }

    // True once every item is written and the closing pause is over.
    public bool Done => step > items.Length && timer >= EndPause;

    // Advances the speech; skip jumps straight to the next line.
    public void Update(float dt, bool skip)
    {
        timer += dt;
        bool talking = step <= items.Length;
        if (talking && (skip || timer >= Beat))
        {
            step++;
            timer = 0;
        }
        else if (!talking && skip) timer = EndPause;
    }

    // Holds the shot on a given line for the --shot-order layout check.
    public void HoldAt(int line)
    {
        step = line;
        timer = float.NegativeInfinity;
    }

    // Draws the shot: the customer across the counter, the speech bubble, and the notepad with the items written so far.
    public void Draw()
    {
        string? said = step < speech.Length ? speech[step] : null;
        // Each item is written as soon as the customer starts saying it.
        int written = Math.Min(step, items.Length);
        Art.DrawOrderShot(Customer, Ticket, said, items.Take(written).ToArray(), items.Length);
    }
}
