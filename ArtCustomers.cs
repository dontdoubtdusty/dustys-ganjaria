using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Raylib_cs;

namespace DustysGanjaria;

// Art part 7: the customer cast, cut by tools/Cutter (roster --flat). resources/sprites/customer-roster.png holds one
// cell per pose, all at one scale with their feet on the cell's ground line; customer-roster.json says where each
// one's feet, head top and face are. A pose's name is "<character>@<pose>": the character starts with its kind ("f-"
// or "m-", for the customer's name) and the person ("m-moustache-blue" and "m-moustache-rust" would be one person in
// other clothes); the poses are calm (standing, neutral face), happy, cross and hmm (the same pose with another face)
// and action (something they do while they wait). The art faces screen right; it is mirrored when the customer faces
// left. Without the files every customer stays on the drawn placeholder.
static partial class Art
{
    sealed record Pose(Rectangle Cell, int FeetX, int FeetY, int HeadTop, int HeadX)
    {
        public int Height => FeetY - HeadTop;
    }

    sealed class CastMember(string name, Dictionary<string, Pose> poses)
    {
        public string Name => name;
        public bool Feminine => name.StartsWith("f-");
        // The person, without the last word of the name (what tells this drawing apart: the clothes).
        public string Person => name.Contains('-') ? name[..name.LastIndexOf('-')] : name;
        // The calm pose sets the character's height, name tag and ID photo, whatever pose is showing.
        public Pose Calm => poses.TryGetValue("calm", out var p) ? p : poses.Values.First();
        public int Height => Calm.Height;
        public Pose PoseOr(string pose) => poses.TryGetValue(pose, out var p) ? p : Calm;
        public bool Has(string pose) => poses.ContainsKey(pose);
    }

    static Texture2D cast;
    static CastMember[] members = Array.Empty<CastMember>();
    static float castRefHeight, castFaceAhead;

    // Placeholder figures stand this tall from the feet to the top of the head, at scale 1.
    const float FigureHeight = HeadHeight + HeadRadius;

    // Motion, all in placeholder units (scaled by s). Tuning.
    const float WalkBob = 5f, WalkRate = 9f, WalkTilt = 1.5f;   // hop height, steps per second-ish, sway degrees
    const float Breathe = 0.012f, BreatheRate = 2.1f;           // idle stretch (of the height) and speed
    const float HopHeight = 16f, HopTime = 0.45f;               // the hop when someone turns happy
    const float ActionEvery = 11f, ActionFor = 3.5f;            // waiting customers do their action this long, this often
    const float CrossBelow = 0.3f;                              // patience fraction below which they look cross
    const float ShotLift = 0.7f;                                // in the shots, how much of a height gap is made up

    // The pose each customer showed last frame and when it started (for the hop).
    sealed class PoseState { public string Pose = "calm"; public double Since; }
    static readonly ConditionalWeakTable<Customer, PoseState> poseStates = new();

    // How old each character looks, in years (lowest, highest), so their ID's birth date fits them. Only someone who
    // could pass for under 21 (lowest age 20 or less) can hold an underage ID. Characters missing here get any age.
    static readonly Dictionary<string, (int Min, int Max)> Ages = new()
    {
        ["m-beard-orange"] = (28, 40),   ["f-phone-pink"] = (19, 27),     ["f-granny-teal"] = (72, 86),
        ["m-cap-teal"] = (32, 45),       ["m-wallet-vest"] = (48, 60),    ["m-cane-cap"] = (70, 84),
        ["f-overalls-blue"] = (19, 26),  ["f-scrubs-blue"] = (29, 40),    ["m-skater-beanie"] = (18, 24),
        ["f-track-ponytail"] = (38, 48), ["m-suit-moustache"] = (42, 56), ["f-shawl-red"] = (50, 62),
    };

    // How old a cast member looks (lowest, highest years), or null for placeholders and unlisted characters.
    public static (int Min, int Max)? AgeRange(int body) =>
        body >= 0 && body < members.Length && Ages.TryGetValue(members[body].Name, out var a) ? a : null;

    // True when an underage ID is believable for this customer: they look young enough, or they are a placeholder.
    public static bool CouldBeUnder21(int body) => AgeRange(body) is not { } a || a.Min <= Ids.LegalAge - 1;

    // How many cast members have sprites (0 = placeholders only).
    public static int CustomerBodyCount => members.Length;

    // True when the cast member reads as a woman (for the name the customer gets).
    public static bool BodyIsFeminine(int body) => body >= 0 && body < members.Length && members[body].Feminine;

    // A random cast member who is not one of the people already there (nor them in other clothes); anyone when all
    // are there.
    // Set by screenshot runs: the cast member every customer is (character name, no pose).
    public static string? ForceBody;

