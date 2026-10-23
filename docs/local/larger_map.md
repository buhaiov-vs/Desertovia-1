# Larger map

Fog on the current 120 by 90 land is stored as the 32 by 32 chunks someone has seen. A missing chunk is fog. The land size, version, and server are unchanged.

The screen now keeps a texture for each 32 by 32 block near the camera. Those blocks use the same coordinates as the fog chunks. Joining still builds the whole 120 by 90 once, so the picture stays the same. Still planned, and still at today's size first: per-chunk generation, a delta village feed, nearby interest, and region pathfinding. The land grows only after those.

Target land: **2,000 by 1,500 tiles**, with the camp in the middle. That is about **280 times** today's area (120 by 90). A phone still draws one screen. The server still simulates only the places where someone is.

Past about **8,000 tiles** on the long side, positions and fog need a further change. That size is the ceiling of this plan, not the goal.

## Travel time, edge to center

The figure on the map moves at **26 pixels per second**. A tile is **16 pixels**, so that is **1.625 tiles per second**.

The server finishes the same walk at **0.6 seconds per tile** when the walker is healthy (`Exploration.SecondsPerTile`). Breakfast (0.75) and a cast (0.8) make that server clock slower. The picture on screen stays at 26 pixels per second.

A work cycle charges walking separately, at **2 seconds per tile** (`WorldMap.SecondsPerTile`). That is the time added to a job, not the speed of the sprite.

These times are a straight line. A path around the river, the ravine, or a mountain is longer. The camera can pan to the edge immediately; only the walker takes this long.

Today the campfire sits in the middle of the rectangle. The left and right edges are about **60 tiles** away, the top and bottom about **45**, and a corner about **75**.

| From | Tiles | On screen (26 px/s) | Server, healthy (0.6 s/tile) | Added to a work cycle (2 s/tile) |
|---|---:|---:|---:|---:|
| Left or right edge | 60 | 37 seconds | 36 seconds | 2 minutes |
| Top or bottom edge | 45 | 28 seconds | 27 seconds | 1 minute 30 seconds |
| A corner | 75 | 46 seconds | 45 seconds | 2 minutes 30 seconds |

On the target map the camp stays in the middle. Half the width is **1,000** tiles, half the height is **750**, and a corner is **1,250** (a 3-4-5 triangle).

| From | Tiles | On screen (26 px/s) | Server, healthy (0.6 s/tile) | Added to a work cycle (2 s/tile) |
|---|---:|---:|---:|---:|
| Left or right edge | 1,000 | 10 minutes 15 seconds | 10 minutes | 33 minutes 20 seconds |
| Top or bottom edge | 750 | 7 minutes 42 seconds | 7 minutes 30 seconds | 25 minutes |
| A corner | 1,250 | 12 minutes 49 seconds | 12 minutes 30 seconds | 41 minutes 40 seconds |

A wide screen shows about **34 tiles** across, so the long edge is about **29 screens** from the camp, and a corner about **37 screens**.

A ten-minute walk is still one session. A half-hour work trip is not a normal job. Far activities are places you travel to, then work inside. The camp, a ruin, and a hunting ground each stay small. The distance between them is what grows.

## What is sent today

`villageUpdated` replaces the whole village state, for every viewer, at most once every 250 milliseconds.

The ground tiles are not in that message. The client builds all **10,800** tiles from the village id, plus a short wilds list (ravine, passes, thickets, cave, lookout). Wild trees outside the camp are drawn the same way.

The message does include every camp tree, the whole fog bitmap (1,350 bytes), every building, site, field, herd count, and every unit of every active battle.

Citizens are already incremental: the full list on join, then only the citizens who changed.

## Changes

### 1. Draw the land in chunks

This is in the client for the current land. A wide view keeps the blocks around the camera, plus one block of margin, and drops the rest. Each block is baked to one texture. Ponds, marsh, the ravine, and the rock under peaks stay one drawing, because there are few of them and they must keep today's pixels. The minimap still reads the whole land. Three million sprites at the target size are not created: a block is drawn only when the view reaches it. The generator still walks the 120 by 90 once, which is what keeps the picture. That walk becomes per chunk before the land grows.

### 2. Generate each chunk from the seed

`buildLayout` and `Wilderness.Generate` walk the whole rectangle and place a fixed set of mountains and thickets. That cost grows with the area, and a feature's position depends on a scan of the entire land.

Each chunk must come from the village seed and the chunk coordinate alone. Rivers, ridges, and clearings stay continuous because neighboring chunks use the same function. Authored points (cave, lookout, a ruin) stay a short list, not a tile grid.

