using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The shop, one day at a time. Each day opens with a card (day number, customers expected); then customers ring
// the door chime on the day's schedule, walk in and queue at the counter, each ordering a strain from the day's
// menu. Taking an order cuts to the order shot, where they say it and the ticket goes up on the rail; they wait
// while you drag tickets onto the weigh station, then (once the bag is in the OUT bin and the ticket is back on the
// rail, marked weighed) onto the pack station, then (closed, marked packed) onto the label station. A labeled
// order cuts to the hand-over shot (bag check, scores, reaction, tip); a customer who runs out of patience walks
// out. At closing time the doors shut, you finish whoever is inside, and the day ends with the results and the tip
// jar. Screens are switched with tabs along the bottom (or the 1 to 4 keys); the rail is on every screen. Both
// shots pause the shop: the clock, arrivals and everyone's patience.
sealed class Shop
{
    enum Screen { Counter, Weigh, Pack, Label }
    enum Phase { Intro, Open, Ended }

    // Most customers that queue in the line, and most taken orders at once (rail, stations and the order shot).
    const int MaxLine = 4;
    const int MaxWaiting = 4;
    // How much faster the clock runs while the shop is empty and the next customer is a while off.
    const float FastForward = 5f, FastIfNextAfter = 1.5f;
    // Seconds after the last customer leaves at closing before the day results come up.
    const float EndPause = 1.2f;

    // Shop view layout, side-on. Feet stand on two rows: the back row (door and waiting bench) and the front row
    // (the line, facing the counter on the right). People on the back row are drawn smaller.
    const float BackY = 470, FrontY = 625;
    const float BackScale = 0.82f;
    static readonly Vector2 DoorSpot = new(85, BackY);
    const float LineFrontX = 850, LineGap = 120;
    const float WaitingX = 300, WaitingGap = 105;
    static readonly Rectangle CounterRect = new(900, 520, 280, 156);
    static readonly Vector2 BudtenderFeet = new(1205, 640);
    // Where a new ticket starts its flight up to the rail: the notepad in the order shot.
    static readonly Vector2 NotepadSpot = new(420, 420);

    static readonly Rectangle TakeButton = new(925, 580, 230, 56);
    static readonly Rectangle CounterTab = new(225, 676, 160, 38);
    static readonly Rectangle WeighTab = new(395, 676, 160, 38);
    static readonly Rectangle PackTab = new(565, 676, 160, 38);
    static readonly Rectangle LabelTab = new(735, 676, 160, 38);
    // Where a weighed ticket starts its flight back up to the rail (the weigh station's OUT bin), and a packed one
    // (the pack station's mat).
    static readonly Vector2 BinSpot = new(1160, 520);
    static readonly Vector2 MatSpot = new(640, 560);
    static readonly Rectangle IntroPanel = new(390, 110, 500, 460);
    static readonly Rectangle OpenButton = new(520, 494, 240, 56);
    static readonly Vector2 ClockSpot = new(990, 30);

    readonly Random rng;
    readonly Progress progress;
    readonly bool persistent;
    readonly WeighStation weigh;
    readonly PackStation pack;
    readonly LabelStation label;
    readonly TicketRail rail = new();
    readonly List<Customer> line = new();
    readonly List<Customer> waiting = new();
    readonly List<Customer> leaving = new();
    // Finished orders waiting for their hand-over shot (one plays at a time, and not during the order shot).
    readonly Queue<(Ticket Ticket, OrderResult Result)> toHandOver = new();

    Day day = null!;
    Phase phase;
    Screen screen = Screen.Counter;
    OrderShot? shot;
    HandOver? handOver;
    DayEnd? dayEnd;
    Ticket? onWeigh, onPack, onLabel;
    int nextTicket = 1;
    bool closingSaid, fast;
    // True if the last customer in had a bad ID (two in a row never happens).
    bool lastIdBad;
    // True while the F1 tuning panel is showing (screenshot runs set it with --tuning).
    public bool Tuning;
    float endTimer;
    string banner = "";
    float bannerTime;

    // True once the player chose Save and quit at the end of a day.
    public bool QuitRequested { get; private set; }

    // Opens the shop on the saved day. persistent = false (screenshot runs) never writes the save.
    public Shop(Random rng, Progress progress, bool persistent)
    {
        this.rng = rng;
        this.progress = progress;
        this.persistent = persistent;
        weigh = new WeighStation(rng);
        pack = new PackStation(rng);
        label = new LabelStation(rng);
        StartDay(progress.Day);
    }

