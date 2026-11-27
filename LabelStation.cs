using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The label station: the closed container from Pack stands on the mat. Click the pad for the order's strain in the
// rack: a hand holds its label up in front of the container, still on its backing. Drag the backing's tab away to
// peel it; the label then slides back and forth over the front and starts to curl. Click to slap it on as it passes
// the guide marks; where it lands is final. A curled label lands with air bubbles (and a lifted corner when badly
// curled) that are rubbed flat by holding the button and moving over them. Another pad swaps the label (a peeled one
// is tossed). The slide gets faster with rank. The shop hands it each ticket with Start and collects the result with
// TakeShipped.
sealed class LabelStation
{
    const float FloorY = 665;
    static readonly Rectangle TicketRect = new(50, 150, 330, 110);
    // The container stands on the mat at MatX, drawn Zoom times its pack station size so placing is readable.
    const float MatX = 560, Zoom = 1.5f;
    // The label rack: one pad per strain on the menu, stacked down the right side.
    const float RackX = 990, RackTop = 330, PadW = 230, PadH = 84, PadGap = 22;
    // A label's size as a share of the container's front, and where the guide centre sits (share of the height).
    const float LabelW = 0.72f, LabelH = 0.3f, GuideY = 0.58f;
    // The slide: how far either side of the guide the label travels (share of the front's width), its speed in
    // pixels a second at rank 1, how much faster each rank above that makes it, and the most it can reach. It moves
    // at an even speed and turns at the ends.
    const float SlideReach = 0.5f, SlideSpeed = 260f, SlidePerRank = 0.1f, SlideMax = 460f;
    // The held label: drawn this much bigger (nearer the viewer) and this many pixels above where it would land; its
    // shadow and a set of corner marks in its colour sit on the landing spot, to line up with the guide marks.
    const float HoverScale = 1.08f, HoverRise = 46f;
    // Peeling: how far the backing's tab must be dragged (any direction) to come off.
    const float PeelPull = 130f;
    // Curl: a peeled label stays flat this many seconds, then curls fully over the next CurlSpan seconds.
    const float CurlGrace = 0.7f, CurlSpan = 2f;
    // Bubbles: a fully curled label lands with MaxBubbles; from CornerAt curl one corner lifts too. Rubbing flattens a
    // bubble in RubTime seconds (a corner in CornerRubTime) while the mouse is held and moving within reach of it.
    const int MaxBubbles = 5;
    const float CornerAt = 0.5f, RubTime = 0.16f, CornerRubTime = 0.3f, RubReach = 20f;
    // The slap: seconds for the label to hit the front, the hand stays pressed this long, then lifts away.
    const float SlamTime = 0.06f, PressTime = 0.12f, LiftTime = 0.22f;
    // The jolt after the hit: the container squashes by this share and wobbles back (decay per second, wobble speed
    // in radians a second); the station shakes up to ShakePx pixels for ShakeTime seconds.
    const float JoltAmp = 0.07f, JoltDecay = 9f, JoltFreq = 30f, ShakePx = 7f, ShakeTime = 0.25f;
    // Pause between the label being done and the order counting as done, so the finish can be seen.
    const float ResultDelay = 0.9f;

    enum Phase { Idle, Pick, Peel, Slide, Smooth, Done }

    // An air bubble under a slapped label: where it is (share of the label's size from its centre), how much of it is
    // left to rub out (1 to 0), and whether it is the lifted corner.
    sealed class Bubble
    {
        public Vector2 At;
        public float Left = 1f;
        public bool Corner;
    }

    static readonly Color Paper = new(245, 240, 225, 255);

    readonly Random rng;
    readonly Effects fx;
    Phase phase;
    Strain wantedStrain = Strains.All[0];
    Container container;
    // The nugs sealed in the bag inside it (they show through a jar's glass).
    BagFill? fill;
    Strain[] menu = Array.Empty<Strain>();
    // The bag's weight as the label prints it, like "3.62 g".
    string netText = "";