The client and the server use the same rectangle today (`MAP_WIDTH` / `MAP_HEIGHT` and `WorldMap.Land`). They keep one definition. The size constant changes only after generation is per chunk.

### 3. Split the village feed

The slow message keeps resources, research, housing, and weather.

Map changes go out as deltas: a tree felled, a node emptied, a building placed, one fog chunk revealed. A chunk is resent when it changes. The explored bitmap of a 2,000 by 1,500 map would be about **366 KB** if it were sent whole; visited chunks stay far smaller.

### 4. Full detail only nearby

A player receives entities near their own citizens and near the camera. A hunt in the far woods is one line until someone walks there.

A battle snapshot today includes every unit and goes to every viewer. On the large map, unit positions go to viewers near that fight. Everyone else receives the phase and the result.

### 5. Run activities by region

Work, raids, and dungeon runs are already separate activities. A node, camp, ruin, or caravan is an entity in one region. The server simulates that region when someone is there, or on a slow timer.

Pathfinding stays inside the region and uses crossings between regions. One grid for the whole land does not grow to three million tiles.

The live budget is how many places are active at once. A few hundred entities near players fit the current process. Empty tiles do not.

### 6. Store fog by visited chunk

This is in the current map. The saved column is still one byte array, now headed `FCH1`. Each block is 32 by 32 tiles (128 bytes), or a complete flag and no bits when every tile in it has been seen. An old 1,350-byte bitmap is read once and written back as chunks. Reveal walks the route, about every 4 tiles, and only looks at the tiles within sight of that sample. The village message sends `exploredChunks`. It also still sends the old `explored` bitmap, because the published page decodes that string and throws if it is missing. The bitmap goes away when that page is replaced.

The client still paints fog on one canvas for the current land. That is the picture, not the store. At the target size a dense bitmap is acceptable, and visited chunks are better. At about 8,000 by 6,000 a dense bitmap is about **6 MB** per village, and the server would load all of it to reveal a few tiles. Only visited chunks are stored.

Sprite coordinates are ordinary numbers. Past a few thousand tiles from the origin, sub-pixel steps start to quantize. Chunk-local coordinates are the fix, and they belong with the 8,000-tile ceiling, not with the 2,000-tile target.

## Order

1. Per-chunk generation, still at 120 by 90, so the picture matches today's map.
2. Chunked drawing at the same size. The ground of the current land is drawn this way.
3. Delta feed. Visited-chunk fog is already how the current map stores and sends what the village has seen.
4. Nearby interest for entities and battles.
5. Region activities and region pathfinding.
6. Raise the land to 2,000 by 1,500.

Far work uses a journey to a place, then a local job. The 2-second work walk stays for distance inside that place.

## Days of travel

A walk uses the same pace: **0.6 seconds per tile**, healthy. The sprite at 26 pixels per second only plays while the figure is on screen. Watching the whole way is about 3 percent longer than the server clock, so one real day on screen is about **140,000 tiles** instead of **144,000**. The clock does not finish a fog march while the player is away. Quiet hands on that march spawn a faster pursuer, and a catch is a fall, not a finished trip.

One village day is **4 real hours**.

| Time to the camp, straight line | Tiles from the edge to the center |
|---|---:|
| 1 real hour | 6,000 |
| 1 village day (4 hours) | 24,000 |
| 1 real day | 144,000 |
| 3 real days | 432,000 |
| 7 real days | 1,008,000 |

The land is twice that on the long side if the camp stays in the middle. One real day from the edge means a map about **288,000 by 216,000** tiles, keeping today's 4:3 shape. A corner is 1.25 times the edge time, so **30 hours** on that map.

A dense fog bitmap of that land is about **7.8 GB** per village. `Exploration.Seen` also copies the whole bitmap, and `Reveal` scans the rectangle around the entire trip. A diagonal day's walk makes that rectangle about 144,000 tiles on a side. Day-scale trips need three further changes on top of the 2,000-tile plan:

1. **A walk is the next step, on revealed ground.** The player orders a tile the village has already seen. The hero walks there. The order does not cross fog, and it does not reveal land by itself. There is no route the server finishes while the player is away from the fight. The client draws the hero only in the chunks around the camera.
2. **Coordinates are local to a chunk.** One real day is about 2.3 million pixels from the camp. A few days past that, ordinary sprite positions can no longer represent every pixel. Each chunk keeps its own origin.
3. **Fog is the chunks someone has seen.** A chunk is stored when a walker sees it or someone changes it. A walker sees **6 tiles** around them. Standing on the edge of known land reveals about 6 tiles past it, and the next order can go there. Scenery in the black is generated from the seed only when that band is revealed. Unseen land is not stored, and it is not a destination.