    // Clears the shop and sets up a day, waiting on its intro card. The day's menu follows the rank reached.
    void StartDay(int number)
    {
        day = new Day(number, Strains.MenuFor(Progress.RankOf(progress.RankPoints), rng), rng);
        weigh.SetMenu(day.Menu);
        phase = Phase.Intro;
        screen = Screen.Counter;
        line.Clear();
        waiting.Clear();
        leaving.Clear();
        toHandOver.Clear();
        rail.Clear();
        weigh.Clear();
        pack.Clear();
        label.Clear();
        shot = null;
        handOver = null;
        dayEnd = null;
        onWeigh = onPack = onLabel = null;
        nextTicket = 1;
        closingSaid = fast = lastIdBad = false;
        endTimer = 0;
        bannerTime = 0;
    }

    // True while a day is being played (not on its intro card or its day-end screens): quitting now loses it.
    public bool DayInProgress => phase == Phase.Open;

    // The pause menu opened: put down anything held, without the costs a tab switch has.
    public void Pause()
    {
        rail.LetGo();
        weigh.LetGo(pausing: true);
        pack.LetGo();
        label.LetGo(pausing: true);
    }

    // Starts today over from its intro card. Nothing was saved yet today, so the save is untouched.
    public void RestartDay() => StartDay(progress.Day);

    // Spot in the line for place i (0 = at the counter).
    static Vector2 LineSpot(int i) => new(LineFrontX - LineGap * i, FrontY);

    // Spot at the waiting bench for place i.
    static Vector2 WaitingSpot(int i) => new(WaitingX + WaitingGap * i, BackY);

    // How big someone is drawn at a floor height: smaller toward the back row.
    static float DepthScale(float y) => BackScale + (1f - BackScale) * Math.Clamp((y - BackY) / (FrontY - BackY), 0f, 1f);

    // The customer standing at the counter, ready to order, or null.
    Customer? AtCounter => line.Count > 0 && line[0].Now == Customer.State.InLine && line[0].Arrived && line[0].Reached ? line[0] : null;

    // Orders taken and not yet served: everyone waiting plus the one giving their order.
    int TakenOrders => waiting.Count + (shot != null ? 1 : 0);

    // True while a shot (order or hand-over) fills the screen; the shop is paused.
    bool InShot => shot != null || handOver != null;

    // True if someone in line or waiting is close to walking out (lights the tab orange).
    static bool AnyUrgent(IEnumerable<Customer> people) => people.Any(c => c.PatienceFraction < 0.3f);

