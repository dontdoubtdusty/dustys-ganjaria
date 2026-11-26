# Dusty's Ganjaria

A time-management game set in a legal dispensary. Customers order, you check their ID, weigh, pack and label
each order, and every order gets a score for speed and accuracy. In early development: shop days, the counter and
the weigh, pack and label stations are playable.

## Install
Local only, with no release. Run `Build Ganjaria.bat` once; it makes a Dusty's Ganjaria shortcut on the Desktop and
in the Start menu. Start the game from that shortcut.

## Usage
- The game plays in shop days. Day 1 has 2 customers and each day brings one more. A card shows the day, how
  many customers to expect and the three strains on today's menu; click Open the shop (or press Enter) to start.
- The clock at the top right runs from 10 AM to 8 PM, faster while the shop is empty. At closing the sign on the
  door flips to Closed: no one else comes in, and you finish whoever is still inside.
- Four screens, switched with the tabs at the bottom or the 1 to 4 keys: Counter, Weigh, Pack and Label. Each tab
  counts the tickets waiting for it and lights up orange when someone there needs you.
- Counter: customers walk in and queue. Click Take order (or press Enter) and the view cuts to the counter.
- First they hold out their ID. Check it against the sign by the counter (today's date, and the last birth date
  that is 21) and against the person: born late enough to be under 21, expired, or a photo that does not look like
  them (hair or shirt) means refuse. There is no timer. Day 1's IDs are all fine; from day 2 about one in six is
  bad. Refusing a bad ID earns rank points; refusing a good one, they walk out; accepting a bad one costs a $5 fine
  at the hand-over.
- Accepted, they say their order (1 g, 3.5 g or 7 g of one strain, in a pouch or a jar) and you write it down, with the
  container size it needs (Jar L, Pouch S); then their ticket goes up on the rail and they sit down to wait.
- New to a step? A yellow arrow points at what to do next (take the order, open a tab, drag a ticket down) until
  you have done that step once.
- Everyone has a patience bar. It drains while they queue and again while they wait for their order. If it runs
  out they walk out, and an order you were making for them is dropped. Taking an order and handing one over pause
  the whole shop.
- Weigh: drag a ticket down from the rail onto the station to start it.
- There is a jar for each strain on the menu, a scale with a weigh tray on it, and an open bag standing ready.
  Each strain has its own colour and bud sizes: Prairie Haze is small and fluffy, Midnight Kush big and chunky,
  Copper Sun in between. A bud's size is the only clue to its weight.
- Pick buds out of the jar of the strain on the ticket with the tongs and set them on the tray. Heavy nugs lag
  behind the tongs. Set them down gently: a nug dropped from high up, or flung, chips a crumb, which is flower
  given away. A nug that ends up on the counter costs points.
- Buds of the wrong strain weigh on the scale but do not count toward the order, and each one costs points.
- Fill until the needle sits in the green band, which runs from the order weight up to a little over it. Under the
  weight (red) is never right. Over is fine up to the end of the green, but the extra is given away for free:
  the day's results list it and its cost comes off your tips (the customer tips a little more for a heavy bag).
  Nugs are never broken; there is always some mix of whole pieces that lands in the green.
- Grab the tray by its handle and carry it over the bag: it tips and the buds slide in. Swing it fast and they
  fly past. Let go and the tray goes back onto the scale with whatever is left on it. Big orders take more than
  one scoop: the needle counts what is already in the bag plus what is on the tray, and a blue marker on the
  dial shows the bag's part.
- Drag the zip tab along the top of the bag to seal it. Go too fast or stray off the line and it slips open.
- Now and then (from day 3) a customer asks for "Big nugs" or "No shake"; it is on the ticket. Get it right for a
  smile and a bigger tip; miss it and the tip is halved. Scores do not change. Shake is the flat, leafy crumbs in
  the jar (the station ticket shows one, crossed out); anything shaped like a bud is not shake, however small.
- Grab the sealed bag and throw it into the OUT bin. Missed? Pick it up and try again. Once it lands, its ticket
  goes back up on the rail, stamped WEIGHED.
- Pack: drag the weighed ticket down onto the pack station. The bag is waiting in the IN tray.
- Drag the container the ticket names off the shelf onto the mat (small holds 1 g or 3.5 g, large holds 7 g).
  Changed your mind? Drag it back off, as long as the bag is not in yet.
- Drag or throw the bag into the container. Once it settles inside, it stays there.
- Jar: grab the lid and drag it to the right, let go, grab it again: about three strokes screw it on, each one
  clicking as the ridges slide. A jerky, too-fast stroke cross-threads it and it pops loose.
- Pouch: hold the mouse on the seal strip and sweep slowly from end to end. Too fast leaves gaps to go back over.
- Wrong container? Start over puts everything back, for a 5 second penalty.
- Once it is closed, its ticket goes back up on the rail, stamped PACKED.
- Label: drag the packed ticket down onto the label station. The closed container is waiting on the mat.
- Take the label for the order's strain off the rack and carry it onto the container. It hangs from the mouse and
  swings as you move, so slow down and let it settle before you let go. Corner marks show where it goes.
- Let go over the container and it sticks on loosely, just as it was hanging. Hold the mouse still on it to press
  it down. Crooked or wrong? Drag it instead to peel it off and place it again (it only costs time).
- Once the label is pressed down, the view cuts to the hand-over: the customer picks up the order and looks it
  over, then the order's scores for Waiting (how long they waited in all), Weighing, Packing and Labeling, the customer's reaction, and their
  tip into the jar. Click to skip; Next carries on.
- At the end of the day: the day's results (customers, service quality, each score's average), then the tip jar
  counts the day's tips and your rank grows with every good order. Next day carries on; Save and quit stops.
- The game saves at the end of every day. To start over from day 1, delete
  `%APPDATA%\DustysGanjaria\save.json`.
- Esc pauses: the whole shop stands still. The pause menu has Resume, the volume (remembered in
  `%APPDATA%\DustysGanjaria\settings.json`, so starting over keeps it), Full screen, Restart day and Quit. Restarting or quitting
  mid-day loses that day, so both ask first. Closing the window also quits.
- F11 switches between full screen and a window (also in the pause menu); the game remembers which.
- F1 shows or hides a tuning panel with the live numbers behind the current screen: patience and waiting times
  on the Counter, the exact weight and green band at Weigh, lid strokes or strip at Pack, tilt and distance to the
  guide at Label, and time against par everywhere. It is for tuning; play without it, or the game is easier than
  it should be.
- Every served order and every walkout adds a row to `%APPDATA%\DustysGanjaria\playlog.csv` with all its part
  scores, for tuning. Delete the file to start a fresh log.

## Known limits
- Windows only.
- Placeholder art, on purpose, until real sprites are drawn.
- Ranks unlock new strains (Frost Mule at rank 3, Dust Devil at rank 5); tips buy nothing yet.

## Credits
Font: Lilita One by Juan Montoreano, SIL Open Font License (`resources/fonts/OFL.txt`).
Door chime: "Convenience Store Door Chime" by zebragrrl (https://freesound.org/s/632226/, based on "Door Chime 3" by Taira Komori), CC BY 4.0 (`resources/sounds/CREDITS.txt`).
