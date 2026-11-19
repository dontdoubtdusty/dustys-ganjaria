using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// One customer: who they are, what they want, how much patience they have left, and where they are walking.
sealed class Customer
{
    public enum State { InLine, Waiting, Leaving }

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
    static readonly float[] Orders = { 1f, 3.5f, 7f };

    const float WalkSpeed = 260f;

    public string Name { get; }
    public Color Shirt { get; }
    public float Grams { get; }
    public State Now = State.InLine;
    public float Patience, PatienceMax;
    public Vector2 Pos, Goal;
    // How they felt when they left: true if served, false if they walked out.
    public bool Served;

    // Makes a random customer standing at the given spot, just off screen.
    public Customer(Random rng, Vector2 start)
    {
        Name = Names[rng.Next(Names.Length)];
        Shirt = Shirts[rng.Next(Shirts.Length)];
        Grams = Orders[rng.Next(Orders.Length)];
        PatienceMax = Patience = Scoring.LinePatience;
        Pos = Goal = start;
    }

    // Share of patience left, 0 to 1.
    public float PatienceFraction => PatienceMax <= 0 ? 0 : Math.Clamp(Patience / PatienceMax, 0f, 1f);

    // True once the customer has reached the spot they were walking to.
    public bool Arrived => MathF.Abs(Pos.X - Goal.X) < 2f;

    // Walks toward the goal at a steady pace.
    public void Walk(float dt)
    {
        float dx = Goal.X - Pos.X;
        float step = WalkSpeed * dt;
        Pos.X = MathF.Abs(dx) <= step ? Goal.X : Pos.X + MathF.Sign(dx) * step;
    }

    // The order as the ticket and speech bubble say it.
    public string OrderText => $"{Grams:0.0#} g flower";
}