    // The label in play (null for none): its strain, its offset from the guide (pixels, sideways), and the way it is
    // sliding (+1 right, -1 left).
    Strain? label;
    float offset, dir = 1f, slideSpeed;
    // Peeling: how far the backing has been pulled (pixels from where it sat), whether its tab is held, and where.
    Vector2 pull, grabFrom;
    bool grabbing;
    // The torn-off backing flying away: where, how fast, its turn and spin (degrees), and seconds left on screen.
    Vector2 backPos, backVel;
    float backAngle, backSpin, backLife;
    // Seconds since the peel (the curl clock), the curl now (0-1), and seconds from pick to peel.
    float sincePeel, curl, peelSeconds;
    // Seconds since the slap (below zero before it), and the slap's placement: pixels off the guide and the offset
    // as a share of the front (for the hand-over).
    float slamT = -1f, slappedOff;
    Vector2 slappedAt;
    readonly List<Bubble> bubbles = new();
    int bubbleCount;
    // Smoothing: seconds spent rubbing out bubbles, where the rubbing hand is, whether it is rubbing, the mouse last
    // frame (rubbing needs movement), and a timer that spaces out the rub sound.
    float rubSeconds, rubSound;
    Vector2 handAt, lastMouse;
    bool rubbing;
    float seconds, doneTime;
    // started: the clock runs. frozen: a staged screenshot, held still.
    bool started, frozen;
    LabelResult? scored, shipped;

    public LabelStation(Random rng)
    {
        this.rng = rng;
        fx = new Effects(rng);
        Clear();
    }

    // True when the station has no ticket and can take the next one.
    public bool IsIdle => phase == Phase.Idle;

    bool Slapped => slamT >= 0;

    // The container's front on the mat.
    Rectangle Front
    {
        get
        {
            var s = Containers.Inside(container.Large) * Zoom;
            return new Rectangle(MatX - s.X / 2, FloorY - s.Y, s.X, s.Y);
        }
    }

    // A label's centre and size on a container front, at an offset from the guide given as a share of the front's
    // size (zero = dead on the guide). Shared with the hand-over, which draws the label where it actually went.
    public static (Vector2 Center, Vector2 Size) LabelOn(Rectangle front, Vector2 at)
    {
        var size = new Vector2(front.Width * LabelW, front.Height * LabelH);
        var guide = new Vector2(front.X + front.Width / 2, front.Y + front.Height * GuideY);
        return (guide + at * new Vector2(front.Width, front.Height), size);
    }

    Vector2 LabelSize => LabelOn(Front, Vector2.Zero).Size;
    Vector2 Guide => LabelOn(Front, Vector2.Zero).Center;
    float Reach => Front.Width * SlideReach;

    // Where the held label is drawn and how big, before the slap.
    Vector2 HeldCenter => Guide + new Vector2(offset, -HoverRise);
    Vector2 HeldSize => LabelSize * HoverScale;

    // The backing's tab while peeling: a grab target at the label's lower left corner, moved with the pull.
    Rectangle TabRect
    {
        get
        {
            var c = HeldCenter + pull - HeldSize / 2 + new Vector2(0, HeldSize.Y);
            return new Rectangle(c.X - 30, c.Y - 22, 52, 40);
        }
    }

    static Rectangle PadRect(int i) => new(RackX, RackTop + i * (PadH + PadGap), PadW, PadH);

    // The label slide's speed at a rank.
    static float SlideSpeedFor(int rank) => MathF.Min(SlideMax, SlideSpeed * (1f + SlidePerRank * (rank - 1)));

    // Starts a ticket: the closed container from Pack on the mat, and a label pad for each strain on the menu. grams
    // is what the bag weighed; the label prints it. bagFill is the nugs in the bag. rank sets the slide's speed.
    public void Start(Customer c, Container used, Strain[] dayMenu, float grams, BagFill? bagFill, int rank)
    {
        Reset();
        netText = $"{grams:0.00} g";
        wantedStrain = c.Strain;
        container = used;
        fill = bagFill;
        menu = dayMenu;
        slideSpeed = SlideSpeedFor(rank);
        phase = Phase.Pick;
    }

