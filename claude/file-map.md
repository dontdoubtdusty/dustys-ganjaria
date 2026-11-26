# Dusty's Ganjaria File Map

One entry per file: what it does today. Read this first each session, then open only the files the task needs. Update it when a file is added, split, renamed or deleted, and when a file's job changes. Describe the current state only; history lives in git and the session log.

Line counts are rough (2026-11-25, version 0.16.0; files touched in 0.19.0 counted 2026-11-26). Files over ~400 lines are marked *. Length only flags a file to look at; a long file with one job is fine.

---

## Start and shop (repo root, flat)

| File | Lines | What it does |
|---|---|---|
| `Program.cs` | 120 | Window, game loop (drawn through `View`'s camera), `Version`, window icon, settings, volume and full screen at start, F11 toggles full screen, Esc opens the pause menu (the shop does not update while it is open), and the `--shot...` screenshot flags (listed in `CLAUDE.md`, with `--body`). |
| `View.cs` | 55 | Full screen and scaling: the game always lays out in 1280 x 720; a Camera2D scales it evenly to the window, black bars cover anything drawn past the edges, the mouse is mapped back (`SetMouseScale`), `ToggleFullscreen` (borderless, saved in settings), `BeginScissor` for clip rects in game units. |
| `Shop.cs` * | 818 | The shop, one day at a time: day intro card, arrivals on the day's schedule, the line, the side-on shop view (layout constants at the top), taking orders, the waiting bench, patience and walkouts, tabs (Counter, Weigh, Pack, Label), ticket drops onto the station a ticket needs next (`Ticket.Next`; each station has `Start` and `TakeShipped`), finished tickets back up to the rail, the hand-over queue, closing time and the end of the day. Both shots pause the clock, arrivals and patience. Also the first-time pointers (`DrawPointer`, `Learn`), the Counter's F1 lines (`DrawTuning`), and `Pause` / `RestartDay` / `DayInProgress` for the pause menu. |
| `Day.cs` | 116 | One shop day: its date (`Date`), the strain menu, customers per day, the arrival schedule with rushes, the wall clock, and the tally (orders, walkouts, IDs caught and missed, tips less fines, metric averages, service quality). |
| `Progress.cs` | 62 | What carries between days (day, tip jar, rank points, `Learned` steps whose pointers no longer show), rank names and steps, the save at `%APPDATA%\DustysGanjaria\save.json`. |
| `Customer.cs` | 148 | One customer: name, looks (shirt, trousers, placeholder hair; `FarShirt` and `OtherHair` for photos that do not match), their `Id`, order, strain and container kind wanted (`ContainerText` "Large jar" and `ContainerShort` "Jar L" add the size the grams need), patience, walking (`Pos` is the feet). Sprite customers: `Body` (index into Art's cast, -1 = placeholder), picked so nobody in the shop appears twice, with a name that fits (women's or men's list) and that nobody in the shop has. |
| `Ticket.cs` | 35 | An order ticket: the station it needs next, the weigh result and the bag's `BagFill` once weighed. |
| `TicketRail.cs` | 106 | The rail across the top of every screen: slots and dragging (`CardRect`, `FirstFor`); the shop decides what a drop does. |
| `Scoring.cs` | 260 | All scoring tuning: the four metric names, the weigh-heavy room (`RoomFor`) and lopsided accuracy (under fails), crumb cost, giveaway price and heavy-bag tip share, looks requests (`Looks`, `Meets`), par times (`WeighPar`, `PackPar`, `LabelPar`, `WaitPar`), slip, spill and wrong-strain costs, shares, Packing and Labeling scores (`PlacementFor`), Start over cost, grades, line and wait patience, tips, reactions. |
| `PlayLog.cs` | 127 | The play log: one CSV row per served order and per walkout (every part score) appended to `%APPDATA%\DustysGanjaria\playlog.csv`; column list at the top. A header change renames the old file and starts a new one. Off in screenshot runs. |
| `PauseMenu.cs` | 110 | The Esc pause menu: Resume, volume slider, Full screen, Restart day and Quit (both confirm while a day is in progress). Layout at the top. |
| `Settings.cs` | 43 | Player settings (volume, full screen), saved apart from progress at `%APPDATA%\DustysGanjaria\settings.json`. |

## Shots and day end

| File | Lines | What it does |
|---|---|---|
| `OrderShot.cs` | 132 | Over-the-shoulder order shot: greeting, the ID check (card, date sign, Accept and Refuse; no timer; `Refused`), then speech lines and items written onto the notepad, click to skip (never past the ID). Layout rects at the top. |
| `IdCard.cs` | 70 | `IdFlaw`, `IdCard` and `Ids`: the shop calendar (`DateOf`, day 1 is Apr 20, 2026), the 21 cutoff, date text, and making a card with one flaw or none (close calls included). The photo is `PhotoBody` (a cast member; a Photo flaw shows another person, via `Art.OtherBody`) or, for placeholders, shirt and hair colours. |
| `HandOver.cs` | 128 | Hand-over shot: the packed container slides over, the customer checks it ("hmm" face, bag crinkle), soft chime, scores land row by row, reaction, tip into the jar. |
| `DayEnd.cs` | 167 | Day results panel, then the tip jar and rank screen. |

## Stations

| File | Lines | What it does |
|---|---|---|
| `WeighStation.cs` * | 972 | Weigh flow: a jar per menu strain (each topped up with small buds, and checked that whole pieces can land in the band, `CanLandInBand`), tongs pick nugs onto the weigh tray (weight lag, crumbs from hard landings), carry the tray over the open bag to pour (tilt, riders; any number of scoops, the needle shows bag plus tray), zip, throw into the OUT bin. Layout, tongs, pouring, zip, bin and seal tuning at the top; feedback tuning (needle spring, tick and puff speeds, ding timing, zip teeth and shake, result delay) in its own block below the fields. |
| `PackStation.cs` * | 605 | Pack flow: container from the shelf onto the mat, bag from the IN tray into it, jar lid in sideways strokes or pouch strip; Start over. Also `ContainerKind`, `Container` and `Containers` (sizes, which one an order needs). Layout and lid, strip and bag tuning at the top. |
| `LabelStation.cs` | 371 | Label flow: closed container on the mat, a label pad per menu strain, carry a label (swings with the mouse), let go to stick loosely, hold still to press, drag to peel. Layout, swing and press tuning at the top; `LabelOn` places a label on any container front (the hand-over uses it). |
| `Strain.cs` | 54 | The strains (name, bud colours, bud size mix, label colour, unlock rank) and the day menu (`Strains.MenuFor`: up to 3, the newest always on). |
| `Physics.cs` | 164 | Small 2D physics: circles for buds (gravity, bud-bud pushes, line-segment walls; the held bud is immovable) and `StepBox` for the thrown sealed bag (axis-aligned walls). |
| `Bag.cs` | 52 | The baggie: position, zip progress, sealed, walls; `SizeFor` and `SealedScale` (shared with Pack). |
| `BagFill.cs` | 19 | The nugs sealed in a bag, as they lay when the zip closed (bag size plus each bud and its offset). The weigh station makes it on sealing; Pack, Label and the hand-over draw it through `Art.DrawSealedBag`. |
| `Bud.cs` | 86 | One piece of flower; radius follows the square root of weight. Its kind (`IsShake`, `IsLarge`, else small) is fixed when the jar is filled, never by weight, so a chipped bud keeps its sprite and never turns into shake for "No shake". `Chip` takes a crumb off it; `Riding` buds sit on the carried weigh tray with no physics; `Dropped` marks a bud let go by the tongs until it lands. `Pick` chooses its sprite; `SpriteAngle` keeps sprites upright with a small tilt and adds only the rolling since it was made. |

## Drawing, sound, effects

| File | Lines | What it does |
|---|---|---|
| `Art.cs` * | 618 | `partial class Art`, part 1: font and text (`Label` / `LabelCentered` / `LabelTurned` / `Measure`), weigh station pieces (buds, jars, scale with its green band and red under-zone, weigh tray, tongs, bags, bin), tickets and rail, shop view (room, door, bench, budtender, customers, patience bars), the order shot, tabs, buttons and panels, the first-time pointer arrow (`DrawPointer`) and the F1 tuning panel (`DrawTuning`, `TuningLine`). Each station's `TuningLines()` and `Shop.DrawTuning` supply its lines. |
| `ArtScreens.cs` | 396 | `Art` part 2: day card, pause panel and volume slider, hand-over, results and tip jar drawing. |
| `ArtPack.cs` | 183 | `Art` part 3: the pack station's pieces, and the sealed bag (`DrawSealedBag` draws a ticket's real nugs from its `BagFill`) and packed container shared with Label and the hand-over. |
| `ArtLabel.cs` | 87 | `Art` part 4: the label station's rack, pads and labels. |
| `ArtId.cs` | 79 | `Art` part 5: placeholder hair, the ID card and its photo (the cast member's head and shoulders, or a placeholder head), the date sign, the red Refuse button. |
| `ArtSprites.cs` | 178 | `Art` part 6: the mid-century nug sheets `resources/sprites/mc-nugs-large`, `mc-nugs-small`, `mc-shake` (png plus json grid) and the nug shader, which colours the gray-mask nug sheets per strain (ramp from the strain's shades widened with an outline, shadow and highlight; hair tinted with `Strain.Hair`). `DrawBud` uses a sprite when its sheet is loaded (shake, small or large by the bud's kind), else the placeholder lobes. `DrawNoShakeSample`: a crossed-out shake piece beside "NO SHAKE" on the station ticket. |
| `ArtCustomers.cs` | 253 | `Art` part 7: the customer cast from `tools/Cutter roster --flat` (`resources/sprites/customer-roster.png` + `.json`: one cell per pose, named `<character>@<pose>`, feet, head top, face x). Poses: calm, happy, cross, hmm (calm with another face) and action. `PoseFor` picks one: hmm in the bag check, the hand-over mood, happy or cross leaving, cross below 30% patience, the action now and then while standing still (`ActionEvery`, `ActionFor`), else calm. Motion: walk bob and sway, idle breathing, a hop on turning happy (tuning constants at the top). In the shots (`inShot`) short people are lifted toward the average head height (`ShotLift`) and everyone slides sideways so their face is where the average face is (a stooped person no longer hides behind the notepad). `ForceBody` (screenshot `--body`) makes every customer one cast member. One mipmapped texture, one scale for all (calm height), mirrored when facing left; `PickBody` (nobody twice), `OtherBody` (another person for a bad ID photo), `BodyIsFeminine` (for names), `DrawIdPhotoSprite` (calm head-and-shoulders crop); `SpeechAnchor` lifts the Ready bubble above tall hair. Without the sheet, customers stay placeholders. |
| `Audio.cs` | 268 | Every sound by name (`Audio.Play(name, volume, pitch)`), a few copies each, random pitch per play. Placeholders made in code; `resources/sounds/<name>.wav` replaces one (names at the top); `doorbell.wav` is the first, a CC BY 4.0 recording credited in `resources/sounds/CREDITS.txt`. |
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
| `art/sprites/` | `sheets.json` (prompt guide per sprite sheet), `picked/` (the source picture of each cut sheet; `picked/midcentury/roster/` holds the 15 pictures the customer cast is cut from), `<sheet>-preview.png` (the sheet coloured as every strain), `candidates/` (git-ignored, with the spend log). |
| `resources/sprites/` | Packed gray-mask sprite sheets (`<sheet>.png` plus `.json`: cell, columns, count) cut by `tools/gen-sprites.js` (the game loads the `mc-` nug sheets; the older Flash `nugs-large`, `nugs-small`, `shake`, `budtender`, `customer-base(s)` and `layers/` are no longer loaded), and the customer cast `customer-roster.png` + `.json` cut by `tools/Cutter roster`. |
| `tools/Cutter/` | Customer art cutter, its own console project (the game csproj leaves `tools/` out). `bases` cuts a lineup into `resources/sprites/customer-bases.png` (part mask: R part id, G ink mix, A coverage; `.json` has key colours, ink, cells, feet), one 9:16 edit picture per base in `art/sprites/picked/bases/`, and `art/sprites/bases-preview.png` (recoloured check). `layer` lines an edited picture up with its base (the model moves and resizes it slightly), measures its fill colours, and cuts what changed into `resources/sprites/layers/<base>-<layer>.png` + `.json` (place on the cell); hair is a part mask (part 4), faces (`--colour`) are ink over the base; preview `art/sprites/layer-preview-*.png`. `look` puts a base and its layers together in four colour looks (`art/sprites/look-<base>.png`). `shade` (`Shade.cs`) flattens a lineup into flat regions (merging the model's own shadow and shine shapes) and adds the same cel shading to every character by rule (shadow band along each part's lower-right edge, shine inset on the top region's upper left, widths scaled by character height): before/after in `art/sprites/shade-<lineup>.png`. `roster` (`Roster.cs`) shades lineups (or with `--flat` keeps their own colours, for the mid-century style) and cuts every character into the game's cast sheet `resources/sprites/customer-roster.png` + `.json` (names like `m-moustache-blue`: kind, person, clothes), edges sharpened from the JPEG blur, green spill taken off edges and thin gaps (`Despill`); check `art/sprites/roster-preview.png`. To recut, pass one `<lineup>:<names>` spec per source picture, in the order of the `source` fields in `customer-roster.json`. The base sheet and layers (`customer-bases`, `layers/`) are no longer used by the game. Usage at the top of `Program.cs` and `Roster.cs`. |
| `tools/gen-sprites.js` | Makes sprite sheets with Gemini through `../claude/kit/imagegen/`: prompt, gen, review, cut. |
| `art/icon/` | Icon scripts: `make_ico.ps1` (used), `draw_icon.py` and the Blender files (kept for reference). |
| `claude/` | Notes for Claude: `vision.md`, `session-log.md`, this map, `mechanics-page.html`, `research-colorado-budtending.md` (sources behind the weigh redesign), copies of `versioning.md` and `readme-format.md`, `reference/` (two genre screenshots for camera framing only). |

## Where to look for X

| Looking for | Go to |
|---|---|
| A score, par time, tip or patience number | `Scoring.cs` |
| How often IDs are bad, the fine, the catch bonus | `Scoring.cs` (ID check block); card dates in `IdCard.cs` |
| Customers per day, arrival gaps, rushes | `Day.cs` |
| How a station feels (springs, speeds, tolerances) | The tuning block at the top of that station's file |
| What a strain looks like or when it unlocks | `Strain.cs` |
| Where something is drawn | `Art*.cs` (the four `Art` parts above) |
| What a drop on the rail does | `Shop.cs` |
| Rank names and steps, the save file | `Progress.cs` |
| What was logged from play, and its columns | `PlayLog.cs`; the file is `%APPDATA%\DustysGanjaria\playlog.csv` |
| A value missing from the F1 panel | That station's `TuningLines()`, or `Shop.DrawTuning` for the Counter |
