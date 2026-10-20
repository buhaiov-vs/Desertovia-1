# Larger map

Plan only. No game code, version, or server change.

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

Joining the village currently creates one sprite per ground tile, then hides the ones off screen. Three million sprites at the target size will not run on a phone.

Build a texture for each block of about **32 by 32** tiles near the camera, and drop blocks that scroll away. The screen stays about 34 tiles wide, so a handful of blocks is enough. The minimap keeps a coarse copy of the whole land.

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

Fog memory on the client is three full-land arrays. At the target size a dense bitmap is acceptable, and visited chunks are better. At about 8,000 by 6,000 a dense bitmap is about **6 MB** per village, and the server would load all of it to reveal a few tiles. Only visited chunks are stored.

Sprite coordinates are ordinary numbers. Past a few thousand tiles from the origin, sub-pixel steps start to quantize. Chunk-local coordinates are the fix, and they belong with the 8,000-tile ceiling, not with the 2,000-tile target.

## Order

1. Per-chunk generation, still at 120 by 90, so the picture matches today's map.
2. Chunked drawing at the same size.
3. Delta feed and visited-chunk fog.
4. Nearby interest for entities and battles.
5. Region activities and region pathfinding.
6. Raise the land to 2,000 by 1,500.

Far work uses a journey to a place, then a local job. The 2-second work walk stays for distance inside that place.
