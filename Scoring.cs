namespace DustysGanjaria;

// The outcome of one bagged order, with its part scores (0-100 each). Room is how far over the order weight the
// weigh-heavy cap allows; Weight is what was sealed in the bag; WrongBuds counts buds of another strain in it.
// Crumbs were chipped off nugs by hard landings (CrumbGrams in all); Over is the bag's weight above the order.
// ShakePieces and BigShare (share of the weight in the strain's large nugs) are what "No shake" and "Big nugs"
// requests are checked against.
readonly record struct WeighResult(
    float Target, float Room, float Weight, float Seconds, int Slips, int Spilled,
    int Accuracy, int Speed, int Handling, int Total, string Grade, int WrongBuds = 0,
    int Crumbs = 0, float CrumbGrams = 0, float Over = 0, int ShakePieces = 0, float BigShare = 0)
{
    // Grams the shop gave away on this order: the bag's overage plus the crumbs.
    public float GivenAway => Over + CrumbGrams;
}

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
// GiveawayCents is what the given-away grams cost the shop (taken off the day's tips). Request is what the customer
// asked about the bag's looks, and RequestMet whether the bag delivered it (both change only the tip).
readonly record struct OrderResult(
    WeighResult Weigh, PackResult Pack, LabelResult Label, int Waiting, int Total, string Grade, int TipCents, float Waited,
    int FineCents = 0, IdFlaw Flaw = IdFlaw.None, int GiveawayCents = 0, Looks Request = Looks.None, bool RequestMet = true)
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
    // Over the cap: how many room widths past it accuracy reaches zero.
    const float BandsToZero = 4f;
    // Under the order weight is a fail: accuracy is at most this, and reaches zero one room width below.
    const float UnderMax = 50f;
    // Points lost per slipped zip, per bud left lying on the counter, and per crumb chipped off by a hard landing.
    const int SlipCost = 25, SpillCost = 10, CrumbCost = 5;
    // Share of the total for accuracy and speed; handling gets the rest.
    const float AccuracyShare = 0.5f, SpeedShare = 0.3f;

    // The weigh-heavy cap: how far over the order weight a bag may go and still score full accuracy. 6% of the
    // order or 0.15 g, whichever is more (1 g: up to 1.15 g, 3.5 g: up to 3.71 g, 7 g: up to 7.42 g). Under the
    // order weight is never right; see vision.md, "Weigh station redesign".
    public static float RoomFor(float target) => MathF.Max(0.06f * target, 0.15f);

    // Points lost per bud of the wrong strain sealed in the bag.
    const int WrongBudCost = 15;

    // Seconds a bag of this many grams may take before weigh speed starts dropping.
    public static float WeighPar(float target) => ParBase + ParPerGram * target;

    // Scores a bag from target, sealed weight, time taken, zip slips, spilled buds and crumbs. Buds of the wrong
    // strain (wrongBuds of them, wrongGrams in all) do not count toward the order's weight, and each one costs
    // handling: a bag of the wrong strain scores no accuracy at all. The tiny allowance keeps a bag of exactly the
    // order weight from failing on float rounding.
    public static WeighResult Score(float target, float room, float weight, float seconds, int slips, int spilled,
                                    float wrongGrams = 0, int wrongBuds = 0, int crumbs = 0, float crumbGrams = 0,
                                    int shakePieces = 0, float bigShare = 0)
    {
        float counted = weight - wrongGrams;
        float acc;
        if (counted < target - 0.001f) acc = UnderMax * (1f - (target - counted) / room);
        else if (counted <= target + room + 0.001f) acc = 100f;
        else acc = 100f * (1f - (counted - target - room) / (room * BandsToZero));

        float par = WeighPar(target);
        float speed = seconds <= par ? 100f : 100f * (1f - (seconds - par) / (2f * par));

        float handling = 100f - SlipCost * slips - SpillCost * spilled - WrongBudCost * wrongBuds - CrumbCost * crumbs;

        int a = Clamp(acc), s = Clamp(speed), h = Clamp(handling);
        int total = (int)MathF.Round(AccuracyShare * a + SpeedShare * s + (1f - AccuracyShare - SpeedShare) * h);
        float over = MathF.Max(0f, weight - target);
        return new WeighResult(target, room, weight, seconds, slips, spilled, a, s, h, total, GradeFor(total), wrongBuds,
                               crumbs, crumbGrams, over, shakePieces, bigShare);
    }

    // ---------- Giveaway and looks requests ----------

    // What a given-away gram costs the shop, in cents (a placeholder: the game has no prices yet).
    public const int CentsPerGram = 1000;
    // A heavy bag pleases the customer: their tip grows by this share of the overage's value (less than its cost,
    // so landing heavy is quick but always costs a little).
    const float HeavyTipShare = 0.25f;
    // A looks request met adds this to the tip; missed, the tip is halved.
    const int RequestBonusCents = 100;
    // Requests start on this day, for about this share of customers.
    public const int RequestsFromDay = 3;
    public const float RequestChance = 0.2f;
    // A "Big nugs" bag needs at least this share of its weight in the strain's large nugs; "No shake" allows no
    // shake piece at all (shake is a kind of piece, `Bud.IsShake`: the flat leafy crumbs, never a bud).
    public const float BigNugsShare = 0.7f;

    // True if a bag met a looks request (always true with no request).
    public static bool Meets(Looks request, WeighResult w) => request switch
    {
        Looks.BigNugs => w.BigShare >= BigNugsShare,
        Looks.NoShake => w.ShakePieces == 0,
        _ => true,
    };

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

    // Share of the Labeling score for placement; speed gets the rest. The right strain is expected, not scored:
    // a wrong one costs WrongStrainCost points off the total. (0.18.1: was strain 50, placement 30, speed 20, which
    // gave 70 points away for free; the play log showed nearly every label at 100.)
    const float PlaceShare = 0.65f, WrongStrainCost = 50f;
    // Placement: full marks within this many pixels of the guide and degrees of straight, zero at the far value.
    // (0.18.1: was 8 px / 2 deg full, 60 px / 15 deg zero; logged labels averaged 5.9 px and 1.8 deg.)
    public const float OffFull = 2f, OffZero = 30f, AngleFull = 0.5f, AngleZero = 8f;
    // Seconds allowed before labeling speed starts dropping, and how many more until it is zero.
    // (0.18.1: was 6 s par, zero at 18 s; logged labels averaged 1.8 s.)
    public const float LabelPar = 1.5f, LabelSpeedSpan = 3f;

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
        int s = Clamp(seconds <= LabelPar ? 100f : 100f * (1f - (seconds - LabelPar) / LabelSpeedSpan));
        int total = Clamp(PlaceShare * place + (1f - PlaceShare) * s - (match == 100 ? 0f : WrongStrainCost));
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
    // fine; it does not change the scores. A heavy bag adds a little to the tip, a looks request met adds a bonus
    // and one missed halves it; the grams given away are costed for the day's tally.
    public static OrderResult ScoreOrder(WeighResult weigh, PackResult pack, LabelResult label, float grams, float waited,
                                         IdFlaw flaw = IdFlaw.None, Looks request = Looks.None)
    {
        float par = WaitPar(grams);
        int wait = Clamp(waited <= par ? 100f : 100f * (1f - (waited - par) / WaitZeroAfter));
        int total = (int)MathF.Round((wait + weigh.Total + pack.Total + label.Total) / 4f);
        int fine = flaw == IdFlaw.None ? 0 : IdFineCents;

        int tip = TipFor(total) + RoundTo5(weigh.Over * CentsPerGram * HeavyTipShare);
        bool met = Meets(request, weigh);
        if (request != Looks.None) tip = met ? tip + RequestBonusCents : RoundTo5(tip / 2f);
        int giveaway = (int)MathF.Round(weigh.GivenAway * CentsPerGram);
        return new OrderResult(weigh, pack, label, wait, total, GradeFor(total), tip, waited, fine, flaw, giveaway, request, met);
    }

    static int RoundTo5(float cents) => (int)MathF.Round(cents / 5f) * 5;

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
