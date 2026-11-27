using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The two kinds of container a customer can ask for.
enum ContainerKind { Pouch, Jar }

// One container: its kind and size tier. Small holds 1 g and 3.5 g bags, large holds 7 g.
readonly record struct Container(ContainerKind Kind, bool Large)
{
    public string Name => $"{(Large ? "large" : "small")} {(Kind == ContainerKind.Jar ? "jar" : "pouch")}";
}

static class Containers
{
    // Orders of this many grams or more need a large container.
    public const float LargeFrom = 7f;

    // The container an order should go in: the kind the customer asked for, the size its grams need.
    public static Container For(Customer c) => new(c.Wants, c.Grams >= LargeFrom);

    // Inside width and depth. A small one is narrower than the sealed 7 g bag, so that bag will not go in.
    public static Vector2 Inside(bool large) => large ? new Vector2(170, 175) : new Vector2(116, 135);
}

// The pack station: drag a container from the shelf onto the mat. The sealed bag is hoisted onto an overhead rail
// and slides back and forth over it; click to drop it in as it passes the mouth. Then close it with a timing
// mini-game: hold to heat a pouch's sealer and let go in the green, or click a jar's lid as its notch passes the
// mark, three times. Once the bag is in it is locked in; Start over resets the station at a time cost. The shop
// hands it each ticket with Start and collects the result with TakeShipped.
sealed class PackStation
{
    const float FloorY = 665;
    static readonly Rectangle TicketRect = new(50, 150, 330, 110);
    static readonly Rectangle TrayRect = new(60, 615, 220, 50);
    static readonly Rectangle RestartButton = new(60, 300, 230, 46);
    // Where a container stands to be packed, and the area a carried container snaps to it from.
    const float MatX = 640;
    static readonly Rectangle MatZone = new(450, 250, 380, FloorY - 250);
    // The shelf: small containers on the shelf board, large ones on the table below it. Pouches left, jars right.
    const float ShelfY = 400;
    const float PouchShelfX = 960, JarShelfX = 1150;

    // Bag drop: the overhead rail's height, the hanging bag's bottom edge (above a large container's mouth), how far
    // either side of the mat the bag slides (centre, pixels), and its speed (radians a second of the sine it follows:
    // 2.85 is one pass across about every 1.1 s, about 480 px/s over the mouth). DropForgive is how many pixels past
    // a clean fit a drop still slides in off the rim. A missed drop is knocked off the rim at MissKick px/s.
    const float RailY = 322, HangBottom = 470, SwingReach = 170, SwingSpeed = 2.85f, DropForgive = 20f, MissKick = 520f;
    // Seconds the bag takes to be hoisted from the tray (or the table) up onto the rail.
    const float HoistSeconds = 0.45f;
    // The bag counts as settled once it has moved slower than this for BagRestSeconds.
    const float RestSpeed = 40f, BagRestSeconds = 0.25f;
    // Pause between closing the container and the order counting as done, so the finish can be seen.
    const float ResultDelay = 0.9f;

    // Pouch heat seal: seconds of holding to heat the gauge from cold to full (full scorches it and lets go).
    // The green band is in Scoring (SealGreenLow, SealGreenHigh).
    const float HeatSeconds = 1.5f;

    // Jar lid notch: clicks on the mark that screw it shut, the notch's starting speed (degrees a second) and how
    // much faster it goes after each good click, and how many degrees either side of the mark count as on it.
    const int NotchTurns = 3;
    const float NotchSpeed = 230f, NotchSpeedUp = 1.22f, NotchWindow = 17f;
    // The notch dial's radius, and how far above the lid its centre sits.
    const float DialRadius = 44f, DialAbove = 92f;

    enum Phase { Idle, Work, Close, Done }
    // Where the bag is while the station waits for it to go in: in the IN tray (no container on the mat yet),
    // being hoisted onto the rail, sliding along it, or falling.
    enum BagState { Tray, Hoist, Swing, Fall }

    readonly Effects fx;
    readonly List<Segment> staticWalls = new();
    readonly List<Segment> walls = new();

    Phase phase;
    BagState bagState;
    bool carrying;
    Container wanted;
    Container? onMat;
    // The strain in the bag (for its colours).
    Strain strain = Strains.All[0];
    // The nugs sealed in the bag at the weigh station (null in a staged shot with no weighing).
    BagFill? fill;
    Container carried;
    Vector2 carryPos;

