using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The end of a shop day, in two screens. First the day's results: customers, service quality, and the four
// metric averages, counting up row by row. Then the tip jar: the day's tips drop in as coins and are counted,
// and the rank meter fills with the day's points (a fanfare on a new rank). The progress is already saved
// when this opens; Next day or Quit closes it.
sealed class DayEnd
{
    public enum Choice { None, NextDay, Quit }
    enum Stage { Results, TipJar }

    // Results: seconds between rows appearing and for a row's number to count up.
    const float RowGap = 0.3f, RowCountUp = 0.4f;
    // Tip jar: one coin per this many cents (at most MaxCoins), seconds between coins (at most), the fall,
    // the pause before the rank meter, and how long it takes to fill.
    const int CentsPerCoin = 25, MaxCoins = 160;
    const float CoinGapMax = 0.08f, CoinSpread = 2.4f, CoinFall = 0.45f, RankPause = 0.4f, RankFill = 1.3f;

    public static readonly Rectangle ResultsPanel = new(320, 70, 640, 580);
    public static readonly Rectangle OkButton = new(510, 580, 260, 56);
    public static readonly Rectangle QuitButton = new(60, 640, 220, 56);
    public static readonly Rectangle NextDayButton = new(1000, 640, 220, 56);
    // The tip jar on the counter; coins pile up inside it.
    public static readonly Rectangle Jar = new(540, 300, 200, 222);

    readonly Day day;
    readonly int pointsBefore, pointsAfter, bankBefore, bankAfter;
    readonly int coins;
    readonly float coinGap;
    Stage stage = Stage.Results;
    float t;
    int rowsTicked, coinsClinked;
    bool rankedUp;

    public DayEnd(Day day, int pointsBefore, int pointsAfter, int bankBefore, int bankAfter)
    {
        this.day = day;
        this.bankBefore = bankBefore;
        this.pointsBefore = pointsBefore;
        this.pointsAfter = pointsAfter;
        this.bankAfter = bankAfter;
        coins = day.TipsCents <= 0 ? 0 : Math.Clamp(day.TipsCents / CentsPerCoin, 1, MaxCoins);
        coinGap = coins == 0 ? 0 : MathF.Min(CoinGapMax, CoinSpread / coins);
    }

    // Rows on the results panel: customers, service quality, then the four metrics.
    static int Rows => 2 + Scoring.MetricNames.Length;
    float ResultsEnd => RowGap * (Rows - 1) + RowCountUp;
    float CoinsEnd => coins == 0 ? 0 : coinGap * (coins - 1) + CoinFall;
    float RankStart => CoinsEnd + RankPause;
    float JarEnd => RankStart + RankFill;

    // Runs the current screen; a click skips its counting to the end. Returns what the player chose, if anything.
    public Choice Update(float dt, bool click, bool confirm, Vector2 mouse)
    {
        if (stage == Stage.Results)
        {
            if (t < ResultsEnd)
            {
                t = click || confirm ? ResultsEnd : t + dt;
                while (rowsTicked < Rows && t >= RowGap * rowsTicked)
                {
                    if (!(click || confirm)) Audio.Play(Audio.Tick, 0.8f, 1.1f + 0.08f * rowsTicked);
                    rowsTicked++;
                }
                return Choice.None;
            }
            if (confirm || (click && Raylib.CheckCollisionPointRec(mouse, OkButton)))
            {
                stage = Stage.TipJar;
                t = 0;
            }
            return Choice.None;
        }

        if (t < JarEnd)
        {
            bool skip = click || confirm;
            t = skip ? JarEnd : t + dt;
            while (coinsClinked < coins && t >= coinGap * coinsClinked + CoinFall)
            {
                if (!skip) Audio.Play(Audio.Coin, 0.5f);
                coinsClinked++;
            }
            if (!rankedUp && t >= RankStart + RankFill * 0.9f && Progress.RankOf(pointsAfter) > Progress.RankOf(pointsBefore))
            {
                rankedUp = true;
                Audio.Play(Audio.Fanfare, 0.7f);
            }
            return Choice.None;
        }
        if (confirm || (click && Raylib.CheckCollisionPointRec(mouse, NextDayButton))) return Choice.NextDay;
        if (click && Raylib.CheckCollisionPointRec(mouse, QuitButton)) return Choice.Quit;
        return Choice.None;
    }

