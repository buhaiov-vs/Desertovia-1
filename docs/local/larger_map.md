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

## Areas and their guardians

A region is a kind of land, one den, and a border. Clawed bark, wallows, bones, and mounds are painted into the chunk texture with the ground. A beast sprite exists while a citizen is inside that den's claim. An empty claim is the seed and one den record.

One claim keeps a small pack: four wolves, three boars, or one bear. A village with people in five places shows at most a few dozen beast sprites. The thousands of trees and territory marks stay in the chunk textures. Dungeon foes stay inside the run. A far hunt is one line until someone walks there.

The night raid on the camp keeps today's list. A bear in a far range stays in that range. Camp-era nights stay wolves; boars join in the Village era and bears in the Town era, as they do now.

Today's 120 by 90 land is the home vale. These rings are the 2,000 by 1,500 land. The camp stays in the middle. Distance is tiles from the camp. Time is the healthy server clock, 0.6 seconds a tile, in a straight line.

| Ring | Tiles from camp | Walk | Dens on the whole land | Guardian at the den | What the claim holds |
|---|---:|---:|---:|---|---|
| Home vale | 0–60 | under a minute | none | the camp raid, at night | today's workplaces, the cave, the lookout |
| Meadows | 60–200 | 1–2 minutes | 6 wolf dens | 4 wolves | rabbits, berries |
| Mast woods | 200–400 | 2–4 minutes | 6 boar stands | 3 boars | thickets, deer |
| Bear ranges | 400–700 | 4–7 minutes | 8 bear dens | 1 bear | deep wood, a pass |
| Barrow downs | 700–1,000 | 7–10 minutes | 4 mounds | the dungeon floors, indoors | ore and ruin goods, through the door |

The long edge is 1,000 tiles, about 10 minutes. A corner is 1,250 tiles, about 12 minutes 30 seconds, and it is still barrow downs.

### Where a den sits

Dens are a short list, the same way the cave and the lookout are. The ring decides which guardian is allowed. The village seed picks the sector and the den tile. Six wolf dens on a ring about 130 tiles out sit roughly 140 tiles apart, about a minute and a half of open meadow between claims. Six boar stands sit a few minutes apart. Eight bears sit a few minutes apart, with a ridge between neighbors. Four mounds sit toward the four corners.

A claim is the ground that den protects:

| Den | Claim radius | Walk across the claim |
|---|---:|---:|
| Wolf | 48 tiles | about 30 seconds |
| Boar | 48 tiles | about 30 seconds |
| Bear | 64 tiles, and the pass it sits on | about 40 seconds |
| Barrow | the mound and the dungeon | the run that exists today |

The river, a ridge, and the ravine are borders even when both sides are the same ring. A ford or a pass is the crossing. The encounter is the guardian of the side you are entering. Open ground between claims has the same beast as a stray: the small per-cycle chance work already uses, and a single sprite for that fight. The stray does not stay after the fight.

### What protection means

Work inside a claim uses that one kind as its danger list, the way rabbit woods lists the wolf today. The chance stays a small chance per work cycle. Come within sight of an occupied den, the 6 tiles a walker already sees, and the pack is there.

Clearing the den quiets the claim for the whole village, the way fog is shared. Wolves return the next night. Boars return after one village day, 4 real hours. A bear returns after three village days. A cleared mound stays empty for one village day, then the same floors are there again. The pack does not follow onto the road. Leave the claim and the sprites go away. Come back while the den is occupied and they are at the den, unless a fight in that claim is already running.

A barrow has no wandering dead on the grass. The mound is a door. Inside, the floors keep the foes they have: grave rats, imps, ghouls, wraiths, and a barrow king. Those kinds stay off the camp raid and off the wild roads.

### Day-scale land

Past the first land, a day's walk is a chain of these claims along a route the village has seen. A den record is written when the 6-tile reveal band first uncovers the chunk that holds it. The guardian comes from the seed and the ring, and it is stored with that chunk. Unseen land has no den and no sprites. A new kind of guardian, when one is added, is one more den record in a ring. The rings and the claim radii stay as they are.

There is no useful tile cap after those three. A week from the edge is about a million tiles and stays cheap, because an empty region is a seed and a journey is one record. The 2,000 by 1,500 land remains the first size to build. Day-scale routes come after chunk-local coordinates and route fog.
