using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The weigh station, set up like a deli counter: a jar per strain on the day's menu (SetMenu), a scale with a weigh
// tray on it, and an open bag standing ready on the counter. Pick nugs of the ordered strain out of its jar with the
// tongs and set them on the tray until the needle sits between the order weight and the weigh-heavy cap (never
// under). Then carry the tray over the bag and tip it in, zip the bag and throw it into the OUT bin. Nugs are never
// broken, but a hard drop chips a crumb off. Design and research: vision.md, "Weigh station redesign". The shop hands
// it each ticket with Start and collects the finished bag with TakeShipped.
sealed class WeighStation
{
    const int MinShake = 8;
    // The zip slips open if the mouse strays this far from the zip line, or moves faster than this.
    const float ZipSlipDistance = 24f;
    const float ZipMaxSpeed = 1500f;
    const float ZipGrabRadius = 22f;
    // A thrown sealed bag flies with the mouse's speed, up to this.
    const float MaxToss = 1800f;

    const float FloorY = 665;
    static readonly Rectangle TicketRect = new(50, 150, 330, 110);
    // The strain jars stand side by side from JarsLeft, one per strain on the menu.
    const float JarsLeft = 40, JarWidth = 170, JarGap = 20, JarTop = 330, JarBottom = 640;
    // Grams in each jar: enough for the order with plenty to spare, the same in every jar.
    const float FillPerGram = 1.3f, FillExtra = 2f;
    // Small buds and shake in every jar always add up to the order (or the strain's largest bud, if that is less)
    // plus the room plus this many grams, so the order can be made from small pieces alone, or from large buds
    // topped up with small ones. Without it a 1 g order from a chunky strain could leave only big nugs.
    const float SmallSpare = 0.5f;
    // Nugs are never broken, so each jar is also checked for some set of whole pieces that lands between the order
    // weight and the cap; small pieces are added (at most this many) until one does.
    const int MostTopUps = 40;
    static readonly Rectangle PlateRect = new(620, 500, 240, 12);
    static readonly Rectangle ScaleBody = new(620, 500, 240, FloorY - 500);
    static readonly Vector2 DialCenter = new(740, 650);
    const float DialRadius = 80;
    // The weigh tray: a shallow dish this wide and deep, its sides sloping in by TraySlope, centred on the plate,
    // with a handle sticking out on the left.
    const float TrayWidth = 230, TrayDepth = 60, TraySlope = 20;
    static readonly Vector2 HandleSize = new(30, 14);
    // The open bag stands on the counter, centred here.
    const float BagCenterX = 985;
    static readonly Rectangle BinRect = new(1105, 480, 160, FloorY - 480);
    // A sealed bag is pressed flat to this share of its size, so even the biggest fits the bin.
    const float SealedScale = Bag.SealedScale;
    // How long a bag must sit still in the bin before the order counts as shipped.
    const float BinRestSeconds = 0.25f;
    // Pause between the bag landing and the order counting as done (the hand-over shot), so the landing (wobble,
    // clean-throw pop) can be seen.
    const float ResultDelay = 0.9f;

    // ---------- Tongs ----------
    // How fast a held nug catches up with the tongs: this rate over (0.4 + its grams), per second, so heavy nugs
    // lag and swing behind.
    const float FollowRate = 16f;
    // A let-go nug keeps the tongs' speed, up to this.
    const float MaxRelease = 1400f;
    // A nug the tongs let go of that lands faster than CrumbSpeed (a fall of about 60 px, or a fling) chips a crumb
    // of CrumbShare of its weight (at least CrumbMin grams). Landings slower than LandSpeed do not count as landing.
    // Shake never chips.
    const float CrumbSpeed = 480f, CrumbShare = 0.05f, CrumbMin = 0.02f, LandSpeed = 40f;

    // ---------- Pouring ----------
    // With its lip over the bag's mouth, the carried tray tips toward TiltMax degrees at TiltSpeed degrees a second
    // (elsewhere it levels out twice as fast). Past SlideFrom the nugs slide toward the lip, up to SlideSpeed at
    // full tilt, and drop off it; they leave with CarryShare of the tray's own speed, so a fast swing throws them
    // past the mouth.
    const float TiltMax = 55f, TiltSpeed = 140f, SlideFrom = 25f, SlideSpeed = 260f, CarryShare = 0.8f;
    // Seconds for a let-go tray to glide back onto the scale.
    const float ReturnSeconds = 0.25f;

    enum Phase { Idle, Fill, Ship, Done }
    enum Drag { None, Bud, Tray, Zip, Bag }

    readonly Random rng;
    readonly Physics physics = new();
    readonly List<Bud> buds = new();
    readonly List<Segment> staticWalls = new();
    readonly List<Segment> walls = new();

    // The jars on the bench, one per strain on the day's menu, and the strain this order asks for.
    readonly List<(Strain Strain, Rectangle Rect)> jars = new();
    Strain? wanted;

    Bag bag = null!;
    float target, room, needle, seconds;
    bool started;
    int slips;
    Phase phase;
    Drag drag;
    WeighResult? shipped;
    WeighResult? scored;
    float doneTime;
    string message = "";
    float messageTime;
    float binRest;
    int crumbs;
    float crumbGrams;

    // The nug in the tongs, and how closed the tongs are (0-1, drawing only).
    Bud? held;
    float tongsClose;
    Vector2 grabOffset, lastMouse, mouseVel;

