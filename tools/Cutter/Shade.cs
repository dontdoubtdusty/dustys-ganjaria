// shade: flattens a lineup's fills and adds the same cel shading to every character by rule, so the shading never
// differs between pictures the way the image model's own shading does.
//   dotnet run --project tools/Cutter -- shade <lineup>
// Writes art/sprites/shade-<lineup name>.png: the original above, flattened and shaded below.
//
// How it works:
//   1. Remove the green key. Flat areas (pixels whose neighbours match them) grow into regions, one per fill.
//   2. Regions the model shaded are merged back: a region touching another directly (no outline between them) is
//      folded into it when it is that colour darkened (a shadow shape) or lightened (a shine), or when it is tiny.
//   3. Every other pixel (outline, soft edges) takes the nearby region whose fill-to-ink mix explains it best.
//   4. Shading by rule, light from the upper left: a band along each region's lower-right edge is darkened, and a
//      thin band inset from the upper-left edge of each character's top region (hair, hat or bald head) is lightened.
//      The band widths scale with the character's height, so every character gets the same look.
using System.Text.Json;

static partial class Cutter
{
    const double ShadowWidth = 0.028;   // of the character's height
    const double ShineInset = 0.008, ShineWidth = 0.010;
    const double ShadowMul = 0.80;      // shadow = fill * this, nudged toward cool purple
    static readonly double[] ShadowTint = { 60, 40, 110 };
    const double ShadowTintAmount = 0.10;
    const double ShineMix = 0.55;       // shine = fill mixed this far toward white

    static void Shade(string lineupPath)
    {
        var (src, shaded, w, h, _, _) = ShadeLineup(lineupPath);
        var outPx = new int[w * h * 2];
        for (int p = 0; p < w * h; p++) { outPx[p] = Over(src[p]); outPx[w * h + p] = Blend(Paper, shaded[p]); }
        string name = Path.GetFileNameWithoutExtension(lineupPath);
        string outPath = Path.Combine(Root, "art", "sprites", $"shade-{name}.png");
        Save(outPx, w, h * 2, outPath);
        Console.WriteLine($"Wrote {outPath}");
    }

