# Dusty's Ganjaria File Map

One entry per file: what it does today. Read this first each session, then open only the files the task needs. Update it when a file is added, split, renamed or deleted, and when a file's job changes. Describe the current state only; history lives in git and the session log.

Line counts are rough (2026-11-25, version 0.11.0). Files over ~400 lines are marked *. Length only flags a file to look at; a long file with one job is fine.

---

## Start and shop (repo root, flat)

| File | Lines | What it does |
|---|---|---|
| `Program.cs` | 80 | Window, game loop, `Version`, window icon, and the `--shot...` screenshot flags (listed in `CLAUDE.md`). |
| `Shop.cs` * | 676 | The shop, one day at a time: day intro card, arrivals on the day's schedule, the line, the side-on shop view (layout constants at the top), taking orders, the waiting bench, patience and walkouts, tabs (Counter, Weigh, Pack, Label), ticket drops onto the station a ticket needs next (`Ticket.Next`; each station has `Start` and `TakeShipped`), finished tickets back up to the rail, the hand-over queue, closing time and the end of the day. Both shots pause the clock, arrivals and patience. |
| `Day.cs` | 106 | One shop day: the strain menu, customers per day, the arrival schedule with rushes, the wall clock, and the tally (orders, walkouts, metric averages, service quality). |
| `Progress.cs` | 60 | What carries between days (day, tip jar, rank points), rank names and steps, the save at `%APPDATA%\DustysGanjaria\save.json`. |
| `Customer.cs` | 91 | One customer: name, looks, order, strain and container kind wanted, patience, walking (`Pos` is the feet). |
| `Ticket.cs` | 33 | An order ticket: the station it needs next, the weigh result once weighed. |
| `TicketRail.cs` | 103 | The rail across the top of every screen: slots and dragging; the shop decides what a drop does. |
| `Scoring.cs` | 190 | All scoring tuning: the four metric names, green band width, par times (`WeighPar`, `PackPar`, `LabelPar`, `WaitPar`), slip, spill and wrong-strain costs, shares, Packing and Labeling scores (`PlacementFor`), Start over cost, grades, line and wait patience, tips, reactions. |
| `PlayLog.cs` | 120 | The play log: one CSV row per served order and per walkout (every part score) appended to `%APPDATA%\DustysGanjaria\playlog.csv`; column list at the top. A header change renames the old file and starts a new one. Off in screenshot runs. |

## Shots and day end

| File | Lines | What it does |
|---|---|---|
| `OrderShot.cs` | 60 | Over-the-shoulder order shot: speech lines, items written onto the notepad, click to skip. |
| `HandOver.cs` | 115 | Hand-over shot: the packed container slides over, the customer checks it ("hmm" face, bag crinkle), soft chime, scores land row by row, reaction, tip into the jar. |
| `DayEnd.cs` | 159 | Day results panel, then the tip jar and rank screen. |

## Stations

| File | Lines | What it does |
|---|---|---|
| `WeighStation.cs` * | 654 | Weigh flow: a jar per menu strain, bag onto the scale, toss buds from the ordered strain's jar, zip, throw into the OUT bin. Layout, zip, bin and seal tuning at the top; feedback tuning (needle spring, tick and puff speeds, ding timing, zip teeth and shake, result delay) in its own block below the fields. |
| `PackStation.cs` * | 578 | Pack flow: container from the shelf onto the mat, bag from the IN tray into it, jar lid in sideways strokes or pouch strip; Start over. Also `ContainerKind`, `Container` and `Containers` (sizes, which one an order needs). Layout and lid, strip and bag tuning at the top. |
| `LabelStation.cs` | 342 | Label flow: closed container on the mat, a label pad per menu strain, carry a label (swings with the mouse), let go to stick loosely, hold still to press, drag to peel. Layout, swing and press tuning at the top; `LabelOn` places a label on any container front (the hand-over uses it). |
| `Strain.cs` | 54 | The strains (name, bud colours, bud size mix, label colour, unlock rank) and the day menu (`Strains.MenuFor`: up to 3, the newest always on). |
| `Physics.cs` | 163 | Small 2D physics: circles for buds (gravity, bud-bud pushes, line-segment walls; the held bud is immovable) and `StepBox` for the thrown sealed bag (axis-aligned walls). |
| `Bag.cs` | 52 | The baggie: position, zip progress, sealed, walls; `SizeFor` and `SealedScale` (shared with Pack). |
| `Bud.cs` | 59 | One bud; radius follows the square root of weight. |

## Drawing, sound, effects

| File | Lines | What it does |
|---|---|---|
| `Art.cs` * | 528 | `partial class Art`, part 1: font and text (`Label` / `LabelCentered` / `LabelTurned` / `Measure`), weigh station pieces (buds, jars, scale, bags, bin), tickets and rail, shop view (room, door, bench, budtender, customers, patience bars), the order shot, tabs, buttons and panels, and the F1 tuning panel (`DrawTuning`, `TuningLine`). Each station's `TuningLines()` and `Shop.DrawTuning` supply its lines. |
| `ArtScreens.cs` | 351 | `Art` part 2: day card, hand-over, results and tip jar drawing. |
| `ArtPack.cs` | 167 | `Art` part 3: the pack station's pieces. |
| `ArtLabel.cs` | 80 | `Art` part 4: the label station's rack, pads and labels. |
| `Audio.cs` | 268 | Every sound by name (`Audio.Play(name, volume, pitch)`), a few copies each, random pitch per play. Placeholders made in code; `resources/sounds/<name>.wav` replaces one (names at the top). |
| `Effects.cs` | 108 | Particles (leaf bits) and floating pop-up words. Visual only, never scoring. |

## Build and project

| File | What it does |
|---|---|
| `DustysGanjaria.csproj` | .NET 10 project, Raylib-cs, `ApplicationIcon`. |
| `Build Ganjaria.bat` | Publishes Release to `..\Dustys Ganjaria`, makes Desktop and Start menu shortcuts. |

## Folders

| Folder | What it holds |
|---|---|
| `resources/fonts/` | `LilitaOne-Regular.ttf`, the game font, and `OFL.txt` (keep together). |
| `resources/icon.ico`, `resources/icon.png` | App and window icon, made by `art/icon/make_ico.ps1`. |
| `resources/art-reference/` | Customer art base (`base.jpg`), hair edit test, the owner's icon picture. |
| `art/icon/` | Icon scripts: `make_ico.ps1` (used), `draw_icon.py` and the Blender files (kept for reference). |
| `claude/` | Notes for Claude: `vision.md`, `session-log.md`, this map, `mechanics-page.html`, copies of `versioning.md` and `readme-format.md`, `reference/` (two genre screenshots for camera framing only). |

## Where to look for X

| Looking for | Go to |
|---|---|
| A score, par time, tip or patience number | `Scoring.cs` |
| Customers per day, arrival gaps, rushes | `Day.cs` |
| How a station feels (springs, speeds, tolerances) | The tuning block at the top of that station's file |
| What a strain looks like or when it unlocks | `Strain.cs` |
| Where something is drawn | `Art*.cs` (the four `Art` parts above) |
| What a drop on the rail does | `Shop.cs` |
| Rank names and steps, the save file | `Progress.cs` |
| What was logged from play, and its columns | `PlayLog.cs`; the file is `%APPDATA%\DustysGanjaria\playlog.csv` |
| A value missing from the F1 panel | That station's `TuningLines()`, or `Shop.DrawTuning` for the Counter |