    // The weigh tray. Its shape is in its own coordinates (top-left of the dish at 0,0); it is placed by holding its
    // point trayHold at trayAnchor and turning it tilt degrees about that point. On the scale trayHold is 0,0.
    Vector2 trayAnchor, trayHold, trayVel;
    float tilt;
    // The nugs riding the tray while it is carried or gliding home.
    readonly List<Bud> riders = new();
    bool trayReturning;
    Vector2 returnFrom;
    float returnFromTilt, returnTime;

    // A fixed mouse position for the --shot-pour screenshot; null in play.
    Vector2? fakeMouse;

    // ---------- Feedback (sound and visuals only; none of this changes the score) ----------
    // The needle is a spring: it overshoots and wobbles before it settles.
    const float NeedleStiffness = 170f, NeedleDamping = 10f;
    // A bud hitting something faster than this ticks; faster than PuffSpeed it also throws off leaf bits.
    const float TickSpeed = 220f, PuffSpeed = 650f, HardHit = 1400f;
    const int MaxTicksPerFrame = 3;
    // The weight must sit in the band this long before the ding, and leave it this long before it can ding again.
    const float DingAfter = 0.25f, DingResetAfter = 0.3f;
    // Spacing of the zip teeth; each one clicks as the tab passes it.
    const float ZipToothGap = 12f;
    // How close to slipping (0-1) the zip starts to shake.
    const float ZipShakeFrom = 0.55f;
    static readonly Color[] LeafBits = { new(96, 150, 70, 255), new(140, 170, 90, 255), new(214, 140, 70, 255) };

    readonly Effects fx;
    float needleVel, greenTime, outTime, greenGlow, zipDanger, binWobble, clock;
    bool dinged, landed, touched;
    int teethClosed;

    public WeighStation(Random rng)
    {
        this.rng = rng;
        fx = new Effects(rng);
        BuildStaticWalls();
        Clear();
    }

    // True when the station has no ticket and can take the next one.
    public bool IsIdle => phase == Phase.Idle;
    // The nugs in the sealed bag as they lay when it closed (null until it is sealed); the shop keeps it on the
    // ticket so the later stations draw the same pile.
    public BagFill? Fill { get; private set; }

    // Full-scale on the dial; smaller means a wider band.
    float GaugeMax => MathF.Max(target, 1f) * 1.35f;

    // Where the tray sits on the scale (top-left of its dish), and where the open bag stands.
    static Vector2 TrayHome => new(PlateRect.X + (PlateRect.Width - TrayWidth) / 2, PlateRect.Y - TrayDepth);
    Vector2 BagHome => new(BagCenterX - bag.Size.X / 2, FloorY - bag.Size.Y);

    // True while the tray rests on the scale (not carried, not gliding home).
    bool TrayHomeNow => drag != Drag.Tray && !trayReturning;

    // A point in the tray's own coordinates, on screen.
    Vector2 TrayWorld(Vector2 local) => trayAnchor + Rotate(local - trayHold, tilt);

    // The tray's dish corners on screen (rim left, bottom left, bottom right, rim right) and its handle's corners.
    Vector2[] TrayDish() => new[]
    {
        TrayWorld(new(0, 0)), TrayWorld(new(TraySlope, TrayDepth)),
        TrayWorld(new(TrayWidth - TraySlope, TrayDepth)), TrayWorld(new(TrayWidth, 0)),
    };
    Vector2[] TrayHandle() => new[]
    {
        TrayWorld(new(-HandleSize.X, 4)), TrayWorld(new(-HandleSize.X, 4 + HandleSize.Y)),
        TrayWorld(new(2, 4 + HandleSize.Y)), TrayWorld(new(2, 4)),
    };

    // Where the tray can be grabbed while it is on the scale: the handle or the dish.
    Rectangle TrayGrabRect => new(TrayHome.X - HandleSize.X, TrayHome.Y, TrayWidth + HandleSize.X, TrayDepth);

    // True if a loose bud sits on the tray while it rests on the scale (the pile can stand well above the rim).
    bool OnTray(Bud b) => TrayHomeNow && !b.Sealed && !b.Riding && b != held
        && b.Pos.X > TrayHome.X && b.Pos.X < TrayHome.X + TrayWidth
        && b.Pos.Y > TrayHome.Y - 160 && b.Pos.Y < TrayHome.Y + TrayDepth;

    bool InBag(Bud b) => !b.Sealed && !b.Riding && b != held && bag.Contains(b.Pos);

    // Grams on the tray (nothing while the tray is lifted off).
    float TrayGrams => TrayHomeNow ? buds.Where(OnTray).Sum(b => b.Grams) : 0;

    // Grams in the open bag, or sealed in it.
    float BagGrams => bag.Sealed ? buds.Where(b => b.Sealed).Sum(b => b.Grams) : buds.Where(InBag).Sum(b => b.Grams);

    // What the needle shows: a running total of what is already in the bag plus what is on the tray, so an order
    // can be made in several scoops. (A marker on the dial shows the bag's part.)
    float Reading => BagGrams + TrayGrams;

    // Live values for the F1 tuning panel.
    public List<TuningLine> TuningLines()
    {
        if (phase == Phase.Idle) return new() { new("No ticket on the station.") };
        var onTray = buds.Where(OnTray).ToList();
        float right = onTray.Concat(buds.Where(InBag)).Where(b => b.Strain == wanted).Sum(b => b.Grams);
        float par = Scoring.WeighPar(target);
        return new()
        {
            new($"Target {target:0.0#} g {wanted?.Name}, room +{room:0.00} g (cap {target + room:0.00} g)"),
            new($"Reads {Reading:0.00} g (bag {BagGrams:0.00} + tray {TrayGrams:0.00}), right strain {right:0.00} g",
                TrayHomeNow && (right < target - 0.001f || right > target + room + 0.001f)),
            new($"Wrong strain on tray: {onTray.Count(b => b.Strain != wanted)} buds", onTray.Any(b => b.Strain != wanted)),
            new($"Crumbs {crumbs} ({crumbGrams:0.00} g), tilt {tilt:0} deg", crumbs > 0),
            new($"Time {seconds:0.0} s, par {par:0.0} s", seconds > par),
            new($"Zip slips {slips}, slip danger {zipDanger * 100:0}%", zipDanger >= ZipShakeFrom),
        };
    }

