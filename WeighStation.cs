using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The weigh station: put a bag on the scale, toss buds in until the needle sits in the green,
// drag the zip across to seal it, then throw the bag into the OUT bin. The shop hands it each ticket
// with Start and collects the finished bag with TakeShipped.
sealed class WeighStation
{
    const int MinShake = 8;
    const float MaxToss = 1800f;
    // The zip slips open if the mouse strays this far from the zip line, or moves faster than this.
    const float ZipSlipDistance = 24f;
    const float ZipMaxSpeed = 1500f;
    const float ZipGrabRadius = 22f;

    const float FloorY = 665;
    static readonly Rectangle TicketRect = new(50, 150, 330, 110);
    static readonly Rectangle JarRect = new(70, 380, 330, 260);
    static readonly Rectangle StackRect = new(480, 590, 120, 70);
    static readonly Rectangle PlateRect = new(730, 470, 300, 12);
    static readonly Rectangle ScaleBody = new(730, 470, 300, FloorY - 470);
    static readonly Vector2 DialCenter = new(880, 645);
    const float DialRadius = 80;
    static readonly Rectangle BinRect = new(1065, 480, 195, FloorY - 480);
    // A sealed bag is pressed flat to this share of its size, so even the biggest fits the bin.
    const float SealedScale = 0.6f;
    // How long a bag must sit still in the bin before the order counts as shipped.
    const float BinRestSeconds = 0.25f;
    // Pause between the bag landing and the order counting as done (the hand-over shot), so the landing (wobble,
    // clean-throw pop) can be seen.
    const float ResultDelay = 0.9f;

    enum Phase { Idle, PlaceBag, Fill, Ship, Done }
    enum Drag { None, Bud, NewBag, Zip, Bag }

    readonly Random rng;
    readonly Physics physics = new();
    readonly List<Bud> buds = new();
    readonly List<Segment> staticWalls = new();
    readonly List<Segment> walls = new();

    Bag bag = null!;
    float target, band, needle, seconds;
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

    Bud? held;
    Vector2 grabOffset, lastMouse, mouseVel;

    // ---------- Feedback (sound and visuals only; none of this changes the score) ----------
    // The needle is a spring: it overshoots and wobbles before it settles.
    const float NeedleStiffness = 170f, NeedleDamping = 10f;
    // A bud hitting something faster than this ticks; faster than PuffSpeed it also throws off leaf bits.
    const float TickSpeed = 220f, PuffSpeed = 650f, HardHit = 1400f;
    const int MaxTicksPerFrame = 3;
    // The weight must sit in the green this long before the ding, and leave it this long before it can ding again.
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

    // Full-scale on the dial; smaller means a wider green band.
    float GaugeMax => MathF.Max(target, 1f) * 1.35f;

    // Where the bag sits when it is on the scale.
    Vector2 BagHome => new(PlateRect.X + PlateRect.Width / 2 - bag.Size.X / 2, PlateRect.Y - bag.Size.Y);

    // True while the bag is resting on the scale plate (not held, not thrown off).
    bool BagOnPlate => drag != Drag.Bag
        && MathF.Abs(bag.Pos.Y + bag.Size.Y - PlateRect.Y) < 3
        && bag.Center.X > PlateRect.X && bag.Center.X < PlateRect.X + PlateRect.Width;

    // Grams the scale feels: loose buds in the bag while filling, the sealed bag while it sits on the plate.
    float BagGrams
    {
        get
        {
            if (bag.Sealed) return BagOnPlate ? buds.Where(b => b.Sealed).Sum(b => b.Grams) : 0;
            if (!bag.Placed) return 0;
            return buds.Where(b => b != held && bag.Contains(b.Pos)).Sum(b => b.Grams);
        }
    }

    // The table, screen edges, jar and scale as physics walls.
    void BuildStaticWalls()
    {
        void Add(float x1, float y1, float x2, float y2) => staticWalls.Add(new Segment(new(x1, y1), new(x2, y2)));

        Add(0, FloorY, Program.Width, FloorY);
        Add(0, 0, 0, FloorY);
        Add(Program.Width, 0, Program.Width, FloorY);

        float jl = JarRect.X, jr = JarRect.X + JarRect.Width, jt = JarRect.Y, jb = JarRect.Y + JarRect.Height;
        Add(jl, jt, jl, jb);
        Add(jl, jb, jr, jb);
        Add(jr, jt, jr, jb);

        float pl = PlateRect.X, pr = PlateRect.X + PlateRect.Width, pt = PlateRect.Y;
        Add(pl, pt, pr, pt);
        Add(pl, pt, pl, FloorY);
        Add(pr, pt, pr, FloorY);

        float bl = BinRect.X, br = BinRect.X + BinRect.Width, bt = BinRect.Y;
        Add(bl, bt, bl, FloorY);
        Add(br, bt, br, FloorY);
    }