    Vector2 bagPos, bagVel, bagSize, hoistFrom;
    float bagRest, hoistTime, swingPhase;
    bool bagSettled;
    // A missed drop ignores the container's walls until it is clear of them, so it falls off the rim to the table.
    bool ghost;

    // heat: the sealer's heat (0-1) while held; sealHeat: where it was let go (-1 = not sealed yet).
    float heat, sealHeat = -1;
    bool heating;
    // notch: the lid notch's angle (degrees, 0 = the mark at the top, clockwise); lidTwist eases toward the turns
    // made, to slide the lid's ridges.
    float notch, notchSpeed, lidTwist, lidPop, notchFlash;
    int turns;

    float seconds, clock, doneTime, messageTime;
    // started: the clock runs. frozen: a staged screenshot, held still.
    bool started, frozen;
    int crossThreads, drops, restarts;
    string message = "";
    PackResult? scored, shipped;

    public PackStation(Random rng)
    {
        fx = new Effects(rng);
        BuildStaticWalls();
        Clear();
    }

    // True when the station has no ticket and can take the next one.
    public bool IsIdle => phase == Phase.Idle;

    // The inside of a container standing on the mat, or on its shelf spot.
    static Rectangle InsideAt(Container c, float centerX, float bottom)
    {
        var s = Containers.Inside(c.Large);
        return new Rectangle(centerX - s.X / 2, bottom - s.Y, s.X, s.Y);
    }

    static Rectangle MatInside(Container c) => InsideAt(c, MatX, FloorY);

    static Rectangle ShelfInside(Container c) =>
        InsideAt(c, c.Kind == ContainerKind.Pouch ? PouchShelfX : JarShelfX, c.Large ? FloorY : ShelfY);

    static readonly Container[] Stock =
    {
        new(ContainerKind.Pouch, false), new(ContainerKind.Jar, false),
        new(ContainerKind.Pouch, true), new(ContainerKind.Jar, true),
    };

    Rectangle BagRect => new(bagPos.X, bagPos.Y, bagSize.X, bagSize.Y);
    Vector2 BagHome => new(TrayRect.X + TrayRect.Width / 2 - bagSize.X / 2, FloorY - bagSize.Y);
    // Where the bag hangs from the rail at a point of its slide.
    Vector2 HangAt(float phaseAngle) =>
        new(MatX + SwingReach * MathF.Sin(phaseAngle) - bagSize.X / 2, HangBottom - bagSize.Y);

    // The lid's centre, the seal strip's height and the lid's width, on the container on the mat.
    Vector2 LidCenter => onMat is { } c ? new Vector2(MatX, MatInside(c).Y - 10 - 18 * lidPop) : Vector2.Zero;
    float StripY => onMat is { } c ? MatInside(c).Y + 14 : 0;
    float LidWidth => onMat is { } c ? MatInside(c).Width + 16 : 0;
    Vector2 DialCenter => onMat is { } c ? new Vector2(MatX, MatInside(c).Y - 10 - DialAbove) : Vector2.Zero;
    // The heat gauge stands to the right of the pouch on the mat.
    Rectangle GaugeRect => onMat is { } c ? new Rectangle(MatX + MatInside(c).Width / 2 + 34, FloorY - 262, 56, 250) : default;

    // The table, screen edges and the IN tray's low sides as physics walls.
    void BuildStaticWalls()
    {
        void Add(float x1, float y1, float x2, float y2) => staticWalls.Add(new Segment(new(x1, y1), new(x2, y2)));
        Add(0, FloorY, Program.Width, FloorY);
        Add(0, TicketRail.Height, 0, FloorY);
        Add(Program.Width, TicketRail.Height, Program.Width, FloorY);
        Add(TrayRect.X, TrayRect.Y, TrayRect.X, FloorY);
        Add(TrayRect.X + TrayRect.Width, TrayRect.Y, TrayRect.X + TrayRect.Width, FloorY);
    }

    // Starts a ticket: the container the customer needs, and their sealed bag (with the nugs weighed into it) waiting
    // in the IN tray.
    public void Start(Customer c, BagFill? bagFill)
    {
        Reset();
        wanted = Containers.For(c);
        strain = c.Strain;
        fill = bagFill;
        bagSize = Bag.SizeFor(c.Grams) * Bag.SealedScale;
        bagPos = BagHome;
        bagState = BagState.Tray;
        phase = Phase.Work;
    }