    // Puts a jar of each strain on the day's menu on the bench, and empties the station.
    public void SetMenu(Strain[] menu)
    {
        jars.Clear();
        for (int i = 0; i < menu.Length; i++)
            jars.Add((menu[i], new Rectangle(JarsLeft + i * (JarWidth + JarGap), JarTop, JarWidth, JarBottom - JarTop)));
        BuildStaticWalls();
        Clear();
    }

    // True if a point is inside any jar.
    bool InAJar(Vector2 p) => jars.Any(j => Raylib.CheckCollisionPointRec(p, j.Rect));

    // The table, screen edges, jars, scale and bin as physics walls.
    void BuildStaticWalls()
    {
        staticWalls.Clear();
        void Add(float x1, float y1, float x2, float y2) => staticWalls.Add(new Segment(new(x1, y1), new(x2, y2)));

        Add(0, FloorY, Program.Width, FloorY);
        Add(0, 0, 0, FloorY);
        Add(Program.Width, 0, Program.Width, FloorY);

        foreach (var (_, jar) in jars)
        {
            float jl = jar.X, jr = jar.X + jar.Width, jt = jar.Y, jb = jar.Y + jar.Height;
            Add(jl, jt, jl, jb);
            Add(jl, jb, jr, jb);
            Add(jr, jt, jr, jb);
        }

        float pl = PlateRect.X, pr = PlateRect.X + PlateRect.Width, pt = PlateRect.Y;
        Add(pl, pt, pr, pt);
        Add(pl, pt, pl, FloorY);
        Add(pr, pt, pr, FloorY);

        float bl = BinRect.X, br = BinRect.X + BinRect.Width, bt = BinRect.Y;
        Add(bl, bt, bl, FloorY);
        Add(br, bt, br, FloorY);
    }

    // The tray's sides and bottom as physics walls, while it rests on the scale.
    IEnumerable<Segment> TrayWalls()
    {
        var d = TrayDish();
        yield return new Segment(d[0], d[1]);
        yield return new Segment(d[1], d[2]);
        yield return new Segment(d[2], d[3]);
    }

    // Starts a ticket: the weight and strain to bag, an open bag standing ready and freshly filled jars.
    public void Start(float grams, Strain strain)
    {
        wanted = strain;
        Reset(grams);
        phase = Phase.Fill;
    }

    // Empties the station with no ticket (at the start, after a bag ships, or when the customer walks out).
    public void Clear()
    {
        wanted = null;
        Reset(0);
        phase = Phase.Idle;
    }

    // Hands over the finished bag's result once. The bag stays in the bin until the shop calls Clear.
    public WeighResult? TakeShipped()
    {
        var done = shipped;
        shipped = null;
        return done;
    }

    // Drops whatever the mouse is holding; used when the player switches to another screen, where a half-done zip
    // slips open and a carried tray glides back onto the scale. Pausing (pausing = true) lets go of the zip without
    // a slip, as a normal release would.
    public void LetGo(bool pausing = false)
    {
        if (drag == Drag.Zip && !pausing) Slip("Zip slipped: you let go");
        if (drag == Drag.Tray) StartReturn();
        held = null;
        drag = Drag.None;
    }

    // Puts every per-order value back to the start for a target weight (0 = no ticket).
    void Reset(float grams)
    {
        target = grams;
        room = Scoring.RoomFor(target);
        shipped = null;
        scored = null;
        Fill = null;
        needle = 0;
        seconds = 0;
        started = false;
        slips = 0;
        crumbs = 0;
        crumbGrams = 0;
        held = null;
        drag = Drag.None;
        messageTime = 0;
        binRest = 0;
        needleVel = greenTime = outTime = greenGlow = zipDanger = binWobble = tongsClose = 0;
        dinged = landed = touched = false;
        teethClosed = 0;
        fx.Clear();
        riders.Clear();
        trayReturning = false;
        trayAnchor = TrayHome;
        trayHold = Vector2.Zero;
        tilt = 0;
        fakeMouse = null;

        bag = new Bag(Bag.SizeFor(target));
        bag.Placed = true;
        bag.Pos = BagHome;

        FillJar();
    }