    // Empties the station with no ticket.
    public void Clear()
    {
        Reset();
        phase = Phase.Idle;
    }

    void Reset()
    {
        label = null;
        offset = seconds = 0;
        dir = 1f;
        pull = Vector2.Zero;
        grabbing = false;
        backLife = 0;
        sincePeel = curl = peelSeconds = 0;
        slamT = -1f;
        slappedOff = 0;
        slappedAt = Vector2.Zero;
        bubbles.Clear();
        bubbleCount = 0;
        rubSeconds = rubSound = 0;
        rubbing = false;
        started = false;
        scored = shipped = null;
        fx.Clear();
    }

    // Live values for the F1 tuning panel: the strain wanted and picked, the slide, the curl and bubbles, how far off
    // the guide the label is and the placement a slap would score now, and time against par.
    public List<TuningLine> TuningLines()
    {
        if (phase == Phase.Idle) return new() { new("No ticket on the station.") };
        var lines = new List<TuningLine>
        {
            new($"Wants {wantedStrain.Name}, picked: {label?.Name ?? "nothing"}", label != null && label != wantedStrain),
            new($"Slide {slideSpeed:0} px/s (rank 1 {SlideSpeed:0}, +{SlidePerRank * 100:0}% a rank, max {SlideMax:0}), reach {Reach:0} px"),
        };
        if (phase == Phase.Peel) lines.Add(new($"Peel {pull.Length():0} of {PeelPull:0} px"));
        if (phase >= Phase.Slide)
            lines.Add(new($"Peeled at {peelSeconds:0.0} s; curl {curl * 100:0}% (flat for {CurlGrace:0.0} s, full {CurlSpan:0.0} s later)", curl > 0));
        if (phase == Phase.Slide)
        {
            float off = MathF.Abs(offset);
            int place = Scoring.PlacementFor(off);
            lines.Add(new($"Off the guide {off:0} px, full marks within {Scoring.OffFull:0}, zero at {Scoring.OffZero:0}", off > Scoring.OffFull));
            lines.Add(new($"Placement if slapped now: {place}, bubbles {(int)MathF.Round(curl * MaxBubbles)}", place < 90));
        }
        if (Slapped)
        {
            lines.Add(new($"Slapped {slappedOff:0} px off: placement {Scoring.PlacementFor(slappedOff)}"));
            lines.Add(new($"Bubbles {bubbles.Count(b => b.Left > 0)} of {bubbleCount} left, rubbing {rubSeconds:0.00} s", bubbleCount > 0));
        }
        lines.Add(new($"Time {seconds:0.0} s, par {Scoring.LabelPar:0.0} s", seconds > Scoring.LabelPar));
        return lines;
    }

    // Hands over the labeled order's result once.
    public LabelResult? TakeShipped()
    {
        var done = shipped;
        shipped = null;
        return done;
    }

    // Sets up the station for a screenshot, the right label picked: "peel" the backing half pulled off, "slap" the
    // instant the label hits (a curled label, so bubbles and a lifted corner), "smooth" rubbing those bubbles out,
    // anything else the peeled label sliding just short of the guide marks, a little curled.
    public void StageForScreenshot(string kind)
    {
        frozen = true;
        label = wantedStrain;
        started = true;
        if (kind == "peel")
        {
            phase = Phase.Peel;
            offset = -Reach;
            pull = new Vector2(-50, 50);
            grabbing = true;
            seconds = 0.6f;
            return;
        }
        peelSeconds = 0.6f;
        offset = -Reach * 0.3f;
        phase = Phase.Slide;
        if (kind == "slap" || kind == "smooth")
        {
            curl = 0.7f;
            seconds = 2.3f;
            Slap();
            fx.Clear();
            slamT = kind == "slap" ? SlamTime + 0.03f : SlamTime + PressTime + LiftTime * 0.5f;
            if (kind == "smooth")
            {
                bubbles[0].Left = 0;
                bubbles[1].Left = 0.5f;
                rubbing = true;
                var (c, size) = LabelOn(Front, slappedAt);
                handAt = c + bubbles[1].At * size;
            }
            return;
        }
        sincePeel = 1.1f;
        curl = 0.2f;
        seconds = 1.9f;
    }

