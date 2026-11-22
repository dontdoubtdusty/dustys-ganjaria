using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The shop, one day at a time. Each day opens with a card (day number, customers expected); then customers ring
// the door chime on the day's schedule, walk in and queue at the counter. Taking an order cuts to the order shot,
// where they say it and the ticket goes up on the rail; they wait while you drag tickets onto the weigh station
// and make them. A finished order cuts to the hand-over shot (bag check, scores, reaction, tip); a customer who
// runs out of patience walks out. At closing time the doors shut, you finish whoever is inside, and the day ends
// with the results and the tip jar. Screens are switched with tabs along the bottom (or the 1 and 2 keys); the
// rail is on every screen. Both shots pause the shop: the clock, arrivals and everyone's patience.
sealed class Shop
{
    enum Screen { Counter, Weigh }
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
    static readonly Rectangle CounterTab = new(470, 676, 170, 38);
    static readonly Rectangle WeighTab = new(650, 676, 170, 38);
    static readonly Rectangle IntroPanel = new(390, 160, 500, 350);
    static readonly Rectangle OpenButton = new(520, 430, 240, 56);
    static readonly Vector2 ClockSpot = new(990, 30);

    readonly Random rng;
    readonly Progress progress;
    readonly bool persistent;
    readonly WeighStation weigh;
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
    Ticket? onWeigh;
    int nextTicket = 1;
    bool closingSaid, fast;
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
        StartDay(progress.Day);
    }

    // Clears the shop and sets up a day, waiting on its intro card.
    void StartDay(int number)
    {
        day = new Day(number, rng);
        phase = Phase.Intro;
        screen = Screen.Counter;
        line.Clear();
        waiting.Clear();
        leaving.Clear();
        toHandOver.Clear();
        rail.Clear();
        weigh.Clear();
        shot = null;
        handOver = null;
        dayEnd = null;
        onWeigh = null;
        nextTicket = 1;
        closingSaid = fast = false;
        endTimer = 0;
        bannerTime = 0;
    }

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
    // --shot-counter: two in line and one waiting, shop view. --shot-order: the order shot mid-way.
    // --shot and --shot-throw: one ticket on the weigh station and another on the rail.
    // --shot-handover: the end of a hand-over; --shot-check: its bag check. --shot-results and --shot-tipjar: the two day-end screens.
    public void StageForScreenshot(string mode)
    {
        phase = Phase.Open;
        if (mode == "--shot-results" || mode == "--shot-tipjar")
        {
            var w = Scoring.Score(3.5f, Scoring.BandFor(3.5f), 3.55f, 13f, 0, 1);
            day.Orders.Add(Scoring.ScoreOrder(w, 3.5f, 40f));
            day.Orders.Add(Scoring.ScoreOrder(Scoring.Score(1f, Scoring.BandFor(1f), 1.02f, 9f, 0, 0), 1f, 70f));
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
            handOver = new HandOver(t, Scoring.ScoreOrder(w, first.Grams, first.Waited));
            if (mode == "--shot-check") handOver.HoldAtCheck();
            else handOver.HoldAt(true);
            return;
        }

        if (mode == "--shot-counter" || mode == "--shot-order")
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
            return;
        }

        var second = Arrive();
        second.Pos = second.Goal;
        second.Reached = true;
        TakeOrder();
        FinishOrder();
        second.Pos = second.Goal = WaitingSpot(1);

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
            if (Raylib.IsKeyPressed(KeyboardKey.One)) SwitchTo(Screen.Counter);
            if (Raylib.IsKeyPressed(KeyboardKey.Two)) SwitchTo(Screen.Weigh);
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
            shot.Update(dt, (press && !tabClicked) || confirm);
            if (shot.Done) FinishOrder();
        }
        else if (screen == Screen.Counter && !tabClicked && !railTook)
        {
            bool click = press && Raylib.CheckCollisionPointRec(mouse, TakeButton);
            if ((click || confirm) && AtCounter != null) TakeOrder();
        }

        bool weighInput = screen == Screen.Weigh && !tabClicked && !railTook && rail.Dragged == null;
        weigh.Update(dt, weighInput);

        var shipped = weigh.TakeShipped();
        if (shipped != null && onWeigh != null) Finish(onWeigh, shipped.Value);

        if (!InShot && toHandOver.Count > 0) StartHandOver();
        UpdateClosing(dt);
    }

    // True when nobody is in the shop and no work is in progress.
    bool ShopEmpty => line.Count == 0 && waiting.Count == 0 && rail.Count == 0 && weigh.IsIdle
                      && shot == null && handOver == null && toHandOver.Count == 0;

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
        int before = progress.RankPoints;
        progress.RankPoints += day.Points;
        progress.TipsCents += day.TipsCents;
        progress.Day = day.Number + 1;
        if (persistent) progress.Save();
        dayEnd = new DayEnd(day, before, progress.RankPoints, progress.TipsCents);
        phase = Phase.Ended;
        rail.LetGo();
    }

    // Changes screen; anything held at the weigh station is dropped first. A ticket being dragged comes along.
    void SwitchTo(Screen to)
    {
        if (to == screen) return;
        if (screen == Screen.Weigh) weigh.LetGo();
        screen = to;
    }

    // A ticket let go: dropped below the rail on the weigh screen it goes onto the station if the station is free.
    // Anywhere else it slides back to the rail.
    void Drop(Ticket t, Vector2 mouse)
    {
        if (mouse.Y <= TicketRail.Height) return;
        if (screen != Screen.Weigh) { Say("Drop tickets on a station: open the Weigh tab"); return; }
        if (!weigh.IsIdle) { Say("Finish the bag on the scale first"); return; }
        PutOnWeigh(t);
    }

    // Moves a ticket from the rail onto the weigh station and starts its bag.
    void PutOnWeigh(Ticket t)
    {
        rail.Remove(t);
        onWeigh = t;
        weigh.Start(t.Customer.Grams);
    }

    // Lets in the next customer when they are due, if the line has room (otherwise they wait outside a moment).
    void UpdateArrivals()
    {
        if (!day.Due || line.Count >= MaxLine) return;
        Arrive();
        day.Arrived();
    }

    // Rings the door chime and adds a customer at the back of the line, walking in from the door.
    Customer Arrive()
    {
        var c = new Customer(rng, DoorSpot);
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
        shot = new OrderShot(new Ticket(nextTicket++, c));
    }

    // The order is said: the ticket goes up on the rail, the customer goes to the waiting bench with fresh
    // patience, and the view cuts back to the shop.
    void FinishOrder()
    {
        if (shot == null) return;
        var c = shot.Customer;
        line.Remove(c);
        c.Now = Customer.State.Waiting;
        c.PatienceMax = c.Patience = Scoring.WaitPatience(c.Grams);
        waiting.Add(c);
        rail.Add(shot.Ticket, NotepadSpot);
        shot = null;
    }

    // The order is done (for now the weigh station is the last station): score it, clear the bench, and line up
    // its hand-over. The customer stops waiting here, so the hand-over queue never costs them points.
    void Finish(Ticket t, WeighResult weighResult)
    {
        var c = t.Customer;
        var result = Scoring.ScoreOrder(weighResult, c.Grams, c.Waited);
        day.Orders.Add(result);
        waiting.Remove(c);
        onWeigh = null;
        weigh.Clear();
        rail.LetGo();
        toHandOver.Enqueue((t, result));
    }

    // Cuts to the hand-over shot for the next finished order.
    void StartHandOver()
    {
        var (t, result) = toHandOver.Dequeue();
        weigh.LetGo();
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
        else weigh.Draw(onWeigh?.Customer, onWeigh?.Number ?? 0, rail.Dragged != null);

        rail.Draw(mouse, shot == null && phase == Phase.Open);
        Art.DrawClock(ClockSpot.X, ClockSpot.Y, day.Number, day.ClockText, day.Closed, fast);

        bool counterNeeds = AtCounter != null || AnyUrgent(line);
        bool weighNeeds = AnyUrgent(waiting) || (weigh.IsIdle && rail.Count > 0);
        string counterText = line.Count > 0 ? $"1 Counter ({line.Count})" : "1 Counter";
        string weighText = rail.Count > 0 ? $"2 Weigh ({rail.Count})" : "2 Weigh";
        Art.DrawTab(CounterTab, counterText, screen == Screen.Counter, Raylib.CheckCollisionPointRec(mouse, CounterTab),
                    screen != Screen.Counter && counterNeeds);
        Art.DrawTab(WeighTab, weighText, screen == Screen.Weigh, Raylib.CheckCollisionPointRec(mouse, WeighTab),
                    screen != Screen.Weigh && weighNeeds);
        Art.Label($"Served {day.Served}/{day.Expected}   Walkouts {day.Walkouts}   Tips {Scoring.Money(day.TipsCents)}",
                  840, 684, 22, Art.Text);

        if (bannerTime > 0) Art.DrawBanner(banner, Program.Width / 2f, TicketRail.Height + 10);

        if (phase == Phase.Intro)
        {
            int rank = Progress.RankOf(progress.RankPoints);
            Art.DrawDayIntro(IntroPanel, day.Number, day.Expected, $"Rank {rank} {Progress.RankName(rank)}",
                             Scoring.Money(progress.TipsCents));
            Art.DrawButton(OpenButton, "Open the shop", Raylib.CheckCollisionPointRec(mouse, OpenButton), true);
        }
        if (phase == Phase.Ended) dayEnd!.Draw(mouse);
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