On today's map a click on fog does nothing. Building on unseen land stays refused. On the large map a click in the fog is allowed, and it is a march into the black, not a timer. The hero walks that way and reveals the 6 tiles they actually see. The march does not look ahead and does not plot a path around danger.

The march watches the hands. A mouse move or a key counts on a desk. On a phone the client must answer a presence ping. **15 seconds** with none of those, and a pursuer appears **12 tiles** ahead in the fog. The contact tile is the hero's tile at that moment. The check keeps running for the whole march. Breaking off one pursuer and going quiet again brings another. A tap at the edge and a device left on the table does not scout the late land.

The pursuer is faster than the hero, so a chase the hero can always win does not exist. The hero takes 0.6 seconds a tile. The pursuer takes **0.48** seconds a tile, one quarter faster. It leaves when the hero is more than **24 tiles** from the contact tile. A hero who turns within about 14 seconds and runs back covers those 24 tiles while the pursuer is still short of a strike, and the pursuer goes. A hero who keeps the order walks into it and is caught in a few seconds. The pursuer leaves none of a point's goods. This is the run. It is not the fight inside a claim.

A catch lays the hero on that tile. Every good they carried drops there and stays for whoever picks it up. Another citizen who reaches the body cures them, and they get up then, at the healthy pace. If nobody comes, the hero wakes alone after **30 minutes**, at **1%** of health, and walks at **2.4 seconds** a tile, four times the healthy pace. The crawl home is the cost of the tap. A 10-minute walk back takes 40 minutes. A fall at the day edge is about four real days of crawling. The walk home has no new pursuer while it stays on revealed ground and out of a living claim.

A walk that stays on revealed ground and out of a living claim has no attack. Marching into the fog is the run above. The ground between claims is not a road, and that distance is not simulated as its own trip.

There is no road yet. A road is a later building. Its job is to make farming faster, including at a patch that a den protects. Until that road is built, every walk takes the full 0.6 seconds a tile.

The player controls one citizen. A click on an enemy is an attack. The dungeon uses this same attack. A click on the ground is a walk. The controlled citizen can enter one claim, and the player clicks the enemies there. Each other citizen stops at the edge of a claim. Those citizens can walk and carry across revealed ground. Those citizens do not enter a claim, and they do not attack. One player can fight at one point.

Pushing the frontier is those watched steps. If the walker stays at the edge, the time to reveal a line is the same as walking it: **one real day** of scouting reaches **144,000 tiles** out. A known path that bends around fog is longer than the straight line, and each bend still has to be walked on revealed tiles.

## How far out each foe starts

On the square of about 288,000 tiles, the camp is in the middle and each edge is one real day away (144,000 tiles). A corner is about 34 hours. The order comes from the sprite descriptions, easiest first, including the archive sheets in `docs/local/pixel-sprite-archives.md`. A figure called weak or small, with no weapon, comes before a larger one. A tooth, a claw, a club, a blade, a spell, or armor comes later. Two weapons come after one. The crowned swordsman is near the end. The lich, seated on a throne with green flames and with no walk of his own, is last. Color variants of the same body share one floor. The fight numbers in the catalogs are not this order.

The map is seen from above. A creature on it must have a picture that faces up, down, left, and right. A picture that only faces sideways is not used. The nine knights all have the four faces, so all nine stand on their floor. The lich does not walk. His throne picture stays.