    // Empties the station with no ticket.
    public void Clear()
    {
        Reset();
        phase = Phase.Idle;
    }

    void Reset()
    {
        carrying = false;
        onMat = null;
        seconds = 0;
        started = false;
        crossThreads = drops = restarts = 0;
        scored = shipped = null;
        messageTime = 0;
        ghost = false;
        fx.Clear();
        ResetClose();
    }

    // Puts the sealer and lid back to open.
    void ResetClose()
    {
        heat = 0;
        sealHeat = -1;
        heating = false;
        notch = 180f;
        notchSpeed = NotchSpeed;
        lidTwist = lidPop = notchFlash = 0;
        turns = 0;
    }

    // Live values for the F1 tuning panel: the container wanted and on the mat, time against par, and the drop,
    // seal or notch numbers.
    public List<TuningLine> TuningLines()
    {
        if (phase == Phase.Idle) return new() { new("No ticket on the station.") };
        var kind = onMat?.Kind ?? wanted.Kind;
        float par = Scoring.PackPar(kind);
        var lines = new List<TuningLine>
        {
            new($"Wants {wanted.Name}, on the mat: {onMat?.Name ?? "nothing"}", onMat is { } m && m != wanted),
            new($"Time {seconds:0.0} s, par {par:0.0} s (Start over adds {Scoring.RestartSeconds:0} s)", seconds > par),
        };
        if (phase == Phase.Work && onMat is { } c)
        {
            float slack = (MatInside(c).Width - bagSize.X) / 2;
            float off = bagPos.X + bagSize.X / 2 - MatX;
            lines.Add(new($"Drop window +-{MathF.Max(0, slack) + DropForgive:0} px, bag at {off:0} px",
                          MathF.Abs(off) > MathF.Max(0, slack) + DropForgive));
            lines.Add(new($"Slide {SwingReach:0} px each way, {SwingReach * SwingSpeed:0} px/s over the mouth"));
        }
        if (phase != Phase.Work && kind == ContainerKind.Pouch)
        {
            float h = sealHeat >= 0 ? sealHeat : heat;
            lines.Add(new($"Heat {h * 100:0}%, green {Scoring.SealGreenLow * 100:0}-{Scoring.SealGreenHigh * 100:0}%, full in {HeatSeconds:0.0} s"));
            lines.Add(new($"Seal if let go now: {Scoring.SealFor(h)}", Scoring.SealFor(h) < 80));
        }
        if (phase != Phase.Work && kind == ContainerKind.Jar)
        {
            lines.Add(new($"Lid {turns} of {NotchTurns} turns, notch {notchSpeed:0} deg/s, window +-{NotchWindow:0} deg"));
            lines.Add(new($"Notch {AngleToMark(notch):0} deg from the mark", MathF.Abs(AngleToMark(notch)) > NotchWindow));
        }
        lines.Add(new($"Notch misses {crossThreads}, drops {drops}, restarts {restarts}", crossThreads + drops > 0));
        return lines;
    }

    // Hands over the packed order's result once.
    public PackResult? TakeShipped()
    {
        var done = shipped;
        shipped = null;
        return done;
    }

    // Drops whatever the mouse is holding (the player switched screens): a carried container goes back on the
    // shelf, a held sealer seals where its heat is. Pausing (pausing = true) cools a held sealer instead, at no cost.
    public void LetGo(bool pausing = false)
    {
        carrying = false;
        if (heating && pausing) { heating = false; heat = 0; }
        else if (heating) Seal();
    }

    // Sets up the station mid-way, for the layout checks: --shot-pack and --shot-pack-jar have the right container
    // on the mat with the bag in it, the sealer heating into the green or the lid one turn in with its notch coming
    // up on the mark; --shot-drop (drop = true) has the bag sliding over the open container, nearly over its mouth.
    public void StageForScreenshot(bool drop = false)
    {
        frozen = true;
        onMat = wanted;
        started = true;
        seconds = 3.4f;
        if (drop)
        {
            swingPhase = -0.35f;
            bagPos = HangAt(swingPhase);
            bagState = BagState.Swing;
            return;
        }
        bagPos = new Vector2(MatX - bagSize.X / 2, FloorY - bagSize.Y);
        bagSettled = true;
        phase = Phase.Close;
        heat = 0.64f;
        heating = true;
        turns = 1;
        lidTwist = 1;
        notch = 335f;
        notchSpeed = NotchSpeed * NotchSpeedUp;
    }