    // Starts a ticket: the weight to bag, a new empty bag in the stack and a newly filled jar.
    public void Start(float grams)
    {
        Reset(grams);
        phase = Phase.PlaceBag;
    }

    // Empties the station with no ticket (at the start, after a bag ships, or when the customer walks out).
    public void Clear()
    {
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

    // Drops whatever the mouse is holding; used when the player switches to another screen.
    // A half-done zip slips open; a bag still being carried from the stack goes back to it.
    public void LetGo()
    {
        if (drag == Drag.Zip) Slip("Zip slipped: you let go");
        held = null;
        drag = Drag.None;
    }

    // Puts every per-order value back to the start for a target weight (0 = no ticket).
    void Reset(float grams)
    {
        target = grams;
        band = Scoring.BandFor(target);
        shipped = null;
        scored = null;
        needle = 0;
        seconds = 0;
        started = false;
        slips = 0;
        held = null;
        drag = Drag.None;
        messageTime = 0;
        binRest = 0;
        needleVel = greenTime = outTime = greenGlow = zipDanger = binWobble = 0;
        dinged = landed = touched = false;
        teethClosed = 0;
        fx.Clear();

        var size = target switch
        {
            <= 1f => new Vector2(120, 120),
            <= 3.5f => new Vector2(165, 160),
            _ => new Vector2(215, 200),
        };
        bag = new Bag(size);

        FillJar();
    }

    // Drops a fresh mix of buds and shake into the jar and lets them settle.
    void FillJar()
    {
        buds.Clear();
        float total = 0;
        int shake = 0;
        while (total < target * 2f + 3f || shake < MinShake)
        {
            double roll = rng.NextDouble();
            float grams;
            if (shake < MinShake || roll < 0.25) { grams = Range(0.05f, 0.15f); shake++; }
            else if (roll < 0.6) grams = Range(0.3f, 0.6f);
            else grams = Range(0.6f, 1.0f);

            var bud = new Bud(MathF.Round(grams, 2), rng);
            bud.Pos = new Vector2(
                Range(JarRect.X + bud.Radius + 4, JarRect.X + JarRect.Width - bud.Radius - 4),
                Range(JarRect.Y + bud.Radius, JarRect.Y + JarRect.Height - bud.Radius - 4));
            buds.Add(bud);
            total += bud.Grams;
        }

        // Run the physics for a couple of seconds so the pile has settled before the player sees it.
        for (int i = 0; i < 150; i++)
            physics.Step(buds, staticWalls, null, 1f / 60f);
        foreach (var b in buds) { b.Vel = Vector2.Zero; b.Impact = 0; }
    }

    // Puts the bag on the scale and drops a few buds into it, for the --shot layout check.
    // With throwBag, the bag is also sealed and thrown at the bin, to check that a throw lands and ships.
    public void StageForScreenshot(bool throwBag)
    {
        bag.Placed = true;
        bag.Pos = BagHome;
        bag.Zip = 0.4f;
        phase = Phase.Fill;
        started = true;
        foreach (var b in buds.Where(b => !b.IsShake).Take(4))
            b.Pos = bag.Center + new Vector2(Range(-30, 30), Range(-60, 0));
        if (!throwBag) return;

        for (int i = 0; i < 90; i++) physics.Step(buds, staticWalls.Concat(bag.Walls).ToList(), null, 1f / 60f);
        Seal();
        bag.Vel = new Vector2(250, -800);
    }

    // Handles input and physics for one frame. Without input (another screen is showing) the physics still runs.
    public void Update(float dt, bool input)
    {
        dt = MathF.Min(dt, 1f / 30f);
        var mouse = Raylib.GetMousePosition();
        bool press = input && Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool release = input && Raylib.IsMouseButtonReleased(MouseButton.Left);
        if (dt > 0) mouseVel = Vector2.Lerp(mouseVel, (mouse - lastMouse) / dt, 0.5f);
        lastMouse = mouse;
        if (messageTime > 0) messageTime -= dt;
        clock += dt;
        fx.Update(dt);
        binWobble = MathF.Max(0, binWobble - dt * 2.5f);
        if (drag != Drag.Zip) zipDanger = MathF.Max(0, zipDanger - dt * 4f);

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
            case Drag.Bud: UpdateHeldBud(mouse, release); break;
            case Drag.NewBag: UpdateNewBag(mouse, release); break;
            case Drag.Zip: UpdateZip(mouse, release); break;
            case Drag.Bag: UpdateShipBag(mouse, release); break;
        }

        walls.Clear();
        walls.AddRange(staticWalls);
        if (bag.Placed && !bag.Sealed) walls.AddRange(bag.Walls);
        physics.Step(buds, walls, held, dt);
        ReactToImpacts(dt);
        if (phase == Phase.Ship && drag != Drag.Bag) UpdateFlyingBag(dt);

        UpdateNeedle(dt);
    }