| Order | Foe | What the sprite says | Not closer than | Walk, healthy |
|---|---|---|---:|---:|
| 1 | Plain slime | Teal blob, dark-blue blob, and the island green slime. Eyes, no weapon. The round sideways slimes are not used | 1,000 | 10 minutes |
| 2 | Grave rat | "Fast, weak", a pack, body about 10 pixels | 7,800 | 1 hour 18 minutes |
| 3 | Imp | "Small", about 9 pixels tall, stubby horns, no weapon | 14,600 | 2 hours 26 minutes |
| 4 | Small goblin | Small green goblin, no weapon drawn | 21,400 | 3 hours 34 minutes |
| 5 | Wolf | Low grey animal, 9 pixels tall, yellow eyes, no weapon | 28,200 | 4 hours 42 minutes |
| 6 | Boar | Short tusks on a low heavy body | 35,000 | 5 hours 50 minutes |
| 7 | Goblin with a club | Green goblin, wooden club | 41,800 | 6 hours 58 minutes |
| 8 | Hooded orc | Green orc in a brown hood, a club | 48,600 | 8 hours 6 minutes |
| 9 | Scarecrow | Straw hat, stick arms | 55,400 | 9 hours 14 minutes |
| 10 | Ghoul | Hunched, about 11 pixels, long claws | 62,200 | 10 hours 22 minutes |
| 11 | Goblin with a sword | Green goblin, a sword | 69,000 | 11 hours 30 minutes |
| 12 | Banner goblin | Green goblin carrying a red-and-white banner | 75,800 | 12 hours 38 minutes |
| 13 | Wraith | Floating, hooded, dark empty eyes, no weapon | 82,600 | 13 hours 46 minutes |
| 14 | Flaming slime | Dark cracked blob with yellow flames. This slime faces four ways | 96,200 | 16 hours 2 minutes |
| 15 | Orc with one axe | Smaller orc, leather and cloth, one axe. This orc faces four ways | 103,000 | 17 hours 10 minutes |
| 16 | Bear | Widest animal, pale claws, 11 pixels tall | 109,800 | 18 hours 18 minutes |
| 17 | Vampire | Pale, a cape, a spell flash, no wings | 123,400 | 20 hours 34 minutes |
| 18 | Winged vampire | Pale, white hair, red bat wings, red cape, a spell flash | 143,800 | 23 hours 58 minutes |
| 19 | Orc with two swords | Blue-grey skin, a gold sword in each hand | 150,600 | 25 hours 6 minutes |
| 20 | Zombie | Standing green corpse, drawn twice as tall, 26 pixels, no weapon | 157,400 | 26 hours 14 minutes |
| 21 | Skeleton | The game's own bone figure, drawn twice as tall, 30 pixels. The sideways archive skeleton is not used | 171,000 | 28 hours 30 minutes |
| 22 | Armored orc | Tall black hair, red and gold armor, two swords | 177,800 | 29 hours 38 minutes |
| 23 | Fortress knight | Nine knights, each with four faces: sword and shield, crossbow, staff, spear, or halberd | 184,600 | 30 hours 46 minutes |
| 24 | Barrow king | "The boss", rusty gold crown, notched sword, a 16 by 24 frame | 191,400 | 31 hours 54 minutes |
| 25 | Lich | Seated on a spiked throne, blue robe, green eyes, green flames. No walk cycle | 198,200 | 33 hours 2 minutes |

The first floor is 10 minutes out, 1,000 tiles. Most steps are 6,800 tiles, which is 1 hour 8 minutes. These pictures only face sideways, so they are not on the map: the poison plant, the fire spirit, the archive skeleton, the round bouncing slimes, and the three large orcs (one axe and a topknot, a cleaver in each hand, a staff). Their ground stays empty. The foes that remain keep their old distances. Inside those 1,000 tiles there is no wild den. A den of a foe is never placed closer than that foe's floor. The straight edge, at 24 hours, is the winged vampire's floor. Everyone after him stands past that edge, toward the corners. The lich's floor is short of the corner, and the throne band runs from there to the corner.

Each level is one row. The seed places **eight** points of that foe around the camp, one every eighth of the circle, so the nights come from different directions. A point's direction is the line from the campfire to the point. On the first 2,000 by 1,500 land a point is kept only when it lies inside the land and past that foe's floor, so level 1 has the east and west points on the long edge. The north and south points fall outside the short edge.

A claim stays a small patch around the den: 48 tiles for a pack, 64 for one large figure. The grave rat shows four. Plain slimes show three: teal, dark blue, and the island green. Wolves show four, boars three, goblins three. The ghoul and the imp show two. One large figure shows one: the wraith, the bear, each orc, each vampire, the zombie, the skeleton, a knight, the barrow king, and the lich. Sprites exist while a citizen is inside the claim. Marks on the ground stay in the chunk texture.

These sheets are not dens. Deer, hare, fox, and black grouse are hunt animals, and the fox has no attack. Farm animals are stock. The orange crab has no attack. The island humans and the three roguelike heroes are people, not dens. Eye stalks, jaws, spikes, tentacles, and the other cursed growths dress the flesh ground. There is no plant den there. Dead trees and mushrooms dress the lich's land.