    // Handles input and physics for one frame. Without input (another screen is showing) a falling bag still falls,
    // a sliding one keeps sliding, and a notch keeps going round.
    public void Update(float dt, bool input)
    {
        if (frozen) return;
        dt = MathF.Min(dt, 1f / 30f);
        var mouse = Raylib.GetMousePosition();
        // Clicks count only on the station itself, between the ticket rail and the tab bar (Space works anywhere).
        bool inPlay = mouse.Y > TicketRail.Height && mouse.Y < FloorY + 8;
        bool press = input && ((Raylib.IsMouseButtonPressed(MouseButton.Left) && inPlay) || Raylib.IsKeyPressed(KeyboardKey.Space));
        bool release = input && Raylib.IsMouseButtonReleased(MouseButton.Left);
        bool down = input && (Raylib.IsMouseButtonDown(MouseButton.Left) || Raylib.IsKeyDown(KeyboardKey.Space));
        clock += dt;
        if (messageTime > 0) messageTime -= dt;
        lidPop = MathF.Max(0, lidPop - dt * 3f);
        notchFlash = MathF.Max(0, notchFlash - dt * 3f);
        lidTwist += (turns - lidTwist) * MathF.Min(1f, dt * 14f);
        fx.Update(dt);

        if (phase == Phase.Done)
        {
            doneTime += dt;
            if (scored != null && doneTime >= ResultDelay) { shipped = scored; scored = null; }
            return;
        }
        if (phase == Phase.Idle) return;

        if (started) seconds += dt;
        bool onRestart = Raylib.CheckCollisionPointRec(mouse, RestartButton);
        if (press && onRestart && Raylib.IsMouseButtonPressed(MouseButton.Left) && CanRestart) { Restart(); return; }

        if (phase == Phase.Work) UpdateWork(mouse, press && !onRestart, release, dt);
        else if (onMat?.Kind == ContainerKind.Pouch) UpdateSeal(press && !onRestart, down, dt);
        else UpdateNotch(press && !onRestart, dt);
    }

    // Start over is offered once a container is on the mat.
    bool CanRestart => (phase == Phase.Work || phase == Phase.Close) && onMat != null;

    // Getting the bag in: carry containers from the shelf to the mat (one on the mat hoists the bag onto the rail),
    // and a click while the bag slides drops it.
    void UpdateWork(Vector2 mouse, bool press, bool release, float dt)
    {
        if (carrying) UpdateCarry(mouse, release);
        else if (press && bagState != BagState.Fall && ShelfAt(mouse) is { } s)
        {
            carried = s;
            carrying = true;
            carryPos = mouse;
            started = true;
            Audio.Play(Audio.Pluck, 0.6f, 0.9f);
        }
        else if (press && bagState == BagState.Swing) Drop();

        switch (bagState)
        {
            case BagState.Hoist:
                hoistTime += dt;
                float t = Math.Clamp(hoistTime / HoistSeconds, 0f, 1f);
                bagPos = Vector2.Lerp(hoistFrom, HangAt(-MathF.PI / 2), t * t * (3f - 2f * t));
                if (t >= 1f) { bagState = BagState.Swing; swingPhase = -MathF.PI / 2; }
                break;
            case BagState.Swing:
                swingPhase += SwingSpeed * dt;
                bagPos = HangAt(swingPhase);
                break;
            case BagState.Fall:
                UpdateFall(dt);
                break;
        }
    }

    // The shelf container under the mouse, if any.
    static Container? ShelfAt(Vector2 mouse)
    {
        foreach (var s in Stock)
            if (Raylib.CheckCollisionPointRec(mouse, Grow(ShelfInside(s), 8))) return s;
        return null;
    }

    // Carries a container; let go over the mat it stands there (replacing any other), anywhere else it goes back.
    // The first container on the mat sends the bag up onto the rail.
    void UpdateCarry(Vector2 mouse, bool release)
    {
        carryPos = mouse;
        if (!release) return;
        carrying = false;
        if (!Raylib.CheckCollisionPointRec(mouse, MatZone)) return;
        onMat = carried;
        Audio.Play(Audio.Tick, 0.7f, 0.6f);
        if (bagState == BagState.Tray) Hoist();
    }