    // Handles input for one frame. A picked label keeps sliding (and curling) even while another screen shows.
    public void Update(float dt, bool input)
    {
        if (frozen) return;
        dt = MathF.Min(dt, 1f / 30f);
        var mouse = Raylib.GetMousePosition();
        // Clicks count only on the station itself, between the ticket rail and the tab bar (Space works anywhere).
        bool onStation = mouse.Y > TicketRail.Height && mouse.Y < FloorY + 8;
        bool click = input && Raylib.IsMouseButtonPressed(MouseButton.Left) && onStation;
        bool down = input && Raylib.IsMouseButtonDown(MouseButton.Left);
        bool press = click || (input && Raylib.IsKeyPressed(KeyboardKey.Space));
        fx.Update(dt);
        UpdateBacking(dt);
        if (Slapped) slamT += dt;

        if (phase == Phase.Done)
        {
            doneTime += dt;
            if (scored != null && doneTime >= ResultDelay) { shipped = scored; scored = null; }
            return;
        }
        if (phase == Phase.Idle) return;
        if (started) seconds += dt;

        if (phase == Phase.Smooth)
        {
            Smooth(dt, mouse, down && onStation);
            lastMouse = mouse;
            return;
        }

        int pad = click ? PadAt(mouse) : -1;
        if (pad >= 0) { Pick(menu[pad]); return; }

        if (phase == Phase.Peel) Peel(mouse, click, down);
        else if (phase == Phase.Slide)
        {
            offset += dir * slideSpeed * dt;
            if (offset > Reach) { offset = Reach; dir = -1f; }
            if (offset < -Reach) { offset = -Reach; dir = 1f; }
            sincePeel += dt;
            curl = Math.Clamp((sincePeel - CurlGrace) / CurlSpan, 0f, 1f);
            if (press) Slap();
        }
    }

    // The rack pad under the mouse, or -1.
    int PadAt(Vector2 mouse)
    {
        for (int i = 0; i < menu.Length; i++)
            if (Raylib.CheckCollisionPointRec(mouse, PadRect(i))) return i;
        return -1;
    }

    // Takes a label off a pad, still on its backing, held up at the left end of the slide. Picking another pad swaps
    // it; a label already peeled is tossed (it cannot go back on its pad).
    void Pick(Strain s)
    {
        if (label == s) return;
        if (label != null && phase == Phase.Slide)
        {
            fx.Puff(HeldCenter, 10, 160f, new[] { Paper, label.Tag });
            fx.Say("Tossed it", HeldCenter + new Vector2(0, -70), Art.Text, 22);
        }
        label = s;
        offset = -Reach;
        dir = 1f;
        pull = Vector2.Zero;
        grabbing = false;
        sincePeel = curl = 0;
        started = true;
        phase = Phase.Peel;
        Audio.Play(Audio.Pluck, 0.6f, 1.1f);
    }

    // Peeling: grab the backing's tab and drag it away; once it has come PeelPull pixels the backing tears off and
    // flies away, and the label starts to slide. Letting go early leaves it half peeled, to grab again.
    void Peel(Vector2 mouse, bool click, bool down)
    {
        if (click && Raylib.CheckCollisionPointRec(mouse, TabRect))
        {
            grabbing = true;
            grabFrom = mouse - pull;
            Audio.Play(Audio.Pluck, 0.4f, 1.5f);
        }
        if (!down) grabbing = false;
        if (!grabbing) return;

        pull = mouse - grabFrom;
        if (pull.Length() < PeelPull) return;

        // Torn off: the backing flies off the way it was pulled.
        var away = Vector2.Normalize(pull);
        backPos = HeldCenter + pull;
        backVel = away * 420f + new Vector2(0, -160f);
        backAngle = 0;
        backSpin = (away.X < 0 ? -1f : 1f) * 540f;
        backLife = 1.2f;
        pull = Vector2.Zero;
        grabbing = false;
        peelSeconds = seconds;
        sincePeel = curl = 0;
        phase = Phase.Slide;
        Audio.Play(Audio.Peel, 0.8f);
    }