A night attack on the camp comes from one spawn point. The fighters appear on the fog nearest the camp along the line toward that point, and they come from that direction. They do not walk in from the point itself. If that line has already been revealed out to the point, they appear at the point, because that direction has no nearer fog. The other points stay out that night. A night fighter leaves none of the point's goods.

While the village has discovered no spawn point, the night picks a level 1 point. Discovering a level 1 point adds every level 2 point to the nights. A night can then come from a level 1 point or a level 2 point, still one point, from that point's direction. Discovering a point of a later level adds the next level that has a den. An empty band is not a level. A level that has joined the nights keeps sending attackers. A point counts as discovered when the 6-tile reveal band uncovers it. An opened level can attack from a point the village has not seen yet.

The 2,000 by 1,500 land reaches 1,000 tiles on the long edge, which is this 10-minute line, so a level 1 point can sit on that edge. The short edge is 750 tiles, 7 minutes 30 seconds, still inside the line. A den record is written when the reveal band uncovers its chunk, and only when that chunk is past the foe's floor. Unseen land has no den.

## Wild goods

Goods that are gathered sit away from the camp, so a trip is the reason to go out. The walk to a patch is at least as long as one bout of work there. Coming home takes the same walk again. Staying for another bout spends the work again, and the walk home waits until the citizen leaves.

Berries are the near patch. They stand 300 tiles out, which is 3 minutes. Picking lasts 3 minutes. The walk home is another 3 minutes, or the picker stays and picks again. The scarecrow's beds, hours farther out, are a later garden. They are not this patch.

The other wild goods are farther than the berries, and the richer ones are past the 10-minute line, so the path to them crosses a spawn point.

| Good | Not closer than | Walk | Where |
|---|---:|---:|---|
| Berries | 300 | 3 minutes | Inside the quiet ring. One bout is 3 minutes |
| Reeds | 600 | 6 minutes | Still inside the quiet ring, by the water |
| Wood | 1,000 | 10 minutes | On the first foe line |
| Stone | 2,000 | 20 minutes | In the slime marsh |
| Birds | 4,000 | 40 minutes | Deeper in that marsh |
| Rabbits, deer, ore | the band that holds them | the walk to that band | Rabbits and deer in the hunting wood, ore in the burrows and the later rock |

Fish at the pier and gold past the bridge stay where those projects stand. Crafted goods are made in the camp. They are not patches on the land.

## What a living point gives

The creatures that live on a point leave one good when they are slain. That good is the reason to keep the point. The same creature in a night raid leaves none of it. While the point stands, the locals return, so the good can be taken again. Ordinary patches, the berries and the wood and the stone, do not depend on the point.

Destroying a point makes that point calm. It leaves the nights, and its locals are gone, so its good stops. A level that was already opened stays open. There is no undo. The destroy action is not an attack. The locals do not strike during it.

Different levels use different actions.

| Action | Levels | What it takes | What becomes calm |
|---|---|---|---|
| Hold | Slime, grave rat, imp, wolf, boar, bear | Eight citizens, one standing on each of the eight points. They do not attack | Spawn at that level stops while all eight remain |
| Flare | Scarecrow, ghoul, wraith, both vampires, zombie, skeleton, barrow king, lich | One flare, used at that point. A flare is 1 wood and 1 feather, made in the camp | That point only |
| Wood | Every goblin | 10 wood, used at that point | That point only |
| Wood and ore | Every orc | 10 wood and 10 iron ore, used at that point | That point only |
| Stone | Flaming slime | 10 stone, used at that point | That point only |
| Stone and iron | The nine knights | 10 stone and 10 iron ingots, used at that point | That point only |

The hold is a mechanism, not a fight and not eight attacks. The eight citizens turn it on by staying on the eight points. While it is on, that level does not spawn, so its nights stop and its good stops. One citizen steps off, and the spawn returns. The other actions clear one point, and they do not turn back on. The other points of that level keep their good, and they can still send a night. Ten wood is one pair of hands. A cost of 100 wood would be ten walks, and the goblin ring is about 7 hours out.

A point never leaves a weapon. A sword, a spear, a bow, an arrow, and a shield are still made only from their camp recipes, after the technology that allows them. The tusk, the sinew, the claw, and the plate pay for that research. They are not the weapon.

Each good is the pile a technology eats before its research can start. The cycles and the crew do not begin until the village has turned that pile in. The pile is consumed. One slain local leaves one of the good. Destroy the point before the pile is in, and that supply stops. Another living point of the same creature can still fill it. Once the pile is turned in, the technology no longer needs the point.

