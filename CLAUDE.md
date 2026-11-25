# Dusty's Ganjaria: notes for Claude
The shared rules in `D:\My Documents\rpret\GitHub\CLAUDE.md` also apply; this file wins where they disagree.

Dusty's Ganjaria is a solo, private time-management game set in a legal cannabis dispensary: customers order, you work the stations, and each order gets a score. It is an original game in the order-and-stations genre: it borrows the genre's structure, never a specific game's name, characters, art, interface, look or code. Direction and decisions: `claude/vision.md`.

## Stack
C# / .NET 10 with Raylib-cs, the same stack as the other games here (add ImGui.NET only if a tool screen needs it).

## Code
What each file holds, and where its tuning lives: `claude/file-map.md`. Update it in the same change when a file is added, split, renamed or deleted, or its job changes.

- All text goes through `Art.Label` / `Art.LabelCentered` / `Art.Measure` so it uses the game font, never `Raylib.DrawText`.
- Screenshot runs: `DustysGanjaria.exe --shot <file.png>` stages a mid-order scene, saves one frame and quits; `--shot-throw <file.png>` also seals the bag and throws it at the bin, and ends with its ticket back on the rail, stamped weighed; `--shot-pack` and `--shot-pack-jar` show a weighed order half closed on the pack station (pouch strip, jar lid); `--shot-label` a packed jar on the label station with its label stuck on loosely; `--shot-intro` the day card with a menu drawn from all five strains; `--shot-counter <file.png>` shows the shop view with a line and a waiting customer; `--shot-order <file.png>` holds the order shot half way; `--shot-handover`, `--shot-check`, `--shot-results` and `--shot-tipjar` show the end of a hand-over, its bag check (frozen half way) and the two day-end screens; `--shot-pause` the pause menu over the shop view. Shots start from a fresh progress, so first-time pointers show in them. Add `--tuning` after the file name to show the F1 tuning panel in the shot. Screenshot runs load every sound but play muted, and never read or write the save or the play log. Use them to check changes (build to a temp folder first; desktop screen captures grab whatever window is on top, which may be the owner's private screen).
- The game font `resources/fonts/LilitaOne-Regular.ttf` is SIL OFL: keep `OFL.txt` next to it.

## Art
Placeholders only (flat shapes and text labels) until the owner supplies PNG sprites. All drawing goes through `Art.cs`, so a placeholder can be swapped for a sprite in one place.

## App icon
The icon is the owner's picture `resources/art-reference/icon-reference.jpg` (HD badge and frame included). `art/icon/make_ico.ps1` turns it into `resources/icon.ico` (the .exe and shortcut icon, `ApplicationIcon` in the csproj) and `resources/icon.png` (window icon, set in `Program.SetWindowIcon`); `-Source <picture>` uses another picture. Not used, kept for reference: `art/icon/draw_icon.py` draws a version in code in Blender 5.3 (`blender -b --python art/icon/draw_icon.py`, renders `art/icon/icon_2048.png`; Blender 5.x only fills strokes with a non-zero `fill_id`).

## Version
`Version` in `Program.cs` (now 0.12.0), follows `claude/versioning.md`.

## Run and build
`Build Ganjaria.bat` publishes a Release copy to `..\Dustys Ganjaria` and puts a "Dusty's Ganjaria" shortcut on the Desktop and in the Start menu. Run it after every meaningful change (close the game first). To run it from a tool without hanging on `pause`, feed it NUL: `Start-Process cmd -ArgumentList '/c','"<path>\Build Ganjaria.bat" < NUL' -NoNewWindow -Wait`.

## The claude/ folder
- Session start: read `claude/file-map.md`, `claude/session-log.md` (open reminders at the top) and `claude/vision.md`.
- `claude/mechanics-page.html` (kit page from `../claude/kit/mechanics/`; open the local file, never publish it): with every change to what the player can see one mechanic do to another, edit its data block in the same change and bump `version` and `updated` (full rule in that kit's README). Check it at every wrap up.
- At wrap up: add a row to `claude/session-log.md` and to the root `../claude/session-log.md`, tagged dustys-ganjaria.

## Git
Git repo on `main`, remote github.com/dontdoubtdusty/dustys-ganjaria. Commit and push only when the owner asks.
