using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The shop: customers ring the door chime, walk in and queue at the counter. Taking an order cuts to the order
// shot, where they say it and the ticket goes up on the rail; they wait while you drag tickets onto the weigh
// station and make them, and each one either gets their order (score and tip) or runs out of patience and walks
// out. Screens are switched with tabs along the bottom (or the 1 and 2 keys); the rail is on every screen.
sealed class Shop
{
    enum Screen { Counter, Weigh }

    // Most customers that queue in the line, and most taken orders at once (rail, stations and the order shot).
    const int MaxLine = 4;
    const int MaxWaiting = 4;
    // Seconds between new customers walking in.
    const float ArriveMin = 14f, ArriveMax = 24f;
    const float FirstArrival = 1.5f;

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
    static readonly Rectangle ResultPanel = new(370, 100, 540, 500);
    static readonly Rectangle NextButton = new(530, 520, 220, 56);

    readonly Random rng;
    readonly WeighStation weigh;
    readonly TicketRail rail = new();
    readonly List<Customer> line = new();
    readonly List<Customer> waiting = new();
    readonly List<Customer> leaving = new();

    Screen screen = Screen.Counter;
    OrderShot? shot;
    Ticket? onWeigh;
    int nextTicket = 1;
    float arriveTimer = FirstArrival;
    int served, walkouts, tips;
    OrderResult? result;
    Customer? resultFor;
    string banner = "";
    float bannerTime;