    // Lifts the bag from wherever it lies up onto the rail, to slide from its left end.
    void Hoist()
    {
        hoistFrom = bagPos;
        hoistTime = 0;
        bagVel = Vector2.Zero;
        ghost = false;
        bagState = BagState.Hoist;
        Audio.Play(Audio.Crinkle, 0.4f, 1.2f);
    }

    // Lets go of the sliding bag. Close enough over the mouth it slides in off the rim (a 7 g bag over a small
    // container lands on the rim instead); otherwise it is knocked off the rim and falls to the table.
    void Drop()
    {
        var inside = MatInside(onMat!.Value);
        float off = bagPos.X + bagSize.X / 2 - MatX;
        float slack = (inside.Width - bagSize.X) / 2;
        started = true;
        bagVel = Vector2.Zero;
        ghost = false;
        if (MathF.Abs(off) <= MathF.Abs(slack) + DropForgive)
        {
            // In the window: a bag that fits is nudged clear of the walls, one too big is centred on the rim.
            float x = slack >= 0 ? Math.Clamp(off, -slack, slack) : 0f;
            bagPos.X = MatX + x - bagSize.X / 2;
        }
        else
        {
            float dir = MathF.Sign(off);
            bool overlaps = bagPos.X < inside.X + inside.Width + 4 && bagPos.X + bagSize.X > inside.X - 4;
            ghost = overlaps;
            bagVel = new Vector2(overlaps ? dir * MissKick : 0f, overlaps ? -120f : 0f);
        }
        bagRest = 0;
        bagSettled = false;
        bagState = BagState.Fall;
        Audio.Play(Audio.Pluck, 0.7f, 0.8f);
    }

    // Lets the dropped bag fall and land. Once it sits still, where it came to rest decides what happens: inside the
    // container it is locked in and closing starts; on the rim it is too big; on the table it counts as dropped.
    // Either miss hoists it back onto the rail.
    void UpdateFall(float dt)
    {
        var inside = MatInside(onMat!.Value);
        if (ghost && (bagPos.X > inside.X + inside.Width + 4 || bagPos.X + bagSize.X < inside.X - 4)) ghost = false;
        walls.Clear();
        walls.AddRange(staticWalls);
        if (!ghost)
        {
            walls.Add(new Segment(new(inside.X, inside.Y), new(inside.X, FloorY)));
            walls.Add(new Segment(new(inside.X + inside.Width, inside.Y), new(inside.X + inside.Width, FloorY)));
        }

        float hit = Physics.StepBox(ref bagPos, ref bagVel, bagSize, walls, dt);
        if (hit > 150f) Audio.Play(Audio.Thump, Math.Clamp(hit / 1500f, 0.2f, 1f));

        if (bagSettled) return;
        if (bagVel.Length() >= RestSpeed) { bagRest = 0; return; }
        bagRest += dt;
        if (bagRest < BagRestSeconds) return;
        bagSettled = true;

        bool within = bagPos.X >= inside.X - 1 && bagPos.X + bagSize.X <= inside.X + inside.Width + 1;
        if (within && bagPos.Y + bagSize.Y >= FloorY - 2) { LockIn(onMat!.Value); return; }
        bool onRim = bagPos.Y + bagSize.Y <= inside.Y + 4 && bagPos.X < inside.X + inside.Width && bagPos.X + bagSize.X > inside.X;
        if (onRim) Say("Too big for this one");
        else
        {
            drops++;
            Say("Missed: dropped on the table");
        }
        Hoist();
    }

    // The bag is in: it stays there, and the container is ready to close.
    void LockIn(Container c)
    {
        phase = Phase.Close;
        ResetClose();
        Audio.Play(Audio.Crinkle, 0.6f);
        var at = new Vector2(MatX, MatInside(c).Y - 170);
        fx.Say(c.Kind == ContainerKind.Jar ? "Click when the notch hits the mark" : "Hold to heat, let go in the green", at, Art.Text, 24);
    }

