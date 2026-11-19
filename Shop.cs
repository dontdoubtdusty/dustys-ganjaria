using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The shop: customers arrive and queue at the counter, you take their orders, they wait while you make them at
// the weigh station, and each one either gets their order (score and tip) or runs out of patience and walks out.
// Screens are switched with tabs along the bottom (or the 1 and 2 keys).
sealed class Shop
{
    enum Screen { Counter, Weigh }

    // Most customers that queue in the line, and most taken orders waiting at once.
    const int MaxLine = 4;
    const int MaxWaiting = 4;
    // Seconds between new customers walking in.
    const float ArriveMin = 14f, ArriveMax = 24f;
    const float FirstArrival = 1.5f;

    // Shop floor layout: heads stand at HeadY; the counter top hides legs from CounterY down.
    const float HeadY = 260, CounterY = 420;
    const float FrontX = 640, LineGap = 140;
    const float WaitingX = 90, WaitingGap = 110;
    const float EnterX = Program.Width + 90, ExitX = -90;

    static readonly Rectangle TakeButton = new(545, 520, 190, 56);
    static readonly Rectangle CounterTab = new(470, 676, 170, 38);
    static readonly Rectangle WeighTab = new(650, 676, 170, 38);
    static readonly Rectangle ResultPanel = new(370, 100, 540, 500);
    static readonly Rectangle NextButton = new(530, 520, 220, 56);

    readonly Random rng;
    readonly WeighStation weigh;
    readonly List<Customer> line = new();
    readonly List<Customer> waiting = new();
    readonly List<Customer> leaving = new();

    Screen screen = Screen.Counter;
    Customer? active;
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
    static Vector2 LineSpot(int i) => new(FrontX + LineGap * i, HeadY);

    // Spot in the waiting area for place i.
    static Vector2 WaitingSpot(int i) => new(WaitingX + WaitingGap * i, HeadY);

    // The customer standing at the counter, ready to order, or null.
    Customer? AtCounter => line.Count > 0 && line[0].Arrived ? line[0] : null;

    // True if someone in line or waiting is close to walking out (lights the tab orange).
    static bool AnyUrgent(IEnumerable<Customer> people) => people.Any(c => c.PatienceFraction < 0.3f);

    // Sets up a scene for the --shot flags: an order already taken and on the weigh station (or two people in
    // line and one waiting, on the counter screen).
    public void StageForScreenshot(string mode)
    {
        arriveTimer = 999;
        var first = Arrive();
        first.Pos = first.Goal;
        TakeOrder();
        first.Pos = first.Goal = WaitingSpot(0);

        if (mode == "--shot-counter")
        {
            for (int i = 0; i < 2; i++) Arrive().Pos = LineSpot(i);
            line[1].Patience = 9;
            screen = Screen.Counter;
            return;
        }

        screen = Screen.Weigh;
        StartNextTicket();
        weigh.StageForScreenshot(mode == "--shot-throw");
    }

    // Runs one frame: tabs, the result panel, arrivals, patience, walking, the counter and the weigh station.
    public void Update(float dt)
    {
        dt = MathF.Min(dt, 1f / 30f);
        var mouse = Raylib.GetMousePosition();
        bool press = Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool confirm = Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter)
                       || Raylib.IsKeyPressed(KeyboardKey.Space);
        if (bannerTime > 0) bannerTime -= dt;

        // The result panel pauses the shop until it is closed.
        if (result != null)
        {
            if (confirm || (press && Raylib.CheckCollisionPointRec(mouse, NextButton))) CloseResult();
            return;
        }