    public static int PickBody(Random rng, IEnumerable<int> there)
    {
        // Screenshot runs can ask for one cast member ("--body f-granny-teal"): every customer is them.
        int forced = ForceBody == null ? -1 : Array.FindIndex(members, m => m.Name.StartsWith(ForceBody + "@") || m.Name == ForceBody);
        if (forced >= 0) return forced;
        var people = there.Where(b => b >= 0 && b < members.Length).Select(b => members[b].Person).ToHashSet();
        var free = Enumerable.Range(0, members.Length).Where(i => !people.Contains(members[i].Person)).ToArray();
        return free.Length > 0 ? free[rng.Next(free.Length)] : rng.Next(members.Length);
    }

    // A cast member clearly not the given one, for an ID photo that does not match: another person (never the same
    // person in other clothes), of the same kind when there is one.
    public static int OtherBody(int body, Random rng)
    {
        if (body < 0 || body >= members.Length) return -1;
        var me = members[body];
        var others = Enumerable.Range(0, members.Length).Where(i => members[i].Person != me.Person).ToArray();
        var same = others.Where(i => members[i].Feminine == me.Feminine).ToArray();
        var pick = same.Length > 0 ? same : others;
        return pick.Length > 0 ? pick[rng.Next(pick.Length)] : -1;
    }