    // Drops a fresh mix of buds and shake into every jar, each in its strain's size mix, tops it up so the order can
    // always be made, and lets it settle.
    void FillJar()
    {
        buds.Clear();
        foreach (var (strain, jar) in jars)
        {
            var inJar = new List<Bud>();
            float total = 0, small = 0;
            int shake = 0;
            void Add(float grams, bool isSmall, bool isShake = false)
            {
                var bud = new Bud(MathF.Round(grams, 2), strain, rng, isShake, !isSmall);
                bud.Pos = new Vector2(
                    Range(jar.X + bud.Radius + 4, jar.X + jar.Width - bud.Radius - 4),
                    Range(jar.Y + bud.Radius, jar.Y + jar.Height - bud.Radius - 4));
                buds.Add(bud);
                inJar.Add(bud);
                total += bud.Grams;
                if (isSmall) small += bud.Grams;
            }

            while (total < target * FillPerGram + FillExtra || shake < MinShake)
            {
                double roll = rng.NextDouble();
                if (shake < MinShake || roll < strain.ShakeShare) { Add(Range(0.05f, 0.15f), true, true); shake++; }
                else if (roll < strain.ShakeShare + strain.SmallShare) Add(Range(strain.SmallMin, strain.SmallMax), true);
                else Add(Range(strain.LargeMin, strain.LargeMax), false);
            }

            // Top up with small buds until the order can be made without any large ones (see SmallSpare).
            float smallNeeded = MathF.Min(target, strain.LargeMax) + room + SmallSpare;
            while (small < smallNeeded) Add(Range(strain.SmallMin, strain.SmallMax), true);

            // Whole pieces must be able to land in the band (nugs are never broken).
            for (int i = 0; target > 0 && i < MostTopUps && !CanLandInBand(inJar); i++)
                Add(i % 2 == 0 ? Range(0.05f, 0.15f) : Range(strain.SmallMin, strain.SmallMax), true);
        }

        // Run the physics for a couple of seconds so the pile has settled before the player sees it.
        for (int i = 0; i < 150; i++)
            physics.Step(buds, staticWalls, null, 1f / 60f);
        foreach (var b in buds) { b.Vel = Vector2.Zero; b.Impact = 0; }
    }

    // True if some set of these pieces adds up to between the order weight and the cap (in hundredths of a gram).
    bool CanLandInBand(List<Bud> pieces)
    {
        int lo = (int)MathF.Round(target * 100), hi = (int)MathF.Floor((target + room) * 100 + 0.001f);
        var can = new bool[hi + 1];
        can[0] = true;
        foreach (var b in pieces)
        {
            int w = (int)MathF.Round(b.Grams * 100);
            for (int s = hi; s >= w; s--)
                if (can[s - w]) can[s] = true;
        }
        for (int s = lo; s <= hi; s++)
            if (can[s]) return true;
        return false;
    }

    // Makes up the order from the ordered strain's nugs for the --shot layout check: about half already in the bag
    // (an earlier scoop) and the rest on the tray, so the dial shows the running total and the bag marker. With
    // throwBag it all goes into the bag, and the bag is sealed and thrown at the bin, to check that a throw ships.
    public void StageForScreenshot(bool throwBag)
    {
        phase = Phase.Fill;
        started = true;
        const float BagShare = 0.5f;
        var toBag = new List<Bud>();
        var toTray = new List<Bud>();
        float sum = 0;
        foreach (var b in buds.Where(b => !b.IsShake && b.Strain == wanted).ToList())
        {
            if (sum >= target) break;
            (throwBag || sum < target * BagShare ? toBag : toTray).Add(b);
            sum += b.Grams;
        }
        // Stack them loosely above where they go and let them fall into place.
        void Above(List<Bud> pile, float left, float width, float bottom)
        {
            for (int i = 0; i < pile.Count; i++)
                pile[i].Pos = new Vector2(left + pile[i].Radius + Range(0, MathF.Max(0, width - 2 * pile[i].Radius)), bottom - 20 - i * 26);
        }
        Above(toBag, bag.Pos.X + 6, bag.Size.X - 12, bag.Pos.Y + bag.Size.Y);
        Above(toTray, TrayHome.X + TraySlope, TrayWidth - 2 * TraySlope, TrayHome.Y + TrayDepth);
        for (int i = 0; i < 180; i++)
        {
            walls.Clear();
            walls.AddRange(staticWalls);
            walls.AddRange(bag.Walls);
            walls.AddRange(TrayWalls());
            physics.Step(buds, walls, null, 1f / 60f);
        }
        foreach (var b in buds) { b.Vel = Vector2.Zero; b.Impact = 0; }
        if (!throwBag) return;

        Seal();
        bag.Vel = new Vector2(200, -1000);
    }

    // Puts a few nugs on the tray and has the tray carried over the bag, so the shot catches it tipping them in,
    // for the --shot-pour check.
    public void StagePour()
    {
        StageForScreenshot(false);
        foreach (var b in buds) { b.Vel = Vector2.Zero; b.Impact = 0; }
        var hold = TrayHome + new Vector2(-HandleSize.X / 2, 4 + HandleSize.Y / 2);
        BeginCarry(hold);
        fakeMouse = new Vector2(bag.Pos.X - 30, bag.Pos.Y - 110);
    }

    // The mouse, or the staged one in a screenshot run.
    Vector2 Mouse => fakeMouse ?? Raylib.GetMousePosition();

