# Vision

## What it is
A time-management shop game set in Dusty's Ganjaria, a legal cannabis dispensary. A customer comes to the counter with an order, you prepare it across a few stations, one screen each, and get a score for speed and accuracy. Tips buy unlocks. It is a solo project for the owner only.

It is an original game in the order-and-stations genre. It borrows the genre's structure, not any existing game's name, characters, art, interface, look or code.

## Decisions
- 2026-11-17: C# / .NET 10 / Raylib-cs, like the owner's other games, with a Build .bat and a Desktop shortcut.
- 2026-11-17: Art is placeholders (flat shapes and labels) until the owner draws PNG sprites. Rejected: art drawn in code, and AI-generated sprites.
- 2026-11-17: The first cut is one station, polished: the weigh station. It settles the feel before the rest is built. Rejected: the full loop first, with or without pre-rolls.
- 2026-11-17: Weigh station plays by dragging single buds (weights vary, size matches weight) onto the scale. Rejected: hold to pour, tap to pour, a pour-then-drag hybrid.
- 2026-11-17: The scale shows the exact live weight. Rejected: lagging readout, hidden until you stop, hidden on later days. Consequence: the station is a quick weight puzzle under time pressure, not a timing test; the jar always has shake (0.1-0.3 g) so the target can be reached.
- 2026-11-17: Overshoot is fixed by a pinch-back (drag a bud off the scale back to the jar), costing 2 seconds. Rejected: locked in, dump and redo.
- 2026-11-17: First build is the weigh station only (target, jar, scale, score screen), no ticket flow or day. Rejected: ticket + weigh + score, adding a day timer.
- 2026-11-17 (0.2.0, replaces the drag, exact-readout and pinch-back decisions above): owner disliked the grams puzzle and wanted the station to feel like Schedule I's packaging bench (mechanics only, not its art or interface). Now: drag a bag onto the scale, toss buds in with 2D physics, fill until a dial needle sits in a green band (no number readout), drag the zip across to seal (slips open if too fast or off the line), drag the bag to the OUT tray. Score = accuracy 50, speed 30, handling 20 (zip slips, buds left outside the jar). Rejected: count units with no scale, first-person 3D, tilt-pour, sorting junk from good buds, fill line with no scale, click to seal, seal plus label.
- 2026-11-17 (0.3.0): the sealed bag is thrown into an open OUT bin with physics instead of dragged to a tray; it ships once it sits still inside. Sealing presses the bag to 60 percent size so the 7 g bag fits the bin. A miss only costs time.
- 2026-11-17 (0.4.0): order queue. Customers walk in every 14-24 s (up to 4 in line) and order 1, 3.5 or 7 g of flower. Two screens on tabs (Counter, Weigh; keys 1 and 2). Taking an order sends the customer to a waiting area (up to 4) and their ticket to the weigh station, oldest first. Patience drains in line (45 s) and again while waiting (30 s + 4 s per gram); at zero they walk out, and if their bag was on the bench it is dropped. Order score = weigh 75, wait 25 (full marks while half their wait patience is left); tip = nothing below 50, then $2 at 50 up to $7 at 100. The result panel pauses the shop. Chosen by Claude as defaults, all tuning in `Scoring.cs` and `Shop.cs`; no day loop yet.
- 2026-11-17 (replaces "Rejected: AI-generated sprites" above, for customers): the owner will generate baseline customer art with an image AI: a few body bases with flat key-colour regions, so the game recolours skin, hair, shirt and pants per customer. Placeholders stay until they arrive.
- 2026-11-17: Customers are built from layers, like Flash characters, made by editing one locked base (see Customer art pipeline). Rejected: asking the AI for layers or part sheets directly (parts never line up), full expression sheets (expressions are brow and mouth swaps).
- 2026-11-17: Game font is Lilita One (chunky, rounded, cartoony; SIL OFL). Rejected: the Raylib default font. Fredoka One was the first pick but is no longer in Google's font repo.

