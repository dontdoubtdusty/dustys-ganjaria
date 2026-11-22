using Raylib_cs;

namespace DustysGanjaria;

// Every sound in the game, by name. Each sound is made in code (a placeholder); a file at
// resources/sounds/<name>.wav replaces it without code changes. Each sound has a few copies (aliases), so it
// can overlap itself, and every play gets a small random pitch change so repeats do not sound robotic.
static class Audio
{
    public const string Doorbell = "doorbell", Tick = "tick", Pluck = "pluck", Ding = "ding", ZipTooth = "zip",
        Seal = "seal", Slip = "slip", Thump = "thump", Pop = "pop", Till = "till",
        Crinkle = "crinkle", Chime = "chime", Coin = "coin", Fanfare = "fanfare";

    const string SoundFolder = "resources/sounds";
    const int Copies = 6;
    const int Rate = 44100;
    // Random pitch change on every play: up to this share either way.
    const float PitchJitter = 0.06f;

    // One loaded sound: the original and its copies, played round-robin.
    sealed class Bank
    {
        public Sound Main;
        public Sound[] Copies = Array.Empty<Sound>();
        public int Next;
    }

    static readonly Dictionary<string, Bank> banks = new();
    static readonly Random rng = new();
    static bool ready;

    // Opens the audio device and loads every sound; the game stays silent if there is no audio device.
    public static void Load()
    {
        Raylib.InitAudioDevice();
        if (!Raylib.IsAudioDeviceReady()) return;

        Add(Doorbell, MakeDoorbell);
        Add(Tick, MakeTick);
        Add(Pluck, MakePluck);
        Add(Ding, MakeDing);
        Add(ZipTooth, MakeZipTooth);
        Add(Seal, MakeSeal);
        Add(Slip, MakeSlip);
        Add(Thump, MakeThump);
        Add(Pop, MakePop);
        Add(Till, MakeTill);
        Add(Crinkle, MakeCrinkle);
        Add(Chime, MakeChime);
        Add(Coin, MakeCoin);
        Add(Fanfare, MakeFanfare);
        ready = true;
    }

    // Plays a sound at a volume (0-1) and pitch (1 = as made), with a little random pitch on top.
    public static void Play(string name, float volume = 1f, float pitch = 1f)
    {
        if (!ready || !banks.TryGetValue(name, out var bank)) return;
        var s = bank.Copies[bank.Next];
        bank.Next = (bank.Next + 1) % bank.Copies.Length;
        float jitter = 1f + PitchJitter * (2f * (float)rng.NextDouble() - 1f);
        Raylib.SetSoundVolume(s, Math.Clamp(volume, 0f, 1f));
        Raylib.SetSoundPitch(s, pitch * jitter);
        Raylib.PlaySound(s);
    }

    // Frees every sound and closes the audio device.
    public static void Unload()
    {
        if (!ready) return;
        foreach (var bank in banks.Values)
        {
            foreach (var c in bank.Copies) Raylib.UnloadSoundAlias(c);
            Raylib.UnloadSound(bank.Main);
        }
        Raylib.CloseAudioDevice();
    }

    // Loads one sound from its file if there is one, or from the code-made placeholder, plus its copies.
    static void Add(string name, Func<float[]> make)
    {
        string path = Path.Combine(AppContext.BaseDirectory, SoundFolder, name + ".wav");
        var wave = File.Exists(path) ? Raylib.LoadWave(path) : Raylib.LoadWaveFromMemory(".wav", ToWav(make()));
        var bank = new Bank { Main = Raylib.LoadSoundFromWave(wave) };
        Raylib.UnloadWave(wave);
        bank.Copies = Enumerable.Range(0, Copies).Select(_ => Raylib.LoadSoundAlias(bank.Main)).ToArray();
        banks[name] = bank;
    }

    // ---------- Placeholder sounds, made in code ----------

