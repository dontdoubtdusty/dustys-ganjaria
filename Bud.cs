using System.Numerics;

namespace DustysGanjaria;

// One piece of flower: its weight, its physics state, and the lobes that give its placeholder shape.
sealed class Bud
{
    // Radius grows with the square root of weight, so a bud's area matches its weight and size never lies.
    const float RadiusPerRootGram = 26f;

    public float Grams { get; private set; }
    // The strain it is: its colours, and whether it is what the order asked for.
    public Strain Strain { get; }
    public float Radius { get; private set; }
    public float InvMass { get; private set; }
    public Vector2 Pos;
    public Vector2 Vel;
    public float Angle;
    // Fastest hit this frame (speed into a wall or another bud); the station reads and resets it for sounds.
    public float Impact;
    // 0-1, how squashed the bud looks after a hard landing; fades back to 0.
    public float Squash;
    // Seconds before this bud can tick again, so a rolling bud does not buzz.
    public float TickCooldown;

    // Riding buds sit on the carried weigh tray: no physics, they move with the tray at LocalOffset.
    public bool Riding;
    // True from the moment the tongs let go of the bud until it first lands; a hard landing then chips a crumb.
    public bool Dropped;

    // Sealed buds are inside a closed bag: no physics, they ride along with the bag at LocalOffset.
    public bool Sealed;
    public Vector2 LocalOffset;
    // Drawing size; sealed buds shrink with the pressed bag.
    public float Scale = 1f;

    public (Vector2 Offset, float Radius, int Shade)[] Lobes { get; }

    // Which sprite on its sheet this bud uses (taken modulo the sheet's count when drawn).
    public int Pick { get; }
    // Sprites are lit from above, so they start upright with a small tilt and turn only as the bud rolls.
    readonly float spinStart, tilt;
    public float SpriteAngle => tilt + Angle - spinStart;

    // What kind of piece it is, fixed when the jar is filled: shake (the flat leafy crumbs, drawn from the shake sheet),
    // a small bud or a large nug. Chipping a crumb off never changes it, so a piece that looks like a bud is never shake.
    public bool IsShake { get; }
    public bool IsLarge { get; }

    // Makes a piece of a strain, weight and kind, with lobe shapes picked from rng.
    public Bud(float grams, Strain strain, Random rng, bool isShake = false, bool isLarge = false)
    {
        Grams = grams;
        IsShake = isShake;
        IsLarge = isLarge;
        Strain = strain;
        Radius = RadiusPerRootGram * MathF.Sqrt(grams);
        InvMass = 1f / grams;
        Angle = (float)(rng.NextDouble() * Math.Tau);
        spinStart = Angle;
        tilt = (float)(rng.NextDouble() * 0.7 - 0.35);
        Pick = rng.Next(1000);

        int count = IsShake ? 1 : 4 + rng.Next(3);
        Lobes = new (Vector2, float, int)[count];
        for (int i = 0; i < count; i++)
        {
            if (count == 1) { Lobes[i] = (Vector2.Zero, Radius, rng.Next(3)); continue; }
            float angle = (float)(rng.NextDouble() * Math.Tau);
            float dist = Radius * (0.35f + 0.2f * (float)rng.NextDouble());
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
            Lobes[i] = (offset, Radius * (0.5f + 0.15f * (float)rng.NextDouble()), rng.Next(3));
        }
    }

    // Knocks a crumb of this many grams off the bud (a hard landing); it gets a little smaller.
    public void Chip(float grams)
    {
        Grams = MathF.Max(0.01f, Grams - grams);
        Radius = RadiusPerRootGram * MathF.Sqrt(Grams);
        InvMass = 1f / Grams;
    }

    // True if the point is on this bud.
    public bool Contains(Vector2 point) => Vector2.Distance(point, Pos) <= Radius;
}
