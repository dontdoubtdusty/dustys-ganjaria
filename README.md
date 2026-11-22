# Dusty's Ganjaria

A time-management game set in a legal dispensary. Customers order, you check their ID, weigh, pack and label
each order, and every order gets a score for speed and accuracy. In early development: shop days, the counter and
the weigh station are playable.

## Install
Local only, with no release. Run `Build Ganjaria.bat` once; it makes a Dusty's Ganjaria shortcut on the Desktop and
in the Start menu. Start the game from that shortcut.

## Usage
- The game plays in shop days. Day 1 has 2 customers and each day brings one more. A card shows the day and how
  many customers to expect; click Open the shop (or press Enter) to start.
- The clock at the top right runs from 10 AM to 8 PM, faster while the shop is empty. At closing the sign on the
  door flips to Closed: no one else comes in, and you finish whoever is still inside.
- Two screens, switched with the tabs at the bottom or the 1 and 2 keys: Counter and Weigh. A tab lights up orange
  when someone there needs you.
- Counter: customers walk in and queue. Click Take order (or press Enter) and the view cuts to the counter, where
  they say their order (1 g, 3.5 g or 7 g); then their ticket goes up on the rail and they sit down to wait.
- Everyone has a patience bar. It drains while they queue and again while they wait for their order. If it runs
  out they walk out, and an order you were making for them is dropped. Taking an order and handing one over pause
  the whole shop.
- Weigh: drag a ticket down from the rail onto the station to start it.
- Drag a bag from the stack onto the scale.
- Grab buds from the jar and toss them into the bag. They fall and tumble. Small shake is there for fine-tuning.
- Fill until the needle on the scale sits in the green band. Too much? Pull a bud back out.
- Drag the zip tab along the top of the bag to seal it. Go too fast or stray off the line and it slips open.
- Grab the sealed bag and throw it into the OUT bin. Missed? Pick it up and try again.
- Once it lands, the view cuts to the hand-over: the customer picks up the bag and looks it over, then the order's scores for Waiting (how long they
  waited in all) and Weighing, the customer's reaction, and their tip into the jar. Click to skip; Next carries on.
- At the end of the day: the day's results (customers, service quality, each score's average), then the tip jar
  counts the day's tips and your rank grows with every good order. Next day carries on; Save and quit stops.
- The game saves at the end of every day. To start over from day 1, delete
  `%APPDATA%\DustysGanjaria\save.json`.
- Esc quits (a day in progress is not saved).

## Known limits
- Windows only.
- Placeholder art, on purpose, until real sprites are drawn.
- No ID check, packing or labeling yet (they show as "not built yet"). Tips and ranks unlock nothing yet.

## Credits
Font: Lilita One by Juan Montoreano, SIL Open Font License (`resources/fonts/OFL.txt`).