    // Handles input and physics for one frame. Without input (another screen is showing) the physics still runs.
    public void Update(float dt, bool input)
    {
        dt = MathF.Min(dt, 1f / 30f);
        var mouse = Mouse;
        bool press = input && Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool release = input && Raylib.IsMouseButtonReleased(MouseButton.Left);
        if (dt > 0) mouseVel = Vector2.Lerp(mouseVel, (mouse - lastMouse) / dt, 0.5f);
        lastMouse = mouse;
        if (messageTime > 0) messageTime -= dt;
        clock += dt;
        fx.Update(dt);
        binWobble = MathF.Max(0, binWobble - dt * 2.5f);
        if (drag != Drag.Zip) zipDanger = MathF.Max(0, zipDanger - dt * 4f);
        tongsClose = MathF.Max(0, MathF.Min(1, tongsClose + (held != null ? 12f : -8f) * dt));

        if (phase == Phase.Done)
        {
            // Hand the result over once the landing has had a moment on screen.
            doneTime += dt;
            if (scored != null && doneTime >= ResultDelay) { shipped = scored; scored = null; }
            return;
        }

        if (started) seconds += dt;
        if (press && drag == Drag.None) BeginDrag(mouse);

        switch (drag)
        {
            case Drag.Bud: UpdateHeldBud(mouse, release, dt); break;
            case Drag.Tray: UpdateTray(mouse, release, dt); break;
            case Drag.Zip: UpdateZip(mouse, release); break;
            case Drag.Bag: UpdateShipBag(mouse, release); break;
        }
        if (trayReturning) UpdateReturn(dt);

        walls.Clear();
        walls.AddRange(staticWalls);
        if (!bag.Sealed) walls.AddRange(bag.Walls);
        if (TrayHomeNow) walls.AddRange(TrayWalls());
        physics.Step(buds, walls, held, dt);
        ReactToImpacts(dt);
        if (phase == Phase.Ship && drag != Drag.Bag) UpdateFlyingBag(dt);

        UpdateNeedle(dt);
    }

    // Turns this frame's bud hits into ticks, squash and leaf bits, and chips a crumb off a nug the tongs let go of
    // that landed hard. The hardest few hits make a sound, so a whole pile settling does not become a rattle.
    void ReactToImpacts(float dt)
    {
        foreach (var b in buds)
        {
            b.Squash = MathF.Max(0, b.Squash - dt * 6f);
            if (b.TickCooldown > 0) b.TickCooldown -= dt;
            if (!b.Dropped) continue;
            if (b.Impact >= LandSpeed)
            {
                b.Dropped = false;
                if (b.Impact >= CrumbSpeed) Crumb(b);
            }
            else if (b.Vel.LengthSquared() < 25f) b.Dropped = false;   // set down gently: it is resting
        }

        var hits = buds.Where(b => b != held && !b.Sealed && b.Impact > TickSpeed && b.TickCooldown <= 0)
                       .OrderByDescending(b => b.Impact).Take(MaxTicksPerFrame);
        foreach (var b in hits)
        {
            float strength = MathF.Min(1f, b.Impact / HardHit);
            b.Squash = MathF.Max(b.Squash, strength);
            b.TickCooldown = 0.08f;
            // Bigger buds knock lower; shake barely makes a sound.
            float size = MathF.Min(1f, b.Radius / 26f);
            Audio.Play(Audio.Tick, MathF.Max(0.15f, strength) * (b.IsShake ? 0.4f : 1f), 1.5f - 0.6f * size);
            if (b.Impact > PuffSpeed && !b.IsShake)
                fx.Puff(b.Pos + new Vector2(0, b.Radius * 0.6f), 4, 260f * strength + 80f, LeafBits);
        }
        foreach (var b in buds) b.Impact = 0;
    }

    // Chips a crumb off a nug that landed hard: it loses a little weight to the counter, and the crumb counts as given
    // away and costs a little handling.
    void Crumb(Bud b)
    {
        float chip = MathF.Max(CrumbMin, MathF.Round(b.Grams * CrumbShare, 2));
        b.Chip(chip);
        crumbs++;
        crumbGrams += chip;
        fx.Puff(b.Pos + new Vector2(0, b.Radius * 0.6f), 8, 300f, LeafBits);
        fx.Say("Crumbs!", b.Pos + new Vector2(0, -b.Radius - 20), Art.Warn);
        Audio.Play(Audio.Crinkle, 0.5f, 1.6f);
    }

    // Moves the needle like a spring toward the running total (bag plus tray), and rings a ding once the weight has settled in
    // the band. The band glows while the needle is in it.
    void UpdateNeedle(float dt)
    {
        float goal = phase == Phase.Fill ? Reading : 0;
        needleVel += ((goal - needle) * NeedleStiffness - needleVel * NeedleDamping) * dt;
        needle += needleVel * dt;
        if (needle < 0) { needle = 0; needleVel = -needleVel * 0.3f; }

        bool InBand(float g) => target > 0 && g >= target - 0.001f && g <= target + room + 0.001f;
        bool weightIn = phase == Phase.Fill && InBand(goal);
        greenGlow += ((phase == Phase.Fill && InBand(needle) ? 1f : 0f) - greenGlow) * MathF.Min(1f, dt * 10f);

        if (weightIn)
        {
            outTime = 0;
            greenTime += dt;
            if (greenTime >= DingAfter && !dinged)
            {
                dinged = true;
                Audio.Play(Audio.Ding, 0.6f);
            }
        }
        else
        {
            greenTime = 0;
            outTime += dt;
            if (outTime >= DingResetAfter) dinged = false;
        }
    }

    // Works out what a fresh mouse press picks up: the sealed bag to ship, the zip tab, a nug with the tongs, or the
    // weigh tray.
    void BeginDrag(Vector2 mouse)
    {
        if (phase == Phase.Idle) return;
        if (phase == Phase.Ship)
        {
            if (Raylib.CheckCollisionPointRec(mouse, bag.Rect))
            {
                drag = Drag.Bag;
                grabOffset = mouse - bag.Pos;
                started = true;
                Audio.Play(Audio.Pluck, 0.7f, 0.8f);
            }
            return;
        }

        if (Vector2.Distance(mouse, bag.ZipPoint) <= ZipGrabRadius && TrayHomeNow)
        {
            if (BagGrams <= 0) { Say("Pour the tray into the bag first"); return; }
            drag = Drag.Zip;
            started = true;
            return;
        }

        var hit = BudAt(mouse);
        if (hit != null)
        {
            held = hit;
            hit.Dropped = false;
            drag = Drag.Bud;
            // Move it to the end of the list so it draws on top.
            buds.Remove(hit);
            buds.Add(hit);
            started = true;
            Audio.Play(Audio.Pluck, hit.IsShake ? 0.35f : 0.6f, 1.3f - 0.4f * MathF.Min(1f, hit.Radius / 26f));
            return;
        }

        if (TrayHomeNow && Raylib.CheckCollisionPointRec(mouse, TrayGrabRect))
        {
            if (!buds.Any(OnTray)) { Say("Put some nugs on the tray first"); return; }
            BeginCarry(mouse);
            started = true;
        }
    }

