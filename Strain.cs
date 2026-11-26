using Raylib_cs;

namespace DustysGanjaria;

// One strain of flower: its name, its bud colours, the mix of bud sizes in its jar, the colour of its labels,
// and the rank that unlocks it. Shake is always 0.05-0.15 g.
sealed record Strain(
    string Name, string Short, Color[] Shades, Color Hair, Color Tag,
    float ShakeShare, float SmallShare, float SmallMin, float SmallMax, float LargeMin, float LargeMax, int Rank);

static class Strains
{
    // Most strains on the menu (and jars on the weigh bench, label pads at Label) in one day.
    public const int MenuSize = 3;

    // Every strain, in unlock order. Names are our own.
    public static readonly Strain[] All =
    {
        // Light, fluffy and small: lots of little buds.
        new("Prairie Haze", "PRAIRIE",
            new Color[] { new(150, 190, 90, 255), new(170, 205, 105, 255), new(135, 175, 85, 255) },
            new(214, 140, 70, 255), new(104, 168, 64, 255), 0.25f, 0.5f, 0.25f, 0.5f, 0.5f, 0.8f, 1),
        // Dark and dense: few small buds, big chunky ones.
        new("Midnight Kush", "MIDNIGHT",
            new Color[] { new(84, 72, 114, 255), new(104, 92, 134, 255), new(72, 104, 72, 255) },
            new(232, 150, 80, 255), new(112, 82, 164, 255), 0.2f, 0.2f, 0.4f, 0.7f, 0.8f, 1.4f, 1),
        // The all-rounder: the old jar's mix, with copper hairs.
        new("Copper Sun", "COPPER",
            new Color[] { new(176, 150, 72, 255), new(196, 164, 84, 255), new(140, 156, 70, 255) },
            new(230, 110, 50, 255), new(210, 120, 50, 255), 0.25f, 0.35f, 0.3f, 0.6f, 0.6f, 1.0f, 1),
        // Frosty and small: the smallest buds and the most shake, but enough mid-size nugs that a 7 g order is about
        // a dozen picks (it was about two dozen, which felt like a chore).
        new("Frost Mule", "FROST",
            new Color[] { new(172, 202, 172, 255), new(200, 222, 202, 255), new(150, 186, 152, 255) },
            new(238, 242, 246, 255), new(110, 170, 196, 255), 0.3f, 0.4f, 0.2f, 0.4f, 0.5f, 0.8f, 3),
        // A wild mix: small buds and some very big ones.
        new("Dust Devil", "DUST",
            new Color[] { new(150, 140, 80, 255), new(172, 152, 92, 255), new(122, 132, 72, 255) },
            new(200, 90, 60, 255), new(172, 132, 70, 255), 0.2f, 0.3f, 0.2f, 0.5f, 0.9f, 1.6f, 5),
    };

    // The day's menu at a rank: every unlocked strain if they fit, otherwise the newest one plus a random pick of
    // the rest, in unlock order.
    public static Strain[] MenuFor(int rank, Random rng)
    {
        var open = All.Where(s => s.Rank <= rank).ToList();
        if (open.Count <= MenuSize) return open.ToArray();
        var newest = open[^1];
        var rest = open.Take(open.Count - 1).OrderBy(_ => rng.Next()).Take(MenuSize - 1);
        return rest.Append(newest).OrderBy(s => Array.IndexOf(All, s)).ToArray();
    }

    // The next strain still locked at a rank, or null when all are open.
    public static Strain? NextLocked(int rank) => All.FirstOrDefault(s => s.Rank > rank);
}
