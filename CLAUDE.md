# Dusty's Ganjaria: notes for Claude
The shared rules in `D:\My Documents\rpret\GitHub\CLAUDE.md` also apply; this file wins where they disagree.

Dusty's Ganjaria is a solo, private time-management game set in a legal cannabis dispensary: customers order, you work the stations, and each order gets a score. It is an original game in the order-and-stations genre: it borrows the genre's structure, never a specific game's name, characters, art, interface, look or code. Direction and decisions: `claude/vision.md`.

## Stack
C# / .NET 10 with Raylib-cs, the same stack as the other games here (add ImGui.NET only if a tool screen needs it).

## Code
- `Program.cs`: window, game loop, `Version`. `DustysGanjaria.exe --shot <file.png>` stages a mid-order scene, saves one frame and quits; `--shot-throw <file.png>` also seals the bag and throws it at the bin, and should end on the result panel; `--shot-counter <file.png>` shows the counter with a line and a waiting customer. Use them to check changes (build to a temp folder first; desktop screen captures grab whatever window is on top, which may be the owner's private screen).
- `Shop.cs`: the order queue and screens: arrivals, the line, taking orders, the waiting area, patience and walkouts, tabs (Counter, Weigh), served/walkout/tip totals and the order result panel. Hands each ticket to the weigh station (`Start`) and collects the bag (`TakeShipped`).
- `Customer.cs`: one customer (name, shirt, order, patience, walking).
- `WeighStation.cs`: the weigh station flow (place bag, toss buds, zip, throw into OUT bin, result panel). Layout rects and zip, bin and seal tuning at the top.
- `Physics.cs`: small 2D physics: circles for buds (gravity, bud-bud pushes, line-segment walls; the held bud is immovable, so it shoves others) and `StepBox` for the thrown sealed bag (axis-aligned walls only).
- `Bag.cs`: the baggie (position, zip progress, sealed, its walls).
- `Bud.cs`: one bud; radius follows the square root of weight, so size is honest.
- `Scoring.cs`: all scoring tuning (green band width, par time, slip and spill costs, shares, grades, line and wait patience, wait share, tips).
- `Art.cs`: every placeholder drawing, colour and text call. All text goes through `Art.Label` / `Art.LabelCentered` / `Art.Measure` so it uses the game font, never `Raylib.DrawText`.
- `resources/fonts/LilitaOne-Regular.ttf`: the game font (SIL Open Font License, `OFL.txt` next to it; keep the licence file with the font).

## Art
Placeholders only (flat shapes and text labels) until the owner supplies PNG sprites. All drawing goes through `Art.cs`, so a placeholder can be swapped for a sprite in one place.

## Version
`Version` in `Program.cs` (now 0.4.0), follows `claude/versioning.md`.

## Run and build
`Build Ganjaria.bat` publishes a Release copy to `..\Dustys Ganjaria` and puts a "Dusty's Ganjaria" shortcut on the Desktop and in the Start menu. Run it after every meaningful change (close the game first). To run it from a tool without hanging on `pause`, feed it NUL: `Start-Process cmd -ArgumentList '/c','"<path>\Build Ganjaria.bat" < NUL' -NoNewWindow -Wait`.

## The claude/ folder
- Session start: read `claude/session-log.md` (open reminders at the top) and `claude/vision.md`.
- Add `claude/file-map.md` once there are more than a handful of files.
- `claude/mechanics-page.html` (kit page from `../claude/kit/mechanics/`; open the local file, never publish it): with every change to what the player can see one mechanic do to another, edit its data block in the same change and bump `version` and `updated` (full rule in that kit's README). Check it at every wrap up.
- At wrap up: add a row to `claude/session-log.md` and to the root `../claude/session-log.md`, tagged dustys-ganjaria.

## Git
Git repo on `main`, remote github.com/dontdoubtdusty/dustys-ganjaria. Commit and push only when the owner asks.
