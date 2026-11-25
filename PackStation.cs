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

// The pack station: drag a container from the shelf onto the mat, drag or throw the sealed bag in from the IN
// tray, then close it: turn a jar's lid shut in sideways strokes, or press a pouch's seal strip along its length.
// Once the bag sits inside it is locked in; Start over resets the station at a time cost. The shop hands it each
// ticket with Start and collects the result with TakeShipped.
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

    // The bag counts as settled once it has moved slower than this for BagRestSeconds.
    const float RestSpeed = 40f, BagRestSeconds = 0.25f;
    const float MaxToss = 1800f;
    // Pause between closing the container and the order counting as done, so the finish can be seen.
    const float ResultDelay = 0.9f;

    // Jar lid, turned in sideways strokes: grab, drag right, let go, grab again (a wrist turning a lid). One stroke
    // counts at most StrokeShare of the lid's width; StrokesToClose of those screw it shut. A stroke faster than
    // MaxStrokeSpeed (pixels a second, smoothed) cross-threads it.
    const float StrokeShare = 0.85f;
    const float StrokesToClose = 2.6f;
    const float MaxStrokeSpeed = 1200f;
    // The lid clicks this many times per stroke.
    const int ClicksPerStroke = 4;
    // How close to cross-threading (0-1) the lid starts to shake.
    const float TwistShakeFrom = 0.6f;

    // Pouch seal strip: segments, seconds of pressing each needs, and how close to the strip the mouse must be.
    const int StripSegments = 8;
    const float SealHold = 0.12f, StripReach = 22f;

    enum Phase { Idle, Work, Close, Done }
    enum Drag { None, Container, Bag, Lid, Strip }

    readonly Effects fx;
    readonly List<Segment> staticWalls = new();
    readonly List<Segment> walls = new();

    Phase phase;
    Drag drag;
    Container wanted;
    Container? onMat;
    // The strain in the bag (for its colours).
    Strain strain = Strains.All[0];
    Container carried;
    Vector2 carryPos;

    Vector2 bagPos, bagVel, bagSize, grabOffset, lastMouse, mouseVel;
    float bagRest;
    bool bagSettled;

    // twist: how far the lid has turned, in pixels of mouse travel. stroke: how much of the current stroke is used.
    float seconds, twist, twistRate, stroke, lidX, twistDanger, lidPop, clock, doneTime, messageTime;
    bool started;
    int crossThreads, drops, restarts, clicks;
    readonly float[] heat = new float[StripSegments];
    readonly bool[] sealedSeg = new bool[StripSegments];
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

    // The lid's centre and the seal strip's height, on the container on the mat.
    Vector2 LidCenter => onMat is { } c ? new Vector2(MatX, MatInside(c).Y - 10 - (lidPop > 0 ? 18 * lidPop : 0)) : Vector2.Zero;
    float StripY => onMat is { } c ? MatInside(c).Y + 14 : 0;

    // The lid's width, the most one stroke can turn it, and the turning that closes it (all in pixels).
    float LidWidth => onMat is { } c ? MatInside(c).Width + 16 : 0;
    float StrokeLength => LidWidth * StrokeShare;
    float TwistGoal => StrokeLength * StrokesToClose;

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

    // Starts a ticket: the container the customer needs, and their sealed bag waiting in the IN tray.
    public void Start(Customer c)
    {
        Reset();
        wanted = Containers.For(c);
        strain = c.Strain;
        bagSize = Bag.SizeFor(c.Grams) * Bag.SealedScale;
        bagPos = BagHome;
        bagSettled = true;
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
        drag = Drag.None;
        onMat = null;
        seconds = 0;
        started = false;
        crossThreads = drops = restarts = 0;
        scored = shipped = null;
        messageTime = 0;
        fx.Clear();
        ResetClose();
    }

    // Puts the lid and seal strip back to open.
    void ResetClose()
    {
        twist = twistRate = stroke = twistDanger = lidPop = 0;
        clicks = 0;
        Array.Clear(heat);
        Array.Clear(sealedSeg);
    }

    // Live values for the F1 tuning panel: the container wanted and on the mat, time against par, and how the lid
    // or strip is going.
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
        if (kind == ContainerKind.Jar)
        {
            float strokes = StrokeLength > 0 ? twist / StrokeLength : 0;
            lines.Add(new($"Lid {strokes:0.0} of {StrokesToClose:0.0} strokes, this stroke {stroke * 100:0}% used"));
            lines.Add(new($"Stroke speed {twistRate:0} px/s, cross-threads at {MaxStrokeSpeed:0}", twistDanger >= TwistShakeFrom));
        }
        else
        {
            lines.Add(new($"Strip {sealedSeg.Count(s => s)} of {StripSegments} sealed, {SealHold:0.00} s each"));
            lines.Add(new($"Reach {StripReach:0} px from the strip"));
        }
        lines.Add(new($"Cross-threads {crossThreads}, drops {drops}, restarts {restarts}", crossThreads + drops > 0));
        return lines;
    }

    // Hands over the packed order's result once.
    public PackResult? TakeShipped()
    {
        var done = shipped;
        shipped = null;
        return done;
    }

    // Drops whatever the mouse is holding (the player switched screens): a held bag falls, a carried container
    // goes back on the shelf, a lid or strip just stops.
    public void LetGo()
    {
        if (drag == Drag.Bag) { bagVel = Vector2.Zero; bagSettled = false; bagRest = 0; }
        drag = Drag.None;
    }

    // Sets up the station half way through closing, for the --shot-pack layout check: the right container on the
    // mat with the bag in it, the lid half turned or the strip half sealed.
    public void StageForScreenshot()
    {
        onMat = wanted;
        bagPos = new Vector2(MatX - bagSize.X / 2, FloorY - bagSize.Y);
        bagSettled = true;
        started = true;
        seconds = 4.2f;
        phase = Phase.Close;
        twist = TwistGoal * 0.45f;
        for (int i = 0; i < StripSegments / 2; i++) sealedSeg[i] = true;
        heat[StripSegments / 2] = SealHold * 0.5f;
    }

    // Handles input and physics for one frame. Without input (another screen is showing) the bag still falls.
    public void Update(float dt, bool input)
    {
        dt = MathF.Min(dt, 1f / 30f);
        var mouse = Raylib.GetMousePosition();
        bool press = input && Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool release = input && Raylib.IsMouseButtonReleased(MouseButton.Left);
        bool down = input && Raylib.IsMouseButtonDown(MouseButton.Left);
        if (dt > 0) mouseVel = Vector2.Lerp(mouseVel, (mouse - lastMouse) / dt, 0.5f);
        lastMouse = mouse;
        clock += dt;
        if (messageTime > 0) messageTime -= dt;
        lidPop = MathF.Max(0, lidPop - dt * 3f);
        if (drag != Drag.Lid) twistDanger = MathF.Max(0, twistDanger - dt * 4f);
        fx.Update(dt);

        if (phase == Phase.Done)
        {
            doneTime += dt;
            if (scored != null && doneTime >= ResultDelay) { shipped = scored; scored = null; }
            return;
        }
        if (phase == Phase.Idle) return;

        if (started) seconds += dt;

        if (press && Raylib.CheckCollisionPointRec(mouse, RestartButton) && CanRestart) { Restart(); return; }
        if (press && drag == Drag.None) BeginDrag(mouse);

        switch (drag)
        {
            case Drag.Container: UpdateCarry(mouse, release); break;
            case Drag.Bag: UpdateHeldBag(mouse, release); break;
            case Drag.Lid: UpdateTwist(mouse, release, dt); break;
            case Drag.Strip: UpdateStrip(mouse, down, dt); break;
        }

        if (drag != Drag.Bag && phase == Phase.Work) UpdateFlyingBag(dt);
    }

    // Start over is offered once anything has moved: a container on the mat or the bag out of the tray.
    bool CanRestart => (phase == Phase.Work || phase == Phase.Close) && (onMat != null || !BagInTray);

    bool BagInTray => bagPos.X >= TrayRect.X - 1 && bagPos.X + bagSize.X <= TrayRect.X + TrayRect.Width + 1
                      && bagPos.Y + bagSize.Y >= FloorY - 2;

    // Works out what a fresh press picks up: the lid or strip while closing; otherwise the bag, the container on
    // the mat, or one from the shelf.
    void BeginDrag(Vector2 mouse)
    {
        if (phase == Phase.Close && onMat is { } c)
        {
            var inside = MatInside(c);
            if (c.Kind == ContainerKind.Jar)
            {
                var lid = new Rectangle(inside.X - 20, LidCenter.Y - 50, inside.Width + 40, 100);
                if (Raylib.CheckCollisionPointRec(mouse, lid))
                {
                    drag = Drag.Lid;
                    stroke = twistRate = 0;
                    lidX = mouse.X;
                    started = true;
                    Audio.Play(Audio.Pluck, 0.5f, 1.2f);
                }
            }
            else if (mouse.X > inside.X - 10 && mouse.X < inside.X + inside.Width + 10 && MathF.Abs(mouse.Y - StripY) <= StripReach)
            {
                drag = Drag.Strip;
                started = true;
            }
            return;
        }

        if (Raylib.CheckCollisionPointRec(mouse, BagRect))
        {
            drag = Drag.Bag;
            grabOffset = mouse - bagPos;
            bagSettled = false;
            started = true;
            Audio.Play(Audio.Pluck, 0.7f, 0.8f);
            return;
        }

        if (onMat is { } m && Raylib.CheckCollisionPointRec(mouse, MatInside(m)))
        {
            carried = m;
            onMat = null;
            drag = Drag.Container;
            carryPos = mouse;
            started = true;
            Audio.Play(Audio.Pluck, 0.6f, 0.9f);
            return;
        }

        foreach (var s in Stock)
        {
            if (!Raylib.CheckCollisionPointRec(mouse, Grow(ShelfInside(s), 8))) continue;
            carried = s;
            drag = Drag.Container;
            carryPos = mouse;
            started = true;
            Audio.Play(Audio.Pluck, 0.6f, 0.9f);
            return;
        }
    }

    // Carries a container; let go over the mat it stands there (replacing any other), anywhere else it goes back.
    void UpdateCarry(Vector2 mouse, bool release)
    {
        carryPos = mouse;
        if (!release) return;
        drag = Drag.None;
        if (!Raylib.CheckCollisionPointRec(mouse, MatZone)) return;
        if (Raylib.CheckCollisionRecs(BagRect, Grow(MatInside(carried), 10)))
        {
            Say("Move the bag off the mat first");
            return;
        }
        onMat = carried;
        Audio.Play(Audio.Tick, 0.7f, 0.6f);
    }

    // Moves the held bag with the mouse; letting go throws it with the mouse's speed.
    void UpdateHeldBag(Vector2 mouse, bool release)
    {
        bagPos = mouse - grabOffset;
        bagVel = ClampLength(mouseVel, MaxToss);
        if (!release) return;
        drag = Drag.None;
        bagRest = 0;
    }

    // Lets the bag fly and land. Once it sits still, where it came to rest decides what happens: inside the
    // container it is locked in and closing starts; on the rim it is too big; on the table it counts as dropped.
    void UpdateFlyingBag(float dt)
    {
        walls.Clear();
        walls.AddRange(staticWalls);
        if (onMat is { } c)
        {
            var inside = MatInside(c);
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

        if (onMat is { } m)
        {
            var inside = MatInside(m);
            bool within = bagPos.X >= inside.X - 1 && bagPos.X + bagSize.X <= inside.X + inside.Width + 1;
            if (within && bagPos.Y + bagSize.Y >= FloorY - 2) { LockIn(m); return; }
            bool onRim = bagPos.Y + bagSize.Y <= inside.Y + 4 && bagPos.X < inside.X + inside.Width && bagPos.X + bagSize.X > inside.X;
            if (onRim) { Say("Too big for this one"); return; }
        }
        if (BagInTray) return;
        drops++;
        Say("Dropped it on the table");
    }

    // The bag is in: it stays there, and the container is ready to close.
    void LockIn(Container c)
    {
        phase = Phase.Close;
        ResetClose();
        Audio.Play(Audio.Crinkle, 0.6f);
        var at = new Vector2(MatX, MatInside(c).Y - 70);
        fx.Say(c.Kind == ContainerKind.Jar ? "Turn the lid shut" : "Press the strip to seal", at, Art.Text, 24);
    }

    // Turns the lid with one sideways stroke of the held mouse: moving right turns it, moving left does nothing.
    // A stroke runs out after StrokeLength, like a wrist at the end of its turn: let go and grab again. Too fast a
    // stroke cross-threads it. The lid clicks as it goes.
    void UpdateTwist(Vector2 mouse, bool release, float dt)
    {
        if (release) { drag = Drag.None; twistRate = 0; return; }
        float dx = mouse.X - lidX;
        lidX = mouse.X;
        float room = StrokeLength - stroke;
        float turn = Math.Clamp(dx, 0f, MathF.Max(0f, room));

        twistRate += ((dt > 0 ? turn / dt : 0) - twistRate) * 0.35f;
        twistDanger = twistRate / MaxStrokeSpeed;
        if (twistRate > MaxStrokeSpeed) { CrossThread(); return; }
        if (turn <= 0) return;

        twist += turn;
        stroke += turn;
        int click = (int)(twist / (StrokeLength / ClicksPerStroke));
        if (click > clicks)
        {
            clicks = click;
            Audio.Play(Audio.Tick, 0.6f, 1.0f + 0.06f * click);
        }
        if (twist >= TwistGoal) { Close("Lid on tight!"); return; }
        if (stroke >= StrokeLength)
            fx.Say("Let go, grab again", LidCenter + new Vector2(0, -44), Art.Text, 20);
    }

    // The lid caught its thread wrong: it pops loose, all turning is lost, and it costs handling.
    void CrossThread()
    {
        crossThreads++;
        twist = twistRate = stroke = twistDanger = 0;
        lidPop = 1f;
        drag = Drag.None;
        Say("Cross-threaded: smoother strokes");
        Audio.Play(Audio.Slip, 0.8f);
    }

    // Heats the strip segment under the held mouse; one held there long enough seals. Sweeping too fast leaves
    // gaps to go back over. Unsealed heat cools off slowly.
    void UpdateStrip(Vector2 mouse, bool down, float dt)
    {
        if (!down) { drag = Drag.None; return; }
        var inside = MatInside(onMat!.Value);
        int under = -1;
        if (MathF.Abs(mouse.Y - StripY) <= StripReach && mouse.X >= inside.X && mouse.X < inside.X + inside.Width)
            under = (int)((mouse.X - inside.X) / inside.Width * StripSegments);

        for (int i = 0; i < StripSegments; i++)
        {
            if (sealedSeg[i]) continue;
            if (i != under) { heat[i] = MathF.Max(0, heat[i] - dt * 0.3f); continue; }
            heat[i] += dt;
            if (heat[i] < SealHold) continue;
            sealedSeg[i] = true;
            int count = sealedSeg.Count(s => s);
            Audio.Play(Audio.ZipTooth, 0.5f, 0.8f + 0.06f * count);
        }
        if (sealedSeg.All(s => s)) Close("Sealed!");
    }

    // The container is closed: score it and hold the result for a moment so the finish can be seen.
    void Close(string pop)
    {
        drag = Drag.None;
        Audio.Play(Audio.Seal, 0.8f);
        var c = onMat!.Value;
        bool right = c == wanted;
        var at = new Vector2(MatX, MatInside(c).Y - 80);
        fx.Say(right ? pop : $"{pop} (they wanted a {wanted.Name})", at, right ? Art.Accent : Art.Warn, right ? 28 : 22);
        scored = Scoring.ScorePack(wanted, c, seconds, crossThreads, drops, restarts);
        doneTime = 0;
        phase = Phase.Done;
    }

    // Start over: the container goes back, the bag goes back in the tray, the lid or strip opens, and the clock
    // gets the time cost.
    void Restart()
    {
        restarts++;
        seconds += Scoring.RestartSeconds;
        onMat = null;
        drag = Drag.None;
        bagPos = BagHome;
        bagVel = Vector2.Zero;
        bagSettled = true;
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
        bool picking = phase == Phase.Work && drag == Drag.None;
        foreach (var s in Stock)
        {
            var r = ShelfInside(s);
            Art.DrawContainerBack(r, s);
            Art.DrawContainerFront(r, s, picking && Raylib.CheckCollisionPointRec(mouse, Grow(r, 8)), false);
        }

        Art.DrawMat(MatX, FloorY, drag == Drag.Container && Raylib.CheckCollisionPointRec(mouse, MatZone), onMat == null);
        Art.DrawTrayBack(TrayRect);

        // The bag is drawn inside the container (between its back and front) unless it is in the hand.
        bool showBag = phase != Phase.Idle && drag != Drag.Bag;
        if (onMat is { } c)
        {
            var inside = MatInside(c);
            Art.DrawContainerBack(inside, c);
            if (showBag) Art.DrawSealedBag(BagRect, false, strain);
            float shake = Math.Clamp((twistDanger - TwistShakeFrom) / (1f - TwistShakeFrom), 0f, 1f);
            var jitter = new Vector2(MathF.Sin(clock * 90f), MathF.Cos(clock * 77f)) * shake * 4f;
            bool closing = phase == Phase.Close || phase == Phase.Done;
            bool closed = phase == Phase.Done;
            bool hot = phase == Phase.Close && (drag == Drag.Lid || drag == Drag.Strip);
            Art.DrawContainerFront(inside, c, false, closed);
            if (closing && c.Kind == ContainerKind.Jar)
            {
                // The arrow fades while the held stroke is used up, to say let go and grab again.
                float arrow = closed ? 0f : drag == Drag.Lid && stroke >= StrokeLength ? 0.25f : 1f;
                float done = closed ? 1f : twist / TwistGoal;
                Art.DrawLid(LidCenter + jitter, LidWidth, closed ? TwistGoal : twist, done, hot, shake, arrow);
            }
            if (closing && c.Kind == ContainerKind.Pouch)
                Art.DrawSealStrip(inside.X, inside.X + inside.Width, StripY, heat, sealedSeg, SealHold, hot);
        }
        else if (showBag) Art.DrawSealedBag(BagRect, false, strain);

        Art.DrawTrayFront(TrayRect);
        if (drag == Drag.Container)
        {
            var size = Containers.Inside(carried.Large);
            var r = new Rectangle(carryPos.X - size.X / 2, carryPos.Y - size.Y / 2, size.X, size.Y);
            Art.DrawContainerBack(r, carried);
            Art.DrawContainerFront(r, carried, true, false);
        }
        if (drag == Drag.Bag) Art.DrawSealedBag(BagRect, true, strain);

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
            Phase.Work => "Drag the bag from the IN tray into the container.",
            Phase.Close when onMat?.Kind == ContainerKind.Jar => "Grab the lid and drag it right, let go, grab again: about three strokes.\nA jerky, too-fast stroke cross-threads it.",
            Phase.Close => "Hold the mouse on the strip and sweep it slowly from end to end.\nToo fast leaves gaps.",
            _ => "",
        };
        Art.Label(hint, 420, 150, 22, Art.Faint);
        if (phase != Phase.Idle)
            Art.Label($"Time {seconds:0.0} s    Cross-threads {crossThreads}    Drops {drops}", 420, 214, 20, Art.Faint);
        if (messageTime > 0) Art.LabelCentered(message, MatX, 262, 26, Art.Warn);
    }

    static Rectangle Grow(Rectangle r, float by) => new(r.X - by, r.Y - by, r.Width + 2 * by, r.Height + 2 * by);

    static Vector2 ClampLength(Vector2 v, float max) =>
        v.LengthSquared() > max * max ? Vector2.Normalize(v) * max : v;
}
