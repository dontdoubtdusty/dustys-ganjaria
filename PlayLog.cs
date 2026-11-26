using System.Globalization;

namespace DustysGanjaria;

// The play log: one CSV row per served order, per walkout and per refused ID, appended to %APPDATA%\DustysGanjaria\playlog.csv,
// for tuning from real play. It records what the game already scored and never changes play. If the columns change
// between versions, the old file is renamed (playlog-<date>.csv) and a fresh one started, so every file has one
// header. A failure to write is ignored: the game goes on, the row is just lost.
static class PlayLog
{
    // Screenshot runs turn it off so they never write the log.
    public static bool Enabled = true;

    static string FolderPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DustysGanjaria");
    static string FilePath => Path.Combine(FolderPath, "playlog.csv");

    // When this run of the game started, so rows from one sitting can be grouped.
    static readonly string Session = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    static readonly string[] Columns =
    {
        "version", "session", "time", "day", "rank", "ticket", "customer", "outcome", "stage",
        "grams", "strain", "wanted", "id", "id_call",
        "waited_s", "wait_par_s", "waiting",
        "w_target", "w_room", "w_weight", "w_wrong_buds", "w_s", "w_par_s", "w_slips", "w_spilled",
        "w_crumbs", "w_crumb_g", "w_over_g", "w_shake_pieces", "w_big_share",
        "w_accuracy", "w_speed", "w_handling", "weighing",
        "p_used", "p_s", "p_par_s", "p_cross_threads", "p_drops", "p_restarts", "p_fit", "p_speed", "p_handling", "packing",
        "l_used", "l_s", "l_par_s", "l_off_px", "l_angle", "l_peels", "l_match", "l_placement", "l_speed", "labeling",
        "total", "tip_cents", "fine_cents", "giveaway_cents", "request", "request_met",
    };

    // Logs a served order with every part score.
    public static void Served(Day day, int rank, Ticket t, OrderResult r)
    {
        var c = t.Customer;
        Write(Base(day, rank, t.Number, c, "served", "done", r.Waited, "accepted")
            .Concat(new[] { F(Scoring.WaitPar(c.Grams)), I(r.Waiting) })
            .Concat(WeighCells(r.Weigh)).Concat(PackCells(r.Pack)).Concat(LabelCells(r.Label))
            .Concat(new[] { I(r.Total), I(r.TipCents), I(r.FineCents), I(r.GiveawayCents), Request(c),
                            c.Request == Looks.None ? "" : (r.RequestMet ? "yes" : "no") }));
    }

    // Logs a walkout: where they were (stage: line, or where their order had got to) and any station work already
    // scored on their ticket. Score columns for stations not reached are left empty.
    public static void Walkout(Day day, int rank, Customer c, Ticket? t, string stage)
    {
        var empty = (int n) => Enumerable.Repeat("", n);
        Write(Base(day, rank, t?.Number ?? 0, c, "walkout", stage, c.Waited, stage == "line" ? "" : "accepted")
            .Concat(new[] { F(Scoring.WaitPar(c.Grams)), "0" })
            .Concat(t?.Weighed is { } w ? WeighCells(w) : empty(17))
            .Concat(t?.Packed is { } p ? PackCells(p) : empty(10))
            .Concat(empty(10))
            .Concat(new[] { "", "0", "0", "0", Request(c), "" }));
    }

    // Logs a customer whose ID was refused: outcome "caught" for a bad ID, "refused good id" for a good one (that
    // counts as a walkout in the day's tally). No station work exists for them.
    public static void Refused(Day day, int rank, Customer c)
    {
        bool caught = c.Id.Flaw != IdFlaw.None;
        Write(Base(day, rank, 0, c, caught ? "caught" : "refused good id", "counter", c.Waited, "refused")
            .Concat(new[] { F(Scoring.WaitPar(c.Grams)), caught ? "" : "0" })
            .Concat(Enumerable.Repeat("", 37))
            .Concat(new[] { "", "0", "0", "0", Request(c), "" }));
    }

    // The columns every row starts with, up to and including seconds waited.
    // idCall is "accepted", "refused", or empty if they never showed it (walked out of the line).
    static IEnumerable<string> Base(Day day, int rank, int ticket, Customer c, string outcome, string stage, float waited,
                                    string idCall) => new[]
    {
        Program.Version, Session, DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        I(day.Number), I(rank), ticket > 0 ? I(ticket) : "", c.Name, outcome, stage,
        F(c.Grams), c.Strain.Name, Containers.For(c).Name,
        c.Id.Flaw == IdFlaw.None ? "ok" : Ids.Why(c.Id.Flaw), idCall, F(waited),
    };

    static IEnumerable<string> WeighCells(WeighResult w) => new[]
    {
        F(w.Target), F(w.Room), F(w.Weight), I(w.WrongBuds), F(w.Seconds), F(Scoring.WeighPar(w.Target)), I(w.Slips), I(w.Spilled),
        I(w.Crumbs), F(w.CrumbGrams), F(w.Over), I(w.ShakePieces), F(w.BigShare),
        I(w.Accuracy), I(w.Speed), I(w.Handling), I(w.Total),
    };

    static IEnumerable<string> PackCells(PackResult p) => new[]
    {
        p.Used.Name, F(p.Seconds), F(Scoring.PackPar(p.Used.Kind)), I(p.CrossThreads), I(p.Drops), I(p.Restarts),
        I(p.Fit), I(p.Speed), I(p.Handling), I(p.Total),
    };

    static IEnumerable<string> LabelCells(LabelResult l) => new[]
    {
        l.Used.Name, F(l.Seconds), F(Scoring.LabelPar), F(l.Off), F(l.Angle), I(l.Peels),
        I(l.Match), I(l.Placement), I(l.Speed), I(l.Total),
    };

    // The looks request as the log writes it: "big nugs", "no shake" or empty.
    static string Request(Customer c) => c.RequestText?.ToLowerInvariant() ?? "";

    // Numbers always with a dot, whatever the PC's region settings.
    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

    // Quotes a cell if it holds a comma or a quote.
    static string Cell(string s) => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

    // Appends one row, starting a new file (with the header) when there is none or its header is out of date.
    static void Write(IEnumerable<string> cells)
    {
        if (!Enabled) return;
        try
        {
            Directory.CreateDirectory(FolderPath);
            string header = string.Join(",", Columns);
            if (File.Exists(FilePath) && File.ReadLines(FilePath).FirstOrDefault() != header)
            {
                string old = Path.Combine(FolderPath, $"playlog-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
                File.Move(FilePath, old);
            }
            if (!File.Exists(FilePath)) File.WriteAllText(FilePath, header + Environment.NewLine);
            File.AppendAllText(FilePath, string.Join(",", cells.Select(Cell)) + Environment.NewLine);
        }
        catch (Exception) { }
    }
}