    // The pouch's heat seal: holding heats the sealer up the gauge; letting go seals at that heat. A full gauge
    // scorches it and seals by itself.
    void UpdateSeal(bool press, bool down, float dt)
    {
        if (!heating)
        {
            if (!press) return;
            heating = true;
            heat = 0;
            started = true;
            Audio.Play(Audio.Tick, 0.5f, 0.7f);
            return;
        }
        heat = MathF.Min(1f, heat + dt / HeatSeconds);
        if (!down || heat >= 1f) Seal();
    }

    // Seals the pouch at the heat reached, and closes it with a word on how it went.
    void Seal()
    {
        heating = false;
        sealHeat = heat;
        int seal = Scoring.SealFor(heat);
        var strip = new Vector2(MatX, StripY);
        if (heat > Scoring.SealGreenHigh)
            fx.Puff(strip, 14, 90f, new[] { new Color(60, 56, 60, 255), new Color(110, 104, 110, 255) });
        string pop = heat < Scoring.SealGreenLow ? "Gappy seal" : heat > Scoring.SealGreenHigh ? "Scorched!" : seal >= 95 ? "Perfect seal!" : "Sealed!";
        Close(pop, seal, seal >= 80);
    }

    // The jar lid: the notch goes round; a click with it on the mark turns the lid one step (and the notch speeds
    // up), a click off the mark cross-threads (costs handling, no turn).
    void UpdateNotch(bool press, float dt)
    {
        notch = (notch + notchSpeed * dt) % 360f;
        if (!press) return;
        started = true;
        if (MathF.Abs(AngleToMark(notch)) <= NotchWindow)
        {
            turns++;
            notchFlash = 1f;
            notchSpeed *= NotchSpeedUp;
            Audio.Play(Audio.Tick, 0.8f, 0.9f + 0.12f * turns);
            if (turns >= NotchTurns) Close(crossThreads == 0 ? "Lid on tight!" : "Lid on", 100, crossThreads == 0);
            return;
        }
        crossThreads++;
        lidPop = 1f;
        Say("Cross-threaded: wait for the mark");
        Audio.Play(Audio.Slip, 0.8f);
    }

    // Degrees from the mark (top of the dial) to a notch angle, -180 to 180.
    static float AngleToMark(float a) => ((a + 180f) % 360f + 360f) % 360f - 180f;

    // The container is closed: score it and hold the result for a moment so the finish can be seen.
    void Close(string pop, int seal, bool good)
    {
        Audio.Play(Audio.Seal, 0.8f);
        var c = onMat!.Value;
        bool right = c == wanted;
        var at = new Vector2(MatX, MatInside(c).Y - 80);
        fx.Say(right ? pop : $"{pop} (they wanted a {wanted.Name})", at, right && good ? Art.Accent : right ? Art.Text : Art.Warn,
               right ? 28 : 22);
        scored = Scoring.ScorePack(wanted, c, seconds, crossThreads, drops, restarts, seal);
        doneTime = 0;
        phase = Phase.Done;
    }

    // Start over: the container goes back, the bag goes back in the tray, the lid or sealer opens, and the clock
    // gets the time cost.
    void Restart()
    {
        restarts++;
        seconds += Scoring.RestartSeconds;
        onMat = null;
        carrying = false;
        bagPos = BagHome;
        bagVel = Vector2.Zero;
        bagState = BagState.Tray;
        ghost = false;
        phase = Phase.Work;
        ResetClose();
        Say($"Start over: +{Scoring.RestartSeconds:0} s");
        Audio.Play(Audio.Pluck, 0.7f, 0.7f);
    }

    // Shows a short message above the mat.
    void Say(string text)
    {
        message = text;
        messageTime = 1.8f;
    }