The count is the time, not a flat 1,000. These are the working numbers. A slain local is back after **10 minutes**. Hands hold **10** of that good, the same carry as today. A kill takes **15 seconds**, which is already inside the times below. One fighter walks to the nearest living point and stays until the hands are full. A second citizen can carry that load home across revealed ground. That walk takes the full time. There is no road under it. The fighter can stay. The carrier does not fight. A pile of 10 is one fight and one walk home.

A flat 1,000 is 100 loads. On the slime ring one load is 52 minutes, so 1,000 is about **88 hours**, three and a half days of one walker. On the skeleton ring one load is already **59 hours**, so 1,000 is about **240 days**. The pile below is what one gatherer carries home in one real day, rounded to the nearest 10, and it is never under 10. Past the ghoul the round trip itself is longer than a day, so the pile stays one load and the time is that expedition.

| Technology | Pile | From | One gatherer |
|---|---|---|---:|
| Herb lore | 270 jelly | Plain slime, a pack of 3, 10 minutes | 24 hours |
| Stone tools | 80 grease | Grave rat, a pack of 4 | 24 hours |
| Mining | 40 cinder | Imp, a pair | 22 hours |
| Archery | 30 sinew | The nearest goblin, a pack of 3 | 23 hours |
| Tracking | 20 fang | Wolf, a pack of 4 | 20 hours |
| Weaponcraft | 20 tusk | Boar, a pack of 3 | 24 hours |
| Agriculture | 10 straw | Scarecrow, one | 20 hours |
| Alchemy | 10 nail | Ghoul, a pair | 21 hours |
| Writing | 10 ash | Wraith, one | 29 hours |
| Brewing | None | No creature. The plant picture only faces sideways | — |
| Smelting | 10 ember | Flaming slime, one | 34 hours |
| Mechanics | 10 tooth | The nearest orc, the hooded one | 18 hours |
| Warfare | 10 claws, and 10 plate scraps | Bear, then a knight | 38 hours, then 63 hours |
| Bookkeeping | 10 dust | The nearer vampire | 43 hours |
| Baking | 10 salt | Zombie, one | 54 hours |
| Forging | None | No creature. The fire spirit only faces sideways | — |
| Carpentry | 10 bone | Skeleton, one | 59 hours |
| Music | 10 crown flakes | Barrow king, one | 65 hours |
| Vigor drinks | 10 green flames | Lich, one | 68 hours |

Weaving, weather lore, brewing, and forging take no creature good. Reeds and the sky still finish the first two. Brewing and forging wait only on their parent technologies. Every other technology waits on a pile.

One slime load is the 10-minute walk out, 30 minutes while the pack of 3 refills the hands, the kills, and the 10-minute walk home. Twenty-seven of those loads are the 270. A rat load waits 20 minutes because four rats refill faster. A single figure waits 90 minutes for the other nine. The far rows are almost all walking: the lich is 33 hours out and 33 hours back, plus that 90-minute wait.

Warfare does not start until both piles are in. Vigor drinks do not start until the flame is in, and brewing is already finished. A child technology still waits on its parent in the catalog. The pile does not replace that. The pile can be gathered before the parent is finished. A carrier can take each slime load home, so the fighter does not walk back between loads. The 270 jelly is then about 14 hours at the point, after the first 10-minute walk. The lich stays one fight and one walk of 68 hours. A second fighter cannot work a second point at the same time, because the player has one attack.

## The shape of the land

The ring stays the rule for who lives there. A foe is never closer than its floor, and the ground changes at the next floor. The river, the coast, and the ridges do not move a floor. They are the reason one direction is not the same walk as another.

Three features are enough. Each is a function of the seed and the coordinate, so a chunk can draw its piece without looking at the chunk beside it.

The camp's river runs on north and south out of the vale. It is the same water, four tiles wide, with a slow bend. It blocks a step. The bridge in the vale is the first crossing. The next ford is twenty minutes upstream, and another is twenty minutes downstream. After those, the river has one ford each hour of its length. Fish stay at the pier, and gold stays past the bridge. A point that would sit in the water slides along its ring to the nearest dry tile, and that tile is still at least the foe's floor away.

The sea is a bay on the west. Its nearest sand is the ten-minute line. Sand, then pebbles, then foam, then water that blocks. The sea fills the west beyond that shore and crosses the later bands, so the west edge is water, not a day of grass. A western den stands on the inland edge of the sand, still no closer than its own floor. The crab is a prop on the sand. Nothing is hunted in the sea, and nothing sails. On the first 2,000 by 1,500 land the west edge is this beach, and the east edge is the slime line on grass.