    // Sets up a scene for the --shot flags. All but the day-end ones have one order already taken.
    // --shot-counter: two in line and one waiting, shop view. --shot-order: the order shot mid-way. --shot-id: the
    // order shot on its ID check, with an under-21 card. --shot-handover and --shot-results include an ID fine.
    // --shot and --shot-throw: one ticket on the weigh station and another on the rail. --shot-pack (a pouch) and
    // --shot-pack-jar: one weighed order half closed on the pack station and another ticket on the rail.
    // --shot-handover: the end of a hand-over; --shot-check: its bag check. --shot-results and --shot-tipjar: the two
    // day-end screens. --shot-label: a packed jar on the label station with its label stuck on loosely. --shot-intro:
    // the day card, at a rank with all five strains open (a menu of three, and no next strain).
    public void StageForScreenshot(string mode)
    {
        if (mode == "--shot-intro")
        {
            progress.RankPoints = Progress.PointsFor(5);
            StartDay(4);
            return;
        }
        phase = Phase.Open;
        // A packed order for the staged scores: the right container, a few seconds, no mistakes (or one, with slip).
        static PackResult Packed(Container c, bool slip) =>
            Scoring.ScorePack(c, c, slip ? 12f : 6f, slip ? 1 : 0, 0, 0);
        // A labeled order for the staged scores: the right strain, a little off and crooked (more so with slip).
        static LabelResult Labeled(Strain s, bool slip) =>
            Scoring.ScoreLabel(s, s, slip ? 8f : 4f, slip ? 18f : 5f, slip ? 6f : 1.5f, 0,
                               slip ? new Vector2(0.06f, -0.04f) : new Vector2(0.02f, 0f));
        if (mode == "--shot-results" || mode == "--shot-tipjar")
        {
            var w = Scoring.Score(3.5f, Scoring.BandFor(3.5f), 3.55f, 13f, 0, 1);
            day.Orders.Add(Scoring.ScoreOrder(w, Packed(new(ContainerKind.Jar, false), true), Labeled(day.Menu[0], true), 3.5f, 40f,
                                              IdFlaw.Expired));
            day.IdsCaught = 1;
            day.Orders.Add(Scoring.ScoreOrder(Scoring.Score(1f, Scoring.BandFor(1f), 1.02f, 9f, 0, 0),
                                              Packed(new(ContainerKind.Pouch, false), false), Labeled(day.Menu[1], false), 1f, 70f));
            day.Walkouts = 1;
            progress.RankPoints = 180;
            progress.TipsCents = 950;
            EndDay();
            dayEnd!.HoldAt(mode == "--shot-tipjar");
            return;
        }

        var first = Arrive();
        first.Pos = first.Goal;
        first.Reached = true;
        TakeOrder();
        FinishOrder();
        first.Pos = first.Goal = WaitingSpot(0);

        if (mode == "--shot-handover" || mode == "--shot-check")
        {
            first.Waited = 50f;
            var w = Scoring.Score(first.Grams, Scoring.BandFor(first.Grams), first.Grams + 0.02f, 12f, 0, 0);
            var t = rail.For(first)!;
            rail.Remove(t);
            waiting.Remove(first);
            handOver = new HandOver(t, Scoring.ScoreOrder(w, Packed(Containers.For(first), false), Labeled(first.Strain, true),
                                                          first.Grams, first.Waited, IdFlaw.Underage));
            if (mode == "--shot-check") handOver.HoldAtCheck();
            else handOver.HoldAt(true);
            return;
        }

        if (mode == "--shot-counter" || mode == "--shot-order" || mode == "--shot-id")
        {
            for (int i = 0; i < 2; i++)
            {
                var c = Arrive();
                c.Pos = c.Goal = LineSpot(i);
                c.Reached = true;
            }
            line[1].Patience = 9;
            screen = Screen.Counter;
            if (mode == "--shot-order")
            {
                TakeOrder();
                shot!.HoldAt(2);
            }
            if (mode == "--shot-id")
            {
                line[0].Id = Ids.Make(line[0], day.Date, IdFlaw.Underage, rng);
                TakeOrder();
                shot!.HoldAtId();
            }
            return;
        }

        var second = Arrive();
        second.Pos = second.Goal;
        second.Reached = true;
        TakeOrder();
        FinishOrder();
        second.Pos = second.Goal = WaitingSpot(1);

        if (mode == "--shot-pack" || mode == "--shot-pack-jar")
        {
            first.Wants = mode == "--shot-pack-jar" ? ContainerKind.Jar : ContainerKind.Pouch;
            var t = rail.For(first)!;
            t.Next = Stage.Pack;
            t.Weighed = Scoring.Score(first.Grams, Scoring.BandFor(first.Grams), first.Grams, 11f, 0, 0);
            screen = Screen.Pack;
            PutOnPack(t);
            pack.StageForScreenshot();
            return;
        }

        if (mode == "--shot-label")
        {
            first.Wants = ContainerKind.Jar;
            var t = rail.For(first)!;
            t.Next = Stage.Label;
            t.Weighed = Scoring.Score(first.Grams, Scoring.BandFor(first.Grams), first.Grams, 11f, 0, 0);
            t.Packed = Packed(Containers.For(first), false);
            rail.For(second)!.Next = Stage.Label;
            screen = Screen.Label;
            PutOnLabel(t);
            label.StageForScreenshot();
            return;
        }

        screen = Screen.Weigh;
        PutOnWeigh(rail.For(first)!);
        weigh.StageForScreenshot(mode == "--shot-throw");
    }

    // Runs one frame: the intro card or day end if showing, otherwise the shots, tabs, the clock, arrivals,
    // patience, walking, the rail and the weigh station, and closing time.
    public void Update(float dt)
    {
        dt = MathF.Min(dt, 1f / 30f);
        var mouse = Raylib.GetMousePosition();
        bool press = Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool release = Raylib.IsMouseButtonReleased(MouseButton.Left);
        bool confirm = Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter)
                       || Raylib.IsKeyPressed(KeyboardKey.Space);
        if (bannerTime > 0) bannerTime -= dt;
        if (Raylib.IsKeyPressed(KeyboardKey.F1)) Tuning = !Tuning;

        if (phase == Phase.Intro)
        {
            if (confirm || (press && Raylib.CheckCollisionPointRec(mouse, OpenButton))) phase = Phase.Open;
            return;
        }
        if (phase == Phase.Ended)
        {
            UpdateWalking(dt);
            var choice = dayEnd!.Update(dt, press, confirm, mouse);
            if (choice == DayEnd.Choice.NextDay) StartDay(progress.Day);
            if (choice == DayEnd.Choice.Quit) QuitRequested = true;
            return;
        }

        // The hand-over shot: everything else waits until Next closes it.
        if (handOver != null)
        {
            UpdateWalking(dt);
            if (handOver.Update(dt, press, confirm, mouse)) CloseHandOver();
            return;
        }

