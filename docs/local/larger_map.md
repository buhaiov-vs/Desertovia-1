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

A walk of hours or days uses the same arrival clock: **0.6 seconds per tile**, healthy, and that clock keeps running when the player is away. The sprite at 26 pixels per second only plays while the figure is on screen. Watching the whole way is about 3 percent longer than the server clock, so one real day on screen is about **140,000 tiles** instead of **144,000**.

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

1. **The journey is one timed route over land the village has already seen.** The destination is a revealed tile, or a tile someone can see right now. The route stays on those tiles; it does not cut through fog. Start, destination, and departure are enough. The position at any moment is a point along that route. The server does not step the walker tile by tile. The client draws them only in the chunks around the camera.
2. **Coordinates are local to a chunk.** One real day is about 2.3 million pixels from the camp. A few days past that, ordinary sprite positions can no longer represent every pixel. Each chunk keeps its own origin.
3. **Fog is the chunks someone has seen.** A chunk is stored when a walker sees it or someone changes it. A walker sees **6 tiles** around them. Standing on the edge of known land reveals about 6 tiles past it, and the next order can go there. Scenery in the black is generated from the seed only when that band is revealed. Unseen land is not stored, and it is not a destination.

A click on fog already does nothing. Building on unseen land is already refused. The server still accepts any walk coordinate; on the large map it refuses a destination, or a route, that leaves revealed land.

Pushing the frontier is a series of those 6-tile bands. If the walker stays at the edge, the time to reveal a line is the same as walking it: **one real day** of scouting reaches **144,000 tiles** out. One order cannot name a ruin in the black and travel there overnight. An overnight journey only crosses land the village has already seen, and a known road that bends around fog is longer than the straight line.

Encounters sit on region crossings: an ambush, a camp, a ruin. They are checked when the traveler enters that region. The road between crossings has no simulation.

## How far out each foe starts

On the square of about 288,000 tiles, the camp is in the middle and each edge is one real day away (144,000 tiles). The order of foes comes from the sprite descriptions, easiest first. A figure the text calls weak or small, with no weapon, comes before a larger one. A weapon comes before an empty hand. The description that says "the boss" and gives him a crown and a sword is last. The fight numbers in the catalogs are not this order.

Wolf, boar, bear, zombie, and skeleton have no prompt row. Their order uses the pixels they are drawn with. Zombie and skeleton are drawn at double size.

| Order | Foe | What the sprite says | Not closer than | Walk, healthy |
|---|---|---|---:|---:|
| 1 | Grave rat | "Fast, weak", a pack, body about 10 pixels | 6,000 | 1 hour |
| 2 | Imp | "Small", about 9 pixels tall, stubby horns, no weapon | 20,000 | 3 hours 20 minutes |
| 3 | Wolf | Low grey animal, 9 pixels tall, yellow eyes, no weapon | 34,000 | 5 hours 40 minutes |
| 4 | Boar | Short tusks on a low heavy body | 48,000 | 8 hours |
| 5 | Ghoul | Hunched, about 11 pixels, long claws reaching forward | 62,000 | 10 hours 20 minutes |
| 6 | Wraith | Floating, hooded, dark empty eyes, no weapon, about 10 pixels | 76,000 | 12 hours 40 minutes |
| 7 | Bear | Widest animal, pale claws, 11 pixels tall | 90,000 | 15 hours |
| 8 | Zombie | Standing green corpse, drawn twice as tall, 26 pixels, no weapon | 104,000 | 17 hours 20 minutes |
| 9 | Skeleton | Bone figure, drawn twice as tall, 30 pixels, glowing eyes; the archive sheet also gives him a short blade | 118,000 | 19 hours 40 minutes |
| 10 | Barrow king | "The boss", rusty gold crown, notched sword, a 16 by 24 frame | 132,000 | 22 hours |

The floors are 14,000 tiles apart, which is 2 hours 20 minutes. Inside 6,000 tiles there is no wild den. A den of a foe is never placed closer than that foe's floor. From one floor to the next, the den is that easier foe. Past 132,000 tiles, including the corners at about 34 hours, the den is the barrow king.

A claim stays a small patch around the den: 48 tiles for a pack, 64 for one large figure. The grave rat's text says a pack, so the den shows four. Wolves show four, boars three. The ghoul shows two. The imp shows two. The wraith, the bear, the zombie, the skeleton, and the barrow king show one. Sprites exist while a citizen is inside the claim. Marks on the ground stay in the chunk texture.

The night raid on the camp is separate and still comes to the fire. Camp-era nights stay wolves; boars join in the Village era and bears in the Town era. Those raids are not dens on this ladder.

The 2,000 by 1,500 land ends about 1,000 tiles from the camp, inside the first hour, so it has no wild den from this list. A den record on the day-scale land is written when the 6-tile reveal band first uncovers its chunk, and only when that chunk is past the foe's floor. Unseen land has no den.

There is no useful tile cap after those three. A week from the edge is about a million tiles and stays cheap, because an empty region is a seed and a journey is one record. The 2,000 by 1,500 land remains the first size to build. Day-scale routes come after chunk-local coordinates and route fog.
