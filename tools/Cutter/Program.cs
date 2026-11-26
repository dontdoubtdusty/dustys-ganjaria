// Customer art cutter. Turns a generated lineup of blank customer bases into a recolourable base sheet, and cuts
// edit layers (hair, faces) out of edited pictures by diffing them against their base.
//
// Usage (from the repo root):
//   dotnet run --project tools/Cutter -- bases <lineup> <name1,name2,...>
//       Finds each figure on the flat green background (left to right), and writes:
//       resources/sprites/customer-bases.png + .json   the base sheet (part mask, see below), one cell per figure, all
//                                                      at the lineup's scale with one shared ground line
//       art/sprites/picked/bases/<name>.png            each figure alone on pure green, a 9:16 picture to send to
//                                                      Gemini as the reference for an edit (same pixels as the lineup)
//       art/sprites/bases-preview.png                  the sheet in key colours and recoloured, to check by eye
//   dotnet run --project tools/Cutter -- layer <base name> <edited picture> <layer name> [--colour]
//       Diffs the edited picture against art/sprites/picked/bases/<base name>.png and writes the changed part as
//       resources/sprites/layers/<base name>-<layer name>.png (+ .json with its place on the base's cell) and a
//       preview over the base. Default: a recolourable layer (part mask, its new colour becomes the hair part).
//       --colour keeps the layer's own colours (eyes, mouths); either way, pixels in the base's skin, top or
//       trouser colours turn into outline ink with matching transparency, so the base shows through and still
//       recolours.
//
//   dotnet run --project tools/Cutter -- look <base name> <layer1,layer2,...>
//       Puts the base and its layers together (in the order given) in four colour looks, as the game will:
//       art/sprites/look-<base name>.png
//
// Part mask format (base sheet and recolourable layers): R = part id * 40 (0 skin, 1 top, 2 trousers, 3 shoes,
// 4 hair), G = how much outline ink is mixed in (0 = the part's own colour, 255 = pure ink), B = 0, A = coverage.
// The game colours each pixel as mix(partColour[id], ink, G) at alpha A. Every edge in this flat style runs between a
// fill and the outline, so this keeps the anti-aliasing while every fill stays one recolourable colour (shadows the
// model drew are flattened into their part).
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;

static partial class Cutter
{
    // The lineup's flat fills in key colours, by part id. Sampled from art/sprites/picked/bodies-fm-3-tq2-nb.jpg.
    static readonly string[] PartNames = { "skin", "top", "trousers", "shoes", "hair" };
    static readonly double[][] Fills =
    {
        new double[] { 255, 180, 122 },  // skin
        new double[] { 64, 114, 173 },   // top
        new double[] { 128, 127, 125 },  // trousers
        new double[] { 46, 44, 53 },     // shoes
    };

    const int MinFigureArea = 3000;  // pieces smaller than this are specks unless they sit inside a figure's box
    const int Pad = 8;               // empty pixels around each figure in its cell
    const int EditW = 432, EditH = 768; // the 9:16 edit picture per base (the lineup is 768 tall)
    static readonly string Root = Directory.GetCurrentDirectory();