        // Tabs are locked during the order shot; it only lasts a few seconds and a click skips ahead.
        bool tabClicked = false;
        if (shot == null)
        {
            if (press && Raylib.CheckCollisionPointRec(mouse, CounterTab)) { SwitchTo(Screen.Counter); tabClicked = true; }
            if (press && Raylib.CheckCollisionPointRec(mouse, WeighTab)) { SwitchTo(Screen.Weigh); tabClicked = true; }
            if (press && Raylib.CheckCollisionPointRec(mouse, PackTab)) { SwitchTo(Screen.Pack); tabClicked = true; }
            if (press && Raylib.CheckCollisionPointRec(mouse, LabelTab)) { SwitchTo(Screen.Label); tabClicked = true; }
            if (Raylib.IsKeyPressed(KeyboardKey.One)) SwitchTo(Screen.Counter);
            if (Raylib.IsKeyPressed(KeyboardKey.Two)) SwitchTo(Screen.Weigh);
            if (Raylib.IsKeyPressed(KeyboardKey.Three)) SwitchTo(Screen.Pack);
            if (Raylib.IsKeyPressed(KeyboardKey.Four)) SwitchTo(Screen.Label);
        }

        if (!InShot)
        {
            fast = ShopEmpty && day.UntilNext > FastIfNextAfter && !day.Closed;
            day.Tick(fast ? dt * FastForward : dt);
            UpdateArrivals();
            UpdatePatience(dt);
        }
        UpdateWalking(dt);

        // The rail gets first pick of a press: a ticket under the mouse is picked up instead of anything below it.
        bool railTook = shot == null && !tabClicked && press && rail.TryGrab(mouse);
        var dropped = rail.UpdateDrag(mouse, release);
        if (dropped != null) Drop(dropped, mouse);
        rail.Update(dt);

        if (shot != null)
        {
            shot.Update(dt, (press && !tabClicked) || confirm, press && !tabClicked, mouse);
            if (shot.Done) FinishOrder();
        }
        else if (screen == Screen.Counter && !tabClicked && !railTook)
        {
            bool click = press && Raylib.CheckCollisionPointRec(mouse, TakeButton);
            if ((click || confirm) && AtCounter != null) TakeOrder();
        }

        bool stationInput = !tabClicked && !railTook && rail.Dragged == null;
        weigh.Update(dt, stationInput && screen == Screen.Weigh);
        pack.Update(dt, stationInput && screen == Screen.Pack);
        label.Update(dt, stationInput && screen == Screen.Label);

        var weighed = weigh.TakeShipped();
        if (weighed != null && onWeigh != null) ToPack(onWeigh, weighed.Value);
        var packed = pack.TakeShipped();
        if (packed != null && onPack != null) ToLabel(onPack, packed.Value);
        var labeled = label.TakeShipped();
        if (labeled != null && onLabel != null) Finish(onLabel, labeled.Value);

