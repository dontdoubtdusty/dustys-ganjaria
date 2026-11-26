using System.Globalization;
using Raylib_cs;

namespace DustysGanjaria;

// What is wrong with an ID, if anything. A bad card has exactly one flaw, so every call is clear-cut.
enum IdFlaw { None, Underage, Expired, Photo }

// The ID a customer holds out at the order shot: their name, birth date, expiry date, and the photo's look (shirt
// and hair colours; null hair is bald). A Photo flaw means the photo shows a different look from the person.
sealed record IdCard(string Name, DateOnly Born, DateOnly Expires, Color PhotoShirt, Color? PhotoHair, IdFlaw Flaw);

static class Ids
{
    // The legal age to buy.
    public const int LegalAge = 21;

    // The shop calendar: day 1 is this date, each shop day the next.
    static readonly DateOnly FirstDay = new(2026, 4, 20);

    // Today's date on a shop day.
    public static DateOnly DateOf(int day) => FirstDay.AddDays(day - 1);

    // The last birth date that is old enough today (born on or before it).
    public static DateOnly Cutoff(DateOnly today) => today.AddYears(-LegalAge);

    // A date as the card and the counter sign print it, like Apr 22, 2005.
    public static string Text(DateOnly d) => d.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    // A date with its weekday, like Wed Apr 22, 2026.
    public static string LongText(DateOnly d) => d.ToString("ddd MMM d, yyyy", CultureInfo.InvariantCulture);

    // What a flaw is called when the game explains a call.
    public static string Why(IdFlaw flaw) => flaw switch
    {
        IdFlaw.Underage => "under 21",
        IdFlaw.Expired => "expired",
        IdFlaw.Photo => "the photo didn't match",
        _ => "nothing wrong",
    };

    // Makes the ID for a customer on a given day, with one flaw or none. Good cards are sometimes close calls (just
    // turned 21, expiring soon) so that the dates have to be read; bad ones are sometimes just over the line.
    public static IdCard Make(Customer c, DateOnly today, IdFlaw flaw, Random rng)
    {
        var cutoff = Cutoff(today);
        bool close = rng.NextDouble() < 0.4;

        var born = flaw == IdFlaw.Underage
            ? cutoff.AddDays(close ? rng.Next(1, 60) : rng.Next(60, 3 * 365))
            : cutoff.AddDays(-(close ? rng.Next(0, 60) : rng.Next(60, 45 * 365)));
        var expires = flaw == IdFlaw.Expired
            ? today.AddDays(-(close ? rng.Next(1, 45) : rng.Next(45, 2 * 365)))
            : today.AddDays(close ? rng.Next(3, 45) : rng.Next(45, 6 * 365));

        var shirt = c.Shirt;
        var hair = c.Hair;
        if (flaw == IdFlaw.Photo)
        {
            // Change one thing about the look, chosen so the difference can be seen.
            if (rng.Next(2) == 0) shirt = Customer.FarShirt(c.Shirt, rng);
            else hair = Customer.OtherHair(c.Hair, rng);
        }
        return new IdCard(c.Name, born, expires, shirt, hair, flaw);
    }
}
