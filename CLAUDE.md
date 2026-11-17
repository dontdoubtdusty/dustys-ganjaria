# Dusty's Ganjaria: notes for Claude
The shared rules in `D:\My Documents\rpret\GitHub\CLAUDE.md` also apply; this file wins where they disagree.

Dusty's Ganjaria is a solo, private time-management game set in a legal cannabis dispensary: customers order, you work the stations, and each order gets a score. It is an original game in the order-and-stations genre. Copy the genre, never a specific game's name, characters, art, layout or look. Direction and decisions: `claude/vision.md`.

## Stack
C# / .NET 10 with Raylib-cs, the same stack as the other games here (add ImGui.NET only if a tool screen needs it). No code yet.

## Art
Placeholders only (flat shapes and text labels) until the owner supplies PNG sprites. Keep drawing behind small helpers so a placeholder can be swapped for a sprite in one place.

## Version
None yet. At the first code: `Version` in `Program.cs` starts at `0.1.0`, follows `claude/versioning.md`, and gets a row in the root `claude/versioning.md` table.

## Run and build
None yet. With the first code: `Build Ganjaria.bat` publishes a Release copy to `..\Dustys Ganjaria` and puts a shortcut on the Desktop and in the Start menu (copy the pattern of `..\srd-helper\kettle\Build Kettle.bat`). Run it after every meaningful change.

## The claude/ folder
- Session start: read `claude/session-log.md` (open reminders at the top) and `claude/vision.md`.
- Add `claude/file-map.md` once there are more than a handful of files.
- Add `claude/mechanics-page.html` from `../claude/kit/mechanics/` when the first mechanic is built, and keep it current from then on (rule in that kit's README).
- At wrap up: add a row to `claude/session-log.md` and to the root `../claude/session-log.md`, tagged dustys-ganjaria.

## Git
Git repo on `main`, remote github.com/dontdoubtdusty/dustys-ganjaria. Commit and push only when the owner asks.