    // Moves the torn-off backing as it falls away.
    void UpdateBacking(float dt)
    {
        if (backLife <= 0) return;
        backLife -= dt;
        backVel.Y += Physics.Gravity * 0.6f * dt;
        backPos += backVel * dt;
        backAngle += backSpin * dt;
    }

    // Slaps the sliding label onto the front where it is: placement is fixed now, and its curl decides how many air
    // bubbles it lands with. The station shakes and the container jolts.
    void Slap()
    {
        slamT = 0;
        slappedOff = MathF.Abs(offset);
        slappedAt = new Vector2(offset / Front.Width, 0f);
        MakeBubbles();
        phase = Phase.Smooth;
        Audio.Play(Audio.Slap, 1f);

        var (center, _) = LabelOn(Front, slappedAt);
        fx.Puff(center, 12, 170f, new[] { Paper, label!.Tag });
        var pop = new Vector2(MatX, Front.Y - 70);
        int place = Scoring.PlacementFor(slappedOff);
        if (label != wantedStrain) fx.Say($"Wrong label! (they wanted {wantedStrain.Name})", pop, Art.Warn, 22);
        else if (place >= 95) fx.Say("Dead on!", pop, Art.Accent);
        else if (place >= 70) fx.Say("Nice slap", pop, Art.Accent, 26);
        else fx.Say("A bit off", pop, Art.Text, 26);
    }

    // Scatters air bubbles over the label for its curl (kept apart from each other), plus the lifted corner.
    void MakeBubbles()
    {
        bubbles.Clear();
        int n = (int)MathF.Round(curl * MaxBubbles);
        for (int i = 0; i < n; i++)
        {
            var at = Vector2.Zero;
            for (int tries = 0; tries < 12; tries++)
            {
                at = new Vector2((float)rng.NextDouble() * 0.7f - 0.35f, (float)rng.NextDouble() * 0.5f - 0.25f);
                if (bubbles.All(b => Vector2.Distance(b.At * new Vector2(3f, 1f), at * new Vector2(3f, 1f)) > 0.3f)) break;
            }
            bubbles.Add(new Bubble { At = at });
        }
        if (curl >= CornerAt) bubbles.Add(new Bubble { At = new Vector2(0.5f, 0.5f), Corner = true });
        bubbleCount = bubbles.Count;
    }

    // Smoothing: once the hand has pressed, rubbing (button held, mouse moving) over a bubble flattens it. When none
    // are left the label is done and scored.
    void Smooth(float dt, Vector2 mouse, bool down)
    {
        rubbing = down;
        handAt = mouse;
        if (slamT < SlamTime + PressTime) return;
        if (bubbles.All(b => b.Left <= 0)) { Finish(); return; }

        rubSeconds += dt;
        rubSound -= dt;
        if (!down || Vector2.Distance(mouse, lastMouse) < 0.5f) return;

        var (center, size) = LabelOn(Front, slappedAt);
        foreach (var b in bubbles)
        {
            if (b.Left <= 0) continue;
            var at = BubblePoint(center, size, b);
            float reach = RubReach + (b.Corner ? size.Y * 0.2f : 0f);
            if (Vector2.Distance(mouse, at) > reach) continue;
            b.Left -= dt / (b.Corner ? CornerRubTime : RubTime);
            if (rubSound <= 0) { Audio.Play(Audio.Rub, 0.5f); rubSound = 0.09f; }
            if (b.Left > 0) continue;
            b.Left = 0;
            Audio.Play(Audio.Tick, 0.7f, 1.6f);
            fx.Puff(at, 4, 80f, new[] { Paper });
        }
    }

