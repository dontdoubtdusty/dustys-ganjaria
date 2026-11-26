// Makes recolourable sprite sheets with Gemini, through the shared image kit (../claude/kit/imagegen).
// Usage (from the repo root):
//   node tools/gen-sprites.js prompt <sheet>               print the prompt only (free)
//   node tools/gen-sprites.js gen <sheet>... [--n 1] [--model pro|nb] [--cap 2]   generate candidates
//   node tools/gen-sprites.js review                       write and open the review page of all candidates
//   node tools/gen-sprites.js cut <sheet> <candidate>      cut a picked candidate into resources/sprites/<sheet>.png
// Sheets and prompt parts live in art/sprites/sheets.json; an entry can override any prompt part and set "aspect",
// "refs" (reference pictures sent along, repo-relative), "mode" (graymask or color for the cut) and "cell", or be an
// "edit" (its own whole prompt, for editing a reference picture). Candidates and the spend log go to art/sprites/candidates/
// (git-ignored); the cap counts everything spent there. A cut keeps a copy of its source in art/sprites/picked/ and
// writes art/sprites/<sheet>-preview.png, the sheet coloured as each strain in Strain.cs.
"use strict";
const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");

const root = path.join(__dirname, "..");
const kitDir = path.join(root, "..", "claude", "kit", "imagegen");
const kit = require(path.join(kitDir, "imagegen.js"));
const guide = JSON.parse(fs.readFileSync(path.join(root, "art", "sprites", "sheets.json"), "utf8"));
const candDir = path.join(root, "art", "sprites", "candidates");
const { pos, opt } = kit.parseArgs(process.argv.slice(2));
const cmd = pos.shift();

// The guide entry for a sheet name, or an error listing the sheets.
function sheetFor(name) {
  const s = guide.sheets[name];
  if (!s) throw new Error(`Unknown sheet "${name}". Sheets: ${Object.keys(guide.sheets).join(", ")}`);
  return s;
}

// The full prompt for one sheet: an edit entry's own prompt as is, else style, subject, colour, background and layout,
// each from the entry if it has its own, else from the guide.
function buildPrompt(name) {
  const s = sheetFor(name);
  if (s.edit) return s.edit;
  const part = k => s[k] ?? guide[k];
  const layout = part("layout").replace("{count}", s.count).replace("{grid}", s.grid);
  return [`Art style: ${part("style")}`, `Subject: ${s.subject}`, `Colour: ${part("colour")}`,
    `Background: ${part("background")}`, `Layout: ${layout}`].join("\n\n");
}

// The reference pictures an entry sends along (paths relative to the repo root), checked to exist.
function refsFor(name) {
  return (sheetFor(name).refs || []).map(r => {
    const p = path.join(root, r);
    if (!fs.existsSync(p)) throw new Error(`${name}: reference picture missing: ${r}`);
    return p;
  });
}

// Reads every strain's name, shades and hair colour from Strain.cs for the preview. The art's gray range runs from
// outline to highlight, so each strain's shades (sorted dark to light) are widened: a near-black outline and a shadow
// below them, a highlight above. The game's shader should build its ramp the same way.
function strainPalettes() {
  const src = fs.readFileSync(path.join(root, "Strain.cs"), "utf8");
  const rgb = s => s.split(",").slice(0, 3).map(Number);
  const lum = c => 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2];
  const re = /new\("([^"]+)",\s*"[^"]+",\s*new Color\[\] \{([^}]*)\},\s*new\(([^)]*)\)/g;
  const out = [];
  for (let m; (m = re.exec(src));) {
    const shades = [...m[2].matchAll(/new\(([^)]*)\)/g)].map(x => rgb(x[1])).sort((a, b) => lum(a) - lum(b));
    const mix = (c, t, k) => c.map((v, i) => Math.round(v + (t[i] - v) * k));
    const stops = [mix(shades[0], [0, 0, 0], 0.75), mix(shades[0], [0, 0, 0], 0.4), ...shades, mix(shades.at(-1), [255, 255, 255], 0.5)];
    out.push({ name: m[1], stops, accent: rgb(m[3]) });
  }
  if (!out.length) throw new Error("Could not read the strains from Strain.cs.");
  return out;
}

// Cuts a picked candidate into the game's sheet and writes the strain preview.
function cut(name, file) {
  const s = sheetFor(name);
  const src = path.resolve(file);
  const picked = path.join(root, "art", "sprites", "picked", name + path.extname(src));
  fs.mkdirSync(path.dirname(picked), { recursive: true });
  fs.copyFileSync(src, picked);
  const mode = s.mode || "graymask";
  const args = ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", path.join(kitDir, "cutout.ps1"),
    "-In", picked, "-Out", path.join(root, "resources", "sprites", name), "-Cell", String(s.cell || 128),
    "-Merge", String(s.merge || 0), "-Mode", mode];
  if (mode === "graymask") {
    const pal = path.join(candDir, "palettes.json");
    fs.mkdirSync(candDir, { recursive: true });
    fs.writeFileSync(pal, JSON.stringify(strainPalettes()));
    args.push("-Palettes", pal, "-PreviewOut", path.join(root, "art", "sprites", name + "-preview.png"));
  }
  execFileSync("powershell", args, { stdio: "inherit" });
}

// Runs the chosen command.
(async () => {
  try {
    if (cmd === "prompt") for (const n of pos) console.log(`=== ${n}\n\n${buildPrompt(n)}\n`);
    else if (cmd === "gen") {
      if (!pos.length) throw new Error(`Name the sheets: ${Object.keys(guide.sheets).join(", ")}`);
      for (const n of pos)
        await kit.genBatch({ dir: candDir, name: n, prompt: buildPrompt(n), model: opt.model || "pro",
          n: Number(opt.n || 1), cap: Number(opt.cap || 2), aspect: sheetFor(n).aspect || "1:1", refs: refsFor(n) });
    } else if (cmd === "review")
      kit.contactSheet({ dir: candDir, title: "Dusty's Ganjaria sprite candidates", bg: "#2a2622",
        footer: "Pick one with: node tools/gen-sprites.js cut <sheet> <file>" });
    else if (cmd === "cut") cut(pos[0], pos[1]);
    else throw new Error("Usage: node tools/gen-sprites.js prompt|gen|review|cut ...  (see the top of this file)");
  } catch (e) {
    console.error(e.message);
    process.exit(1);
  }
})();
