namespace DustysGanjaria;

// The outcome of one bagged order, with its part scores (0-100 each).
readonly record struct WeighResult(
    float Target, float Band, float Weight, float Seconds, int Slips, int Spilled,
    int Accuracy, int Speed, int Handling, int Total, string Grade);

// The outcome of one served customer: the weigh result, the wait score (0-100), the order total, grade and tip.
readonly record struct OrderResult(WeighResult Weigh, int Wait, int Total, string Grade, int Tip);

// Turns a finished bag into scores. All tuning numbers for scoring live here.
static class Scoring
{
    // Seconds allowed before speed starts dropping: a base plus some per gram.
    const float ParBase = 8f, ParPerGram = 1.5f;
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
    public const float LinePatience = 45f;
    // Seconds a customer waits for their order once it is taken; bigger orders get a little longer.
    public static float WaitPatience(float grams) => 30f + 4f * grams;
    // Share of the order score that comes from the customer's wait; the weigh station gets the rest.
    const float WaitShare = 0.25f;
    // Patience share still left at which the wait score starts dropping.
    const float WaitFullAbove = 0.5f;

    // Scores a served order: the weigh score plus how long the customer waited, and the tip they leave.
    public static OrderResult ScoreOrder(WeighResult weigh, float patienceLeft)
    {
        int wait = Clamp(100f * patienceLeft / WaitFullAbove);
        int total = (int)MathF.Round((1f - WaitShare) * weigh.Total + WaitShare * wait);
        return new OrderResult(weigh, wait, total, GradeFor(total), TipFor(total));
    }

    // Dollars tipped: nothing below 50, then one more dollar per 10 points (2 at 50, 7 at 100).
    static int TipFor(int total) => total < 50 ? 0 : (total - 30) / 10;

    // Names a total score.
    static string GradeFor(int total) => total switch
    {
        >= 95 => "Perfect",
        >= 85 => "Great",
        >= 70 => "Good",
        >= 50 => "OK",
        _ => "Poor",
    };

    static int Clamp(float v) => (int)MathF.Round(Math.Clamp(v, 0f, 100f));
}