        bool tabClicked = false;
        if (press && Raylib.CheckCollisionPointRec(mouse, CounterTab)) { SwitchTo(Screen.Counter); tabClicked = true; }
        if (press && Raylib.CheckCollisionPointRec(mouse, WeighTab)) { SwitchTo(Screen.Weigh); tabClicked = true; }
        if (Raylib.IsKeyPressed(KeyboardKey.One)) SwitchTo(Screen.Counter);
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) SwitchTo(Screen.Weigh);

        UpdateArrivals(dt);
        UpdatePatience(dt);
        UpdateWalking(dt);

        if (screen == Screen.Counter && !tabClicked)
        {
            bool click = press && Raylib.CheckCollisionPointRec(mouse, TakeButton);
            if ((click || confirm) && AtCounter != null) TakeOrder();
        }

        if (weigh.IsIdle) StartNextTicket();
        weigh.Update(dt, screen == Screen.Weigh && !tabClicked);

        var shipped = weigh.TakeShipped();
        if (shipped != null && active != null) Serve(active, shipped.Value);
    }

    // Changes screen; anything held at the weigh station is dropped first.
    void SwitchTo(Screen to)
    {
        if (to == screen) return;
        if (screen == Screen.Weigh) weigh.LetGo();
        screen = to;
    }

    // Brings in a new customer every so often, while the line has room.
    void UpdateArrivals(float dt)
    {
        arriveTimer -= dt;
        if (arriveTimer > 0) return;
        arriveTimer = ArriveMin + (float)rng.NextDouble() * (ArriveMax - ArriveMin);
        if (line.Count < MaxLine) Arrive();
    }

    // Adds a customer at the back of the line, walking in from the right.
    Customer Arrive()
    {
        var c = new Customer(rng, new Vector2(EnterX, HeadY));
        line.Add(c);
        c.Goal = LineSpot(line.Count - 1);
        return c;
    }

    // Drains everyone's patience; anyone who runs out walks out.
    void UpdatePatience(float dt)
    {
        foreach (var c in line.Concat(waiting).ToList())
        {
            // Patience only starts once they reach their spot, so walking in costs nothing.
            if (c.Now == Customer.State.InLine && c.Pos.X > LineSpot(MaxLine).X) continue;
            c.Patience -= dt;
            if (c.Patience <= 0) WalkOut(c);
        }
    }

    // A customer gives up: they leave, the walkout counts, and if their order was on the bench it is dropped.
    void WalkOut(Customer c)
    {
        line.Remove(c);
        waiting.Remove(c);
        Leave(c, served: false);
        walkouts++;
        Say($"{c.Name} got tired of waiting and walked out");
        if (c == active)
        {
            active = null;
            weigh.Clear();
        }
    }

    // Sends a customer out of the shop to the left.
    void Leave(Customer c, bool served)
    {
        c.Now = Customer.State.Leaving;
        c.Served = served;
        c.Goal = new Vector2(ExitX, HeadY);
        leaving.Add(c);
    }

    // Keeps everyone walking to their spot; the line and waiting area close up gaps.
    void UpdateWalking(float dt)
    {
        for (int i = 0; i < line.Count; i++) line[i].Goal = LineSpot(i);
        for (int i = 0; i < waiting.Count; i++) waiting[i].Goal = WaitingSpot(i);
        foreach (var c in line.Concat(waiting).Concat(leaving)) c.Walk(dt);
        leaving.RemoveAll(c => c.Arrived);
    }

    // The customer at the counter gives their order and moves to the waiting area with fresh patience.
    void TakeOrder()
    {
        var c = AtCounter;
        if (c == null) return;
        if (waiting.Count >= MaxWaiting) { Say("The waiting area is full: finish an order first"); return; }
        line.RemoveAt(0);
        c.Now = Customer.State.Waiting;
        c.PatienceMax = c.Patience = Scoring.WaitPatience(c.Grams);
        waiting.Add(c);
    }

    // Puts the oldest taken order on the weigh station, if there is one.
    void StartNextTicket()
    {
        active = waiting.FirstOrDefault();
        if (active != null) weigh.Start(active.Grams);
    }

    // The bag reached the customer: score it with their wait, take the tip, and show the result.
    void Serve(Customer c, WeighResult weighResult)
    {
        result = Scoring.ScoreOrder(weighResult, c.PatienceFraction);
        resultFor = c;
        waiting.Remove(c);
        Leave(c, served: true);
        active = null;
        served++;
        tips += result.Value.Tip;
    }

    // Closes the result panel and clears the bench for the next ticket.
    void CloseResult()
    {
        result = null;
        resultFor = null;
        weigh.Clear();
    }

    // Shows a short message across the top of the screen.
    void Say(string text)
    {
        banner = text;
        bannerTime = 2.5f;
    }

    // Draws the current screen, the tabs and totals, any banner, and the result panel on top.
    public void Draw()
    {
        var mouse = Raylib.GetMousePosition();
        if (screen == Screen.Counter) DrawCounter(mouse);
        else weigh.Draw(active ?? resultFor, Math.Max(0, waiting.Count - (active != null ? 1 : 0)));

        bool counterNeeds = AtCounter != null || AnyUrgent(line);
        bool weighNeeds = AnyUrgent(waiting);
        string counterText = line.Count > 0 ? $"1 Counter ({line.Count})" : "1 Counter";
        string weighText = waiting.Count > 0 ? $"2 Weigh ({waiting.Count})" : "2 Weigh";
        Art.DrawTab(CounterTab, counterText, screen == Screen.Counter, Raylib.CheckCollisionPointRec(mouse, CounterTab),
                    screen != Screen.Counter && counterNeeds);
        Art.DrawTab(WeighTab, weighText, screen == Screen.Weigh, Raylib.CheckCollisionPointRec(mouse, WeighTab),
                    screen != Screen.Weigh && weighNeeds);
        Art.Label($"Served {served}   Walkouts {walkouts}   Tips ${tips}", 870, 684, 22, Art.Text);

        if (bannerTime > 0) Art.LabelCentered(banner, Program.Width / 2f, 10, 26, Art.Warn);
        if (result != null) DrawResult(mouse);
    }

    // Draws the counter screen: the waiting area, the line, the counter, and the take order button.
    void DrawCounter(Vector2 mouse)
    {
        Art.DrawShopWall(CounterY, WaitingX + WaitingGap * 1.5f, FrontX + LineGap * 1.5f);
        foreach (var c in waiting) Art.DrawCustomer(c, true);
        foreach (var c in line) Art.DrawCustomer(c, true);
        foreach (var c in leaving) Art.DrawCustomer(c, false);
        Art.DrawCounterTop(CounterY);

        var front = AtCounter;
        if (front != null) Art.DrawSpeech(front.Pos, front.OrderText);
        bool canTake = front != null && waiting.Count < MaxWaiting;
        Art.DrawButton(TakeButton, "Take order", canTake && Raylib.CheckCollisionPointRec(mouse, TakeButton), canTake);

        string hint = front != null
            ? (canTake ? "Take the order (or press Enter), then make it at the weigh station." : "The waiting area is full: finish an order first.")
            : line.Count > 0 ? "Someone is walking up to the counter." : "Nobody in line yet.";
        Art.LabelCentered(hint, Program.Width / 2f, 600, 22, Art.Faint);
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
