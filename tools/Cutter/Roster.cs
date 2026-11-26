// roster: the customer cast. Shades each lineup by rule (see Shade.cs) and cuts every character into one sheet.
//   dotnet run --project tools/Cutter -- roster [--flat] <lineup>:<name1,name2,...> [<lineup>:<names> ...]
// --flat keeps the lineups' own colours (only the green key comes off), for styles with no outlines to shade by
// rule, such as mid-century.
// Names go left to right and start with the character's kind, then the person, then what tells this drawing apart:
// "m-moustache-blue". Two names that differ only in the last word are the same person in other clothes (the game
// never uses one as the other's ID photo).
// Writes:
//   resources/sprites/customer-roster.png + .json   one cell per character, all at the lineups' scale, feet on one line
//   art/sprites/roster-preview.png                  the sheet on paper, to check by eye
using System.Text.Json;

static partial class Cutter
{
    const int RosterPad = 12;     // empty pixels around each character in its cell (mipmaps blur into it)
    const int RosterColumns = 6;

    record Cut(string Name, string Source, int[] Px, int W, int H, int FeetX, int HeadX);

    static void Roster(string[] specs)
    {
        var cuts = new List<Cut>();
        double[]? ink = null;
        bool flat = specs.Contains("--flat");
        foreach (var spec in specs.Where(s => s != "--flat"))
        {
            int colon = spec.LastIndexOf(':');
            if (colon <= 1) throw new Exception($"Expected <lineup>:<names>, got {spec}");
            string path = spec[..colon];
            var names = spec[(colon + 1)..].Split(',');
            var (_, shaded, w, h, figs, lineInk) = flat ? FlatLineup(path) : ShadeLineup(path);
            ink ??= lineInk;
            if (figs.Count != names.Length) throw new Exception($"{path}: found {figs.Count} characters but got {names.Length} names.");
            for (int i = 0; i < figs.Count; i++)
            {
                var f = figs[i];
                int cw = f.MaxX - f.MinX + 1, ch = f.MaxY - f.MinY + 1;
                var px = new int[cw * ch];
                foreach (int p in f.Pixels) px[(p / w - f.MinY) * cw + p % w - f.MinX] = Crisp(shaded[p]);
                cuts.Add(new Cut(names[i], Path.GetRelativePath(Root, Path.GetFullPath(path)).Replace('\\', '/'), px, cw, ch,
                                 MeanX(px, cw, ch, ch - (int)(0.04 * ch), ch), MeanX(px, cw, ch, (int)(0.04 * ch), (int)(0.16 * ch))));
                Console.WriteLine($"{names[i]}: {cw} x {ch} px");
            }
        }

        int cellW = cuts.Max(c => c.W) + 2 * RosterPad, cellH = cuts.Max(c => c.H) + 2 * RosterPad;
        int cols = Math.Min(RosterColumns, cuts.Count), rows = (cuts.Count + cols - 1) / cols;
        int sheetW = cellW * cols, sheetH = cellH * rows;
        // Empty pixels carry the outline's colour at zero alpha, so filtering at the edges blends toward ink, not black.
        int empty = Argb(0, ink!) & 0x00FFFFFF;
        var sheet = new int[sheetW * sheetH];
        Array.Fill(sheet, empty);
        var meta = new List<object>();
        for (int i = 0; i < cuts.Count; i++)
        {
            var c = cuts[i];
            int ox = i % cols * cellW + (cellW - c.W) / 2, oy = i / cols * cellH + cellH - RosterPad - c.H;
            for (int y = 0; y < c.H; y++)
                for (int x = 0; x < c.W; x++)
                {
                    int v = c.Px[y * c.W + x];
                    if (Alpha(v) > 0) sheet[(oy + y) * sheetW + ox + x] = v;
                }
            int cx = ox - i % cols * cellW, cy = oy - i / cols * cellH;  // the character's box, in its cell
            meta.Add(new
            {
                name = c.Name,
                source = c.Source,
                feetX = cx + c.FeetX, feetY = cy + c.H - 1,   // feet: bottom of the shoes, centred under them
                headTop = cy, headX = cx + c.HeadX,           // top of the head (hair included), centre of the face
            });
        }

        string outBase = Path.Combine(Root, "resources", "sprites", "customer-roster");
        Save(sheet, sheetW, sheetH, outBase + ".png");
        File.WriteAllText(outBase + ".json", JsonSerializer.Serialize(new
        {
            format = "straight-alpha colour, one cell per character, row by row",
            cellW, cellH, columns = cols, count = cuts.Count, characters = meta,
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote {outBase}.png ({sheetW} x {sheetH}) and .json");

        var preview = new int[sheet.Length];
        for (int p = 0; p < sheet.Length; p++) preview[p] = Blend(Paper, sheet[p]);
        Save(preview, sheetW, sheetH, Path.Combine(Root, "art", "sprites", "roster-preview.png"));
    }

    // A lineup with only the green key taken off: its own colours, its characters, and its darkest colour (the colour
    // empty pixels carry, so filtering at the edges blends toward it).
    static (int[] src, int[] fg, int w, int h, List<Figure> figs, double[] ink) FlatLineup(string lineupPath)
    {
        var (src, w, h) = Load(lineupPath);
        var key = BorderColour(src, w, h);
        var fg = Despill(src, KeepInterior(src, Unmix(src, key), key, w, h), key, w, h);
        return (src, fg, w, h, FindFigures(fg, w, h), InkColour(fg));
    }

    // Takes the green cast off pixels near the background: the key's spill and the JPEG halo leave edge pixels
    // greener than the art, which reads as green fuzz. Within DespillReach px of a background pixel, green may not
    // rise above the larger of red and blue (the cast's teal sits right at that line, so it barely moves). Anywhere
    // else, a pixel clearly greener than that (thin gaps between fingers, where the key is too dark to read as
    // background) gets the same cap; nothing in the cast is green.
    const int DespillAnywhere = 12;
    const int DespillReach = 4;
    static int[] Despill(int[] src, int[] fg, double[] key, int w, int h)
    {
        double keyLean = key[1] - (key[0] + key[2]) / 2;
        var bg = new List<int>();
        for (int p = 0; p < src.Length; p++)
        {
            double r = (src[p] >> 16) & 255, g = (src[p] >> 8) & 255, b = src[p] & 255;
            if (g - (r + b) / 2 > 0.6 * keyLean) bg.Add(p);
        }
        var o = (int[])fg.Clone();
        var near = new HashSet<int>(Dilate(bg, w, h, DespillReach));
        for (int p = 0; p < o.Length; p++)
        {
            int c = o[p];
            if (Alpha(c) == 0) continue;
            int r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255, cap = Math.Max(r, b);
            if (g > cap + (near.Contains(p) ? 0 : DespillAnywhere)) o[p] = (c & unchecked((int)0xFFFF00FF)) | (cap << 8);
        }
        return o;
    }

    // The lineups' JPEG blur fades the outline out over about 5 px, which reads as a dark glow when drawn big; this
    // steepens the fade to about 2 px (coverage below EdgeLo goes, above EdgeHi is solid).
    const int EdgeLo = 110, EdgeHi = 230;
    static int Crisp(int c)
    {
        int a = Alpha(c);
        if (a == 0 || a == 255) return c;
        int na = Math.Clamp((a - EdgeLo) * 255 / (EdgeHi - EdgeLo), 0, 255);
        return na == 0 ? 0 : (na << 24) | (c & 0x00FFFFFF);
    }

    // The mean x of the opaque pixels in rows [y0, y1).
    static int MeanX(int[] px, int w, int h, int y0, int y1)
    {
        long sum = 0, n = 0;
        for (int y = Math.Max(0, y0); y < Math.Min(h, y1); y++)
            for (int x = 0; x < w; x++)
                if (Alpha(px[y * w + x]) >= 128) { sum += x; n++; }
        return n == 0 ? w / 2 : (int)(sum / n);
    }
}
