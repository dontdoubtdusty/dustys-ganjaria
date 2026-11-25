# Dusty's Ganjaria: notes for Claude
The shared rules in `D:\My Documents\rpret\GitHub\CLAUDE.md` also apply; this file wins where they disagree.

Dusty's Ganjaria is a solo, private time-management game set in a legal cannabis dispensary: customers order, you work the stations, and each order gets a score. It is an original game in the order-and-stations genre: it borrows the genre's structure, never a specific game's name, characters, art, interface, look or code. Direction and decisions: `claude/vision.md`.

## Stack
C# / .NET 10 with Raylib-cs, the same stack as the other games here (add ImGui.NET only if a tool screen needs it).

## Code
- `Program.cs`: window, game loop, `Version`. `DustysGanjaria.exe --shot <file.png>` stages a mid-order scene, saves one frame and quits; `--shot-throw <file.png>` also seals the bag and throws it at the bin, and ends with its ticket back on the rail, stamped weighed; `--shot-pack` and `--shot-pack-jar` show a weighed order half closed on the pack station (pouch strip, jar lid); `--shot-label` a packed jar on the label station with its label stuck on loosely; `--shot-intro` the day card with a menu drawn from all five strains; `--shot-counter <file.png>` shows the shop view with a line and a waiting customer; `--shot-order <file.png>` holds the order shot half way; `--shot-handover`, `--shot-check`, `--shot-results` and `--shot-tipjar` show the end of a hand-over, its bag check (frozen half way) and the two day-end screens. Screenshot runs load every sound but play muted, and never read or write the save. Use them to check changes (build to a temp folder first; desktop screen captures grab whatever window is on top, which may be the owner's private screen).
- `Shop.cs`: the shop and its screens, one day at a time: the day intro card, arrivals on the day's schedule, the line, the side-on shop view (layout constants at the top), taking orders, the waiting bench, patience and walkouts, tabs (Counter, Weigh, Pack, Label), ticket drops onto the station a ticket needs next (`Ticket.Next`; each station has `Start` and `TakeShipped`), a weighed ticket going back up to the rail, the hand-over queue, closing time and the end of the day. Both shots pause the clock, arrivals and patience.
- `Day.cs`: one shop day: the strain menu, customers per day, the arrival schedule with rushes, the wall clock, and the tally (orders, walkouts, metric averages, service quality).
- `HandOver.cs`: the hand-over shot (the packed container slides over, the customer lifts and checks it with a neutral "hmm" face and a bag crinkle, a soft chime, scores land row by row, reaction, tip into the jar).
- `DayEnd.cs`: the day results panel, then the tip jar and rank screen.
- `Progress.cs`: what carries between days (day, tip jar, rank points), rank names and steps, and the save at `%APPDATA%\DustysGanjaria\save.json` (delete it to start over).
- `Customer.cs`: one customer (name, looks, order, strain and the container kind they want, patience, walking; `Pos` is the feet).
- `OrderShot.cs`: the over-the-shoulder order shot (speech lines, items written onto the notepad, click to skip).
- `Ticket.cs` and `TicketRail.cs`: an order ticket (the station it needs next, the weigh result once weighed), and the rail across the top of every screen (slots, dragging; the shop decides what a drop does).
- `Audio.cs`: every sound by name (`Audio.Play(name, volume, pitch)`), each with a few copies so it can overlap and a small random pitch per play. All are placeholders made in code; `resources/sounds/<name>.wav` replaces one (names at the top: doorbell, tick, pluck, ding, zip, seal, slip, thump, pop, till, crinkle, chime, coin, fanfare).
- `Effects.cs`: particles (leaf bits) and floating pop-up words. Visual only, never scoring.
- `WeighStation.cs`: the weigh station flow (a jar per strain on the menu, place bag, toss buds from the ordered strain's jar, zip, throw into OUT bin). Layout rects and zip, bin and seal tuning at the top; feedback tuning (needle spring, tick and puff speeds, ding timing, zip teeth and shake, result delay) in its own block below the fields.
- `PackStation.cs`: the pack station flow (container from the shelf onto the mat, bag from the IN tray into it, then turn the jar lid in sideways strokes or press the pouch strip; Start over). Also the `ContainerKind` and `Container` types and `Containers` (sizes, which one an order needs). Layout and lid, strip and bag tuning at the top.
- `LabelStation.cs`: the label station flow (the closed container on the mat, a label pad per strain on the menu, carry a label over (it swings with the mouse), let go to stick it on loosely, hold still on it to press it down or drag to peel it). Layout, swing and press tuning at the top; `LabelOn` places a label on any container front (the hand-over uses it).
- `Strain.cs`: the strains (name, bud colours, bud size mix, label colour, rank that unlocks it) and the day menu (`Strains.MenuFor`: up to 3, the newest always on).
- `Physics.cs`: small 2D physics: circles for buds (gravity, bud-bud pushes, line-segment walls; the held bud is immovable, so it shoves others) and `StepBox` for the thrown sealed bag (axis-aligned walls only).
- `Bag.cs`: the baggie (position, zip progress, sealed, its walls; `SizeFor` and `SealedScale`, shared with the pack station).
- `Bud.cs`: one bud; radius follows the square root of weight, so size is honest.
- `Scoring.cs`: all scoring tuning (the four metric names, green band width, par time, slip and spill costs, shares, the Packing score and Start over cost, grades, line and wait patience, the Waiting par, tips, reactions).
- `Art.cs`, `ArtScreens.cs`, `ArtPack.cs` and `ArtLabel.cs` (one `partial class`; the second holds the day, hand-over, results and tip jar drawing, the third the pack station's pieces, the fourth the label station's rack, pads and labels): every placeholder drawing, colour and text call. All text goes through `Art.Label` / `Art.LabelCentered` / `Art.Measure` so it uses the game font, never `Raylib.DrawText`.
- `resources/fonts/LilitaOne-Regular.ttf`: the game font (SIL Open Font License, `OFL.txt` next to it; keep the licence file with the font).

## Art
Placeholders only (flat shapes and text labels) until the owner supplies PNG sprites. All drawing goes through `Art.cs`, so a placeholder can be swapped for a sprite in one place.

## App icon
The icon is the owner's picture `resources/art-reference/icon-reference.jpg` (HD badge and frame included). `art/icon/make_ico.ps1` turns it into `resources/icon.ico` (the .exe and shortcut icon, `ApplicationIcon` in the csproj) and `resources/icon.png` (window icon, set in `Program.SetWindowIcon`); `-Source <picture>` uses another picture. Not used, kept for reference: `art/icon/draw_icon.py` draws a version in code in Blender 5.3 (`blender -b --python art/icon/draw_icon.py`, renders `art/icon/icon_2048.png`; Blender 5.x only fills strokes with a non-zero `fill_id`).

## Version
`Version` in `Program.cs` (now 0.10.0), follows `claude/versioning.md`.

## Run and build
`Build Ganjaria.bat` publishes a Release copy to `..\Dustys Ganjaria` and puts a "Dusty's Ganjaria" shortcut on the Desktop and in the Start menu. Run it after every meaningful change (close the game first). To run it from a tool without hanging on `pause`, feed it NUL: `Start-Process cmd -ArgumentList '/c','"<path>\Build Ganjaria.bat" < NUL' -NoNewWindow -Wait`.

## The claude/ folder
- Session start: read `claude/session-log.md` (open reminders at the top) and `claude/vision.md`.
- Add `claude/file-map.md` once there are more than a handful of files.
- `claude/mechanics-page.html` (kit page from `../claude/kit/mechanics/`; open the local file, never publish it): with every change to what the player can see one mechanic do to another, edit its data block in the same change and bump `version` and `updated` (full rule in that kit's README). Check it at every wrap up.
- At wrap up: add a row to `claude/session-log.md` and to the root `../claude/session-log.md`, tagged dustys-ganjaria.

## Git
Git repo on `main`, remote github.com/dontdoubtdusty/dustys-ganjaria. Commit and push only when the owner asks.