Two ridges, and no more. Each is an arc of about a third of its circle, with two passes of open trail, and the seed picks where the arc sits. The first is at the seven-hour line, where the goblin camp starts. The second is at the day edge, between the ritual ground and the high ruin. The rest of those rings is open. A pass is the crossing a region path uses. The ridge itself is mountain, and it blocks.

What stops a step stays close to today's feet. Water, a mountain ridge, and a ravine block. A pit blocks: the green-rim pits, the flesh pits, and the fortress pit. A standing wall, a barred gate, a portcullis, and a cave mouth block. Trees, bushes, reeds, crops, mushrooms, bones, rubble, crystals, tents, grave stones, and the flesh floor do not. Broken masonry is floor. A thicket is a work site you are sent to clear, not a maze of the hunting wood. Rockfall sits on the ridge, beside the passes.

The quiet ring is the land inside the ten-minute line, past the vale the game already draws.

| From the camp | What you walk through |
|---|---|
| The vale, under a minute | Today's grass, river, bridge, pier, fields, and trees. Unchanged |
| Out to 3 minutes | Grass and dirt. The trees thin. No berries yet, and no den |
| 3 minutes | Eight berry patches on the dry circle. A patch that would fall in the river slides to the bank. One bout is 3 minutes |
| 3 to 6 minutes | More of that grass. The river is the only break |
| 6 minutes | Reed beds on both banks, and on the bay shore. These are the reeds that are cut |
| 6 to 10 minutes | The grass lowers toward the west. The other directions stay dry |
| 10 minutes west | Sand and the first slime marsh inland of it |
| 10 minutes east, and the other dry directions | The slime line on grass |

A citizen carries food for the walk, or gathers it on the way: berries, birds, rabbits, and deer, at the distances already set. Fish stay at the pier.

A fire is the checkpoint. It costs **5 wood** to light on revealed ground, outside a claim, and not on water, a ridge, or a pit. It burns **8 hours**. Another 5 wood adds 8 hours. At a burning fire a citizen eats carried food and rests, and energy returns there. Without a fire, the citizen cannot rest on the walk.

A new building must stand within **16 tiles** of a burning fire. The camp fire already permits the vale. A journey fire permits the same buildings beside it. Research still starts only at the camp. A store beside a journey fire can hold wood, stone, and food. It does not accept a technology pile.

A burning fire is also a raid target. One night still comes from one spawn point. The night can strike the camp fire or one journey fire. The fighters appear on the fog nearest that fire, on the line toward the chosen point. A dead fire is not a target. The buildings remain, but nobody can rest there or add a building until the fire burns again.

## Areas

The open grounds in the archives are six. Island: sea, foam, sand, pebbles, grass, dirt, a dirt path, brown cliffs, a cave mouth. Undead: grey cracked earth, black cliffs, pits with a green rim, pebble patches. Cursed: pink flesh, red vein cliffs, pits. Fortress: grass, dirt, grey paving, a pit. Grey dungeon: slab, cobble, floors with a blue water rim. Roguelike rooms: grey stone floor, a darker cracked floor. There is no lava, no ash field, no snow field, and no desert of dunes. Snow bushes and a cactus exist, and they have no ground to stand on, so they stay unused. Sand is the western bay, not a dune sea, and no foe claims it. The crab stands on that sand as a prop.

Dungeon rooms, tables, chairs, and bookshelves are the inside of a door, not the open land. The game's own grass, river, reeds, marsh, mountains, ravine, thicket, and cave stay the home vale. A ploughed field tile is the game's farm beds. The archive crops are plants, not soil.

