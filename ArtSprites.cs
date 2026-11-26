using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace DustysGanjaria;

// Art part 6: sprite sheets made by tools/gen-sprites.js. Nug sheets are gray-mask (red = shading, green = hair mask,
// alpha = coverage); a shader colours them per strain. Any sheet that is missing leaves its art on the placeholder.
static partial class Art
{
    // One loaded sheet: its texture and grid.
    sealed record Sheet(Texture2D Texture, int Cell, int Cols, int Count);

    static Sheet? nugsLarge, nugsSmall, shake;
    static Shader nugShader;
    static bool nugShaderLoaded;
    static int locStops, locAccent;
    static readonly Dictionary<Strain, float[]> rampCache = new();

    // Sprites are cut to fill the cell less its padding; a bud's circle is drawn this much wider than the sprite's
    // longer side so the lumpy outline covers the physics circle.
    const int SheetPad = 4;
    const float SpriteOverCircle = 1.12f;
    // Large-nug sprites that came out as near-perfect circles (real buds never are): drawn stretched along their own
    // length (width, height), roughly the same area, the narrow side still covering the physics circle. Sprite index
    // in resources/sprites/mc-nugs-large.png, reading order from 0. Only the roundish one (5) needs it.
    static readonly Dictionary<int, Vector2> LargeStretch = new() { [5] = new(0.9f, 1.1f) };

    const string NugVertex = @"#version 330
in vec3 vertexPosition; in vec2 vertexTexCoord; in vec4 vertexColor;
uniform mat4 mvp;
out vec2 fragTexCoord; out vec4 fragColor;
void main() { fragTexCoord = vertexTexCoord; fragColor = vertexColor; gl_Position = mvp * vec4(vertexPosition, 1.0); }";

    // Maps the sheet's shading (red) through six strain colours, dark to light, then tints the hair (green).
    const string NugFragment = @"#version 330
in vec2 fragTexCoord; in vec4 fragColor;
uniform sampler2D texture0;
uniform vec4 colDiffuse;
uniform vec3 stops[6];
uniform vec3 accent;
out vec4 finalColor;
void main() {
    vec4 s = texture(texture0, fragTexCoord);
    float t = clamp(s.r, 0.0, 1.0) * 5.0;
    int i = int(min(floor(t), 4.0));
    vec3 c = mix(stops[i], stops[i + 1], t - float(i));
    c = mix(c, accent, clamp(s.g, 0.0, 1.0));
    finalColor = vec4(c, s.a) * colDiffuse * fragColor;
}";

    // Loads the customer sprites, the nug sheets and their shader. Call once after the window opens.
    public static void LoadSprites()
    {
        LoadCustomerSprites();
        nugsLarge = LoadSheet("resources/sprites/mc-nugs-large");
        nugsSmall = LoadSheet("resources/sprites/mc-nugs-small");
        shake = LoadSheet("resources/sprites/mc-shake");
        if (nugsLarge == null && nugsSmall == null && shake == null) return;
        nugShader = Raylib.LoadShaderFromMemory(NugVertex, NugFragment);
        locStops = Raylib.GetShaderLocation(nugShader, "stops");
        locAccent = Raylib.GetShaderLocation(nugShader, "accent");
        // A shader that failed to compile falls back to Raylib's default, which has neither uniform.
        nugShaderLoaded = nugShader.Id > 0;
        if (locStops < 0 || locAccent < 0) { UnloadSprites(); return; }
    }

    // Frees the sheets and the shader.
    public static void UnloadSprites()
    {
        UnloadCustomerSprites();
        foreach (var s in new[] { nugsLarge, nugsSmall, shake })
            if (s != null) Raylib.UnloadTexture(s.Texture);
        if (nugShaderLoaded) Raylib.UnloadShader(nugShader);
        nugsLarge = nugsSmall = shake = null;
        nugShaderLoaded = false;
    }

    // Loads <path>.png and its <path>.json grid, or null if either is missing or unreadable.
    static Sheet? LoadSheet(string path)
    {
        string png = Path.Combine(AppContext.BaseDirectory, path + ".png"), json = Path.Combine(AppContext.BaseDirectory, path + ".json");
        if (!File.Exists(png) || !File.Exists(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(json));
            var r = doc.RootElement;
            var tex = Raylib.LoadTexture(png);
            if (tex.Id == 0) return null;
            Raylib.GenTextureMipmaps(ref tex);
            Raylib.SetTextureFilter(tex, TextureFilter.Trilinear);
            return new Sheet(tex, r.GetProperty("cell").GetInt32(), r.GetProperty("cols").GetInt32(), r.GetProperty("count").GetInt32());
        }
        catch (Exception) { return null; }
    }

    // The sheet a bud is drawn from: shake, small or large by its weight and strain.
    static Sheet? SheetFor(Bud bud) =>
        bud.IsShake ? shake : bud.IsLarge ? nugsLarge : nugsSmall;