    public Shop(Random rng)
    {
        this.rng = rng;
        weigh = new WeighStation(rng);
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

    // True if someone in line or waiting is close to walking out (lights the tab orange).
    static bool AnyUrgent(IEnumerable<Customer> people) => people.Any(c => c.PatienceFraction < 0.3f);

    // Sets up a scene for the --shot flags. All of them have one order already taken.
    // --shot-counter: two in line and one waiting, shop view. --shot-order: the order shot mid-way.
    // --shot and --shot-throw: one ticket on the weigh station and another on the rail.
    public void StageForScreenshot(string mode)
    {
        arriveTimer = 999;
        var first = Arrive();
        first.Pos = first.Goal;
        first.Reached = true;
        TakeOrder();
        FinishOrder();
        first.Pos = first.Goal = WaitingSpot(0);

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

    // Runs one frame: tabs, the result panel, the order shot, arrivals, patience, walking, the rail and the weigh station.
    public void Update(float dt)
    {
        dt = MathF.Min(dt, 1f / 30f);
        var mouse = Raylib.GetMousePosition();
        bool press = Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool release = Raylib.IsMouseButtonReleased(MouseButton.Left);
        bool confirm = Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter)
                       || Raylib.IsKeyPressed(KeyboardKey.Space);
        if (bannerTime > 0) bannerTime -= dt;

        // The result panel pauses the shop until it is closed.
        if (result != null)
        {
            if (confirm || (press && Raylib.CheckCollisionPointRec(mouse, NextButton))) CloseResult();
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

        UpdateArrivals(dt);
        UpdatePatience(dt);
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
        if (shipped != null && onWeigh != null) Serve(onWeigh, shipped.Value);
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

    // Brings in a new customer every so often, while the line has room.
    void UpdateArrivals(float dt)
    {
        arriveTimer -= dt;
        if (arriveTimer > 0) return;
        arriveTimer = ArriveMin + (float)rng.NextDouble() * (ArriveMax - ArriveMin);
        if (line.Count < MaxLine) Arrive();
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

    // Drains everyone's patience; anyone who runs out walks out. Nobody loses patience while walking in or giving
    // their order.
    void UpdatePatience(float dt)
    {
        foreach (var c in line.Concat(waiting).ToList())
        {
            if (c.Now == Customer.State.Ordering || (c.Now == Customer.State.InLine && !c.Reached)) continue;
            c.Patience -= dt;
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
        walkouts++;
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

    // The bag reached the customer: score it with their wait, take the tip, and show the result.
    void Serve(Ticket t, WeighResult weighResult)
    {
        var c = t.Customer;
        result = Scoring.ScoreOrder(weighResult, c.PatienceFraction);
        resultFor = c;
        waiting.Remove(c);
        Leave(c, served: true);
        onWeigh = null;
        served++;
        tips += result.Value.Tip;
        if (result.Value.Tip > 0) Audio.Play(Audio.Till, 0.7f);
        rail.LetGo();
    }

    // Closes the result panel and clears the bench for the next ticket.
    void CloseResult()
    {
        result = null;
        resultFor = null;
        weigh.Clear();
    }

    // Shows a short message under the rail.
    void Say(string text)
    {
        banner = text;
        bannerTime = 2.5f;
    }

    // Draws the current screen, the rail, the tabs and totals, any banner, and the result panel on top.
    public void Draw()
    {
        var mouse = Raylib.GetMousePosition();
        if (shot != null) shot.Draw();
        else if (screen == Screen.Counter) DrawShopView(mouse);
        else weigh.Draw(onWeigh?.Customer ?? resultFor, onWeigh?.Number ?? 0, rail.Dragged != null);

        rail.Draw(mouse, shot == null && result == null);

        bool counterNeeds = AtCounter != null || AnyUrgent(line);
        bool weighNeeds = AnyUrgent(waiting) || (weigh.IsIdle && rail.Count > 0);
        string counterText = line.Count > 0 ? $"1 Counter ({line.Count})" : "1 Counter";
        string weighText = rail.Count > 0 ? $"2 Weigh ({rail.Count})" : "2 Weigh";
        Art.DrawTab(CounterTab, counterText, screen == Screen.Counter, Raylib.CheckCollisionPointRec(mouse, CounterTab),
                    screen != Screen.Counter && counterNeeds);
        Art.DrawTab(WeighTab, weighText, screen == Screen.Weigh, Raylib.CheckCollisionPointRec(mouse, WeighTab),
                    screen != Screen.Weigh && weighNeeds);
        Art.Label($"Served {served}   Walkouts {walkouts}   Tips ${tips}", 870, 684, 22, Art.Text);

        if (bannerTime > 0) Art.DrawBanner(banner, Program.Width / 2f, TicketRail.Height + 10);
        if (result != null) DrawResult(mouse);
    }

    // Draws the side-on shop view: the door, the waiting bench, everyone on the floor (back row first),
    // the budtender behind the counter, and the take order button.
    void DrawShopView(Vector2 mouse)
    {
        Art.DrawShopRoom(TicketRail.Height, BackY);
        Art.DrawDoor(DoorSpot);
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
            : line.Count > 0 ? "Someone is walking up to the counter." : "Nobody in line yet.";
        Art.Label(hint, 20, TicketRail.Height + 12, 20, Art.Faint);
    }

    // Draws the result of a served order: the grade, the weigh breakdown, the wait and the tip.
    void DrawResult(Vector2 mouse)
    {
        var r = result!.Value;
        var w = r.Weigh;
        Raylib.DrawRectangle(0, 0, Program.Width, Program.Height, new Color(0, 0, 0, 140));
        Art.DrawPanel(ResultPanel);
        float cx = ResultPanel.X + ResultPanel.Width / 2;

        Art.LabelCentered(r.Grade, cx, ResultPanel.Y + 20, 56, r.Total >= 70 ? Art.Accent : Art.Warn);
        Art.LabelCentered($"{resultFor?.Name} paid, score {r.Total}, tip ${r.Tip}", cx, ResultPanel.Y + 84, 28, Art.Text);

        float diff = w.Weight - w.Target;
        string where = MathF.Abs(diff) <= w.Band ? "in the green"
            : diff > 0 ? $"{diff:0.00} g over" : $"{-diff:0.00} g short";
        float x = ResultPanel.X + 44, y = ResultPanel.Y + 140;
        Art.Label($"Bagged {w.Weight:0.00} g for a {w.Target:0.0#} g order: {where}", x, y, 22, Art.Text);
        Art.Label($"Weigh station {w.Total}", x, y + 42, 22, Art.Text);
        Art.Label($"Accuracy {w.Accuracy}   Speed {w.Speed} ({w.Seconds:0.0} s)", x + 24, y + 74, 20, Art.Faint);
        Art.Label($"Handling {w.Handling} ({w.Slips} zip slips, {w.Spilled} spilled)", x + 24, y + 102, 20, Art.Faint);
        Art.Label($"Wait {r.Wait}", x, y + 144, 22, Art.Text);
        Art.Label("Full marks while they still had half their patience.", x + 24, y + 176, 20, Art.Faint);

        Art.DrawButton(NextButton, "Next", Raylib.CheckCollisionPointRec(mouse, NextButton), true);
    }
}