    // Jumps to the end of the results (false) or of the tip jar (true), for the --shot-results and --shot-tipjar checks.
    public void HoldAt(bool tipJar)
    {
        stage = tipJar ? Stage.TipJar : Stage.Results;
        t = tipJar ? JarEnd : ResultsEnd;
        rowsTicked = Rows;
        coinsClinked = coins;
    }

    // Draws the current screen. Results are drawn over whatever the shop drew underneath.
    public void Draw(Vector2 mouse)
    {
        if (stage == Stage.Results) DrawResults(mouse);
        else DrawTipJar(mouse);
    }

    // The results panel: each row's number counts up from zero as it appears.
    void DrawResults(Vector2 mouse)
    {
        float Shown(int row) => Math.Clamp((t - RowGap * row) / RowCountUp, 0f, 1f);
        var metrics = new (string Name, int? Value, bool Built, float Shown)[Scoring.MetricNames.Length];
        for (int i = 0; i < metrics.Length; i++)
            metrics[i] = (Scoring.MetricNames[i], day.Average(i), Day.Built(i), Shown(2 + i));

        // The ID line, only on days with a bad ID.
        string? ids = day.IdsCaught + day.IdsMissed == 0 ? null
            : $"Bad IDs: {day.IdsCaught} caught, {day.IdsMissed} missed" + (day.FinesCents > 0 ? $" ({Scoring.Money(day.FinesCents)} in fines)" : "");
        Art.DrawDayResults(ResultsPanel, day.Number, day.Served, day.Walkouts, Shown(0), day.Quality, Shown(1), metrics,
                           t >= ResultsEnd ? ids : null, day.IdsMissed > 0);
        if (t >= ResultsEnd)
            Art.DrawButton(OkButton, "Count the tips", Raylib.CheckCollisionPointRec(mouse, OkButton), true);
    }

    // The tip jar screen: coins falling into the jar, the money counted so far, and the rank meter.
    void DrawTipJar(Vector2 mouse)
    {
        // Where each coin is: falling from above the jar, or resting on the pile (row by row, alternate rows offset).
        var coinSpots = new List<Vector2>();
        const float coinW = 20, rowH = 7;
        int perRow = (int)((Jar.Width - 24) / coinW);
        for (int i = 0; i < coins; i++)
        {
            float start = coinGap * i;
            if (t < start) break;
            int row = i / perRow, col = i % perRow;
            var rest = new Vector2(Jar.X + 22 + col * coinW + (row % 2) * coinW / 2, Jar.Y + Jar.Height - 12 - row * rowH);
            float fall = Math.Clamp((t - start) / CoinFall, 0f, 1f);
            var from = new Vector2(rest.X, Jar.Y - 160);
            coinSpots.Add(Vector2.Lerp(from, rest, fall * fall));
        }

        int counted = coins == 0 ? 0 : (int)((long)day.TipsCents * coinsClinked / coins);
        if (t >= JarEnd) counted = day.TipsCents;

        float fill = Math.Clamp((t - RankStart) / RankFill, 0f, 1f);
        int points = pointsBefore + (int)MathF.Round((pointsAfter - pointsBefore) * fill);

        bool done = t >= JarEnd;
        Art.DrawTipJarScreen(Jar, day.Number, coinSpots, counted, t >= JarEnd ? bankAfter : Math.Max(0, bankBefore + counted),
                             points, pointsAfter - pointsBefore, rankedUp);
        Art.DrawButton(QuitButton, "Save and quit", done && Raylib.CheckCollisionPointRec(mouse, QuitButton), done);
        Art.DrawButton(NextDayButton, "Next day", done && Raylib.CheckCollisionPointRec(mouse, NextDayButton), done);
        if (!done) Art.LabelCentered("Click to skip", Program.Width / 2f, 690, 20, Art.Faint);
    }
}