    // Moves the held nug toward the tongs; heavy nugs lag behind and swing. Letting go drops it with the tongs'
    // speed, and its first landing decides whether it chips.
    void UpdateHeldBud(Vector2 mouse, bool release, float dt)
    {
        var goal = mouse + new Vector2(0, held!.Radius * 0.55f);
        float rate = MathF.Min(1f, FollowRate / (0.4f + held.Grams) * dt);
        var next = Vector2.Lerp(held.Pos, goal, rate);
        held.Vel = dt > 0 ? (next - held.Pos) / dt : Vector2.Zero;
        held.Pos = next;
        held.Angle += held.Vel.X * dt / held.Radius * 0.2f;
        if (!release) return;

        held.Vel = ClampLength(held.Vel, MaxRelease);
        held.Dropped = !held.IsShake;
        held = null;
        drag = Drag.None;
    }

    // Lifts the tray off the scale with everything on it, held at the given point.
    void BeginCarry(Vector2 at)
    {
        // Collect the riders first: OnTray only counts while the tray is home.
        var onTray = buds.Where(OnTray).ToList();
        drag = Drag.Tray;
        trayReturning = false;
        trayHold = at - TrayHome;
        trayAnchor = at;
        trayVel = Vector2.Zero;
        tilt = 0;
        riders.Clear();
        foreach (var b in onTray)
        {
            b.LocalOffset = b.Pos - TrayHome;
            b.Riding = true;
            b.Dropped = false;
            b.Vel = Vector2.Zero;
            riders.Add(b);
        }
        Audio.Play(Audio.Pluck, 0.7f, 0.7f);
    }

    // Carries the tray. With its lip over the bag's mouth it tips; past SlideFrom the nugs slide to the lip and drop
    // off into the bag. Letting go sends the tray back to the scale with whatever is still on it.
    void UpdateTray(Vector2 mouse, bool release, float dt)
    {
        var before = trayAnchor;
        trayAnchor = mouse;
        if (dt > 0) trayVel = Vector2.Lerp(trayVel, (trayAnchor - before) / dt, 0.5f);

        bool over = OverBagMouth();
        float goal = over ? TiltMax : 0f;
        float rate = (over ? TiltSpeed : TiltSpeed * 2f) * dt;
        tilt = tilt < goal ? MathF.Min(goal, tilt + rate) : MathF.Max(goal, tilt - rate);

        if (tilt > SlideFrom)
        {
            float push = SlideSpeed * (tilt - SlideFrom) / (TiltMax - SlideFrom);
            var downhill = Rotate(Vector2.UnitX, tilt);
            foreach (var b in riders.ToList())
            {
                // Each nug slides at its own pace, so they leave one after another rather than as a block.
                float pace = 0.7f + 0.6f * (MathF.Sin(b.Angle * 7f) * 0.5f + 0.5f);
                b.LocalOffset.X += push * pace * dt;
                if (b.LocalOffset.X < TrayWidth - b.Radius * 0.3f) continue;
                b.Pos = TrayWorld(b.LocalOffset);
                b.Riding = false;
                b.Vel = trayVel * CarryShare + downhill * push * 0.6f;
                riders.Remove(b);
                Audio.Play(Audio.Tick, 0.4f, 1.1f);
            }
        }
        PlaceRiders();
        if (release) { drag = Drag.None; StartReturn(); }
    }

    // True if, tipped all the way, the tray's lip would be over the open bag's mouth.
    bool OverBagMouth()
    {
        if (bag.Sealed) return false;
        var lip = trayAnchor + Rotate(new Vector2(TrayWidth, 0) - trayHold, TiltMax);
        return lip.X > bag.Pos.X + 6 && lip.X < bag.Pos.X + bag.Size.X - 6 && trayAnchor.Y < bag.Pos.Y - 10;
    }

    // Sends the tray gliding back onto the scale, level, with whatever still rides on it.
    void StartReturn()
    {
        trayReturning = true;
        returnFrom = trayAnchor;
        returnFromTilt = tilt;
        returnTime = 0;
    }

    // Glides the tray home; once there, its riders become loose nugs on the tray again.
    void UpdateReturn(float dt)
    {
        returnTime += dt;
        float t = MathF.Min(1f, returnTime / ReturnSeconds);
        float e = t * t * (3f - 2f * t);
        trayAnchor = Vector2.Lerp(returnFrom, TrayHome + trayHold, e);
        tilt = returnFromTilt * (1f - e);
        PlaceRiders();
        if (t < 1f) return;

        trayReturning = false;
        trayAnchor = TrayHome;
        trayHold = Vector2.Zero;
        tilt = 0;
        foreach (var b in riders) { b.Riding = false; b.Vel = Vector2.Zero; }
        riders.Clear();
        Audio.Play(Audio.Tick, 0.6f, 0.7f);
    }

    // Puts every riding nug where it sits on the tray now.
    void PlaceRiders()
    {
        foreach (var b in riders) b.Pos = TrayWorld(b.LocalOffset);
    }

