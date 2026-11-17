# Vision

## What it is
A time-management shop game set in Dusty's Ganjaria, a legal cannabis dispensary. A customer comes to the counter with an order, you prepare it across a few stations, one screen each, and get a score for speed and accuracy. Tips buy unlocks. It is a solo project for the owner only.

It is an original game in the order-and-stations genre. It borrows the genre's structure, not any existing game's name, characters, art, interface or look.

## Decisions
- 2026-11-17: C# / .NET 10 / Raylib-cs, like the owner's other games, with a Build .bat and a Desktop shortcut.
- 2026-11-17: Art is placeholders (flat shapes and labels) until the owner draws PNG sprites. Rejected: art drawn in code, and AI-generated sprites.
- 2026-11-17: The first cut is one station, polished: the weigh station. It settles the feel before the rest is built. Rejected: the full loop first, with or without pre-rolls.

## Draft design (not yet confirmed by the owner)
One customer at a time to start, with stations as screens switched by tabs along the bottom.
1. Counter: the customer gives an order ticket with a strain type (indica, sativa or hybrid), a form (flower or pre-roll) and an amount (1 g, 3.5 g or 7 g).
2. ID check: accept or refuse. Some customers are under 21, some IDs are expired, and some photos do not match the face. Serving one of them costs a lot; refusing a legal customer costs a little.
3. Weigh: pull bud from the right jar onto a scale and stop at the target weight. Closer means a better score; going over wastes stock.
4. Roll (pre-rolls only): fill the cone to the line, then twist the end with a timing bar.
5. Package and checkout: pick the container, put the label on straight and hand it over.

Score: wait time, correct product, weight accuracy, packaging and the ID call. Later: edibles (a bake station), new strains, shop upgrades, and regulars with favourite orders.

## Open questions
- How the weigh station plays: drag and drop single buds, tap to pour, or hold to pour. Decide before building it.
- Whether weight is shown on the scale as it changes or only after you stop (a skill question).