    // Where a bubble is on the label: its point for rubbing (a corner's sits just inside the corner).
    static Vector2 BubblePoint(Vector2 center, Vector2 size, Bubble b) =>
        b.Corner ? center + size / 2 - new Vector2(size.Y * 0.14f) : center + b.At * size;

    // The label is flat: score it and hold the result for a moment so the finish can be seen.
    void Finish()
    {
        scored = Scoring.ScoreLabel(wantedStrain, label!, seconds, slappedOff, slideSpeed, slappedAt,
                                    peelSeconds, curl, bubbleCount, rubSeconds);
        if (bubbleCount > 0) fx.Say("Smooth", new Vector2(MatX, Front.Y - 40), Art.Accent, 22);
        doneTime = 0;
        phase = Phase.Done;
    }

    // Draws the station. The ticket holder glows while a ticket is dragged toward an idle station. After a slap the
    // whole station shakes for a moment.
    public void Draw(Customer? customer, int ticketNumber, bool dropGlow)
    {
        var mouse = Raylib.GetMousePosition();
        var shake = Shake();
        Rlgl.PushMatrix();
        Rlgl.Translatef(shake.X, shake.Y, 0);

        Art.DrawTable(FloorY);
        Art.DrawTicket(TicketRect, customer, ticketNumber, dropGlow && phase == Phase.Idle);
        if (phase != Phase.Idle)
        {
            Art.DrawLabelRack(new Rectangle(RackX - 16, RackTop - 16, PadW + 32, menu.Length * (PadH + PadGap) - PadGap + 32));
            bool picking = phase is Phase.Pick or Phase.Peel or Phase.Slide;
            for (int i = 0; i < menu.Length; i++)
                Art.DrawLabelPad(PadRect(i), menu[i], picking && Raylib.CheckCollisionPointRec(mouse, PadRect(i)));
        }

        Art.DrawMat(MatX, FloorY, false, phase == Phase.Idle, "LABEL HERE");
        if (phase != Phase.Idle)
        {
            var front = JoltedFront();
            Art.DrawPackedContainer(front, container, wantedStrain, true, fill);
            if (!Slapped) Art.DrawLabelGuide(Guide, LabelSize);
            if (label != null) DrawLabel(front, mouse);
        }
        if (backLife > 0) Art.DrawLabelBacking(backPos, HeldSize, backAngle, false);

        fx.Draw();
        Rlgl.PopMatrix();
        DrawHud();
    }

    // The station's shake offset just after a slap: strong at the hit, gone after ShakeTime.
    Vector2 Shake()
    {
        if (!Slapped || slamT < SlamTime || slamT > SlamTime + ShakeTime) return Vector2.Zero;
        float u = slamT - SlamTime, k = 1f - u / ShakeTime;
        return new Vector2(MathF.Sin(u * 97f), MathF.Cos(u * 71f) * 0.6f) * ShakePx * k * k;
    }

    // The container's front squashed by the jolt after a slap (bottom stays on the mat), wobbling back to rest.
    Rectangle JoltedFront()
    {
        var f = Front;
        if (!Slapped || slamT < SlamTime) return f;
        float u = slamT - SlamTime;
        float sq = JoltAmp * MathF.Exp(-u * JoltDecay) * MathF.Cos(u * JoltFreq);
        float w = f.Width * (1f + sq * 0.5f), h = f.Height * (1f - sq);
        return new Rectangle(f.X + (f.Width - w) / 2, f.Y + f.Height - h, w, h);
    }