    // Draws the station. The ticket holder glows while a ticket is dragged toward an idle station.
    public void Draw(Customer? customer, int ticketNumber, bool dropGlow)
    {
        var mouse = Raylib.GetMousePosition();
        Art.DrawTable(FloorY);
        Art.DrawTicket(TicketRect, customer, ticketNumber, dropGlow && phase == Phase.Idle);

        Art.DrawShelf(PouchShelfX - 110, JarShelfX + 110, ShelfY, FloorY);
        bool picking = phase == Phase.Work && !carrying && bagState != BagState.Fall;
        foreach (var s in Stock)
        {
            var r = ShelfInside(s);
            Art.DrawContainerBack(r, s);
            Art.DrawContainerFront(r, s, picking && Raylib.CheckCollisionPointRec(mouse, Grow(r, 8)), false);
        }

        Art.DrawMat(MatX, FloorY, carrying && Raylib.CheckCollisionPointRec(mouse, MatZone), onMat == null);
        Art.DrawTrayBack(TrayRect);

        // The rail and the cord the bag hangs from while it is hoisted or sliding.
        bool hanging = phase == Phase.Work && (bagState == BagState.Hoist || bagState == BagState.Swing);
        if (phase == Phase.Work && onMat != null)
            Art.DrawDropRail(MatX - SwingReach - 70, MatX + SwingReach + 70, RailY,
                             hanging ? new Vector2(bagPos.X + bagSize.X / 2, bagPos.Y) : null);

        // The bag is drawn inside the container (between its back and front).
        bool showBag = phase != Phase.Idle;
        if (onMat is { } c)
        {
            var inside = MatInside(c);
            Art.DrawContainerBack(inside, c);
            if (showBag) Art.DrawSealedBag(BagRect, hanging, strain, fill);
            bool closing = phase == Phase.Close || phase == Phase.Done;
            bool closed = phase == Phase.Done;
            Art.DrawContainerFront(inside, c, false, closed);
            if (closing && c.Kind == ContainerKind.Jar)
            {
                var jitter = new Vector2(MathF.Sin(clock * 90f), MathF.Cos(clock * 77f)) * lidPop * 4f;
                Art.DrawLid(LidCenter + jitter, LidWidth, lidTwist * 30f, lidTwist / NotchTurns, notchFlash > 0, lidPop, 0);
                if (!closed) Art.DrawNotchDial(DialCenter, DialRadius, notch, NotchWindow, turns, NotchTurns, notchFlash);
            }
            if (closing && c.Kind == ContainerKind.Pouch)
            {
                Art.DrawHeatSeal(inside.X, inside.X + inside.Width, StripY, sealHeat >= 0 ? sealHeat : heat, heating, sealHeat >= 0);
                if (!closed || sealHeat >= 0)
                    Art.DrawHeatGauge(GaugeRect, sealHeat >= 0 ? sealHeat : heat, Scoring.SealGreenLow, Scoring.SealGreenHigh, heating);
            }
        }
        else if (showBag) Art.DrawSealedBag(BagRect, false, strain, fill);

        Art.DrawTrayFront(TrayRect);
        if (carrying)
        {
            var size = Containers.Inside(carried.Large);
            var r = new Rectangle(carryPos.X - size.X / 2, carryPos.Y - size.Y / 2, size.X, size.Y);
            Art.DrawContainerBack(r, carried);
            Art.DrawContainerFront(r, carried, true, false);
        }

        if (CanRestart)
            Art.DrawButton(RestartButton, $"Start over +{Scoring.RestartSeconds:0}s", Raylib.CheckCollisionPointRec(mouse, RestartButton), true);

        fx.Draw();
        DrawHud();
    }

    // Draws the hint for the current step, the timer and counts, and any short message.
    void DrawHud()
    {
        string hint = phase switch
        {
            Phase.Idle => "No ticket on the station. Drag a weighed ticket down from the rail.",
            Phase.Work when onMat == null => "Pick the container off the shelf and drag it onto the mat.\nSmall holds 1 g and 3.5 g, large holds 7 g.",
            Phase.Work => "Click to drop the bag as it passes over the container.\nWrong container? Drag another one onto the mat.",
            Phase.Close when onMat?.Kind == ContainerKind.Jar => "Click when the notch reaches the mark: three turns.\nA click off the mark cross-threads the lid.",
            Phase.Close => "Hold the mouse to heat the sealer, let go in the green.\nToo short leaves gaps, too long scorches.",
            _ => "",
        };
        Art.Label(hint, 420, 150, 22, Art.Faint);
        if (phase != Phase.Idle)
            Art.Label($"Time {seconds:0.0} s    Cross-threads {crossThreads}    Drops {drops}", 420, 214, 20, Art.Faint);
        if (messageTime > 0) Art.LabelCentered(message, MatX, 262, 26, Art.Warn);
    }

    static Rectangle Grow(Rectangle r, float by) => new(r.X - by, r.Y - by, r.Width + 2 * by, r.Height + 2 * by);
}
