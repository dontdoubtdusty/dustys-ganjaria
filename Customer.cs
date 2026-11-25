using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// One customer: who they are, what they want, how much patience they have left, and where they are walking.
// Pos is where their feet touch the floor in the shop view.
sealed class Customer
{
    public enum State { InLine, Ordering, Waiting, Leaving }

    // Placeholder looks until the owner draws sprites.
    static readonly string[] Names =
    {
        "Mara", "Dev", "Rosa", "Theo", "Jun", "Priya", "Walt", "Kiki", "Omar", "Bea", "Gus", "Lena",
        "Sol", "Ida", "Ray", "Nell", "Arlo", "Tess", "Bo", "Fern", "Cal", "Vi", "Hank", "June",
    };
    static readonly Color[] Shirts =
    {
        new(200, 90, 90, 255), new(90, 140, 210, 255), new(220, 180, 70, 255), new(150, 100, 190, 255),
        new(80, 170, 150, 255), new(230, 130, 60, 255), new(170, 170, 180, 255), new(120, 160, 80, 255),
    };
    static readonly Color[] Pants =
    {
        new(70, 80, 110, 255), new(95, 85, 75, 255), new(60, 60, 66, 255), new(120, 110, 90, 255),
    };
    static readonly float[] Orders = { 1f, 3.5f, 7f };

    const float WalkSpeed = 260f;

    public string Name { get; }
    public Color Shirt { get; }
    public Color Trousers { get; }
    public float Grams { get; }
    // The strain they asked for, from the day's menu.
    public Strain Strain { get; }
    // The container they asked for. The size is not said: it follows the grams (Containers.SizeFor).
    // Settable only so screenshot runs can stage a jar order.
    public ContainerKind Wants { get; set; }
    public State Now = State.InLine;
    public float Patience, PatienceMax;
    public Vector2 Pos, Goal;
    // True once they have reached the line for the first time; line patience only drains from then on.
    public bool Reached;
    // How they felt when they left: true if served, false if they walked out.
    public bool Served;
    // Seconds spent waiting so far: in line once they reached it, then from order to hand-over. Paused shots
    // (taking the order, the hand-over) do not count. The Waiting score is judged on this.
    public float Waited;
    // A face set from outside (0 = upset, 1 = happy), used in the hand-over shot; null follows patience.
    public float? Mood;
    // True while they look the bag over in the hand-over shot: eyes on the bag, one brow up, a neutral "hmm".
    public bool Inspecting;

    // Makes a random customer standing at the given spot (the door), ordering a strain from the menu.
    public Customer(Random rng, Vector2 start, Strain[] menu)
    {
        Name = Names[rng.Next(Names.Length)];
        Shirt = Shirts[rng.Next(Shirts.Length)];
        Trousers = Pants[rng.Next(Pants.Length)];
        Grams = Orders[rng.Next(Orders.Length)];
        Strain = menu[rng.Next(menu.Length)];
        Wants = rng.Next(2) == 0 ? ContainerKind.Pouch : ContainerKind.Jar;
        PatienceMax = Patience = Scoring.LinePatience;
        Pos = Goal = start;
    }

    // Share of patience left, 0 to 1.
    public float PatienceFraction => PatienceMax <= 0 ? 0 : Math.Clamp(Patience / PatienceMax, 0f, 1f);

    // True once the customer has reached the spot they were walking to.
    public bool Arrived => Vector2.Distance(Pos, Goal) < 2f;

    // Walks straight toward the goal at a steady pace.
    public void Walk(float dt)
    {
        var to = Goal - Pos;
        float step = WalkSpeed * dt;
        Pos = to.Length() <= step ? Goal : Pos + Vector2.Normalize(to) * step;
        if (Now == State.InLine && Arrived) Reached = true;
    }

    // The amount as the tickets say it.
    public string OrderText => $"{Grams:0.0#} g";

    // The whole order on one line, like 3.5 g Midnight Kush.
    public string FullOrderText => $"{Grams:0.0#} g {Strain.Name}";

    // The container as the tickets say it.
    public string ContainerText => Wants == ContainerKind.Jar ? "Jar" : "Pouch";
}