    // Draws the label in its current state: held on its backing, peeling, sliding and curling, slamming down with
    // the hand, or stuck on with its bubbles; plus the hand that holds, slaps or rubs it.
    void DrawLabel(Rectangle front, Vector2 mouse)
    {
        var (land, landSize) = LabelOn(front, Slapped ? slappedAt : new Vector2(offset / Front.Width, 0f));
        if (!Slapped)
        {
            // Its shadow on the landing spot, then the backing (while on), the label, its curl and the holding hand.
            Raylib.DrawRectangleRec(new Rectangle(land.X - landSize.X / 2, land.Y - landSize.Y / 2, landSize.X, landSize.Y),
                                    new Color(0, 0, 0, 70));
            Art.DrawLabelGuide(land, landSize, label!.Tag);
            var c = HeldCenter;
            var size = HeldSize;
            if (phase == Phase.Peel)
                Art.DrawLabelBacking(c + pull, size, pull.X * -0.06f,
                                     !grabbing && Raylib.CheckCollisionPointRec(mouse, TabRect));
            Art.DrawStrainLabel(c, size, 0, label!, false, false, 0, false, netText);
            if (phase == Phase.Slide) Art.DrawLabelCurl(c, size, curl);
            Art.DrawSlapHand(HandOn(c, size), size.Y / 80f, 1f);
            return;
        }

        // Slamming down: from held to the front, speeding up, then a squash on impact that settles.
        float k = Math.Clamp(slamT / SlamTime, 0f, 1f);
        k *= k;
        var held = Guide + new Vector2(slappedAt.X * Front.Width, -HoverRise);
        var at = Vector2.Lerp(held, land, k);
        var labelSize = landSize * (HoverScale + (1f - HoverScale) * k);
        if (slamT >= SlamTime)
        {
            float u = MathF.Max(0, 1f - (slamT - SlamTime) * 4f);
            labelSize *= new Vector2(1f + 0.06f * u, 1f - 0.1f * u);
        }
        Art.DrawStrainLabel(at, labelSize, 0, label!, false, false, 0, false, netText);
        foreach (var b in bubbles)
        {
            if (b.Left <= 0) continue;
            if (b.Corner) Art.DrawLabelCorner(at, labelSize, b.Left);
            else Art.DrawLabelBubble(at + b.At * labelSize, labelSize.Y * 0.13f + 4f, b.Left);
        }

        // The hand presses, then lifts up and away, fading; while rubbing, a smaller hand follows the mouse.
        float lift = Math.Clamp((slamT - SlamTime - PressTime) / LiftTime, 0f, 1f);
        if (lift < 1f)
        {
            float pressed = slamT >= SlamTime && lift <= 0 ? 1.06f : 1f;
            var hand = HandOn(at, labelSize) + new Vector2(30f, -70f) * lift;
            Art.DrawSlapHand(hand, labelSize.Y / 80f * pressed * (1f + 0.25f * lift), 1f - lift);
        }
        if (phase == Phase.Smooth && rubbing && lift > 0)
            Art.DrawSlapHand(handAt + new Vector2(0, 8), 0.55f, 0.9f);
    }

    // Where the hand's palm sits on a label: on its top right corner, clear of the strain name.
    static Vector2 HandOn(Vector2 center, Vector2 size) => center + new Vector2(size.X * 0.5f + 4f, -size.Y * 0.18f);

    // Draws the hint for the current step and the timer.
    void DrawHud()
    {
        string hint = phase switch
        {
            Phase.Idle => "No ticket on the station. Drag a packed ticket down from the rail.",
            Phase.Pick => "Click the order's strain in the label rack.",
            Phase.Peel => "Grab the backing's tab and drag it away to peel it.\nWrong strain? Click the right pad to swap.",
            Phase.Slide => "Click to slap it on as it lines up with the corner marks.\nIt curls if you wait: curled labels land with bubbles.",
            Phase.Smooth when bubbles.Any(b => b.Left > 0) => "Hold the button and rub out the bubbles.",
            _ => "",
        };
        Art.Label(hint, 420, 150, 22, Art.Faint);
        if (phase != Phase.Idle)
            Art.Label($"Time {seconds:0.0} s", 420, 214, 20, Art.Faint);
    }
}