    static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 3 && args[0] == "bases") Bases(args[1], args[2].Split(','));
            else if (args.Length >= 3 && args[0] == "look") Look(args[1], args[2].Split(','));
            else if (args.Length >= 2 && args[0] == "shade") Shade(args[1]);
            else if (args.Length >= 2 && args[0] == "roster") Roster(args[1..]);
            else if (args.Length >= 4 && args[0] == "layer") Layer(args[1], args[2], args[3], args.Contains("--colour"));
            else { Console.Error.WriteLine("Usage: bases <lineup> <names>  |  layer <base> <edited> <layer> [--colour]  |  look <base> <layer1,...>  |  shade <lineup>  |  roster [--flat] <lineup>:<names> ..."); return 1; }
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
    }

    // ---------- bases ----------

    static void Bases(string lineupPath, string[] names)
    {
        var (src, w, h) = Load(lineupPath);
        var key = BorderColour(src, w, h);
        var fg = Unmix(src, key);
        var ink = InkColour(fg);
        Console.WriteLine($"Key {Fmt(key)}, ink {Fmt(ink)}");

        var figures = FindFigures(fg, w, h);
        if (figures.Count != names.Length)
            throw new Exception($"Found {figures.Count} figures but got {names.Length} names.");

        int top = figures.Min(f => f.MinY) - Pad, bottom = figures.Max(f => f.MaxY) + Pad;
        int cellW = figures.Max(f => f.MaxX - f.MinX + 1) + 2 * Pad, cellH = bottom - top + 1;
        var sheet = new int[cellW * names.Length * cellH];
        int sheetW = cellW * names.Length;
        var meta = new List<object>();
        Directory.CreateDirectory(Path.Combine(Root, "art", "sprites", "picked", "bases"));

        for (int i = 0; i < figures.Count; i++)
        {
            var f = figures[i];
            int cx = (f.MinX + f.MaxX) / 2;
            int dx = i * cellW + cellW / 2 - cx, dy = -top;  // lineup pixel -> sheet pixel
            foreach (int p in f.Pixels)
            {
                int x = p % w, y = p / w;
                sheet[(y + dy) * sheetW + x + dx] = Encode(fg[p], ink, out _);
            }

            // The edit picture: the figure's own lineup pixels (with a 2 px ring for soft edges) on pure green.
            int ex = EditW / 2 - cx;  // lineup x -> edit x (same y)
            var edit = new int[EditW * EditH];
            Array.Fill(edit, unchecked((int)0xFF00FF00));
            var ring = Dilate(f.Pixels, w, h, 2);
            foreach (int p in ring)
            {
                int x = p % w + ex, y = p / w;
                if (x >= 0 && x < EditW && y < EditH) edit[y * EditW + x] = src[p];
            }
            Save(edit, EditW, EditH, Path.Combine(Root, "art", "sprites", "picked", "bases", names[i] + ".png"));

            meta.Add(new
            {
                name = names[i],
                feetX = cellW / 2, feetY = f.MaxY - top,       // feet: bottom centre of the figure, in its cell
                headTop = f.MinY - top,
                editToCell = new[] { cellW / 2 - EditW / 2, -top }, // add to an edit-picture pixel to get the cell pixel
            });
            Console.WriteLine($"{names[i]}: {f.MaxX - f.MinX + 1} x {f.MaxY - f.MinY + 1} px");
        }

        string outBase = Path.Combine(Root, "resources", "sprites", "customer-bases");
        Save(sheet, sheetW, cellH, outBase + ".png");
        File.WriteAllText(outBase + ".json", JsonSerializer.Serialize(new
        {
            source = Path.GetRelativePath(Root, Path.GetFullPath(lineupPath)).Replace('\\', '/'),
            format = "part mask: R = part id * 40, G = ink mix, A = coverage",
            parts = PartNames,
            keyColours = Fills.Select(c => c.Select(v => (int)v).ToArray()).ToArray(),
            ink = ink.Select(v => (int)v).ToArray(),
            cellW, cellH, count = names.Length, bases = meta,
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote {outBase}.png ({sheetW} x {cellH}) and .json");

        Preview(sheet, sheetW, cellH, ink, Path.Combine(Root, "art", "sprites", "bases-preview.png"));
    }

    record Figure(int MinX, int MinY, int MaxX, int MaxY, List<int> Pixels);

    // The figures: 8-connected pieces of over-half coverage, big ones first; small pieces inside a figure's box join
    // it, the rest are dropped. Each figure's pixels include its soft edge (any coverage, 2 px around it).
    static List<Figure> FindFigures(int[] fg, int w, int h)
    {
        var label = new int[w * h];
        var pieces = new List<(int minX, int minY, int maxX, int maxY, List<int> px)>();
        var stack = new Stack<int>();
        for (int s = 0; s < fg.Length; s++)
        {
            if (label[s] != 0 || Alpha(fg[s]) < 128) continue;
            var px = new List<int>();
            int minX = w, minY = h, maxX = 0, maxY = 0;
            label[s] = pieces.Count + 1; stack.Push(s);
            while (stack.Count > 0)
            {
                int p = stack.Pop(), x = p % w, y = p / w;
                px.Add(p);
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                for (int ny = y - 1; ny <= y + 1; ny++)
                    for (int nx = x - 1; nx <= x + 1; nx++)
                    {
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int q = ny * w + nx;
                        if (label[q] != 0 || Alpha(fg[q]) < 128) continue;
                        label[q] = pieces.Count + 1; stack.Push(q);
                    }
            }
            pieces.Add((minX, minY, maxX, maxY, px));
        }

        var figs = pieces.Where(p => p.px.Count >= MinFigureArea).OrderBy(p => p.minX).ToList();
        var result = new List<Figure>();
        foreach (var f in figs)
        {
            var px = new List<int>(f.px);
            foreach (var p in pieces)
                if (p.px.Count < MinFigureArea && p.minX >= f.minX - 4 && p.maxX <= f.maxX + 4 && p.minY >= f.minY - 4 && p.maxY <= f.maxY + 4)
                    px.AddRange(p.px);
            // Soft edge: any coverage within 2 px.
            var edge = Dilate(px, w, h, 2).Where(p => Alpha(fg[p]) > 0).ToList();
            int minX = edge.Min(p => p % w), maxX = edge.Max(p => p % w), minY = edge.Min(p => p / w), maxY = edge.Max(p => p / w);
            result.Add(new Figure(minX, minY, maxX, maxY, edge));
        }
        return result;
    }

    // ---------- layer ----------

    static void Layer(string baseName, string editedPath, string layerName, bool keepColour)
    {
        string basePic = Path.Combine(Root, "art", "sprites", "picked", "bases", baseName + ".png");
        if (!File.Exists(basePic)) throw new Exception($"No base picture {basePic}; run bases first.");
        var (b, bw, bh) = Load(basePic);
        var (e0, _, _) = Load(editedPath, bw, bh);
        var key = new double[] { 0, 255, 0 };
        var meta = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "resources", "sprites", "customer-bases.json"))).RootElement;
        var ink = meta.GetProperty("ink").EnumerateArray().Select(v => (double)v.GetInt32()).ToArray();
        var baseFg = Unmix(b, key);

        // The model redraws the whole picture: the figure comes back a few pixels off and about 1% resized, and its
        // fills drift. So line the edit up with the base first, then measure the edit's own fill colours.
        var e = Align(b, e0, bw, bh);
        var fgE = Unmix(e, BorderColour(e0, bw, bh));
        var editFills = EditFills(baseFg, fgE, ink);
        Console.WriteLine("Edit fills " + string.Join(" ", editFills.Select(Fmt)));

        // Changed pixels: the edit's part (by its own fills) or ink differs from the base's at that spot and at every
        // neighbour within 1 px (so a 1 px outline wobble is not a change), or it is a new colour; specks dropped.
        var bPart = new int[b.Length]; var baseT = new double[b.Length];
        for (int i = 0; i < b.Length; i++)
        {
            if (Alpha(baseFg[i]) < 128) { bPart[i] = -1; continue; }
            Classify(Rgb(baseFg[i]), ink, Fills, out bPart[i], out baseT[i]);
        }
        var changed = new List<int>();
        for (int i = 0; i < b.Length; i++)
        {
            int x = i % bw, y = i / bw;
            int part = -1; double t = 0; bool fresh = false;
            if (Alpha(fgE[i]) >= 128)
                fresh = Classify(Rgb(fgE[i]), ink, editFills, out part, out t) > 40;
            bool same = false;
            for (int ny = Math.Max(0, y - 1); ny <= Math.Min(bh - 1, y + 1) && !same; ny++)
                for (int nx = Math.Max(0, x - 1); nx <= Math.Min(bw - 1, x + 1) && !same; nx++)
                {
                    int q = ny * bw + nx;
                    if (part == -1) same = bPart[q] == -1;
                    else if (bPart[q] != -1)
                        same = Math.Abs(baseT[q] - t) < 0.5 && (bPart[q] == part || t > 0.8 && baseT[q] > 0.8);
                }
            if (fresh || !same) changed.Add(i);
        }
        changed = DropSpecks(changed, bw, bh, 40);
        Console.WriteLine($"{changed.Count} changed pixels after dropping specks");

        // A recolourable layer's new colour: the commonest changed colour that is none of the base's fills or ink. A
        // colour can also sit on a fill-to-ink line (brown hair is close to skin darkened halfway to the outline), so a
        // mid mix counts as new too when it fills a flat patch; real outline edges are only a pixel or two wide.
        double[]? hair = null;
        if (!keepColour)
        {
            bool Flat(int p)
            {
                int x = p % bw, y = p / bw;
                if (x < 2 || y < 2 || x >= bw - 2 || y >= bh - 2) return false;
                var c = Rgb(fgE[p]);
                for (int ny = y - 2; ny <= y + 2; ny++)
                    for (int nx = x - 2; nx <= x + 2; nx++)
                    {
                        int q = ny * bw + nx;
                        if (Alpha(fgE[q]) < 250 || Distance(Rgb(fgE[q]), c) > 18) return false;
                    }
                return true;
            }
            bool New(int p)
            {
                if (Alpha(fgE[p]) < 250) return false;
                double err = Classify(Rgb(fgE[p]), ink, editFills, out _, out double t);
                return err >= 40 || t > 0.15 && t < 0.85 && Flat(p);
            }
            var cands = changed.Where(New).ToList();
            if (cands.Count == 0) throw new Exception("No new colour in the edit; use --colour for ink-only layers.");
            var counts = new Dictionary<int, int>();
            foreach (int p in cands) { int q = Quant(Rgb(fgE[p])); counts[q] = counts.GetValueOrDefault(q) + 1; }
            int top = counts.MaxBy(kv => kv.Value).Key;
            var near = cands.Where(p => Quant(Rgb(fgE[p])) == top).Select(p => Rgb(fgE[p])).ToList();
            hair = new[] { near.Average(c => c[0]), near.Average(c => c[1]), near.Average(c => c[2]) };
            Console.WriteLine($"Layer colour {Fmt(hair)} ({near.Count} px)");

            // Pixels near the hair that match it join the cut even where the diff missed them: an old outline's soft
            // edge (skin halfway to ink) looks like brown hair, so the bald head's outline would show through.
            var withHair = editFills.Append(hair).ToArray();
            // Flood from the hair through every flat patch of its colour (inside the old head, brown is too close to
            // skin for the diff to see), then take in the hair-coloured soft edges within 3 px.
            bool HairFlat(int p) => Alpha(fgE[p]) >= 250 && Distance(Rgb(fgE[p]), hair) < 30 && Flat(p);
            var flood = new HashSet<int>(cands.Where(HairFlat));
            var todo = new Stack<int>(flood);
            while (todo.Count > 0)
            {
                int p = todo.Pop(), x = p % bw, y = p / bw;
                foreach (int q in new[] { p - 1, p + 1, p - bw, p + bw })
                    if (q >= 0 && q < fgE.Length && Math.Abs(q % bw - x) <= 1 && !flood.Contains(q) && HairFlat(q)) { flood.Add(q); todo.Push(q); }
            }
            var hairNear = Dilate(flood.ToList(), bw, bh, 3);
            var set = new HashSet<int>(changed);
            foreach (int p in hairNear)
                if (Alpha(fgE[p]) > 0 && Classify(Rgb(fgE[p]), ink, withHair, out int hp, out _) < 40 && hp == withHair.Length - 1)
                    set.Add(p);
            changed = set.ToList();
        }
        changed = NearMain(changed, bw, bh, keepColour ? 40 : 24);
        var region = Dilate(changed, bw, bh, 2);
        var fills = hair == null ? editFills : editFills.Append(hair).ToArray();

        var layer = new int[b.Length];
        foreach (int p in region)
        {
            int a = Alpha(fgE[p]);
            if (a == 0) continue;
            var c = Rgb(fgE[p]);
            Classify(c, ink, fills, out int part, out double t);
            bool basePart = part < Fills.Length - 1 || (part == 3 && hair == null);  // skin, top, trousers (shoes too)
            double err = Classify(c, ink, fills, out _, out _);
            if (basePart && err < 40)
            {
                // The base's own colour shows through: keep only the ink part, as transparency.
                int ia = (int)Math.Round(a * t);
                if (ia < 8) continue;
                layer[p] = keepColour ? Argb(ia, ink) : (ia << 24) | (255 << 8);
            }
            else if (keepColour) layer[p] = fgE[p];
            else  // the layer's own colour mixed with ink: on a recolourable layer that is always the hair part
            {
                Classify(c, ink, new[] { hair! }, out _, out double th);
                layer[p] = (a << 24) | ((Fills.Length * 40) << 16) | ((int)Math.Round(th * 255) << 8);
            }
        }

        int minX = bw, minY = bh, maxX = -1, maxY = -1;
        for (int i = 0; i < layer.Length; i++)
            if (Alpha(layer[i]) > 0) { minX = Math.Min(minX, i % bw); maxX = Math.Max(maxX, i % bw); minY = Math.Min(minY, i / bw); maxY = Math.Max(maxY, i / bw); }
        if (maxX < 0) throw new Exception("The edit changed nothing that could be cut.");
        int lw = maxX - minX + 1, lh = maxY - minY + 1;
        var crop = new int[lw * lh];
        for (int y = 0; y < lh; y++) Array.Copy(layer, (y + minY) * bw + minX, crop, y * lw, lw);

        var bmeta = meta.GetProperty("bases").EnumerateArray().First(x => x.GetProperty("name").GetString() == baseName);
        var off = bmeta.GetProperty("editToCell").EnumerateArray().Select(v => v.GetInt32()).ToArray();
        string dir = Path.Combine(Root, "resources", "sprites", "layers");
        Directory.CreateDirectory(dir);
        string outPath = Path.Combine(dir, $"{baseName}-{layerName}");
        Save(crop, lw, lh, outPath + ".png");
        File.WriteAllText(outPath + ".json", JsonSerializer.Serialize(new
        {
            baseName, layer = layerName, format = keepColour ? "colour (ink over the base)" : "part mask (hair = id 4)",
            hairKey = hair?.Select(v => (int)Math.Round(v)).ToArray(),
            x = minX + off[0], y = minY + off[1],  // top-left on the base's cell
            source = Path.GetRelativePath(Root, Path.GetFullPath(editedPath)).Replace('\\', '/'),
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote {outPath}.png ({lw} x {lh}) at {minX + off[0]},{minY + off[1]} on the cell");

        // Preview: the base picture with the layer drawn over it, cut and recoloured.
        var prev = new int[b.Length];
        for (int i = 0; i < b.Length; i++)
        {
            int c = OverPaper(Decode(Encode(baseFg[i], ink, out _), ink, Fills.Select(f => f).ToArray()));
            int l = layer[i];
            if (Alpha(l) > 0)
                c = Blend(c, keepColour ? l : Decode(l, ink, hair == null ? Fills : Fills.Append(new double[] { 178, 64, 40 }).ToArray()));
            prev[i] = c;
        }
        Save(prev, bw, bh, Path.Combine(Root, "art", "sprites", $"layer-preview-{baseName}-{layerName}.png"));
    }

    // The edit redrawn onto the base's frame. The model gives the figure back slightly moved and resized, so find the
    // scale and offset (edit = base * s + d) that best lay the edit's body (below the head, which edits change) over
    // the base's: a rough guess from the body boxes, then a fine search on the silhouettes.
    static int[] Align(int[] b, int[] e, int w, int h)
    {
        static bool[] Mask(int[] p)
        {
            var m = new bool[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                int r = (p[i] >> 16) & 255, g = (p[i] >> 8) & 255, bl = p[i] & 255;
                m[i] = g - (r + bl) / 2 < 100;
            }
            return m;
        }
        (int l, int r, int t, int bo) Box(bool[] m, int y0)
        {
            int l = w, r = -1, t = h, bo = -1;
            for (int y = Math.Max(0, y0); y < h; y++)
                for (int x = 0; x < w; x++)
                    if (m[y * w + x]) { l = Math.Min(l, x); r = Math.Max(r, x); t = Math.Min(t, y); bo = Math.Max(bo, y); }
            return (l, r, t, bo);
        }
        var mb = Mask(b); var me = Mask(e);
        var fb = Box(mb, 0); var fe = Box(me, 0);
        int yb = fb.t + (int)((fb.bo - fb.t) * 0.45);
        var bb = Box(mb, yb);
        double s = 1; var eb = fe;
        for (int k = 0; k < 4; k++) { eb = Box(me, (int)(fe.bo - (fb.bo - yb) * s)); s = (double)(eb.r - eb.l) / (bb.r - bb.l); }
        double cxb = (bb.l + bb.r) / 2.0, cxe = (eb.l + eb.r) / 2.0;

        double best = double.MaxValue, bs = s, bdx = cxe - cxb * s, bdy = eb.bo - bb.bo * s;
        double s0 = s;
        for (double ss = s0 - 0.03; ss <= s0 + 0.03; ss += 0.0025)
            for (double ox = -3; ox <= 3; ox += 0.5)
                for (double oy = -3; oy <= 3; oy += 0.5)
                {
                    double dx = cxe + ox - cxb * ss, dy = eb.bo + oy - bb.bo * ss;
                    int bad = 0;
                    for (int y = yb; y < h; y += 2)
                        for (int x = 0; x < w; x += 2)
                        {
                            int ex = (int)Math.Round(x * ss + dx), ey = (int)Math.Round(y * ss + dy);
                            bool ve = ex >= 0 && ey >= 0 && ex < w && ey < h && me[ey * w + ex];
                            if (ve != mb[y * w + x]) bad++;
                        }
                    if (bad < best) { best = bad; bs = ss; bdx = dx; bdy = dy; }
                }
        Console.WriteLine($"Aligned: edit = base x {bs:0.000} + ({bdx:0.0}, {bdy:0.0})");

        // Bilinear resample of the edit at base * s + d; outside the picture is the green key.
        var o = new int[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double fx = x * bs + bdx, fy = y * bs + bdy;
                int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
                double ax = fx - x0, ay = fy - y0;
                double[] acc = new double[3];
                for (int k = 0; k < 4; k++)
                {
                    int sx = x0 + (k & 1), sy = y0 + (k >> 1);
                    double wt = ((k & 1) == 1 ? ax : 1 - ax) * ((k >> 1) == 1 ? ay : 1 - ay);
                    int c = sx >= 0 && sy >= 0 && sx < w && sy < h ? e[sy * w + sx] : unchecked((int)0xFF00FF00);
                    acc[0] += wt * ((c >> 16) & 255); acc[1] += wt * ((c >> 8) & 255); acc[2] += wt * (c & 255);
                }
                o[y * w + x] = Argb(255, acc);
            }
        return o;
    }

    // The edit's own fill colours: for each base part, the median edit colour where the base shows that fill pure
    // (the model shifts every fill a little when it redraws). Falls back to the key colour when there is too little.
    static double[][] EditFills(int[] baseFg, int[] fgE, double[] ink)
    {
        var lists = Fills.Select(_ => new List<double[]>()).ToArray();
        for (int i = 0; i < baseFg.Length; i++)
        {
            if (Alpha(baseFg[i]) < 255 || Alpha(fgE[i]) < 255) continue;
            if (Classify(Rgb(baseFg[i]), ink, Fills, out int part, out double t) > 30 || t > 0.05) continue;
            var c = Rgb(fgE[i]);
            if (Distance(c, Fills[part]) < 70) lists[part].Add(c);
        }
        return lists.Select((l, k) =>
        {
            if (l.Count < 200) return Fills[k];
            double Med(int ch) { var v = l.Select(c => c[ch]).OrderBy(x => x).ToList(); return v[v.Count / 2]; }
            return new[] { Med(0), Med(1), Med(2) };
        }).ToArray();
    }

    // Keeps the biggest piece of the cut and the pieces whose box comes within reach px of its box; drops the
    // outline wobbles the model leaves elsewhere on the body.
    static List<int> NearMain(List<int> px, int w, int h, int reach)
    {
        var set = new HashSet<int>(px); var seen = new HashSet<int>();
        var comps = new List<(List<int> p, int l, int t, int r, int b)>();
        foreach (int s0 in px)
        {
            if (!seen.Add(s0)) continue;
            var c = new List<int> { s0 };
            for (int k = 0; k < c.Count; k++)
            {
                int x = c[k] % w, y = c[k] / w;
                for (int ny = y - 2; ny <= y + 2; ny++)
                    for (int nx = x - 2; nx <= x + 2; nx++)
                    {
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int q = ny * w + nx;
                        if (set.Contains(q) && seen.Add(q)) c.Add(q);
                    }
            }
            comps.Add((c, c.Min(q => q % w), c.Min(q => q / w), c.Max(q => q % w), c.Max(q => q / w)));
        }
        if (comps.Count == 0) return px;
        var m = comps.MaxBy(c => c.p.Count);
        var keep = comps.Where(c => c.r >= m.l - reach && c.l <= m.r + reach && c.b >= m.t - reach && c.t <= m.b + reach).ToList();
        if (keep.Count < comps.Count) Console.WriteLine($"Dropped {comps.Count - keep.Count} stray pieces ({comps.Sum(c => c.p.Count) - keep.Sum(c => c.p.Count)} px)");
        return keep.SelectMany(c => c.p).ToList();
    }

    static double Distance(double[] a, double[] c) =>
        Math.Sqrt((a[0] - c[0]) * (a[0] - c[0]) + (a[1] - c[1]) * (a[1] - c[1]) + (a[2] - c[2]) * (a[2] - c[2]));

    // ---------- look ----------

    static void Look(string baseName, string[] layers)
    {
        string sp = Path.Combine(Root, "resources", "sprites");
        var meta = JsonDocument.Parse(File.ReadAllText(Path.Combine(sp, "customer-bases.json"))).RootElement;
        var ink = meta.GetProperty("ink").EnumerateArray().Select(v => (double)v.GetInt32()).ToArray();
        int cw = meta.GetProperty("cellW").GetInt32(), ch = meta.GetProperty("cellH").GetInt32();
        int idx = meta.GetProperty("bases").EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToList().IndexOf(baseName);
        if (idx < 0) throw new Exception($"No base {baseName}.");
        var (sheet, sw, _) = Load(Path.Combine(sp, "customer-bases.png"));
        var looks = new[]
        {
            new[] { Fills[0], Fills[1], Fills[2], Fills[3], new double[] { 120, 80, 45 } },
            new[] { new double[] { 141, 85, 54 }, new double[] { 214, 88, 66 }, new double[] { 54, 62, 84 }, new double[] { 230, 226, 214 }, new double[] { 30, 26, 24 } },
            new[] { new double[] { 247, 214, 186 }, new double[] { 96, 160, 98 }, new double[] { 70, 92, 150 }, new double[] { 120, 62, 40 }, new double[] { 236, 200, 110 } },
            new[] { new double[] { 92, 56, 38 }, new double[] { 236, 196, 70 }, new double[] { 168, 140, 104 }, new double[] { 60, 60, 60 }, new double[] { 178, 64, 40 } },
        };
        var o = new int[cw * looks.Length * ch];
        int ow = cw * looks.Length;
        var lays = layers.Select(n =>
        {
            var j = JsonDocument.Parse(File.ReadAllText(Path.Combine(sp, "layers", $"{baseName}-{n}.json"))).RootElement;
            var (px, lw, lh) = Load(Path.Combine(sp, "layers", $"{baseName}-{n}.png"));
            return (px, lw, lh, x: j.GetProperty("x").GetInt32(), y: j.GetProperty("y").GetInt32(), mask: j.GetProperty("format").GetString()!.StartsWith("part"));
        }).ToList();
        for (int k = 0; k < looks.Length; k++)
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    int c = OverPaper(Decode(sheet[y * sw + idx * cw + x], ink, looks[k]));
                    foreach (var l in lays)
                    {
                        int lx = x - l.x, ly = y - l.y;
                        if (lx < 0 || ly < 0 || lx >= l.lw || ly >= l.lh) continue;
                        int lp = l.px[ly * l.lw + lx];
                        if (Alpha(lp) > 0) c = Blend(c, l.mask ? Decode(lp, ink, looks[k]) : lp);
                    }
                    o[y * ow + k * cw + x] = c;
                }
        string outPath = Path.Combine(Root, "art", "sprites", $"look-{baseName}.png");
        Save(o, ow, ch, outPath);
        Console.WriteLine($"Wrote {outPath}");
    }

    // ---------- colour ----------

    // Encodes an un-mixed foreground pixel as a part mask pixel.
    static int Encode(int px, double[] ink, out int part, double[][]? fills = null)
    {
        int a = Alpha(px);
        if (a == 0) { part = -1; return 0; }
        Classify(Rgb(px), ink, fills ?? Fills, out part, out double t);
        return (a << 24) | ((part * 40) << 16) | ((int)Math.Round(t * 255) << 8);
    }

    // Colours a part mask pixel with the given part colours (premultiplied-free ARGB out).
    static int Decode(int px, double[] ink, double[][] colours)
    {
        int a = Alpha(px);
        if (a == 0) return 0;
        int part = Math.Min(colours.Length - 1, ((px >> 16) & 255) / 40);
        double t = ((px >> 8) & 255) / 255.0;
        var f = colours[part];
        return Argb(a, new[] { f[0] + (ink[0] - f[0]) * t, f[1] + (ink[1] - f[1]) * t, f[2] + (ink[2] - f[2]) * t });
    }

    // The nearest fill-to-ink mix: which fill, how much ink, and the distance left over.
    static double Classify(double[] c, double[] ink, double[][] fills, out int part, out double tOut)
    {
        double best = double.MaxValue; part = 0; tOut = 0;
        for (int i = 0; i < fills.Length; i++)
        {
            var f = fills[i];
            double[] d = { ink[0] - f[0], ink[1] - f[1], ink[2] - f[2] };
            double len2 = d[0] * d[0] + d[1] * d[1] + d[2] * d[2];
            double t = len2 < 1 ? 0 : ((c[0] - f[0]) * d[0] + (c[1] - f[1]) * d[1] + (c[2] - f[2]) * d[2]) / len2;
            t = Math.Clamp(t, 0, 1);
            double e0 = c[0] - (f[0] + d[0] * t), e1 = c[1] - (f[1] + d[1] * t), e2 = c[2] - (f[2] + d[2] * t);
            double err = Math.Sqrt(e0 * e0 + e1 * e1 + e2 * e2);
            if (err < best) { best = err; part = i; tOut = t; }
        }
        return best;
    }

    // The outline colour: the median of the dark, fully covered pixels.
    static double[] InkColour(int[] fg)
    {
        var dark = fg.Where(p => Alpha(p) == 255).Select(Rgb).Where(c => c[0] + c[1] + c[2] < 90).ToList();
        if (dark.Count == 0) return new double[] { 15, 15, 20 };
        double Med(int ch) { var v = dark.Select(c => c[ch]).OrderBy(x => x).ToList(); return v[v.Count / 2]; }
        return new[] { Med(0), Med(1), Med(2) };
    }

    // The background colour: the median of the picture's outer 4-pixel border.
    static double[] BorderColour(int[] px, int w, int h)
    {
        var r = new List<int>(); var g = new List<int>(); var bl = new List<int>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (x >= 4 && y >= 4 && x < w - 4 && y < h - 4) continue;
                int c = px[y * w + x];
                r.Add((c >> 16) & 255); g.Add((c >> 8) & 255); bl.Add(c & 255);
            }
        r.Sort(); g.Sort(); bl.Sort();
        return new double[] { r[r.Count / 2], g[g.Count / 2], bl[bl.Count / 2] };
    }

    // Removes the green key: alpha from how green a pixel leans, colour solved from pixel = a*fg + (1-a)*key.
    static int[] Unmix(int[] px, double[] key)
    {
        double Lean(double r, double g, double b) => g - (r + b) / 2;
        double keyLean = Math.Max(1, Lean(key[0], key[1], key[2]));
        var o = new int[px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            double r = (px[i] >> 16) & 255, g = (px[i] >> 8) & 255, b = px[i] & 255;
            double a = 1 - Lean(r, g, b) / keyLean;
            if (a > 0.92) a = 1;
            if (a < 0.06) continue;
            a = Math.Min(a, 1);
            o[i] = Argb((int)Math.Round(a * 255), new[] { (r - (1 - a) * key[0]) / a, (g - (1 - a) * key[1]) / a, (b - (1 - a) * key[2]) / a });
        }
        return o;
    }

    // ---------- preview ----------

    // The base sheet in key colours, then recoloured three ways, at half size on a light ground.
    static void Preview(int[] sheet, int w, int h, double[] ink, string path)
    {
        var looks = new[]
        {
            Fills,
            new[] { new double[] { 141, 85, 54 }, new double[] { 214, 88, 66 }, new double[] { 54, 62, 84 }, new double[] { 230, 226, 214 } },
            new[] { new double[] { 247, 214, 186 }, new double[] { 96, 160, 98 }, new double[] { 70, 92, 150 }, new double[] { 120, 62, 40 } },
            new[] { new double[] { 92, 56, 38 }, new double[] { 236, 196, 70 }, new double[] { 168, 140, 104 }, new double[] { 60, 60, 60 } },
        };
        int ph = h * looks.Length;
        var full = new int[w * ph];
        for (int k = 0; k < looks.Length; k++)
            for (int i = 0; i < sheet.Length; i++)
                full[k * sheet.Length + i] = OverPaper(Decode(sheet[i], ink, looks[k]));
        using var big = FromPixels(full, w, ph);
        using var small = new Bitmap(w / 2, ph / 2, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(big, 0, 0, w / 2, ph / 2); }
        small.Save(path, ImageFormat.Png);
        Console.WriteLine($"Wrote {path}");
    }

    static int OverPaper(int c) => Blend(unchecked((int)0xFFEDE6DA), c);

    static int Blend(int under, int over)
    {
        double a = Alpha(over) / 255.0;
        int Ch(int s) => (int)Math.Round(((over >> s) & 255) * a + ((under >> s) & 255) * (1 - a));
        return unchecked((int)0xFF000000) | (Ch(16) << 16) | (Ch(8) << 8) | Ch(0);
    }

    // ---------- pixels ----------

    static List<int> Dilate(List<int> px, int w, int h, int r)
    {
        var set = new HashSet<int>(px);
        var o = new HashSet<int>(px);
        foreach (int p in px)
        {
            int x = p % w, y = p / w;
            for (int ny = Math.Max(0, y - r); ny <= Math.Min(h - 1, y + r); ny++)
                for (int nx = Math.Max(0, x - r); nx <= Math.Min(w - 1, x + r); nx++)
                    o.Add(ny * w + nx);
        }
        return o.ToList();
    }

    static List<int> DropSpecks(List<int> px, int w, int h, int minArea)
    {
        var set = new HashSet<int>(px);
        var seen = new HashSet<int>();
        var keep = new List<int>();
        foreach (int s in px)
        {
            if (!seen.Add(s)) continue;
            var comp = new List<int> { s };
            for (int k = 0; k < comp.Count; k++)
            {
                int x = comp[k] % w, y = comp[k] / w;
                for (int ny = y - 1; ny <= y + 1; ny++)
                    for (int nx = x - 1; nx <= x + 1; nx++)
                    {
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int q = ny * w + nx;
                        if (set.Contains(q) && seen.Add(q)) comp.Add(q);
                    }
            }
            if (comp.Count >= minArea) keep.AddRange(comp);
        }
        return keep;
    }

    static int Alpha(int c) => (c >> 24) & 255;
    static double[] Rgb(int c) => new double[] { (c >> 16) & 255, (c >> 8) & 255, c & 255 };
    static int Quant(double[] c) => ((int)c[0] / 24 << 16) | ((int)c[1] / 24 << 8) | (int)c[2] / 24;
    static int Clamp(double v) => v < 0 ? 0 : v > 255 ? 255 : (int)Math.Round(v);
    static int Argb(int a, double[] c) => (a << 24) | (Clamp(c[0]) << 16) | (Clamp(c[1]) << 8) | Clamp(c[2]);
    static string Fmt(double[] c) => $"({c[0]:0},{c[1]:0},{c[2]:0})";

    // Loads a picture as ARGB ints, resized to w x h when given (an edit can come back at another size).
    static (int[] px, int w, int h) Load(string path, int w = 0, int h = 0)
    {
        using var src = new Bitmap(path);
        if (w == 0) { w = src.Width; h = src.Height; }
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, 0, 0, w, h);
        }
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var px = new int[w * h];
        Marshal.Copy(data.Scan0, px, 0, px.Length);
        bmp.UnlockBits(data);
        return (px, w, h);
    }

    static Bitmap FromPixels(int[] px, int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(px, 0, data.Scan0, px.Length);
        bmp.UnlockBits(data);
        return bmp;
    }

    static void Save(int[] px, int w, int h, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bmp = FromPixels(px, w, h);
        bmp.Save(path, ImageFormat.Png);
    }
}
