using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// Short-lived feedback a station can spawn: bits that puff out and fall (particles) and words that float
// up and fade (pop-ups, like "In the green!"). Purely visual: nothing here changes the game.
sealed class Effects
{
    sealed class Particle
    {
        public Vector2 Pos, Vel;
        public float Life, MaxLife, Size;
        public Color Color;
    }

    sealed class Popup
    {
        public string Text = "";
        public Vector2 Pos;
        public float Life;
        public Color Color;
        public int Size;
    }

    const float PopupSeconds = 1.1f;
    const float PopupRise = 60f;

    readonly Random rng;
    readonly List<Particle> particles = new();
    readonly List<Popup> popups = new();

    public Effects(Random rng) => this.rng = rng;

    // Throws out a few small bits from a point, mostly upward, in the given colours.
    public void Puff(Vector2 at, int count, float speed, Color[] colors)
    {
        for (int i = 0; i < count; i++)
        {
            float a = -MathF.PI / 2 + Range(-1.3f, 1.3f);
            float life = Range(0.35f, 0.7f);
            particles.Add(new Particle
            {
                Pos = at,
                Vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * speed * Range(0.4f, 1f),
                Life = life,
                MaxLife = life,
                Size = Range(1.5f, 3.5f),
                Color = colors[rng.Next(colors.Length)],
            });
        }
    }

    // Floats a word up from a point.
    public void Say(string text, Vector2 at, Color color, int size = 28)
    {
        popups.Add(new Popup { Text = text, Pos = at, Life = PopupSeconds, Color = color, Size = size });
    }

    // Clears everything (a new ticket).
    public void Clear()
    {
        particles.Clear();
        popups.Clear();
    }

    // Moves and ages everything; anything past its life is removed.
    public void Update(float dt)
    {
        foreach (var p in particles)
        {
            p.Vel.Y += Physics.Gravity * 0.5f * dt;
            p.Pos += p.Vel * dt;
            p.Life -= dt;
        }
        particles.RemoveAll(p => p.Life <= 0);

        foreach (var p in popups) p.Life -= dt;
        popups.RemoveAll(p => p.Life <= 0);
    }

    // Draws the particles, then the pop-ups on top. Pop-ups pop in slightly large, then rise and fade.
    public void Draw()
    {
        foreach (var p in particles)
        {
            var c = p.Color;
            c.A = (byte)(255 * Math.Clamp(p.Life / p.MaxLife * 1.5f, 0f, 1f));
            Raylib.DrawCircleV(p.Pos, p.Size, c);
        }

        foreach (var p in popups)
        {
            float age = PopupSeconds - p.Life;
            float grow = age < 0.12f ? 1.35f - 0.35f * age / 0.12f : 1f;
            float fade = Math.Clamp(p.Life / 0.35f, 0f, 1f);
            int size = (int)(p.Size * grow);
            var c = p.Color;
            c.A = (byte)(255 * fade);
            var shadow = new Color(0, 0, 0, (int)(160 * fade));
            float y = p.Pos.Y - PopupRise * MathF.Sqrt(age / PopupSeconds);
            Art.LabelCentered(p.Text, p.Pos.X + 2, y + 2, size, shadow);
            Art.LabelCentered(p.Text, p.Pos.X, y, size, c);
        }
    }

    float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
}