| Area | Walk | Ground | Decorations | Foes |
|---|---|---|---|---|
| Home vale | under 10 minutes | Today's land, then grass out to the berry circle, the river north and south, and the western sand at the slime line | Camp, pier, fields, thinning trees. Eight berry patches at 3 minutes, reed beds at 6 minutes on the banks and the bay | None by day. A night comes from one undiscovered level 1 point, from that point's direction |
| Slime marsh | 10 minutes to 1 hour 18 minutes | Island grass, the game's marsh, shallow water and foam | Ferns, flowering bushes, reeds. Wood, stone, and birds are gathered here | Plain slime |
| Burrows | 1 hour 18 minutes to 3 hours 34 minutes | Dirt, beige rock outcrops | A few loose bones, small beige mushrooms | Grave rat, then imp. Ore starts here |
| Hunting wood | 3 hours 34 minutes to 6 hours 58 minutes | Island grass, dirt path | Ball trees, plain green bushes. Deer, hare, and grouse are animals here, not foes | Small goblin, wolf, boar. Rabbits and deer are hunted here |
| Goblin camp and field | 6 hours 58 minutes to 10 hours 22 minutes | Grass and dirt. The field uses the game's farm beds | Autumn bushes, tan tents, crates, a campfire. Sunflower, onion, and berry bushes on the beds | Club goblin, hooded orc, scarecrow |
| First graves | 10 hours 22 minutes to 11 hours 30 minutes | Grey cracked earth | Grave stones, thorn bushes, dead trees, small blue mushrooms | Ghoul |
| Brown ruin | 11 hours 30 minutes to 13 hours 46 minutes | Grass, with brown masonry on it | Broken walls, an arch, rubble, a round tower | Sword goblin, banner goblin |
| White dead wood | 13 hours 46 minutes to 14 hours 54 minutes | Grey cracked earth, black cliff edges | White bare trees, bone piles, pale mushrooms | Wraith |
| Flesh pits | 14 hours 54 minutes to 16 hours 2 minutes | Pink flesh, red vein cliffs, pits | Eye stalks, a mound of eyes, jaws, red spines, tentacles, a meat flower, pustules | No den. The plant picture only faces sideways |
| Burned rock | 16 hours 2 minutes to 17 hours 10 minutes | No ground matches. The nearest sheets are dark rock props | A burned black trunk, orange-spotted rocks, red and yellow crystals, a brazier | Flaming slime |
| Rocky wood | 17 hours 10 minutes to 20 hours 34 minutes | Dirt and brown cliffs. Loose grey rocks are props, not a floor | Fern trees, oval-leaf trees, black and orange mushrooms, cave mouths, a deer statue, yurts, weapon racks | Orc with one axe, then the bear. The large sideways orc is not used |
| Blue ruin | 20 hours 34 minutes to 23 hours 58 minutes | Grey paving, cobble, blue-grey masonry, then dirt and totems | Mossy walls, columns, luminous trees, a chest, wall torches. Bones and a bone circle further out | Vampire. The two large sideways orcs are not used. This band reaches the straight edge |
| High ruin | 23 hours 58 minutes to 26 hours 14 minutes | Fortress grey walls and paving, the small keep | A barred gate, arrow slits, a wooden door, a pit in the grass, white and luminous trees | Winged vampire, orc with two swords |
| Deep graves | 26 hours 14 minutes to 27 hours 22 minutes | Black cliffs, pits with a green rim | Skull doorway, reaching dead arms, piles of skulls, rib cages | Zombie |
| Fire waste | 27 hours 22 minutes to 28 hours 30 minutes | No ground matches | Red, yellow, and orange crystals, braziers, the bone-ring fire, orange-spotted rock | No den. The fire spirit only faces sideways |
| Bone field | 28 hours 30 minutes to 29 hours 38 minutes | Grey cracked earth | The large bone piles, a human skeleton prop, scattered skulls | Skeleton |
| Keep | 29 hours 38 minutes to 31 hours 54 minutes | Fortress walls and the grey dungeon floor, inside the doors | Portcullis, chests, weapon racks, shields, barrels, a guillotine as a prop | Armored orc, then the nine knights on one floor |
| Barrow | 31 hours 54 minutes to 33 hours 2 minutes | Grey cracked earth, grave rows | Mounds, a standing stone, the skull doorway | Barrow king |
| Throne | 33 hours 2 minutes to the corner | Dead-land cliffs, the green glow along the pit rims | The spiked throne, the cairn with skulls and green smoke, green flames | Lich |

Three grounds are missing, so three foes stand on props.

- The imp has no brimstone and no lava. He stands on dirt and beige rock.
- The flaming slime has no ash field and no lava. He stands on dark rocks, crystals, and fire props. The fire spirit is not on the map.
- The scarecrow's soil is the game's farm tile. The archive only supplies the crops on it.

The distance order also breaks two themes in half. The brown ruin, with living goblins, sits between the ghoul's graves and the wraith's dead wood. The bear, the unwinged vampire, and the zombie each sit between orc bands, so the rocky camp stops and starts again. The ground changes at those floors. It does not slide sideways to keep one theme together.

There is no useful tile cap after those three. A week from the edge is about a million tiles and stays cheap, because an empty region is a seed and a journey is one record. The 2,000 by 1,500 land remains the first size to build. Day-scale routes come after chunk-local coordinates and route fog.
