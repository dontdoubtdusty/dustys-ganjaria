# Dusty's Ganjaria

A time-management game set in a legal dispensary. Customers order, you check their ID, weigh, roll and package
each order, and every order gets a score for speed and accuracy. In early development: the counter and the weigh
station are playable.

## Install
Local only, with no release. Run `Build Ganjaria.bat` once; it makes a Dusty's Ganjaria shortcut on the Desktop and
in the Start menu. Start the game from that shortcut.

## Usage
- Two screens, switched with the tabs at the bottom or the 1 and 2 keys: Counter and Weigh. A tab lights up orange
  when someone there needs you.
- Counter: customers walk in and queue. The one at the counter says their order (1 g, 3.5 g or 7 g); click Take
  order (or press Enter) and they move to the waiting area.
- Everyone has a patience bar. It drains while they queue and again while they wait for their order. If it runs
  out they walk out, and an order you were making for them is dropped.
- Weigh: the ticket shows whose order you are making and their patience. Tickets are made oldest first.
- Drag a bag from the stack onto the scale.
- Grab buds from the jar and toss them into the bag. They fall and tumble. Small shake is there for fine-tuning.
- Fill until the needle on the scale sits in the green band. Too much? Pull a bud back out.
- Drag the zip tab along the top of the bag to seal it. Go too fast or stray off the line and it slips open.
- Grab the sealed bag and throw it into the OUT bin. Missed? Pick it up and try again. Once it sits in the bin the
  customer pays: the score counts the weigh station (accuracy, speed, handling) and how long they waited, and a good
  score earns a tip. Next (or Enter) carries on.
- The bottom bar keeps count of customers served, walkouts and tips.
- Esc quits.

## Known limits
- Windows only.
- Placeholder art, on purpose, until real sprites are drawn.
- No ID check, rolling, packaging or shop day yet. Tips are counted but buy nothing yet.

## Credits
Font: Lilita One by Juan Montoreano, SIL Open Font License (`resources/fonts/OFL.txt`).