## Full design (owner's answers, 2026-11-17, four question rounds)
**Familiarity comes first.** A fan of the Papa's series should sit down and know how to play within a minute: a dispensary twist on that genre. That means the genre's whole familiar flow: take the order at the counter, ticket on the rail, stations on tabs, each station a hands-on minigame, per-station points revealed at hand-over, tips, rank-ups that unlock content, named regulars with favourite orders, shop days with rushes. Every series entry has its own stations, and so do we: ours come from dispensary work. What stays our own in every specific: names, characters, art, interface layout and code (nothing taken from any Papa's game file).

- Session: shop days. A clock runs from opening to closing (about 6 real minutes) with a quiet morning and busier lunch and evening rushes. At closing the doors shut, you finish whoever is inside, then a day summary, then a save. Rejected: endless shift, fixed-count challenge runs, story weeks, even flow, fixed customer count per day.
- Pressure: cozy but busy. Walkouts happen but are rare if you keep moving; care is rewarded more than raw speed. Rejected: hard juggling, no fail state.
- Stations: Counter (take the order and check ID), Weigh (flower) or Roll (pre-rolls), Package (container, label, hand-over). Three screens per order, four stations in all. Later: a closed-shop late-night session (trimming or similar), designed when we get there. Rejected: ID as its own screen; every order weighed then rolled; 2 or 5+ stations.
- Tickets: a ticket rail along the top of every screen; drag a ticket onto a station to work on it. Several orders can be in progress at different stations at once. Rejected: automatic oldest-first (what 0.4.0 does), click to pick, one order at a time.
- Menu: flower only on day 1; pre-rolls and the Roll station unlock after a few days. Rejected: everything from day 1, edibles at the start.
- Judging: per-station points (wait, weigh or roll, package), shown one by one at hand-over and averaged into the order score. Rejected: one overall score, stars only.
- Progress: tips buy shop upgrades (faster needle, bigger jar, comfier waiting area that slows patience drain, decor) and cosmetics (uniform, shop colours, signs); order points raise the shop rank, which unlocks content (pre-rolls, strains, stations, customers).
- Customers: named regulars with favourite orders and their own patience, plus random walk-ins. New regulars appear as you rank up.
- ID check (at the Counter): glance and decide. The customer holds out an ID; check birth date, expiry and photo, accept or refuse. Most are fine, a few a day are not. Rejected: ID only on suspicion, no ID check.
- Package: drop the sealed bag into the right container (pouch or jar), drag the label on straight (scored on angle and position), hand it over at the counter. Physics like the weigh station. Rejected: label only, one paper bag per order.
- Roll (when unlocked): toss ground flower into a cone up to a fill line, tap to pack, twist the tip with a timing ring. Rejected: grind then roll.
- Counter staging (added 2026-11-17, owner): the Counter is never first person. Two shots, the genre's usual staging:
  - Shop view: a side-on, third-person view of the shop floor. Customers walk in through the door, queue at the counter and wait in the waiting area; the budtender stands behind the counter with the till. This is the Counter tab's normal screen.
  - Order shot: pressing Take order cuts to an over-the-shoulder shot. The budtender is in the foreground from behind (shoulder, arm, notepad), the customer faces the camera across the counter. The customer says the order one item at a time in a speech bubble (amount, strain, container); each item writes itself onto the ticket as it is said. When the ticket is done it goes up on the rail and the view cuts back to the shop view. The ID check happens in this shot too (the customer holds the ID up).
  - Hand-over uses the same over-the-shoulder shot: the customer reacts to each station's points as they count up.
  - Our own layout and art throughout; only the camera convention is shared with the genre.
  - Reference for the two camera shots only (official genre screenshots, never traced or copied): `claude/reference/genre-shop-view.jpg`, `claude/reference/genre-order-shot.jpg`.
  Rejected: front-on first-person counter (what 0.4.0 does).
- Build order: 1 ticket rail and parallel orders, with the two-shot Counter (shop view, order shot), 2 Package station (a full flower order runs Counter, Weigh, Package), 3 shop day, 4 ID check, then rank, unlocks, regulars, Roll.

## Customer art pipeline (planned 2026-11-17, paused)
The image AI never makes layers. It makes one simple change at a time to a locked base, and a tool cuts the layer out.
- Base (locked): `resources/art-reference/base.jpg`. Gemini, tall portrait 768x1376, front view, bald, blank face, arms down, head about a third of the height, flat fills, dark outline, solid pure green background, no shadow. Shirt blue, pants mid grey (kept away from the outline colour so they can be recoloured), shoes dark.
- One part per prompt, as an edit of the base with the base attached: "Edit this image: add <part>. Change nothing else: same outline, same colours, same pose, same position, same green background." Never ask it to edit the hands (they come out differently each time).
- Layers, back to front: back hair, body (torso and arms), legs, head, eyes, brows, mouth, front hair, accessory (hat or glasses). Expressions are brow and mouth swaps; motion in Raylib (body bob, blink by eye swap, mouth swaps for happy, impatient, angry).
- Test 1 passed: short hair (`resources/art-reference/test-hair-short.jpg`). A pixel diff against the base changed only the hair area plus about a dozen fingertip specks. The base did not move.
- Still to test: a small feature (one mouth edit, "add a small smiling mouth, change nothing else"), where a few pixels of drift matter more. Long hair needs a back and a front layer: two edits, or treat hair outside the head shape as back.
- To build: a C# console cutter in `tools/`. Input base plus edited image; remove the green, keep changed pixels, drop specks, snap colours to the key palette (skin, hair, shirt, pants), save a transparent PNG the size of the base.
- Wanted: PNG downloads from Gemini when offered (JPEG edge noise hurts the colour snap and the diff). Body variety later means two or three more locked bases, each with its own parts.

## Open questions
- Queue tuning, after playing it: arrival gap, line and wait patience, the wait share of the score, and tips. Does the result panel pausing the shop feel right, or should customers keep waiting while it is open?
- Weigh tuning, after playing it: green band width (`Scoring.BandFor`), par time (8 s + 1.5 s per gram), zip slip speed and distance, toss strength, the bud size mix, and physics bounce and friction. In `Scoring.cs`, `WeighStation.cs` and `Physics.cs`.