    // Turns this frame's bud hits into ticks, squash and leaf bits. The hardest few hits make a sound, so a
    // whole pile settling does not become a rattle.
    void ReactToImpacts(float dt)
    {
        foreach (var b in buds)
        {
            b.Squash = MathF.Max(0, b.Squash - dt * 6f);
            if (b.TickCooldown > 0) b.TickCooldown -= dt;
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

    // Moves the needle like a spring toward the real weight, and rings a ding once the weight has settled
    // in the green. The green band glows while the needle is in it.
    void UpdateNeedle(float dt)
    {
        float goal = BagGrams;
        needleVel += ((goal - needle) * NeedleStiffness - needleVel * NeedleDamping) * dt;
        needle += needleVel * dt;
        if (needle < 0) { needle = 0; needleVel = -needleVel * 0.3f; }

        bool weightIn = target > 0 && phase == Phase.Fill && MathF.Abs(goal - target) <= band;
        bool needleIn = target > 0 && MathF.Abs(needle - target) <= band && (phase == Phase.Fill || bag.Sealed);
        greenGlow += ((needleIn ? 1f : 0f) - greenGlow) * MathF.Min(1f, dt * 10f);

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

    // Works out what a fresh mouse press picks up: the bag to ship, the zip tab, a bud, or a new bag.
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

        if (phase == Phase.Fill && Vector2.Distance(mouse, bag.ZipPoint) <= ZipGrabRadius)
        {
            if (BagGrams <= 0) { Say("Put something in the bag first"); return; }
            drag = Drag.Zip;
            started = true;
            return;
        }

        var hit = BudAt(mouse);
        if (hit != null)
        {
            held = hit;
            drag = Drag.Bud;
            grabOffset = hit.Pos - mouse;
            // Move it to the end of the list so it draws on top.
            buds.Remove(hit);
            buds.Add(hit);
            started = true;
            Audio.Play(Audio.Pluck, hit.IsShake ? 0.35f : 0.6f, 1.3f - 0.4f * MathF.Min(1f, hit.Radius / 26f));
            return;
        }

        if (phase == Phase.PlaceBag && Raylib.CheckCollisionPointRec(mouse, StackRect))
        {
            drag = Drag.NewBag;
            started = true;
            Audio.Play(Audio.Pluck, 0.8f, 0.7f);
        }
    }

    // Moves the held bud with the mouse; letting go throws it with the mouse's speed.
    void UpdateHeldBud(Vector2 mouse, bool release)
    {
        held!.Pos = mouse + grabOffset;
        held.Vel = ClampLength(mouseVel, MaxToss);
        if (!release) return;
        held = null;
        drag = Drag.None;
    }

    // Carries a new bag from the stack; dropped over the scale it snaps onto the plate.
    void UpdateNewBag(Vector2 mouse, bool release)
    {
        bag.Pos = mouse - bag.Size / 2;
        if (!release) return;
        drag = Drag.None;

        bool overScale = mouse.X > PlateRect.X && mouse.X < PlateRect.X + PlateRect.Width
                         && mouse.Y < PlateRect.Y + 30 && mouse.Y > PlateRect.Y - 320;
        if (!overScale) return;
        bag.Placed = true;
        bag.Pos = BagHome;
        phase = Phase.Fill;
        Audio.Play(Audio.Tick, 0.7f, 0.6f);
        needleVel = 0.4f * GaugeMax;  // the plate dips a little under the bag
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
            if (b.Pos.X <= oldPos.X || b.Pos.X >= oldPos.X + bag.Size.X / SealedScale
                || b.Pos.Y <= oldPos.Y - 4 || b.Pos.Y >= oldPos.Y + bag.Size.Y / SealedScale) continue;
            b.Sealed = true;
            b.Vel = Vector2.Zero;
            b.Scale = SealedScale;
            b.LocalOffset = (b.Pos - oldPos) * SealedScale;
        }
        MoveBag(bag.Pos);

        // Say how the weight came out, right where the player is looking.
        Audio.Play(Audio.Seal, 0.8f);
        float diff = buds.Where(b => b.Sealed).Sum(b => b.Grams) - target;
        var at = new Vector2(bag.Center.X, bag.Pos.Y - 30);
        if (MathF.Abs(diff) <= band) fx.Say("In the green!", at, Art.Accent);
        else fx.Say(diff > 0 ? $"{diff:0.00} g over" : $"{-diff:0.00} g short", at, Art.Warn);
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

    // Scores the shipped bag and holds the result for the shop to collect.
    void Finish()
    {
        float grams = buds.Where(b => b.Sealed).Sum(b => b.Grams);
        int spilled = buds.Count(b => !b.Sealed && !Raylib.CheckCollisionPointRec(b.Pos, JarRect));
        scored = Scoring.Score(target, band, grams, seconds, slips, spilled);
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
        var mouse = Raylib.GetMousePosition();

        Art.DrawTable(FloorY);
        Art.DrawTicket(TicketRect, customer, ticketNumber, dropGlow && phase == Phase.Idle);
        Art.DrawJarBack(JarRect);
        Art.DrawScale(ScaleBody, PlateRect, DialCenter, DialRadius, needle, target, band, GaugeMax, greenGlow);
        Art.DrawBagStack(StackRect, phase == Phase.PlaceBag && drag == Drag.None);
        // The bin shakes side to side for a moment after a bag lands in it (drawing only; its walls stay put).
        var bin = BinRect;
        bin.X += MathF.Sin(clock * 45f) * binWobble * 5f;
        Art.DrawBinBack(bin, phase == Phase.Ship);

        bool showBag = bag.Placed || drag == Drag.NewBag;
        if (showBag) Art.DrawBagBack(bag.Rect);

        bool canGrab = drag == Drag.None && (phase == Phase.PlaceBag || phase == Phase.Fill);
        var hover = canGrab ? BudAt(mouse) : null;
        foreach (var bud in buds)
            if (bud != held) Art.DrawBud(bud, bud == hover, false);

        Art.DrawJarFront(JarRect);

        if (showBag)
        {
            bool zipHot = drag == Drag.Zip || (canGrab && phase == Phase.Fill && Vector2.Distance(mouse, bag.ZipPoint) <= ZipGrabRadius);
            float shake = Math.Clamp((zipDanger - ZipShakeFrom) / (1f - ZipShakeFrom), 0f, 1f);
            var jitter = new Vector2(MathF.Sin(clock * 90f), MathF.Cos(clock * 77f)) * shake * 4f;
            Art.DrawBagFront(bag.Rect, bag.Zip, bag.Sealed, zipHot, shake, jitter, ZipToothGap);
        }

        Art.DrawBinFront(bin);
        if (held != null) Art.DrawBud(held, false, true);

        fx.Draw();
        DrawHud();
    }

    // Draws the timer, the hint for the current step and any short message.
    void DrawHud()
    {
        Art.Label($"Time {seconds:0.0} s", 1060, 150, 30, Art.Text);
        Art.Label($"Zip slips {slips}", 1060, 188, 20, Art.Faint);

        string hint = phase switch
        {
            Phase.PlaceBag => "Drag a bag from the stack onto the scale.",
            Phase.Fill => "Toss buds in the bag until the needle sits in the green.\nThen drag the zip tab across to seal it. Not too fast.",
            Phase.Ship => "Grab the sealed bag and throw it into the OUT bin.",
            Phase.Idle => "No ticket on the station. Drag one down from the rail.",
            _ => "",
        };
        Art.Label(hint, 420, 160, 22, Art.Faint);

        if (messageTime > 0)
            Art.LabelCentered(message, bag.Center.X, bag.Pos.Y - 50, 24, Art.Warn);
    }

    // The top-most loose bud under the point, or null.
    Bud? BudAt(Vector2 point)
    {
        for (int i = buds.Count - 1; i >= 0; i--)
            if (!buds[i].Sealed && buds[i].Contains(point)) return buds[i];
        return null;
    }

    static Vector2 ClampLength(Vector2 v, float max) =>
        v.LengthSquared() > max * max ? Vector2.Normalize(v) * max : v;

    float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
}
