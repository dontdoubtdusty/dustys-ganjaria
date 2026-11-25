using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The label station: the closed container from Pack stands on the mat. Pick the label for the order's strain off
// its pad in the rack and carry it over: it hangs from the finger and swings with the mouse, so a fast move tilts
// it. Let go over the container and it sticks on loosely, just as it was (crooked if it was still swinging). Hold
// the mouse still on it to press it down, which finishes the order; drag a loose label instead to peel it off and
// place it again. The shop hands it each ticket with Start and collects the result with TakeShipped.
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
    // Where the finger holds a carried label: this far below its top edge.
    const float HoldFromTop = 12f;
    // The swing: degrees of tilt per pixel a second of sideways mouse speed (capped at MaxTilt), and the spring
    // that pulls it back toward that tilt (stiffness and damping; it overshoots a little, then settles).
    const float TiltPerSpeed = 0.03f, MaxTilt = 35f, SwingStiffness = 120f, SwingDamping = 9f;
    // Pressing: seconds of holding still on a loose label to press it down. Moving further than PeelDistance
    // while holding it peels it off.
    const float PressHold = 0.35f, PeelDistance = 8f;
    // Pause between pressing the label down and the order counting as done, so the finish can be seen.
    const float ResultDelay = 0.9f;

    enum Phase { Idle, Work, Done }
    enum Hold { None, Carry, Press }

    readonly Effects fx;
    Phase phase;
    Hold hold;
    Strain wantedStrain = Strains.All[0];
    Container container;
    Strain[] menu = Array.Empty<Strain>();

    // The label in play (null for none): its strain, centre and tilt (degrees, clockwise), and whether it is stuck
    // on loosely or pressed down.
    Strain? label;
    Vector2 labelPos;
    float angle, angleVel;
    bool loose, pressed;

    Vector2 pressStart, lastMouse, mouseVel;
    float pressTime, seconds, doneTime, messageTime;
    bool started;
    int peels;
    string message = "";
    LabelResult? scored, shipped;

    public LabelStation(Random rng)
    {
        fx = new Effects(rng);
        Clear();
    }

    // True when the station has no ticket and can take the next one.
    public bool IsIdle => phase == Phase.Idle;

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

    static Rectangle PadRect(int i) => new(RackX, RackTop + i * (PadH + PadGap), PadW, PadH);

    // Starts a ticket: the closed container from Pack on the mat, and a label pad for each strain on the menu.
    public void Start(Customer c, Container used, Strain[] dayMenu)
    {
        Reset();
        wantedStrain = c.Strain;
        container = used;
        menu = dayMenu;
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
        hold = Hold.None;
        label = null;
        loose = pressed = false;
        angle = angleVel = pressTime = seconds = 0;
        started = false;
        peels = 0;
        scored = shipped = null;
        messageTime = 0;
        fx.Clear();
    }

    // Live values for the F1 tuning panel: the strain wanted and held, the swing, how far off the guide it is and
    // the placement that would score, the press, and time against par.
    public List<TuningLine> TuningLines()
    {
        if (phase == Phase.Idle) return new() { new("No ticket on the station.") };
        var lines = new List<TuningLine>
        {
            new($"Wants {wantedStrain.Name}, holding: {label?.Name ?? "nothing"}", label != null && label != wantedStrain),
        };
        if (label != null)
        {
            float off = (labelPos - Guide).Length();
            int place = Scoring.PlacementFor(off, angle);
            float target = Math.Clamp(mouseVel.X * TiltPerSpeed, -MaxTilt, MaxTilt);
            lines.Add(new($"Tilt {angle:0.0} deg (swinging toward {target:0.0}), full marks within {Scoring.AngleFull:0}",
                          MathF.Abs(angle) > Scoring.AngleFull));
            lines.Add(new($"Off the guide {off:0} px, full marks within {Scoring.OffFull:0}", off > Scoring.OffFull));
            lines.Add(new($"Placement if pressed now: {place}", place < 90));
            lines.Add(new(hold == Hold.Press ? $"Pressing {pressTime:0.00} of {PressHold:0.00} s" : loose ? "Stuck on loosely" : "Carrying"));
        }
        lines.Add(new($"Time {seconds:0.0} s, par {Scoring.LabelPar:0.0} s, peels {peels}", seconds > Scoring.LabelPar));
        return lines;
    }

    // Hands over the labeled order's result once.
    public LabelResult? TakeShipped()
    {
        var done = shipped;
        shipped = null;
        return done;
    }

    // Drops whatever the mouse is holding (the player switched screens): a carried label is let go where it is,
    // a press stops. Pausing (pausing = true) puts a carried label back on its pad instead, at no cost.
    public void LetGo(bool pausing = false)
    {
        if (hold == Hold.Carry && pausing) label = null;
        else if (hold == Hold.Carry) Release();
        hold = Hold.None;
        pressTime = 0;
    }

    // Sets up the station for the --shot-label layout check: the right label stuck on loosely, a little off and
    // a little crooked.
    public void StageForScreenshot()
    {
        label = wantedStrain;
        loose = true;
        angle = 4f;
        labelPos = Guide + new Vector2(10, -6);
        seconds = 3.1f;
        started = true;
    }

    // Handles input for one frame.
    public void Update(float dt, bool input)
    {
        dt = MathF.Min(dt, 1f / 30f);
        var mouse = Raylib.GetMousePosition();
        bool press = input && Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool release = input && Raylib.IsMouseButtonReleased(MouseButton.Left);
        bool down = input && Raylib.IsMouseButtonDown(MouseButton.Left);
        if (dt > 0) mouseVel = Vector2.Lerp(mouseVel, (mouse - lastMouse) / dt, 0.5f);
        lastMouse = mouse;
        if (messageTime > 0) messageTime -= dt;
        fx.Update(dt);

        if (phase == Phase.Done)
        {
            doneTime += dt;
            if (scored != null && doneTime >= ResultDelay) { shipped = scored; scored = null; }
            return;
        }
        if (phase == Phase.Idle) return;

        if (started) seconds += dt;
        if (press && hold == Hold.None) Begin(mouse);

        if (hold == Hold.Carry) UpdateCarry(mouse, release, dt);
        else if (hold == Hold.Press) UpdatePress(mouse, down, dt);
    }

    // Works out what a fresh press does: on a loose label it starts pressing it down; on a pad it takes a label.
    void Begin(Vector2 mouse)
    {
        if (label != null && loose)
        {
            if (OnLabel(mouse))
            {
                hold = Hold.Press;
                pressStart = mouse;
                pressTime = 0;
                return;
            }
            for (int i = 0; i < menu.Length; i++)
                if (Raylib.CheckCollisionPointRec(mouse, PadRect(i))) { Say("Peel the label off first: drag it"); return; }
            return;
        }

        for (int i = 0; i < menu.Length; i++)
        {
            if (!Raylib.CheckCollisionPointRec(mouse, PadRect(i))) continue;
            label = menu[i];
            loose = false;
            hold = Hold.Carry;
            angle = angleVel = 0;
            labelPos = CenterFrom(mouse, angle);
            started = true;
            Audio.Play(Audio.Pluck, 0.6f, 1.1f);
            return;
        }
    }

    // A carried label hangs from the mouse and swings toward a tilt set by how fast the mouse moves sideways.
    void UpdateCarry(Vector2 mouse, bool release, float dt)
    {
        float tilt = Math.Clamp(mouseVel.X * TiltPerSpeed, -MaxTilt, MaxTilt);
        angleVel += ((tilt - angle) * SwingStiffness - angleVel * SwingDamping) * dt;
        angle += angleVel * dt;
        labelPos = CenterFrom(mouse, angle);
        if (release) Release();
    }

    // Lets go of the carried label: over the container it sticks on loosely just as it hangs; anywhere else it is
    // thrown away.
    void Release()
    {
        hold = Hold.None;
        if (Raylib.CheckCollisionPointRec(labelPos, Front))
        {
            loose = true;
            angleVel = 0;
            Audio.Play(Audio.Tick, 0.7f, 0.7f);
            return;
        }
        label = null;
        Say("Missed the container");
    }

    // Holding still on a loose label presses it down; moving off peels it back up into the hand.
    void UpdatePress(Vector2 mouse, bool down, float dt)
    {
        if (Vector2.Distance(mouse, pressStart) > PeelDistance)
        {
            peels++;
            loose = false;
            hold = Hold.Carry;
            pressTime = 0;
            Audio.Play(Audio.Crinkle, 0.5f, 1.3f);
            return;
        }
        if (!down) { hold = Hold.None; pressTime = 0; return; }
        pressTime += dt;
        if (pressTime >= PressHold) Stick();
    }

    // The label is pressed down: score it and hold the result for a moment so the finish can be seen.
    void Stick()
    {
        hold = Hold.None;
        loose = false;
        pressed = true;
        var front = Front;
        var offset = labelPos - Guide;
        var at = new Vector2(offset.X / front.Width, offset.Y / front.Height);
        scored = Scoring.ScoreLabel(wantedStrain, label!, seconds, offset.Length(), angle, peels, at);
        Audio.Play(Audio.Seal, 0.7f, 1.2f);

        var pop = new Vector2(MatX, front.Y - 70);
        if (label != wantedStrain) fx.Say($"Wrong label! (they wanted {wantedStrain.Name})", pop, Art.Warn, 22);
        else if (scored.Value.Placement >= 90) fx.Say("Nice and straight!", pop, Art.Accent);
        else fx.Say("Stuck on", pop, Art.Text, 26);
        doneTime = 0;
        phase = Phase.Done;
    }

    // Where a label held at the mouse has its centre: below the finger, swung round by its tilt.
    Vector2 CenterFrom(Vector2 finger, float degrees)
    {
        float d = LabelSize.Y / 2 - HoldFromTop;
        float a = degrees * MathF.PI / 180f;
        return finger + new Vector2(-d * MathF.Sin(a), d * MathF.Cos(a));
    }

    // True if a point is on the (turned) label.
    bool OnLabel(Vector2 p)
    {
        float a = -angle * MathF.PI / 180f;
        var d = p - labelPos;
        var local = new Vector2(d.X * MathF.Cos(a) - d.Y * MathF.Sin(a), d.X * MathF.Sin(a) + d.Y * MathF.Cos(a));
        var half = LabelSize / 2;
        return MathF.Abs(local.X) <= half.X + 4 && MathF.Abs(local.Y) <= half.Y + 4;
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

        bool picking = phase == Phase.Work && hold == Hold.None && label == null;
        if (phase != Phase.Idle)
        {
            Art.DrawLabelRack(new Rectangle(RackX - 16, RackTop - 16, PadW + 32, menu.Length * (PadH + PadGap) - PadGap + 32));
            for (int i = 0; i < menu.Length; i++)
                Art.DrawLabelPad(PadRect(i), menu[i], picking && Raylib.CheckCollisionPointRec(mouse, PadRect(i)));
        }

        Art.DrawMat(MatX, FloorY, false, phase == Phase.Idle, "LABEL HERE");
        if (phase != Phase.Idle)
        {
            Art.DrawPackedContainer(Front, container, wantedStrain, true);
            if (!pressed) Art.DrawLabelGuide(Guide, LabelSize);
            if (label != null)
            {
                bool hover = loose && hold == Hold.None && OnLabel(mouse);
                float press = hold == Hold.Press ? pressTime / PressHold : 0f;
                Art.DrawStrainLabel(labelPos, LabelSize, angle, label, hold == Hold.Carry, loose, press, hover);
            }
        }

        fx.Draw();
        DrawHud();
    }

    // Draws the hint for the current step, the timer and peels, and any short message.
    void DrawHud()
    {
        string hint = phase switch
        {
            Phase.Idle => "No ticket on the station. Drag a packed ticket down from the rail.",
            Phase.Work when loose => "Hold the mouse on the label to press it down.\nCrooked? Drag it to peel it off and place it again.",
            Phase.Work => "Take the order's strain label off the rack and carry it onto the container.\nIt swings as you move: hold still before you let go.",
            _ => "",
        };
        Art.Label(hint, 420, 150, 22, Art.Faint);
        if (phase != Phase.Idle)
            Art.Label($"Time {seconds:0.0} s    Peels {peels}", 420, 214, 20, Art.Faint);
        if (messageTime > 0) Art.LabelCentered(message, MatX, 262, 26, Art.Warn);
    }
}
