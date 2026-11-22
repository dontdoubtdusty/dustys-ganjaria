namespace DustysGanjaria;

// The outcome of one bagged order, with its part scores (0-100 each).
readonly record struct WeighResult(
    float Target, float Band, float Weight, float Seconds, int Slips, int Spilled,
    int Accuracy, int Speed, int Handling, int Total, string Grade);

// The outcome of one served customer: a score (0-100) for each of the four things every order is judged on
// (null for a station that is not built yet), the order score, grade, tip in cents, and seconds waited.
readonly record struct OrderResult(
    WeighResult Weigh, int Waiting, int? Packing, int? Labeling, int Total, string Grade, int TipCents, float Waited)
{
    // The four scores in station order, matching Scoring.MetricNames.
    public int?[] Metrics => new int?[] { Waiting, Weigh.Total, Packing, Labeling };
}

// Turns finished work into scores. All tuning numbers for scoring live here.
static class Scoring
{
    // The four things every order is judged on, in station order (Counter, Weigh, Pack, Label).
    public static readonly string[] MetricNames = { "Waiting", "Weighing", "Packing", "Labeling" };

    // Seconds allowed before speed starts dropping: a base plus some per gram.
    const float ParBase = 10f, ParPerGram = 2f;
    // How far outside the green band (in band widths) accuracy reaches zero.
    const float BandsToZero = 4f;
    // Points lost per slipped zip and per bud left lying outside the jar.
    const int SlipCost = 25, SpillCost = 10;
    // Share of the total for accuracy and speed; handling gets the rest.
    const float AccuracyShare = 0.5f, SpeedShare = 0.3f;

    // How wide the green band is either side of the target, in grams.
    public static float BandFor(float target) => 0.08f + 0.03f * target;

    // Scores a bag from target, sealed weight, time taken, zip slips and spilled buds.
    public static WeighResult Score(float target, float band, float weight, float seconds, int slips, int spilled)
    {
        float off = MathF.Abs(weight - target);
        float acc = off <= band ? 100f : 100f * (1f - (off - band) / (band * BandsToZero));

        float par = ParBase + ParPerGram * target;
        float speed = seconds <= par ? 100f : 100f * (1f - (seconds - par) / (2f * par));

        float handling = 100f - SlipCost * slips - SpillCost * spilled;

        int a = Clamp(acc), s = Clamp(speed), h = Clamp(handling);
        int total = (int)MathF.Round(AccuracyShare * a + SpeedShare * s + (1f - AccuracyShare - SpeedShare) * h);
        return new WeighResult(target, band, weight, seconds, slips, spilled, a, s, h, total, GradeFor(total));
    }

    // ---------- Customers ----------

    // Seconds a customer waits in line before walking out, if nobody takes their order.
    public const float LinePatience = 70f;
    // Seconds a customer waits for their order once it is taken; bigger orders get a little longer.
    public static float WaitPatience(float grams) => 60f + 6f * grams;

    // The Waiting score covers the whole visit: seconds in line plus seconds from order to hand-over.
    // Full marks up to par, then it drops to zero over WaitZeroAfter more seconds. Par grows with the order
    // size; raise OrderParBase when the Pack and Label stations are added (they make every order longer).
    const float LinePar = 12f, OrderParBase = 40f, OrderParPerGram = 4f, WaitZeroAfter = 60f;
    public static float WaitPar(float grams) => LinePar + OrderParBase + OrderParPerGram * grams;

    // Scores a served order: each built station's score plus the wait, averaged, and the tip it earns.
    public static OrderResult ScoreOrder(WeighResult weigh, float grams, float waited)
    {
        float par = WaitPar(grams);
        int wait = Clamp(waited <= par ? 100f : 100f * (1f - (waited - par) / WaitZeroAfter));
        int?[] metrics = { wait, weigh.Total, null, null };
        var built = metrics.Where(m => m != null).Select(m => m!.Value).ToList();
        int total = (int)MathF.Round((float)built.Average());
        return new OrderResult(weigh, wait, null, null, total, GradeFor(total), TipFor(total), waited);
    }

    // Tip in cents: nothing below 40, then rising faster toward the top, to $5.00 at 100; rounded to 5 cents.
    static int TipFor(int total)
    {
        if (total < 40) return 0;
        float t = (total - 40) / 60f;
        float dollars = 0.5f + 4.5f * MathF.Pow(t, 1.3f);
        return (int)MathF.Round(dollars * 20f) * 5;
    }

    // What the customer says when they see their score.
    public static string ReactionFor(int total) => total switch
    {
        >= 95 => "Perfect! You're the best!",
        >= 85 => "Great, thank you!",
        >= 70 => "Nice, thanks.",
        >= 50 => "It'll do.",
        _ => "...seriously?",
    };

    // Names a total score.
    static string GradeFor(int total) => total switch
    {
        >= 95 => "Perfect",
        >= 85 => "Great",
        >= 70 => "Good",
        >= 50 => "OK",
        _ => "Poor",
    };

    // Money as the game shows it, like $3.45.
    public static string Money(int cents) => $"${cents / 100}.{cents % 100:00}";

    static int Clamp(float v) => (int)MathF.Round(Math.Clamp(v, 0f, 100f));
}