    // Makes a buffer of the given length from a function of time (seconds) and a shared noise source.
    static float[] Synth(float seconds, Func<float, Func<float>, float> sample)
    {
        var noiseRng = new Random(5);
        float Noise() => 2f * (float)noiseRng.NextDouble() - 1f;
        var buf = new float[(int)(Rate * seconds)];
        for (int i = 0; i < buf.Length; i++) buf[i] = sample(i / (float)Rate, Noise);
        return buf;
    }

    static float Sine(float freq, float t) => MathF.Sin(2 * MathF.PI * freq * t);
    static float Decay(float t, float rate) => MathF.Exp(-t * rate);
    static float Attack(float t, float seconds = 0.003f) => MathF.Min(1f, t / seconds);

    // A two-note shop door chime (E then C), each a struck bell with fading overtones.
    static float[] MakeDoorbell() => Synth(1.7f, (t, _) =>
    {
        float Bell(float start, float f)
        {
            float u = t - start;
            if (u < 0) return 0;
            return Attack(u, 0.005f) * (Sine(f, u) * Decay(u, 3f)
                   + 0.35f * Sine(f * 2.76f, u) * Decay(u, 7f) + 0.15f * Sine(f * 5.4f, u) * Decay(u, 12f));
        }
        return Bell(0f, 659.25f) + Bell(0.38f, 523.25f);
    });

    // A soft, woody tick: a bud landing. A low knock plus a very short burst of noise.
    static float[] MakeTick()
    {
        float low = 0;
        return Synth(0.09f, (t, noise) =>
        {
            low += (noise() - low) * 0.25f; // smoothed noise: a duller, softer click
            return Attack(t, 0.001f) * (0.8f * Sine(320, t) * Decay(t, 60) + 0.6f * low * Decay(t, 90));
        });
    }

    // A dry rustle: picking a bud out of the jar or a bag off the stack.
    static float[] MakePluck()
    {
        float low = 0;
        return Synth(0.14f, (t, noise) =>
        {
            low += (noise() - low) * 0.5f;
            float flutter = 0.6f + 0.4f * Sine(70, t);
            return Attack(t, 0.01f) * low * flutter * Decay(t, 22);
        });
    }

    // A small bright bell: the needle has come to rest in the green.
    static float[] MakeDing() => Synth(0.6f, (t, _) =>
        Attack(t) * (Sine(1318.5f, t) * Decay(t, 7) + 0.3f * Sine(1318.5f * 2.4f, t) * Decay(t, 14)));

    // One zip tooth clicking shut: a tiny, sharp click.
    static float[] MakeZipTooth() => Synth(0.03f, (t, noise) =>
        Attack(t, 0.0005f) * (0.6f * noise() + 0.6f * Sine(2400, t)) * Decay(t, 220));

    // The zip pressing fully closed: two quick clicks and a soft press.
    static float[] MakeSeal() => Synth(0.22f, (t, noise) =>
    {
        float Click(float start) { float u = t - start; return u < 0 ? 0 : (0.7f * noise() + Sine(1800, u)) * Decay(u, 160); }
        return Click(0) + Click(0.06f) + 0.7f * Sine(140, t) * Decay(t, 18) * Attack(t, 0.01f);
    });

    // The zip ripping open: a falling buzz of clicks.
    static float[] MakeSlip() => Synth(0.28f, (t, noise) =>
    {
        float rate = 90 - 200 * t;  // clicks per second, slowing as it opens
        float buzz = Sine(rate, t) > 0.6f ? noise() : 0;
        return 0.8f * buzz * Decay(t, 6) * Attack(t, 0.005f);
    });

    // A soft thump: the bag landing on the table or in the bin.
    static float[] MakeThump()
    {
        float low = 0;
        return Synth(0.3f, (t, noise) =>
        {
            low += (noise() - low) * 0.08f;
            float f = 110 - 50 * t;
            return Attack(t, 0.002f) * (Sine(f, t) * Decay(t, 14) + 1.5f * low * Decay(t, 30));
        });
    }

    // A bright rising blip: a clean throw.
    static float[] MakePop() => Synth(0.16f, (t, _) =>
    {
        float f = 600 + 2600 * t;
        return Attack(t, 0.002f) * MathF.Sin(2 * MathF.PI * f * t) * Decay(t, 18);
    });

