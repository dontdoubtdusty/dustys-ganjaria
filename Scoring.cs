namespace DustysGanjaria;

// The outcome of one bagged order, with its part scores (0-100 each). Weight is what the scale read; WrongBuds
// counts buds of another strain sealed in with it.
readonly record struct WeighResult(
    float Target, float Band, float Weight, float Seconds, int Slips, int Spilled,
    int Accuracy, int Speed, int Handling, int Total, string Grade, int WrongBuds = 0);

// The outcome of one labeled order: the strain asked for and the label used, how far off and how crooked it went
// on, peels, and the part scores (0-100 each). At (offset over the container's size) and Angle let the hand-over
// draw the label where it actually went.
readonly record struct LabelResult(
    Strain Wanted, Strain Used, float Seconds, float Off, float Angle, int Peels, System.Numerics.Vector2 At,
    int Match, int Placement, int Speed, int Total);

// The outcome of one packed order: the container asked for and the one used, and the part scores (0-100 each).
readonly record struct PackResult(
    Container Wanted, Container Used, float Seconds, int CrossThreads, int Drops, int Restarts,
    int Fit, int Speed, int Handling, int Total);

// The outcome of one served customer: a score (0-100) for each of the four things every order is judged on,
// the order score, grade, tip in cents, and seconds waited.
// FineCents is taken off the tip when a bad ID was accepted (Flaw says what was wrong with it).
readonly record struct OrderResult(
    WeighResult Weigh, PackResult Pack, LabelResult Label, int Waiting, int Total, string Grade, int TipCents, float Waited,
    int FineCents = 0, IdFlaw Flaw = IdFlaw.None)
{
    // The four scores in station order, matching Scoring.MetricNames (nullable so a future unbuilt station can show
    // as not built).
    public int?[] Metrics => new int?[] { Waiting, Weigh.Total, Pack.Total, Label.Total };
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

    // Points lost per bud of the wrong strain sealed in the bag.
    const int WrongBudCost = 15;

    // Seconds a bag of this many grams may take before weigh speed starts dropping.
    public static float WeighPar(float target) => ParBase + ParPerGram * target;

    // Scores a bag from target, sealed weight, time taken, zip slips and spilled buds. Buds of the wrong strain
    // (wrongBuds of them, wrongGrams in all) weigh on the scale but do not count toward the order's weight, and each
    // one costs handling: a bag of the wrong strain scores no accuracy at all.
    public static WeighResult Score(float target, float band, float weight, float seconds, int slips, int spilled,
                                    float wrongGrams = 0, int wrongBuds = 0)
    {
        float off = MathF.Abs(weight - wrongGrams - target);
        float acc = off <= band ? 100f : 100f * (1f - (off - band) / (band * BandsToZero));

        float par = WeighPar(target);
        float speed = seconds <= par ? 100f : 100f * (1f - (seconds - par) / (2f * par));

        float handling = 100f - SlipCost * slips - SpillCost * spilled - WrongBudCost * wrongBuds;

        int a = Clamp(acc), s = Clamp(speed), h = Clamp(handling);
        int total = (int)MathF.Round(AccuracyShare * a + SpeedShare * s + (1f - AccuracyShare - SpeedShare) * h);
        return new WeighResult(target, band, weight, seconds, slips, spilled, a, s, h, total, GradeFor(total), wrongBuds);
    }

    // ---------- Packing ----------

    // Container points: right kind and size, right kind but wrong size, wrong kind but right size, both wrong.
    const int FitRight = 100, FitWrongSize = 40, FitWrongKind = 20, FitBothWrong = 0;
    // Seconds allowed before packing speed starts dropping (a jar has more turning to do than a pouch strip).
    const float PackParPouch = 7f, PackParJar = 9f;
    // Points lost per cross-threaded lid and per bag dropped on the table.
    const int CrossThreadCost = 25, DropCost = 10;
    // Share of the Packing score for the container and for speed; handling gets the rest.
    const float FitShare = 0.5f, PackSpeedShare = 0.3f;
    // Seconds Start over adds to the packing clock.
    public const float RestartSeconds = 5f;

    // Seconds packing this kind of container may take before speed starts dropping.
    public static float PackPar(ContainerKind kind) => kind == ContainerKind.Jar ? PackParJar : PackParPouch;

    // Scores a closed container: the right one, how long it took, cross-threaded lids and dropped bags.
    public static PackResult ScorePack(Container wanted, Container used, float seconds, int crossThreads, int drops, int restarts)
    {
        bool kind = wanted.Kind == used.Kind, size = wanted.Large == used.Large;
        int fit = kind && size ? FitRight : kind ? FitWrongSize : size ? FitWrongKind : FitBothWrong;

        float par = PackPar(used.Kind);
        float speed = seconds <= par ? 100f : 100f * (1f - (seconds - par) / (2f * par));
        float handling = 100f - CrossThreadCost * crossThreads - DropCost * drops;

        int s = Clamp(speed), h = Clamp(handling);
        int total = (int)MathF.Round(FitShare * fit + PackSpeedShare * s + (1f - FitShare - PackSpeedShare) * h);
        return new PackResult(wanted, used, seconds, crossThreads, drops, restarts, fit, s, h, total);
    }

    // ---------- Labeling ----------

    // Share of the Labeling score for the right strain and for placement; speed gets the rest.
    const float MatchShare = 0.5f, PlaceShare = 0.3f;
    // Placement: full marks within this many pixels of the guide and degrees of straight, zero at the far value.
    public const float OffFull = 8f, OffZero = 60f, AngleFull = 2f, AngleZero = 15f;
    // Seconds allowed before labeling speed starts dropping.
    public const float LabelPar = 6f;

    // Placement points (0-100) for a label this many pixels off the guide and this many degrees crooked.
    public static int PlacementFor(float off, float angle)
    {
        static float Ramp(float v, float full, float zero) => v <= full ? 100f : 100f * (1f - (v - full) / (zero - full));
        return Clamp((Ramp(off, OffFull, OffZero) + Ramp(MathF.Abs(angle), AngleFull, AngleZero)) / 2f);
    }

    // Scores a pressed-on label: the right strain, how close to the guide and how straight, and how long it took.
    // Peels cost only the time they take.
    public static LabelResult ScoreLabel(Strain wanted, Strain used, float seconds, float off, float angle, int peels,
                                         System.Numerics.Vector2 at)
    {
        int match = wanted == used ? 100 : 0;
        int place = PlacementFor(off, angle);
        int s = Clamp(seconds <= LabelPar ? 100f : 100f * (1f - (seconds - LabelPar) / (2f * LabelPar)));
        int total = (int)MathF.Round(MatchShare * match + PlaceShare * place + (1f - MatchShare - PlaceShare) * s);
        return new LabelResult(wanted, used, seconds, off, angle, peels, at, match, place, s, total);
    }

    // ---------- ID check ----------

    // Bad IDs start on this day (day 1 teaches the flow with good ones only).
    public const int BadIdsFromDay = 2;
    // Chance a customer's ID is bad, never two in a row; with that rule about 1 in 6 come out bad.
    public const float BadIdChance = 0.2f;
    // Accepting a bad ID: this fine comes out of the order's tip (the day's tips can go below zero, the jar cannot).
    public const int IdFineCents = 500;
    // Refusing a bad ID: rank points for the catch. Refusing a good one counts as a walkout.
    public const int IdCatchPoints = 15;

    // ---------- Customers ----------

    // Seconds a customer waits in line before walking out, if nobody takes their order.
    // Went from 70 to 73.5 (+5%) in 0.13.1.
    public const float LinePatience = 73.5f;
    // Seconds a customer waits for their order once it is taken; bigger orders get a little longer. The base went
    // from 60 to 72 when Label was added; base and per-gram went up 5% in 0.13.1.
    public static float WaitPatience(float grams) => 75.6f + 6.3f * grams;

    // The Waiting score covers the whole visit: seconds in line plus seconds from order to hand-over.
    // Full marks up to par, then it drops to zero over WaitZeroAfter more seconds. Par grows with the order
    // size; OrderParBase went from 40 to 52 when Pack was added, and to 62 with Label.
    const float LinePar = 12f, OrderParBase = 62f, OrderParPerGram = 4f, WaitZeroAfter = 60f;
    public static float WaitPar(float grams) => LinePar + OrderParBase + OrderParPerGram * grams;

    // Scores a served order: the four metrics averaged, and the tip it earns. An accepted bad ID (flaw) adds the
    // fine; it does not change the scores.
    public static OrderResult ScoreOrder(WeighResult weigh, PackResult pack, LabelResult label, float grams, float waited,
                                         IdFlaw flaw = IdFlaw.None)
    {
        float par = WaitPar(grams);
        int wait = Clamp(waited <= par ? 100f : 100f * (1f - (waited - par) / WaitZeroAfter));
        int total = (int)MathF.Round((wait + weigh.Total + pack.Total + label.Total) / 4f);
        int fine = flaw == IdFlaw.None ? 0 : IdFineCents;
        return new OrderResult(weigh, pack, label, wait, total, GradeFor(total), TipFor(total), waited, fine, flaw);
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

    // Money as the game shows it, like $3.45 or -$5.00.
    public static string Money(int cents) =>
        $"{(cents < 0 ? "-" : "")}${Math.Abs(cents) / 100}.{Math.Abs(cents) % 100:00}";

    static int Clamp(float v) => (int)MathF.Round(Math.Clamp(v, 0f, 100f));
}
