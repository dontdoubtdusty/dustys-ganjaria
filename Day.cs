namespace DustysGanjaria;

// One shop day: how many customers come and when, the clock on the wall, and the tally of how it went.
// The clock is shown as 10:00 AM to 8:00 PM, stretched over the real seconds the day lasts, so a busier day
// runs longer. Customers arrive on a schedule made at the start, with a lunch and an evening rush.
sealed class Day
{
    // Customers on day 1, how many more each day after, and the most in one day.
    const int FirstDayCustomers = 2, MoreEachDay = 1, MostCustomers = 16;
    // Seconds before the first customer, the usual gap between customers, and how much a rush shortens it.
    const float FirstArrival = 3f, GapMin = 20f, GapMax = 32f, RushGap = 0.6f;
    // Seconds the doors stay open after the last customer has come in.
    const float OpenAfterLast = 15f;
    // Clock hours shown at opening and closing.
    const int OpenHour = 10, CloseHour = 20;

    public int Number { get; }
    public int Expected => arrivals.Count;
    public float Clock { get; private set; }
    public float CloseAt { get; }

    readonly List<float> arrivals = new();
    int nextArrival;

    // The tally: every served order's result, and the walkouts.
    public readonly List<OrderResult> Orders = new();
    public int Walkouts;

    public Day(int number, Random rng)
    {
        Number = number;
        int count = Math.Min(FirstDayCustomers + MoreEachDay * (number - 1), MostCustomers);
        float t = FirstArrival;
        for (int i = 0; i < count; i++)
        {
            arrivals.Add(t);
            float share = count <= 1 ? 0 : i / (float)(count - 1);
            bool rush = share is >= 0.35f and <= 0.55f or >= 0.75f and <= 0.9f;
            t += (GapMin + (float)rng.NextDouble() * (GapMax - GapMin)) * (rush ? RushGap : 1f);
        }
        CloseAt = arrivals[^1] + OpenAfterLast;
    }

    // True once closing time has passed: no more customers come in.
    public bool Closed => Clock >= CloseAt && nextArrival >= arrivals.Count;

    // Customers still to come today.
    public int StillToCome => arrivals.Count - nextArrival;

    // Seconds until the next customer is due (0 if one is due now, infinity if nobody else is coming).
    public float UntilNext => nextArrival < arrivals.Count ? MathF.Max(0, arrivals[nextArrival] - Clock) : float.PositiveInfinity;

    // Moves the clock on.
    public void Tick(float dt) => Clock += dt;

    // True if a customer is due; call Arrived once they are let in (the shop may hold them while the line is full).
    public bool Due => nextArrival < arrivals.Count && Clock >= arrivals[nextArrival];
    public void Arrived() => nextArrival++;

    // The wall clock as text, like 1:35 PM, in five-minute steps.
    public string ClockText
    {
        get
        {
            float share = Math.Clamp(Clock / CloseAt, 0f, 1f);
            int minutes = (int)(share * (CloseHour - OpenHour) * 60) / 5 * 5 + OpenHour * 60;
            int h = minutes / 60, m = minutes % 60;
            string ampm = h >= 12 ? "PM" : "AM";
            int h12 = h % 12 == 0 ? 12 : h % 12;
            return $"{h12}:{m:00} {ampm}";
        }
    }

    // ---------- Tally ----------

    public int Served => Orders.Count;
    public int TipsCents => Orders.Sum(o => o.TipCents);
    // Rank points earned: each served order's score; walkouts earn nothing.
    public int Points => Orders.Sum(o => o.Total);

    // The day's average for one of the four metrics, or null if that station is not built yet or nobody was
    // scored on it. Walkouts count as 0 for Waiting (it is the one they failed); the others only count served orders.
    public int? Average(int metric)
    {
        if (!Built(metric)) return null;
        var values = Orders.Select(o => (float)(o.Metrics[metric] ?? 0)).ToList();
        if (metric == 0) values.AddRange(Enumerable.Repeat(0f, Walkouts));
        return values.Count == 0 ? null : (int)MathF.Round(values.Average());
    }

    // Service quality: the average of the metric averages that exist.
    public int Quality
    {
        get
        {
            var avgs = Enumerable.Range(0, Scoring.MetricNames.Length).Select(Average).Where(a => a != null).ToList();
            return avgs.Count == 0 ? 0 : (int)MathF.Round((float)avgs.Average(a => a!.Value));
        }
    }

    // True if a metric's station exists yet (the day results show the others as not built).
    public static bool Built(int metric) => metric <= 1;
}