    // Drags the zip tab along the bag top; straying off the line or going too fast makes it slip open.
    // Each tooth clicks as the tab passes it, a little higher as the zip closes; the tab shakes when close to slipping.
    void UpdateZip(Vector2 mouse, bool release)
    {
        float p = (mouse.X - bag.Pos.X) / bag.Size.X;
        // How close each slip rule is to tripping (1 = slips); the worst one sets the shake.
        zipDanger = MathF.Max(MathF.Abs(mouse.Y - bag.ZipY) / ZipSlipDistance,
                    MathF.Max(mouseVel.X / ZipMaxSpeed, (p - bag.Zip) / 0.2f));

        if (MathF.Abs(mouse.Y - bag.ZipY) > ZipSlipDistance) { Slip("Zip slipped: stay on the line"); return; }
        if (mouseVel.X > ZipMaxSpeed) { Slip("Zip slipped: too fast"); return; }
        if (p > bag.Zip + 0.2f) { Slip("Zip slipped: too fast"); return; }
        if (p > bag.Zip) bag.Zip = MathF.Min(1f, p);

        int teeth = (int)(bag.Zip * bag.Size.X / ZipToothGap);
        int clicks = 0;
        while (teethClosed < teeth)
        {
            teethClosed++;
            if (clicks++ < 2) Audio.Play(Audio.ZipTooth, 0.5f, 0.85f + 0.5f * bag.Zip);
        }

        if (bag.Zip >= 0.98f) { Seal(); return; }
        if (release) drag = Drag.None;
    }

    // Opens the zip back up and counts the slip.
    void Slip(string why)
    {
        bag.Zip = 0;
        slips++;
        teethClosed = 0;
        zipDanger = 0;
        drag = Drag.None;
        Say(why);
        Audio.Play(Audio.Slip, 0.8f);
    }

    // Closes the bag: everything inside is locked in, the bag is pressed flat and can be thrown.
    void Seal()
    {
        bag.Zip = 1;
        bag.Sealed = true;
        drag = Drag.None;
        phase = Phase.Ship;

        var oldPos = bag.Pos;
        var bottomCenter = new Vector2(bag.Center.X, bag.Pos.Y + bag.Size.Y);
        bag.Size *= SealedScale;
        bag.Pos = bottomCenter - new Vector2(bag.Size.X / 2, bag.Size.Y);
        bag.Vel = Vector2.Zero;

        foreach (var b in buds)
        {
            if (b.Riding || b == held) continue;
            if (b.Pos.X <= oldPos.X || b.Pos.X >= oldPos.X + bag.Size.X / SealedScale
                || b.Pos.Y <= oldPos.Y - 4 || b.Pos.Y >= oldPos.Y + bag.Size.Y / SealedScale) continue;
            b.Sealed = true;
            b.Vel = Vector2.Zero;
            b.Scale = SealedScale;
            b.LocalOffset = (b.Pos - oldPos) * SealedScale;
        }
        MoveBag(bag.Pos);
        Fill = new BagFill(bag.Size, buds.Where(b => b.Sealed).Select(b => (b, b.LocalOffset)).ToList());

        // Say how the weight came out, right where the player is looking.
        Audio.Play(Audio.Seal, 0.8f);
        var inside = buds.Where(b => b.Sealed).ToList();
        float counted = inside.Where(b => b.Strain == wanted).Sum(b => b.Grams);
        var at = new Vector2(bag.Center.X, bag.Pos.Y - 30);
        if (inside.Any(b => b.Strain != wanted)) fx.Say("Wrong strain in the bag!", at, Art.Warn);
        else if (counted < target - 0.001f) fx.Say($"{target - counted:0.00} g short!", at, Art.Warn);
        else if (counted <= target + room + 0.001f) fx.Say(counted - target < 0.005f ? "Right on!" : "In the green!", at, Art.Accent);
        else fx.Say($"{counted - target:0.00} g over the cap", at, Art.Warn);
    }

    // Carries the sealed bag; letting go throws it with the mouse's speed.
    void UpdateShipBag(Vector2 mouse, bool release)
    {
        MoveBag(mouse - grabOffset);
        bag.Vel = ClampLength(mouseVel, MaxToss);
        if (!release) return;
        drag = Drag.None;
        landed = touched = false;
    }

    // Lets a thrown bag fly and land; once it sits still inside the bin, the order is shipped.
    // A throw whose first touch is the bottom of the bin counts as clean (a pop, no points).
    void UpdateFlyingBag(float dt)
    {
        float hit = Physics.StepBox(ref bag.Pos, ref bag.Vel, bag.Size, staticWalls, dt);
        MoveBag(bag.Pos);

        bool overBin = bag.Pos.X >= BinRect.X - 1 && bag.Pos.X + bag.Size.X <= BinRect.X + BinRect.Width + 1;
        bool inBin = overBin && bag.Pos.Y + bag.Size.Y >= FloorY - 2;
        if (hit > 0 && !landed)
        {
            if (inBin)
            {
                landed = true;
                binWobble = MathF.Min(1f, hit / 900f);
                if (!touched)
                {
                    Audio.Play(Audio.Pop, 0.7f);
                    fx.Say("Clean throw!", new Vector2(BinRect.X + BinRect.Width / 2, BinRect.Y - 70), Art.Accent);
                }
            }
            else touched = true;
        }
        if (hit > 150f) Audio.Play(Audio.Thump, Math.Clamp(hit / 1500f, 0.2f, 1f));
        if (inBin && bag.Vel.Length() < 40f)
        {
            binRest += dt;
            if (binRest >= BinRestSeconds) Finish();
        }
        else binRest = 0;
    }