    // A strain's six ramp colours as 18 floats: its shades sorted dark to light, widened with a near-black outline
    // and a shadow below and a highlight above (the same ramp as the previews from tools/gen-sprites.js).
    static float[] RampFor(Strain strain)
    {
        if (rampCache.TryGetValue(strain, out var ramp)) return ramp;
        static float Lum(Color c) => 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
        static Vector3 Mix(Color c, float to, float k) =>
            new(c.R + (to - c.R) * k, c.G + (to - c.G) * k, c.B + (to - c.B) * k);
        var sh = strain.Shades.OrderBy(Lum).ToArray();
        var stops = new[]
        {
            Mix(sh[0], 0, 0.75f), Mix(sh[0], 0, 0.4f), Mix(sh[0], 0, 0), Mix(sh[1], 0, 0), Mix(sh[2], 0, 0), Mix(sh[2], 255, 0.5f),
        };
        ramp = stops.SelectMany(v => new[] { v.X / 255f, v.Y / 255f, v.Z / 255f }).ToArray();
        rampCache[strain] = ramp;
        return ramp;
    }

    // Draws a bud from its sprite: centred on center, squashed in screen space, turned by its sprite angle, scaled
    // to its radius times k. Returns false (nothing drawn) when its sheet is not loaded.
    static bool DrawBudSprite(Bud bud, Vector2 center, Vector2 squash, float k, bool lifted, bool highlight)
    {
        var sheet = SheetFor(bud);
        if (sheet == null) return false;
        int index = bud.Pick % sheet.Count;
        var src = new Rectangle(index % sheet.Cols * sheet.Cell, index / sheet.Cols * sheet.Cell, sheet.Cell, sheet.Cell);
        float size = 2f * bud.Radius * k * SpriteOverCircle * sheet.Cell / (sheet.Cell - 2f * SheetPad);
        var stretch = sheet == nugsLarge && LargeStretch.TryGetValue(index, out var st) ? st : Vector2.One;
        var dest = new Rectangle(0, 0, size * stretch.X, size * stretch.Y);
        var origin = new Vector2(dest.Width / 2, dest.Height / 2);
        float degrees = bud.SpriteAngle * (180f / MathF.PI);

        // Shadow: the sprite's own silhouette in translucent black.
        if (lifted || highlight)
        {
            DrawTransformed(center + new Vector2(5, 7), squash, () =>
                Raylib.DrawTexturePro(sheet.Texture, src, dest, origin, degrees, new Color(0, 0, 0, lifted ? 70 : 45)));
        }

        var ramp = RampFor(bud.Strain);
        Raylib.SetShaderValueV(nugShader, locStops, ramp, ShaderUniformDataType.Vec3, 6);
        var hair = bud.Strain.Hair;
        Raylib.SetShaderValue(nugShader, locAccent, new Vector3(hair.R / 255f, hair.G / 255f, hair.B / 255f), ShaderUniformDataType.Vec3);
        Raylib.BeginShaderMode(nugShader);
        DrawTransformed(center, squash, () => Raylib.DrawTexturePro(sheet.Texture, src, dest, origin, degrees, Color.White));
        // Ending the shader mode draws the batch now, so the next bud's colours do not leak onto this one.
        Raylib.EndShaderMode();
        return true;
    }

    // Draws one shake piece in a strain's colours, size px across, centred at a point, crossed out in red: the
    // "NO SHAKE" ticket's picture of what the rule means. Draws nothing without the shake sheet.
    public static void DrawNoShakeSample(Vector2 at, float size, Strain strain)
    {
        if (shake == null) return;
        const int sample = 1;
        var src = new Rectangle(sample % shake.Cols * shake.Cell, sample / shake.Cols * shake.Cell, shake.Cell, shake.Cell);
        Raylib.SetShaderValueV(nugShader, locStops, RampFor(strain), ShaderUniformDataType.Vec3, 6);
        var hair = strain.Hair;
        Raylib.SetShaderValue(nugShader, locAccent, new Vector3(hair.R / 255f, hair.G / 255f, hair.B / 255f), ShaderUniformDataType.Vec3);
        Raylib.BeginShaderMode(nugShader);
        Raylib.DrawTexturePro(shake.Texture, src, new Rectangle(at.X - size / 2, at.Y - size / 2, size, size), Vector2.Zero, 0, Color.White);
        Raylib.EndShaderMode();
        var red = new Color(200, 50, 40, 255);
        Raylib.DrawRing(at, size * 0.5f, size * 0.5f + 3, 0, 360, 32, red);
        Raylib.DrawLineEx(at + new Vector2(-size * 0.36f, size * 0.36f), at + new Vector2(size * 0.36f, -size * 0.36f), 3, red);
    }

    // Draws something about a point with a screen-space squash (x wide, y tall) applied after any turning.
    static void DrawTransformed(Vector2 at, Vector2 squash, Action draw)
    {
        Rlgl.PushMatrix();
        Rlgl.Translatef(at.X, at.Y, 0);
        Rlgl.Scalef(squash.X, squash.Y, 1);
        draw();
        Rlgl.PopMatrix();
    }
}
