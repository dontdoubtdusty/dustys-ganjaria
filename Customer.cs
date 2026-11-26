using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// What a customer asks about how their bag looks (a deli-counter habit): nothing, big nugs, or no shake.
enum Looks { None, BigNugs, NoShake }

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
    // The same names by kind, for sprite customers (the name should fit the person drawn). Jun and Sol go either way.
    static readonly string[] WomensNames =
        { "Mara", "Rosa", "Jun", "Priya", "Kiki", "Bea", "Lena", "Sol", "Ida", "Nell", "Tess", "Fern", "Vi", "June" };
    static readonly string[] MensNames =
        { "Dev", "Theo", "Jun", "Walt", "Omar", "Gus", "Sol", "Ray", "Arlo", "Bo", "Cal", "Hank" };
    static readonly Color[] Shirts =
    {
        new(200, 90, 90, 255), new(90, 140, 210, 255), new(220, 180, 70, 255), new(150, 100, 190, 255),
        new(80, 170, 150, 255), new(230, 130, 60, 255), new(170, 170, 180, 255), new(120, 160, 80, 255),
    };
    static readonly Color[] Pants =
    {
        new(70, 80, 110, 255), new(95, 85, 75, 255), new(60, 60, 66, 255), new(120, 110, 90, 255),
    };
    // Hair colours; one in HairOptions is bald (null).
    static readonly Color?[] HairOptions =
    {
        new Color(40, 32, 28, 255), new Color(110, 72, 40, 255), new Color(220, 180, 100, 255),
        new Color(170, 70, 40, 255), new Color(190, 190, 195, 255), null,
    };
    static readonly float[] Orders = { 1f, 3.5f, 7f };

    const float WalkSpeed = 260f;

    public string Name { get; }
    public Color Shirt { get; }
    public Color Trousers { get; }
    // Placeholder hair colour (null = bald), so ID photos can differ by hair as well as by shirt.
    public Color? Hair { get; }
    // Which cast member they are (an index into Art's customer sprites; -1 = the drawn placeholder).
    public int Body { get; }
    // The ID they hold out when ordering; the shop makes it when they arrive (it needs the day's date).
    public IdCard Id { get; set; } = null!;
    public float Grams { get; }
    // The strain they asked for, from the day's menu.
    public Strain Strain { get; }
    // The container they asked for. The size is not said: it follows the grams (Containers.SizeFor).
    // Settable only so screenshot runs can stage a jar order.
    public ContainerKind Wants { get; set; }
    // What they asked about the bag's looks; the shop sets it when they arrive (requests start on a later day).
    public Looks Request { get; set; }
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
    // Sprite customers are someone not already in the shop (present), with a name nobody there has.
    public Customer(Random rng, Vector2 start, Strain[] menu, IReadOnlyCollection<Customer>? present = null)
    {
        Name = Names[rng.Next(Names.Length)];
        Shirt = Shirts[rng.Next(Shirts.Length)];
        Trousers = Pants[rng.Next(Pants.Length)];
        Hair = HairOptions[rng.Next(HairOptions.Length)];
        Grams = Orders[rng.Next(Orders.Length)];
        Strain = menu[rng.Next(menu.Length)];
        Wants = rng.Next(2) == 0 ? ContainerKind.Pouch : ContainerKind.Jar;
        PatienceMax = Patience = Scoring.LinePatience;
        Pos = Goal = start;
        // Picked last, so the draws above stay as they were before sprites.
        present ??= Array.Empty<Customer>();
        Body = Art.CustomerBodyCount > 0 ? Art.PickBody(rng, present.Select(c => c.Body)) : -1;
        if (Body >= 0)
        {
            var names = Art.BodyIsFeminine(Body) ? WomensNames : MensNames;
            var free = names.Where(n => present.All(c => c.Name != n)).ToArray();
            if (free.Length == 0) free = names;
            Name = free[rng.Next(free.Length)];
        }
    }

    // A shirt colour clearly different from the given one (for a photo that does not match): one of the shirts
    // whose colour is far from it.
    public static Color FarShirt(Color shirt, Random rng)
    {
        static int Distance(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
        var far = Shirts.Where(s => Distance(s, shirt) > 150).ToArray();
        return far.Length > 0 ? far[rng.Next(far.Length)] : Shirts[0];
    }

    // A hair colour other than the given one (bald counts as one of the choices).
    public static Color? OtherHair(Color? hair, Random rng)
    {
        var others = HairOptions.Where(h => !Equals(h, hair)).ToArray();
        return others[rng.Next(others.Length)];
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

    // The looks request as the tickets say it ("Big nugs", "No shake"), or null.
    public string? RequestText => Request switch { Looks.BigNugs => "Big nugs", Looks.NoShake => "No shake", _ => null };

    // The container with its size, as the budtender writes it on the notepad and the station ticket: "Large jar".
    // The customer only says the kind; the size follows the grams.
    public string ContainerText => (Grams >= Containers.LargeFrom ? "Large " : "Small ") + (Wants == ContainerKind.Jar ? "jar" : "pouch");

    // The same, short, as the rail ticket and the shelf labels say it: "Jar L".
    public string ContainerShort => (Wants == ContainerKind.Jar ? "Jar " : "Pouch ") + (Grams >= Containers.LargeFrom ? "L" : "S");
}