        if (!InShot && toHandOver.Count > 0) StartHandOver();
        UpdateClosing(dt);
    }

    // True when nobody is in the shop and no work is in progress.
    bool ShopEmpty => line.Count == 0 && waiting.Count == 0 && rail.Count == 0 && weigh.IsIdle && pack.IsIdle
                      && label.IsIdle && shot == null && handOver == null && toHandOver.Count == 0;

    // At closing time the doors shut; once everyone inside has been served or has left, the day ends.
    void UpdateClosing(float dt)
    {
        if (!day.Closed) return;
        if (!closingSaid)
        {
            closingSaid = true;
            if (!ShopEmpty) Say("Closing time: finish the orders inside");
        }
        if (!ShopEmpty) { endTimer = 0; return; }
        endTimer += dt;
        if (endTimer >= EndPause) EndDay();
    }

    // Adds the day to the progress, saves it, and opens the day-end screens.
    void EndDay()
    {
        int before = progress.RankPoints, bankBefore = progress.TipsCents;
        progress.RankPoints += day.Points;
        // Fines can take a day's tips below zero, but never the jar.
        progress.TipsCents = Math.Max(0, progress.TipsCents + day.TipsCents);
        progress.Day = day.Number + 1;
        if (persistent) progress.Save();
        dayEnd = new DayEnd(day, before, progress.RankPoints, bankBefore, progress.TipsCents);
        phase = Phase.Ended;
        rail.LetGo();
    }

    // Changes screen; anything held at a station is dropped first. A ticket being dragged comes along.
    void SwitchTo(Screen to)
    {
        if (to == screen) return;
        if (screen == Screen.Weigh) weigh.LetGo();
        if (screen == Screen.Pack) pack.LetGo();
        if (screen == Screen.Label) label.LetGo();
        screen = to;
    }

    // A ticket let go: dropped below the rail on the screen of the station it needs next, it goes onto that
    // station if the station is free. Anywhere else it slides back to the rail.
    void Drop(Ticket t, Vector2 mouse)
    {
        if (mouse.Y <= TicketRail.Height) return;
        if (t.Next == Stage.Weigh)
        {
            if (screen != Screen.Weigh) { Say("This order needs weighing: open the Weigh tab"); return; }
            if (!weigh.IsIdle) { Say("Finish the bag on the scale first"); return; }
            PutOnWeigh(t);
        }
        else if (t.Next == Stage.Pack)
        {
            if (screen != Screen.Pack) { Say("This order is weighed: open the Pack tab"); return; }
            if (!pack.IsIdle) { Say("Finish the order on the pack station first"); return; }
            PutOnPack(t);
        }
        else
        {
            if (screen != Screen.Label) { Say("This order is packed: open the Label tab"); return; }
            if (!label.IsIdle) { Say("Finish the order on the label station first"); return; }
            PutOnLabel(t);
        }
    }

    // Moves a packed ticket from the rail onto the label station; its closed container is waiting on the mat.
    void PutOnLabel(Ticket t)
    {
        rail.Remove(t);
        onLabel = t;
        label.Start(t.Customer, t.Packed!.Value.Used, day.Menu);
        Learn("label");
    }

    // Moves a weighed ticket from the rail onto the pack station; its bag is waiting in the IN tray.
    void PutOnPack(Ticket t)
    {
        rail.Remove(t);
        onPack = t;
        pack.Start(t.Customer);
        Learn("pack");
    }

    // Moves a ticket from the rail onto the weigh station and starts its bag.
    void PutOnWeigh(Ticket t)
    {
        rail.Remove(t);
        onWeigh = t;
        weigh.Start(t.Customer.Grams, t.Customer.Strain);
        Learn("weigh");
    }

    // Marks a step as done once, so its first-time pointer stops showing (saved with the day).
    void Learn(string step)
    {
        if (!progress.Learned.Contains(step)) progress.Learned.Add(step);
    }

    // Lets in the next customer when they are due, if the line has room (otherwise they wait outside a moment).
    void UpdateArrivals()
    {
        if (!day.Due || line.Count >= MaxLine) return;
        Arrive();
        day.Arrived();
    }

    // Rings the door chime and adds a customer at the back of the line, walking in from the door, with their ID:
    // from day BadIdsFromDay some are bad (never two in a row), with one flaw picked at random.
    Customer Arrive()
    {
        var c = new Customer(rng, DoorSpot, day.Menu);
        bool bad = day.Number >= Scoring.BadIdsFromDay && !lastIdBad && rng.NextDouble() < Scoring.BadIdChance;
        var flaw = bad ? (IdFlaw)rng.Next(1, 4) : IdFlaw.None;
        c.Id = Ids.Make(c, day.Date, flaw, rng);
        lastIdBad = bad;
        line.Add(c);
        c.Goal = LineSpot(line.Count - 1);
        Audio.Play(Audio.Doorbell);
        return c;
    }

    // Drains everyone's patience and adds to their waiting time; anyone who runs out walks out. Nobody waits
    // while walking in or giving their order.
    void UpdatePatience(float dt)
    {
        foreach (var c in line.Concat(waiting).ToList())
        {
            if (c.Now == Customer.State.Ordering || (c.Now == Customer.State.InLine && !c.Reached)) continue;
            c.Patience -= dt;
            c.Waited += dt;
            if (c.Patience <= 0) WalkOut(c);
        }
    }

    // A customer gives up: they leave, the walkout counts, and their ticket comes off the rail or, if their
    // order was on the bench, the bench is cleared.
    void WalkOut(Customer c)
    {
        var ticket = rail.For(c) ?? (onWeigh?.Customer == c ? onWeigh : onPack?.Customer == c ? onPack
                                     : onLabel?.Customer == c ? onLabel : null);
        string stage = line.Contains(c) ? "line"
            : onWeigh?.Customer == c ? "on weigh" : onPack?.Customer == c ? "on pack" : onLabel?.Customer == c ? "on label"
            : ticket != null ? $"rail for {ticket.Next.ToString().ToLowerInvariant()}" : "waiting";
        PlayLog.Walkout(day, Progress.RankOf(progress.RankPoints), c, ticket, stage);

        line.Remove(c);
        waiting.Remove(c);
        Leave(c, served: false);
        day.Walkouts++;
        Say($"{c.Name} got tired of waiting and walked out");

        var t = rail.For(c);
        if (t != null) rail.Remove(t);
        if (onWeigh?.Customer == c)
        {
            onWeigh = null;
            weigh.Clear();
        }
        if (onPack?.Customer == c)
        {
            onPack = null;
            pack.Clear();
        }
        if (onLabel?.Customer == c)
        {
            onLabel = null;
            label.Clear();
        }
    }

    // Sends a customer out through the door.
    void Leave(Customer c, bool served)
    {
        c.Now = Customer.State.Leaving;
        c.Served = served;
        c.Goal = DoorSpot;
        leaving.Add(c);
    }

    // Keeps everyone walking to their spot; the line and waiting bench close up gaps.
    void UpdateWalking(float dt)
    {
        for (int i = 0; i < line.Count; i++) line[i].Goal = LineSpot(i);
        for (int i = 0; i < waiting.Count; i++) waiting[i].Goal = WaitingSpot(i);
        foreach (var c in line.Concat(waiting).Concat(leaving)) c.Walk(dt);
        leaving.RemoveAll(c => c.Arrived);
    }

    // The customer at the counter starts giving their order: the view cuts to the order shot.
    void TakeOrder()
    {
        var c = AtCounter;
        if (c == null) return;
        if (TakenOrders >= MaxWaiting) { Say("The waiting area is full: finish an order first"); return; }
        c.Now = Customer.State.Ordering;
        shot = new OrderShot(new Ticket(nextTicket++, c), day.Date);
        Learn("take");
    }

    // The order is said: the ticket goes up on the rail, the customer goes to the waiting bench with fresh
    // patience, and the view cuts back to the shop.
    void FinishOrder()
    {
        if (shot == null) return;
        var c = shot.Customer;
        if (shot.Refused) { RefuseCustomer(c); return; }
        line.Remove(c);
        c.Now = Customer.State.Waiting;
        c.PatienceMax = c.Patience = Scoring.WaitPatience(c.Grams);
        waiting.Add(c);
        rail.Add(shot.Ticket, NotepadSpot);
        shot = null;
    }

    // The ID was refused: the customer leaves without an order. A bad ID caught earns rank points; a good one
    // refused counts as a walkout.
    void RefuseCustomer(Customer c)
    {
        shot = null;
        line.Remove(c);
        Leave(c, served: false);
        bool caught = c.Id.Flaw != IdFlaw.None;
        if (caught)
        {
            day.IdsCaught++;
            Say($"Good catch: {Ids.Why(c.Id.Flaw)} (+{Scoring.IdCatchPoints} rank points)");
        }
        else
        {
            day.Walkouts++;
            Say($"{c.Name}'s ID was fine: they walked out");
        }
        PlayLog.Refused(day, Progress.RankOf(progress.RankPoints), c);
    }

    // The bag is in the OUT bin: the weigh station clears, and the ticket flies back up to the rail marked
    // weighed, to be dragged onto the pack station.
    void ToPack(Ticket t, WeighResult weighResult)
    {
        t.Weighed = weighResult;
        t.Next = Stage.Pack;
        onWeigh = null;
        weigh.Clear();
        rail.Add(t, BinSpot);
    }

    // The container is closed: the pack station clears, and the ticket flies back up to the rail marked packed, to
    // be dragged onto the label station.
    void ToLabel(Ticket t, PackResult packResult)
    {
        t.Packed = packResult;
        t.Next = Stage.Label;
        onPack = null;
        pack.Clear();
        rail.Add(t, MatSpot);
    }

    // The order is done (the label station is the last station): score it, clear the station, and line up its
    // hand-over. The customer stops waiting here, so the hand-over queue never costs them points.
    void Finish(Ticket t, LabelResult labelResult)
    {
        var c = t.Customer;
        var result = Scoring.ScoreOrder(t.Weighed!.Value, t.Packed!.Value, labelResult, c.Grams, c.Waited, c.Id.Flaw);
        day.Orders.Add(result);
        PlayLog.Served(day, Progress.RankOf(progress.RankPoints), t, result);
        waiting.Remove(c);
        onLabel = null;
        label.Clear();
        rail.LetGo();
        toHandOver.Enqueue((t, result));
    }

    // Cuts to the hand-over shot for the next finished order.
    void StartHandOver()
    {
        var (t, result) = toHandOver.Dequeue();
        weigh.LetGo();
        pack.LetGo();
        label.LetGo();
        handOver = new HandOver(t, result);
    }

    // Closes the hand-over: the customer heads out happy (or not) and the shop carries on.
    void CloseHandOver()
    {
        Leave(handOver!.Customer, served: true);
        handOver = null;
    }

    // Shows a short message under the rail.
    void Say(string text)
    {
        banner = text;
        bannerTime = 2.5f;
    }

    // Draws the current screen, the rail, the tabs, the clock and totals, any banner, and the intro card or
    // day-end screens on top.
    public void Draw()
    {
        var mouse = Raylib.GetMousePosition();
        if (handOver != null) { handOver.Draw(mouse); return; }

        if (shot != null) shot.Draw();
        else if (screen == Screen.Counter) DrawShopView(mouse);
        else if (screen == Screen.Weigh) weigh.Draw(onWeigh?.Customer, onWeigh?.Number ?? 0, rail.Dragged?.Next == Stage.Weigh);
        else if (screen == Screen.Pack) pack.Draw(onPack?.Customer, onPack?.Number ?? 0, rail.Dragged?.Next == Stage.Pack);
        else label.Draw(onLabel?.Customer, onLabel?.Number ?? 0, rail.Dragged?.Next == Stage.Label);

        rail.Draw(mouse, shot == null && phase == Phase.Open);
        Art.DrawClock(ClockSpot.X, ClockSpot.Y, day.Number, day.ClockText, day.Closed, fast);

        // Tickets waiting on the rail for each station, and whether a station's tab should light up: it is free
        // with a ticket waiting, or someone whose order is there is close to walking out.
        int toWeigh = rail.Waiting(Stage.Weigh), toPack = rail.Waiting(Stage.Pack), toLabel = rail.Waiting(Stage.Label);
        bool UrgentAt(Stage s, Ticket? on) =>
            waiting.Any(c => c.PatienceFraction < 0.3f && (on?.Customer == c || rail.For(c)?.Next == s));
        bool counterNeeds = AtCounter != null || AnyUrgent(line);
        bool weighNeeds = UrgentAt(Stage.Weigh, onWeigh) || (weigh.IsIdle && toWeigh > 0);
        bool packNeeds = UrgentAt(Stage.Pack, onPack) || (pack.IsIdle && toPack > 0);
        bool labelNeeds = UrgentAt(Stage.Label, onLabel) || (label.IsIdle && toLabel > 0);
        string counterText = line.Count > 0 ? $"1 Counter ({line.Count})" : "1 Counter";
        string weighText = toWeigh > 0 ? $"2 Weigh ({toWeigh})" : "2 Weigh";
        string packText = toPack > 0 ? $"3 Pack ({toPack})" : "3 Pack";
        string labelText = toLabel > 0 ? $"4 Label ({toLabel})" : "4 Label";
        Art.DrawTab(CounterTab, counterText, screen == Screen.Counter, Raylib.CheckCollisionPointRec(mouse, CounterTab),
                    screen != Screen.Counter && counterNeeds);
        Art.DrawTab(WeighTab, weighText, screen == Screen.Weigh, Raylib.CheckCollisionPointRec(mouse, WeighTab),
                    screen != Screen.Weigh && weighNeeds);
        Art.DrawTab(PackTab, packText, screen == Screen.Pack, Raylib.CheckCollisionPointRec(mouse, PackTab),
                    screen != Screen.Pack && packNeeds);
        Art.DrawTab(LabelTab, labelText, screen == Screen.Label, Raylib.CheckCollisionPointRec(mouse, LabelTab),
                    screen != Screen.Label && labelNeeds);
        Art.Label($"Served {day.Served}/{day.Expected}   Walkouts {day.Walkouts}   Tips {Scoring.Money(day.TipsCents)}",
                  910, 686, 20, Art.Text);

        if (bannerTime > 0) Art.DrawBanner(banner, Program.Width / 2f, TicketRail.Height + 10);
        DrawPointer();
        if (Tuning && phase == Phase.Open && shot == null) DrawTuning();

        if (phase == Phase.Intro)
        {
            int rank = Progress.RankOf(progress.RankPoints);
            var next = Strains.NextLocked(rank);
            string? nextText = next == null ? null : $"Next strain: {next.Name}, at rank {next.Rank} ({Progress.RankName(next.Rank)})";
            string? notice = day.Number == Scoring.BadIdsFromDay ? "From today, some IDs are bad: check every one." : null;
            Art.DrawDayIntro(IntroPanel, day.Number, day.Expected, $"Rank {rank} {Progress.RankName(rank)}",
                             Scoring.Money(progress.TipsCents), day.Menu, nextText, Ids.LongText(day.Date), notice);
            Art.DrawButton(OpenButton, "Open the shop", Raylib.CheckCollisionPointRec(mouse, OpenButton), true);
        }
        if (phase == Phase.Ended) dayEnd!.Draw(mouse);
    }

    // Draws the first-time pointer: an arrow at the next thing to do for one step the player has not done yet
    // (take an order, then put a ticket on each station). The other screen's tab first, then the button or the
    // ticket to drag. A step on the current screen wins over one elsewhere. Nothing shows while dragging.
    void DrawPointer()
    {
        if (phase != Phase.Open || shot != null || rail.Dragged != null) return;
        bool New(string step) => !progress.Learned.Contains(step);

        var steps = new List<(Screen At, Rectangle Tab, string Name, Ticket? Ticket)>();
        if (New("take") && AtCounter != null && TakenOrders < MaxWaiting) steps.Add((Screen.Counter, CounterTab, "Counter", null));
        if (New("weigh") && weigh.IsIdle && rail.FirstFor(Stage.Weigh) is { } w) steps.Add((Screen.Weigh, WeighTab, "Weigh", w));
        if (New("pack") && pack.IsIdle && rail.FirstFor(Stage.Pack) is { } p) steps.Add((Screen.Pack, PackTab, "Pack", p));
        if (New("label") && label.IsIdle && rail.FirstFor(Stage.Label) is { } l) steps.Add((Screen.Label, LabelTab, "Label", l));
        if (steps.Count == 0) return;

        var step = steps.FirstOrDefault(s => s.At == screen);
        if (step.Name == null)
        {
            step = steps[0];
            Art.DrawPointer(new Vector2(step.Tab.X + step.Tab.Width / 2, step.Tab.Y - 2), $"Open {step.Name}", up: false);
            return;
        }
        if (step.Ticket == null)
        {
            Art.DrawPointer(new Vector2(TakeButton.X + TakeButton.Width / 2, TakeButton.Y - 2), "Take the order", up: false);
            return;
        }
        var card = TicketRail.CardRect(step.Ticket);
        Art.DrawPointer(new Vector2(card.X + card.Width / 2, card.Y + card.Height + 2), "Drag this ticket down onto the station", up: true);
    }

    // Draws the F1 tuning panel for the current screen. The Counter's shows the day's pace and each customer's
    // patience and waiting time against the Waiting par; each station supplies its own lines.
    void DrawTuning()
    {
        switch (screen)
        {
            case Screen.Weigh: Art.DrawTuning("Weigh tuning", weigh.TuningLines()); return;
            case Screen.Pack: Art.DrawTuning("Pack tuning", pack.TuningLines()); return;
            case Screen.Label: Art.DrawTuning("Label tuning", label.TuningLines()); return;
        }
        string next = float.IsInfinity(day.UntilNext) ? "nobody else coming" : $"next in {day.UntilNext:0} s";
        var lines = new List<TuningLine>
        {
            new($"Day {day.Clock:0} of {day.CloseAt:0} s, {next}, {day.StillToCome} to come"),
        };
        foreach (var c in line.Concat(waiting))
        {
            string where = c.Now == Customer.State.Waiting ? "waiting" : "in line";
            float par = Scoring.WaitPar(c.Grams);
            lines.Add(new($"{c.Name} {where}: patience {c.Patience:0}/{c.PatienceMax:0} s, waited {c.Waited:0}/{par:0} s",
                          c.PatienceFraction < 0.3f || c.Waited > par));
        }
        if (line.Count + waiting.Count == 0) lines.Add(new("Nobody in the shop."));
        Art.DrawTuning("Counter tuning", lines);
    }

    // Draws the side-on shop view: the door with its open sign, the waiting bench, everyone on the floor (back row
    // first), the budtender behind the counter, and the take order button.
    void DrawShopView(Vector2 mouse)
    {
        Art.DrawShopRoom(TicketRail.Height, BackY);
        Art.DrawDoor(DoorSpot);
        Art.DrawDoorSign(DoorSpot, phase == Phase.Open && !day.Closed);
        Art.DrawBench(WaitingSpot(0).X - 60, WaitingSpot(MaxWaiting - 1).X + 60, BackY);

        foreach (var c in line.Concat(waiting).Concat(leaving).OrderBy(c => c.Pos.Y))
            Art.DrawCustomer(c, c.Pos, DepthScale(c.Pos.Y), c.Now != Customer.State.Leaving);

        Art.DrawBudtender(BudtenderFeet);
        Art.DrawCounterSide(CounterRect);

        var front = AtCounter;
        if (front != null) Art.DrawSpeech(Art.HeadOf(front.Pos, DepthScale(front.Pos.Y)), "Ready!");
        bool canTake = front != null && TakenOrders < MaxWaiting;
        Art.DrawButton(TakeButton, "Take order", canTake && Raylib.CheckCollisionPointRec(mouse, TakeButton), canTake);

        string hint = front != null
            ? (canTake ? "Take the order (or press Enter)." : "The waiting area is full: finish an order first.")
            : line.Count > 0 ? "Someone is walking up to the counter."
            : day.Closed ? "Closed for the day." : "Nobody in line yet.";
        Art.Label(hint, 20, TicketRail.Height + 12, 20, Art.Faint);
    }
}