    // Moves the sealed bag and its contents together.
    void MoveBag(Vector2 pos)
    {
        bag.Pos = pos;
        foreach (var b in buds)
            if (b.Sealed) b.Pos = bag.Pos + b.LocalOffset;
    }

    // Scores the shipped bag and holds the result for the shop to collect. Nugs left on the tray are not spilled
    // (they go back in the jar); anything else outside a jar is.
    void Finish()
    {
        var inside = buds.Where(b => b.Sealed).ToList();
        float grams = inside.Sum(b => b.Grams);
        var wrong = inside.Where(b => b.Strain != wanted).ToList();
        int spilled = buds.Count(b => !b.Sealed && !InAJar(b.Pos) && !OnTray(b));
        int shakePieces = inside.Count(b => b.IsShake);
        float big = inside.Where(b => b.IsLarge).Sum(b => b.Grams);
        float bigShare = grams > 0 ? big / grams : 0;
        scored = Scoring.Score(target, room, grams, seconds, slips, spilled, wrong.Sum(b => b.Grams), wrong.Count,
                               crumbs, crumbGrams, shakePieces, bigShare);
        doneTime = 0;
        phase = Phase.Done;
    }

    // Shows a short message near the bag.
    void Say(string text)
    {
        message = text;
        messageTime = 1.6f;
    }

    // Draws the station. The ticket holder shows whose order it is and their patience; it glows while a ticket is dragged.
    public void Draw(Customer? customer, int ticketNumber, bool dropGlow)
    {
        var mouse = Mouse;

        Art.DrawTable(FloorY);
        Art.DrawTicket(TicketRect, customer, ticketNumber, dropGlow && phase == Phase.Idle);
        foreach (var (_, jar) in jars) Art.DrawJarBack(jar);
        Art.DrawScale(ScaleBody, PlateRect, DialCenter, DialRadius, needle, target, room, GaugeMax, greenGlow,
                      phase == Phase.Fill ? BagGrams : 0);
        // The bin shakes side to side for a moment after a bag lands in it (drawing only; its walls stay put).
        var bin = BinRect;
        bin.X += MathF.Sin(clock * 45f) * binWobble * 5f;
        Art.DrawBinBack(bin, phase == Phase.Ship);

        bool working = phase != Phase.Idle;
        if (working) Art.DrawBagBack(bag.Rect);
        bool trayGlow = phase == Phase.Fill && drag == Drag.None && TrayHomeNow && greenTime >= DingAfter && buds.Any(OnTray);
        Art.DrawTrayBack(TrayDish());

        bool canGrab = drag == Drag.None && phase == Phase.Fill;
        var hover = canGrab ? BudAt(mouse) : null;
        foreach (var bud in buds)
            if (bud != held) Art.DrawBud(bud, bud == hover, false);

        foreach (var (strain, jar) in jars) Art.DrawJarFront(jar, strain);
        Art.DrawTrayFront(TrayDish(), TrayHandle(), trayGlow);

        if (working)
        {
            bool zipHot = drag == Drag.Zip || (canGrab && Vector2.Distance(mouse, bag.ZipPoint) <= ZipGrabRadius);
            float shake = Math.Clamp((zipDanger - ZipShakeFrom) / (1f - ZipShakeFrom), 0f, 1f);
            var jitter = new Vector2(MathF.Sin(clock * 90f), MathF.Cos(clock * 77f)) * shake * 4f;
            Art.DrawBagFront(bag.Rect, bag.Zip, bag.Sealed, zipHot, shake, jitter, ZipToothGap);
        }

        Art.DrawBinFront(bin);
        if (held != null) Art.DrawBud(held, false, true);
        // The tongs: closed on the held nug, or open over a nug they could pick.
        if (held != null || hover != null) Art.DrawTongs(mouse, tongsClose);

        fx.Draw();
        DrawHud();
    }

    // Draws the timer, the hint for the current step and any short message.
    void DrawHud()
    {
        Art.Label($"Time {seconds:0.0} s", 1060, 150, 30, Art.Text);
        Art.Label($"Zip slips {slips}   Crumbs {crumbs}", 1060, 188, 20, Art.Faint);

        string hint = phase switch
        {
            Phase.Fill => $"Pick {wanted?.Name} nugs onto the tray with the tongs, then carry\nit over the bag to pour. Take as many scoops as you need: the\nneedle counts bag and tray. Never under the weight.",
            Phase.Ship => "Grab the sealed bag and throw it into the OUT bin.",
            Phase.Idle => "No ticket on the station. Drag one down from the rail.",
            _ => "",
        };
        Art.Label(hint, 420, 150, 22, Art.Faint);

        if (messageTime > 0)
            Art.LabelCentered(message, bag.Center.X, bag.Pos.Y - 50, 24, Art.Warn);
    }

    // The top-most loose bud under the point, or null.
    Bud? BudAt(Vector2 point)
    {
        for (int i = buds.Count - 1; i >= 0; i--)
            if (!buds[i].Sealed && !buds[i].Riding && buds[i].Contains(point)) return buds[i];
        return null;
    }

    // Turns a vector by degrees (clockwise on screen).
    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float a = degrees * MathF.PI / 180f;
        float c = MathF.Cos(a), s = MathF.Sin(a);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    static Vector2 ClampLength(Vector2 v, float max) =>
        v.LengthSquared() > max * max ? Vector2.Normalize(v) * max : v;

    float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
}