    // A till: a short drawer rattle and two bright bells.
    static float[] MakeTill() => Synth(0.9f, (t, noise) =>
    {
        float Bell(float start, float f)
        {
            float u = t - start;
            return u < 0 ? 0 : Attack(u) * (Sine(f, u) + 0.4f * Sine(f * 2.1f, u)) * Decay(u, 6);
        }
        float rattle = t < 0.12f ? noise() * 0.4f * Decay(t, 20) : 0;
        return rattle + Bell(0.08f, 2093f) + 0.8f * Bell(0.18f, 2637f);
    });

    // A plastic bag crinkle: the customer picking up the bag in the hand-over. Random sharp crackles of bright
    // noise over a faint rustle, swelling and fading over half a second.
    static float[] MakeCrinkle()
    {
        const float seconds = 0.55f;
        var crackles = new Random(11);
        float low = 0, age = 1f, amp = 0f;
        return Synth(seconds, (t, noise) =>
        {
            float env = MathF.Sqrt(MathF.Sin(MathF.PI * t / seconds));
            if (crackles.NextDouble() < 140.0 * env / Rate) { age = 0f; amp = 0.4f + 0.6f * (float)crackles.NextDouble(); }
            age += 1f / Rate;
            float n = noise();
            low += (n - low) * 0.2f;
            float bright = n - low;                               // the noise minus its low part: crisp plastic
            return bright * (amp * Decay(age, 350) + 0.12f * env);
        });
    }

    // A soft two-note chime (G then C, high and light): the scores start landing on the card.
    static float[] MakeChime() => Synth(0.9f, (t, _) =>
    {
        float Bell(float start, float f)
        {
            float u = t - start;
            if (u < 0) return 0;
            return Attack(u, 0.004f) * (Sine(f, u) * Decay(u, 6f) + 0.25f * Sine(f * 2.76f, u) * Decay(u, 14f));
        }
        return 0.6f * (Bell(0f, 1568f) + Bell(0.1f, 2093f));
    });

    // A small coin clinking onto the others in the tip jar.
    static float[] MakeCoin() => Synth(0.25f, (t, _) =>
        Attack(t, 0.0005f) * (Sine(2900, t) * Decay(t, 22) + 0.6f * Sine(4350, t) * Decay(t, 30)
                              + 0.3f * Sine(6100, t) * Decay(t, 45)));

    // A short rising fanfare: a new rank.
    static float[] MakeFanfare() => Synth(1.3f, (t, _) =>
    {
        float Note(float start, float length, float f)
        {
            float u = t - start;
            if (u < 0 || u > length) return 0;
            float env = Attack(u, 0.01f) * MathF.Min(1f, (length - u) / 0.05f);
            return env * (Sine(f, u) + 0.4f * Sine(2 * f, u) + 0.2f * Sine(3 * f, u));
        }
        return Note(0f, 0.14f, 523.25f) + Note(0.15f, 0.14f, 659.25f) + Note(0.3f, 0.14f, 783.99f)
               + Note(0.45f, 0.8f, 1046.5f) * Decay(MathF.Max(0, t - 0.45f), 2.5f);
    });

    // Wraps samples in a 16-bit mono WAV file in memory, scaled so the loudest point is at 80 percent.
    static byte[] ToWav(float[] samples)
    {
        float peak = MathF.Max(1e-6f, samples.Max(MathF.Abs));
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        w.Write("RIFF"u8);
        w.Write(36 + samples.Length * 2);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)1);     // PCM
        w.Write((short)1);     // mono
        w.Write(Rate);
        w.Write(Rate * 2);     // bytes per second
        w.Write((short)2);     // bytes per sample
        w.Write((short)16);    // bits per sample
        w.Write("data"u8);
        w.Write(samples.Length * 2);
        foreach (float s in samples) w.Write((short)(s / peak * 0.8f * short.MaxValue));
        w.Flush();
        return stream.ToArray();
    }
}