    // The lineup flattened and shaded, as straight-alpha pixels (see the top of this file), with its characters and
    // outline ink colour.
    static (int[] src, int[] shaded, int w, int h, List<Figure> figs, double[] ink) ShadeLineup(string lineupPath)
    {
        area = rtop = rbot = null;
        var (src, w, h) = Load(lineupPath);
        var key = BorderColour(src, w, h);
        var fg = KeepInterior(src, Unmix(src, key), key, w, h);
        var ink = InkColour(fg);
        Console.WriteLine($"Ink {Fmt(ink)}");

        // 1. Flat regions.
        var label = new int[w * h];
        Array.Fill(label, -1);
        var cols = new List<double[]>();
        var sizes = new List<int>();
        bool Core(int p)
        {
            if (Alpha(fg[p]) < 250) return false;
            var c = Rgb(fg[p]);
            if (Distance(c, ink) < 30) return false;
            int x = p % w, y = p / w;
            if (x == 0 || y == 0 || x == w - 1 || y == h - 1) return false;
            foreach (int q in new[] { p - 1, p + 1, p - w, p + w })
                if (Alpha(fg[q]) < 250 || Distance(Rgb(fg[q]), c) > 16) return false;
            return true;
        }
        var core = new bool[w * h];
        for (int p = 0; p < core.Length; p++) core[p] = Core(p);
        for (int s = 0; s < core.Length; s++)
        {
            if (!core[s] || label[s] >= 0) continue;
            int id = cols.Count;
            var seed = Rgb(fg[s]);
            var px = new List<int> { s };
            label[s] = id;
            for (int k = 0; k < px.Count; k++)
            {
                int p = px[k], x = p % w;
                foreach (int q in new[] { p - 1, p + 1, p - w, p + w })
                {
                    if (q < 0 || q >= core.Length || Math.Abs(q % w - x) > 1 || !core[q] || label[q] >= 0) continue;
                    if (Distance(Rgb(fg[q]), Rgb(fg[p])) > 14 || Distance(Rgb(fg[q]), seed) > 40) continue;
                    label[q] = id; px.Add(q);
                }
            }
            cols.Add(Median(px.Select(p => Rgb(fg[p]))));
            sizes.Add(px.Count);
        }
        Console.WriteLine($"{cols.Count} flat regions");

        // 2. Merge shading shapes and crumbs into the region they touch directly.
        var parent = Enumerable.Range(0, cols.Count).ToArray();
        int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        for (int pass = 0; pass < 3; pass++)
        {
            var touch = new Dictionary<(int, int), int>();
            for (int p = 0; p < label.Length; p++)
            {
                if (label[p] < 0) continue;
                int a = Find(label[p]);
                // Direct contact: the next region right or down within 4 px, across soft-edge pixels but no outline.
                int px0 = p % w;
                foreach (int step in new[] { 1, w })
                    for (int k = 1; k <= 4; k++)
                    {
                        int q = p + step * k;
                        if (q >= label.Length || (step == 1 && q % w < px0)) break;
                        if (label[q] < 0)
                        {
                            if (Alpha(fg[q]) < 250 || Distance(Rgb(fg[q]), ink) < 90) break;
                            continue;
                        }
                        int b = Find(label[q]);
                        if (a != b)
                        {
                            var k2 = a < b ? (a, b) : (b, a);
                            touch[k2] = touch.GetValueOrDefault(k2) + 1;
                        }
                        break;
                    }
            }
            int merged = 0;
            var size = new Dictionary<int, int>();
            for (int i = 0; i < cols.Count; i++) size[Find(i)] = size.GetValueOrDefault(Find(i)) + sizes[i];
            foreach (var ((a0, b0), n) in touch.OrderByDescending(kv => kv.Value))
            {
                int a = Find(a0), b = Find(b0);
                if (a == b || n < 6) continue;
                int big = size[a] >= size[b] ? a : b, small = big == a ? b : a;
                var cb = cols[big]; var cs = cols[small];
                bool shadow = IsScaled(cs, cb, 0.55, 0.93);
                bool shine = IsTowardWhite(cs, cb);
                bool crumb = size[small] < 60;
                if (!(shadow || shine || crumb)) continue;
                parent[small] = big; size[big] += size[small]; merged++;
            }
            if (merged == 0) break;
            Console.WriteLine($"Merged {merged} shading shapes and crumbs");
        }
        // Same fill on both sides of a thin line (a nose line, a fold) is one region.
        int joined = 0;
        for (int p = 0; p < label.Length; p++)
        {
            if (label[p] < 0) continue;
            int x0 = p % w, y0 = p / w;
            foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (1, 1), (1, -1) })
                for (int k = 1; k <= 9; k++)
                {
                    int x = x0 + dx * k, y = y0 + dy * k;
                    if (x < 0 || y < 0 || x >= w || y >= h) break;
                    int q = y * w + x;
                    if (label[q] >= 0)
                    {
                        int a = Find(label[p]), b = Find(label[q]);
                        if (k > 1 && a != b && Distance(cols[a], cols[b]) < 14) { parent[b] = a; joined++; }
                        break;
                    }
                    if (Alpha(fg[q]) < 128) break;
                }
        }
        Console.WriteLine($"Joined {joined} pieces split by lines");
        for (int p = 0; p < label.Length; p++) if (label[p] >= 0) label[p] = Find(label[p]);

        // 3. Outline and soft-edge pixels: the nearby region that explains them best, with how much ink.
        var t = new double[w * h];
        var region = (int[])label.Clone();
        for (int p = 0; p < label.Length; p++)
        {
            if (label[p] >= 0 || Alpha(fg[p]) == 0) continue;
            int x = p % w, y = p / w;
            var c = Rgb(fg[p]);
            double best = double.MaxValue; int bestId = -1; double bestT = 1;
            var seen = new HashSet<int>();
            for (int ny = Math.Max(0, y - 3); ny <= Math.Min(h - 1, y + 3); ny++)
                for (int nx = Math.Max(0, x - 3); nx <= Math.Min(w - 1, x + 3); nx++)
                {
                    int id = label[ny * w + nx];
                    if (id < 0 || !seen.Add(id)) continue;
                    double err = Classify(c, ink, new[] { cols[id] }, out _, out double tt);
                    if (err < best) { best = err; bestId = id; bestT = tt; }
                }
            region[p] = bestId;
            t[p] = bestId < 0 ? 1 : bestT;
            // A little ink on an opaque pixel is the soft seam of a merged shading shape, not an outline: plain fill.
            if (bestId >= 0 && t[p] < 0.3 && Alpha(fg[p]) >= 250) t[p] = 0;
        }

        // 4. Characters (for the band widths and the top region), then shading.
        var figs = FindFigures(fg, w, h);
        var height = new double[w * h];
        var figArea = new double[w * h];
        var topRegion = new HashSet<int>();
        foreach (var f in figs)
        {
            double fh = f.MaxY - f.MinY + 1;
            foreach (int p in f.Pixels) { height[p] = fh; figArea[p] = f.Pixels.Count; }
            // The top region: the region of the highest flat pixel near the top centre third.
            int found = -1;
            for (int y = f.MinY; y <= f.MaxY && found < 0; y++)
                for (int x = f.MinX; x <= f.MaxX; x++)
                    if (label[y * w + x] >= 0 && sizes.Count > 0) { found = label[y * w + x]; break; }
            if (found >= 0) topRegion.Add(found);
        }
        // Light comes from the upper left; v points away from it.
        double vx = 0.86, vy = 0.5;
        bool Inside(int id, double fx, double fy)
        {
            int x = (int)Math.Round(fx), y = (int)Math.Round(fy);
            if (x < 0 || y < 0 || x >= w || y >= h) return false;
            int q = y * w + x;
            return region[q] == id && t[q] < 0.5;
        }

        var shaded = new int[w * h];
        for (int p = 0; p < w * h; p++)
        {
            int x = p % w, y = p / w;
            int a = Alpha(fg[p]);
            if (a == 0) continue;
            int id = region[p];
            double[] fill = id >= 0 ? cols[id] : ink;
            double fh = height[p] > 0 ? height[p] : 500;
            if (id >= 0 && sizes[id] > 0 && t[p] < 0.98)
            {
                double d = ShadowWidth * fh;
                double fa = figArea[p] > 0 ? figArea[p] : 80000;
                // Four widths around d, so the band's inner edge is soft rather than stair-stepped.
                double shade = 0;
                if (Area(id) > 0.02 * fa && Distance(fill, ink) > 45)
                    foreach (double dd in new[] { d - 1.5, d - 0.5, d + 0.5, d + 1.5 })
                        if (EdgeWithin(id, x, y, dd, fa)) shade += 0.25;
                double si = ShineInset * fh, sw = (ShineInset + ShineWidth) * fh;
                bool shine = topRegion.Contains(id) && Inside(id, x - vx * si, y - vy * si) && !Inside(id, x - vx * sw, y - vy * sw)
                             && y < RegionTop(id) + 0.35 * RegionHeight(id);
                if (shade > 0) fill = Mix(fill, Mix(Scale(fill, ShadowMul), ShadowTint, ShadowTintAmount), shade);
                if (shade < 1 && shine) fill = Mix(fill, new double[] { 255, 255, 255 }, ShineMix);
            }
            shaded[p] = Argb(a, Mix(fill, ink, t[p]));
        }
        return (src, shaded, w, h, figs, ink);

        // True when walking from (x, y) away from the light meets this region's real edge within d: the background or
        // another big region. Outline ink and small regions (eyes, mouths, buttons) are walked through, and the
        // region carrying on past them means the line was inside it (a fold, a nose), not its edge.
        bool EdgeWithin(int id, int x, int y, double d, double fa)
        {
            for (double k = 1; k <= d + 10; k += 1)
            {
                int qx = (int)Math.Round(x + vx * k), qy = (int)Math.Round(y + vy * k);
                if (qx < 0 || qy < 0 || qx >= w || qy >= h) return k <= d;
                int q = qy * w + qx;
                if (Alpha(fg[q]) < 128) return k <= d;
                int r = region[q];
                bool through = r < 0 || t[q] >= 0.5 || (r != id && Area(r) < 0.04 * fa);
                if (through) continue;
                if (r == id) { if (k > d) return false; continue; }
                return k <= d;
            }
            return false;
        }

        // Region areas and extents, computed once on first use.
        int Area(int id) { Extents(); return area![id]; }
        int RegionTop(int id) { Extents(); return rtop![id]; }
        int RegionHeight(int id) { Extents(); return rbot![id] - rtop![id] + 1; }
        void Extents()
        {
            if (area != null) return;
            area = new int[cols.Count]; rtop = Enumerable.Repeat(h, cols.Count).ToArray(); rbot = new int[cols.Count];
            for (int p = 0; p < label.Length; p++)
            {
                int id = label[p]; if (id < 0) continue;
                area[id]++; rtop[id] = Math.Min(rtop[id], p / w); rbot[id] = Math.Max(rbot[id], p / w);
            }
        }
    }
    static int[]? area, rtop, rbot;

    // Only pixels near the background can be see-through: the key test reads yellows and light greens as partly
    // green, so anything with no clearly-background pixel within 3 px keeps its own colour, fully opaque.
    static int[] KeepInterior(int[] src, int[] fg, double[] key, int w, int h)
    {
        double keyLean = key[1] - (key[0] + key[2]) / 2;
        var bg = new List<int>();
        for (int p = 0; p < src.Length; p++)
        {
            double r = (src[p] >> 16) & 255, g = (src[p] >> 8) & 255, b = src[p] & 255;
            if (g - (r + b) / 2 > 0.6 * keyLean) bg.Add(p);
        }
        var near = new HashSet<int>(Dilate(bg, w, h, 3));
        // Small background holes (a gap between legs) can be too dark to pass the test above, but nothing in the cast
        // is green, so green-leaning pixels go by the key test too.
        for (int p = 0; p < src.Length; p++)
        {
            double r = (src[p] >> 16) & 255, g = (src[p] >> 8) & 255, b = src[p] & 255;
            if (g - Math.Max(r, b) > 0.1 * keyLean) near.Add(p);
        }
        var o = (int[])fg.Clone();
        for (int p = 0; p < o.Length; p++) if (!near.Contains(p)) o[p] = src[p] | unchecked((int)0xFF000000);
        return o;
    }

    static readonly int Paper = unchecked((int)0xFFEDE6DA);
    static int Over(int c) => Blend(Paper, c);

    // True when s is c scaled down by one factor in [lo, hi] (a shadow shape of c).
    static bool IsScaled(double[] s, double[] c, double lo, double hi)
    {
        double num = 0, den = 0;
        for (int i = 0; i < 3; i++) { num += s[i] * c[i]; den += c[i] * c[i]; }
        if (den < 1) return false;
        double k = num / den;
        if (k < lo || k > hi) return false;
        return Distance(s, Scale(c, k)) < 22;
    }

    // True when s is c mixed toward white (a shine on c).
    static bool IsTowardWhite(double[] s, double[] c)
    {
        double[] wh = { 255, 255, 255 };
        double[] d = { wh[0] - c[0], wh[1] - c[1], wh[2] - c[2] };
        double len2 = d[0] * d[0] + d[1] * d[1] + d[2] * d[2];
        if (len2 < 400) return false;
        double k = ((s[0] - c[0]) * d[0] + (s[1] - c[1]) * d[1] + (s[2] - c[2]) * d[2]) / len2;
        if (k < 0.12 || k > 0.9) return false;
        return Distance(s, Mix(c, wh, k)) < 22;
    }

    static double[] Scale(double[] c, double k) => new[] { c[0] * k, c[1] * k, c[2] * k };
    static double[] Mix(double[] a, double[] b, double k) =>
        new[] { a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k, a[2] + (b[2] - a[2]) * k };
    static double[] Median(IEnumerable<double[]> cs)
    {
        var l = cs.ToList();
        double M(int ch) { var v = l.Select(c => c[ch]).OrderBy(x => x).ToList(); return v[v.Count / 2]; }
        return new[] { M(0), M(1), M(2) };
    }
}