    static void LoadCustomerSprites()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "resources", "sprites");
        string png = Path.Combine(dir, "customer-roster.png"), json = Path.Combine(dir, "customer-roster.json");
        if (!File.Exists(png) || !File.Exists(json)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(json));
            var r = doc.RootElement;
            int cellW = r.GetProperty("cellW").GetInt32(), cellH = r.GetProperty("cellH").GetInt32();
            int cols = r.GetProperty("columns").GetInt32();
            // Characters in the order they first appear; each gathers its poses.
            var order = new List<string>();
            var poses = new Dictionary<string, Dictionary<string, Pose>>();
            int i = 0;
            foreach (var m in r.GetProperty("characters").EnumerateArray())
            {
                string full = m.GetProperty("name").GetString()!;
                int at = full.IndexOf('@');
                string who = at < 0 ? full : full[..at], pose = at < 0 ? "calm" : full[(at + 1)..];
                if (!poses.ContainsKey(who)) { poses[who] = new(); order.Add(who); }
                poses[who][pose] = new Pose(new Rectangle(i % cols * cellW, i / cols * cellH, cellW, cellH),
                                            m.GetProperty("feetX").GetInt32(), m.GetProperty("feetY").GetInt32(),
                                            m.GetProperty("headTop").GetInt32(), m.GetProperty("headX").GetInt32());
                i++;
            }
            if (order.Count == 0) return;
            cast = Raylib.LoadTexture(png);
            if (cast.Id == 0) return;
            // Mipmaps keep the art smooth at the small sizes in the shop; empty pixels carry the art's darkest
            // colour at zero alpha, so the edges blend toward it, not black.
            Raylib.GenTextureMipmaps(ref cast);
            Raylib.SetTextureFilter(cast, TextureFilter.Trilinear);
            var list = order.Select(w => new CastMember(w, poses[w])).ToList();
            // One scale for everyone (so tall people stay taller): the average cast member, head top to feet, is
            // drawn as tall as a placeholder figure.
            castRefHeight = (float)list.Average(m => m.Height);
            // How far the average face sits ahead of the feet (calm pose, facing right), for lining faces up in the shots.
            castFaceAhead = (float)list.Average(m => m.Calm.HeadX - m.Calm.FeetX);
            members = list.ToArray();
        }
        catch (Exception) { members = Array.Empty<CastMember>(); }
    }

    static void UnloadCustomerSprites()
    {
        if (cast.Id != 0) Raylib.UnloadTexture(cast);
        cast = default;
        members = Array.Empty<CastMember>();
    }

    static CastMember? MemberOf(int body) => body >= 0 && body < members.Length && cast.Id != 0 ? members[body] : null;

    // A steady per-customer number in [0, 1), so customers do not all bob, breathe and fidget in step.
    static float PhaseOf(Customer c) => (RuntimeHelpers.GetHashCode(c) & 1023) / 1024f;

    // Which pose a customer shows: the bag check's "hmm", the hand-over reaction, happy or cross on the way out, cross
    // when patience runs low, now and then their action while they stand waiting, else calm.
    static string PoseFor(CastMember m, Customer c, bool moving)
    {
        if (c.Inspecting) return "hmm";
        if (c.Mood is float mood) return mood > 0.5f ? "happy" : mood > 0.25f ? "calm" : "cross";
        if (c.Now == Customer.State.Leaving) return c.Served ? "happy" : "cross";
        if (c.Now == Customer.State.Ordering) return "calm";
        if (c.PatienceFraction < CrossBelow) return "cross";
        if (!moving && m.Has("action"))
        {
            double t = Raylib.GetTime() + PhaseOf(c) * ActionEvery;
            if (t % ActionEvery < ActionFor) return "action";
        }
        return "calm";
    }

    // Draws a customer's sprite with its feet at feet, at the placeholder's scale s, facing left or right, in the
    // pose that fits the moment, walking, breathing or hopping. inShot lifts short people (and lowers tall ones) part
    // of the way to the average head height, so the counter in the order and hand-over shots never hides a face, and
    // slides them sideways so their face is where the average face is (a stooped or leaning person would otherwise
    // put their face behind the notepad or the score card).
    // Returns the screen y of the top of their head (hair included), or null when they have no sprite.
    static float? DrawCustomerSprite(Customer c, Vector2 feet, float s, bool faceLeft, bool inShot = false)
    {
        if (MemberOf(c.Body) is not { } m) return null;
        float k = FigureHeight * s / castRefHeight;
        if (inShot)
        {
            feet.Y -= (castRefHeight - m.Height) * k * ShotLift;
            feet.X -= (m.Calm.HeadX - m.Calm.FeetX - castFaceAhead) * k * (faceLeft ? -1 : 1);
        }

        bool moving = !inShot && Vector2.Distance(c.Pos, c.Goal) > 1f;
        string name = PoseFor(m, c, moving);
        var state = poseStates.GetOrCreateValue(c);
        double now = Raylib.GetTime();
        if (state.Pose != name) { state.Pose = name; state.Since = now; }
        var pose = m.PoseOr(name);

        float t = (float)now, ph = PhaseOf(c) * MathF.Tau;
        float lift = 0, tilt = 0, stretch = 1;
        if (moving)
        {
            float step = MathF.Sin(t * WalkRate + ph);
            lift = MathF.Abs(step) * WalkBob * s;
            tilt = step * WalkTilt * (faceLeft ? -1 : 1);
        }
        else stretch = 1 + Breathe * MathF.Sin(t * BreatheRate + ph);
        float since = (float)(now - state.Since);
        if (name == "happy" && since < HopTime) lift += MathF.Sin(MathF.PI * since / HopTime) * HopHeight * s;

        var cell = pose.Cell;
        var src = new Rectangle(cell.X, cell.Y, faceLeft ? -cell.Width : cell.Width, cell.Height);
        float fx = faceLeft ? cell.Width - pose.FeetX : pose.FeetX;
        float w = cell.Width * k * (2 - stretch), h = cell.Height * k * stretch;
        // Drawn around the feet, so the sway and the breathing pivot on the ground.
        var dest = new Rectangle(feet.X, feet.Y - lift, w, h);
        var origin = new Vector2(fx * k * (2 - stretch), pose.FeetY * k * stretch);
        Raylib.DrawTexturePro(cast, src, dest, origin, tilt, Color.White);
        return feet.Y - m.Height * k;
    }

    // The screen y of the top of a sprite customer's head (hair included, calm pose), or null without a sprite.
    static float? SpriteTop(Customer c, Vector2 feet, float s)
    {
        if (MemberOf(c.Body) is not { } m) return null;
        return feet.Y - m.Height * FigureHeight * s / castRefHeight;
    }

    // Where a speech bubble goes for a customer (the point DrawSpeech takes): above their name, which sits above the
    // top of their head, so tall hair lifts the bubble with it.
    public static Vector2 SpeechAnchor(Customer c, Vector2 feet, float s)
    {
        var head = HeadOf(feet, s);
        float top = SpriteTop(c, feet, s) ?? head.Y - HeadRadius * s;
        return new Vector2(head.X, MathF.Min(head.Y, top + HeadRadius));
    }

    // Draws a cast member's head and shoulders filling the photo box, like an ID photo (calm pose). False without a
    // sprite.
    static bool DrawIdPhotoSprite(Rectangle photo, int body)
    {
        if (MemberOf(body) is not { } m) return false;
        var p = m.Calm;
        // The crop: the top 40% of the figure, a little room above the head, the photo's shape, centred on the face
        // and kept inside the cell.
        float h = p.Height * 0.40f, w = h * photo.Width / photo.Height;
        float x = Math.Clamp(p.HeadX - w / 2, 0, p.Cell.Width - w), y = Math.Max(0, p.HeadTop - p.Height * 0.03f);
        var src = new Rectangle(p.Cell.X + x, p.Cell.Y + y, w, h);
        Raylib.DrawTexturePro(cast, src, photo, Vector2.Zero, 0, Color.White);
        return true;
    }
}
