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

On the square of about 288,000 tiles, the camp is in the middle and each edge is one real day away (144,000 tiles). A corner is about 34 hours. The order comes from the sprite descriptions, easiest first, including the archive sheets in `docs/local/pixel-sprite-archives.md`. A figure called weak or small, with no weapon, comes before a larger one. A tooth, a claw, a club, a blade, a spell, or armor comes later. Two weapons come after one. The crowned swordsman is near the end. The lich, seated on a throne with green flames and with no walk of his own, is last. Color variants of the same body share one floor. The fight numbers in the catalogs are not this order.

| Order | Foe | What the sprite says | Not closer than | Walk, healthy |
|---|---|---|---:|---:|
| 1 | Plain slime | Round blue, green, or red jelly; teal or dark-blue blob; island green slime. Eyes, no weapon | 6,000 | 1 hour |
| 2 | Grave rat | "Fast, weak", a pack, body about 10 pixels | 12,800 | 2 hours 8 minutes |
| 3 | Imp | "Small", about 9 pixels tall, stubby horns, no weapon | 19,600 | 3 hours 16 minutes |
| 4 | Small goblin | Small green goblin, no weapon drawn | 26,400 | 4 hours 24 minutes |
| 5 | Wolf | Low grey animal, 9 pixels tall, yellow eyes, no weapon | 33,200 | 5 hours 32 minutes |
| 6 | Boar | Short tusks on a low heavy body | 40,000 | 6 hours 40 minutes |
| 7 | Goblin with a club | Green goblin, wooden club | 46,800 | 7 hours 48 minutes |
| 8 | Hooded orc | Green orc in a brown hood, a club | 53,600 | 8 hours 56 minutes |
| 9 | Scarecrow | Straw hat, stick arms | 60,400 | 10 hours 4 minutes |
| 10 | Ghoul | Hunched, about 11 pixels, long claws | 67,200 | 11 hours 12 minutes |
| 11 | Goblin with a sword | Green goblin, a sword | 74,000 | 12 hours 20 minutes |
| 12 | Banner goblin | Green goblin carrying a red-and-white banner | 80,800 | 13 hours 28 minutes |
| 13 | Wraith | Floating, hooded, dark empty eyes, no weapon | 87,600 | 14 hours 36 minutes |
| 14 | Poison plant | Dark purple bulb, leafy crown, poison puff and poison cloud | 94,400 | 15 hours 44 minutes |
| 15 | Flaming slime | Dark cracked blob with yellow flames | 101,200 | 16 hours 52 minutes |
| 16 | Orc with one axe | Smaller orc, leather and cloth, one axe | 108,000 | 18 hours |
| 17 | Bear | Widest animal, pale claws, 11 pixels tall | 114,800 | 19 hours 8 minutes |
| 18 | Orc warrior | Large orc, topknot, one axe | 121,600 | 20 hours 16 minutes |
| 19 | Vampire | Pale, a cape, a spell flash, no wings | 128,400 | 21 hours 24 minutes |
| 20 | Orc berserk | Large, tusks, bare chest, a cleaver in each hand | 135,200 | 22 hours 32 minutes |
| 21 | Orc shaman | Large, grey tunic, wooden staff, two spell casts | 142,000 | 23 hours 40 minutes |
| 22 | Winged vampire | Pale, white hair, red bat wings, red cape, a spell flash | 148,800 | 24 hours 48 minutes |
| 23 | Orc with two swords | Blue-grey skin, a gold sword in each hand | 155,600 | 25 hours 56 minutes |
| 24 | Zombie | Standing green corpse, drawn twice as tall, 26 pixels, no weapon | 162,400 | 27 hours 4 minutes |
| 25 | Fire spirit | Yellow-orange flame with a face, a shot, a charge, and an explosion | 169,200 | 28 hours 12 minutes |
| 26 | Skeleton | Bone figure, drawn twice as tall, 30 pixels; the archive sheet gives him a short blade | 176,000 | 29 hours 20 minutes |
| 27 | Armored orc | Tall black hair, red and gold armor, two swords | 182,800 | 30 hours 28 minutes |
| 28 | Fortress knight | Plate or a helm, sword and shield, or a spear, halberd, crossbow, or staff. Nine looks, one floor | 189,600 | 31 hours 36 minutes |
| 29 | Barrow king | "The boss", rusty gold crown, notched sword, a 16 by 24 frame | 196,400 | 32 hours 44 minutes |
| 30 | Lich | Seated on a spiked throne, blue robe, green eyes, green flames. No walk cycle | 203,200 | 33 hours 52 minutes |

The floors are 6,800 tiles apart, which is 1 hour 8 minutes. Inside 6,000 tiles there is no wild den. A den of a foe is never placed closer than that foe's floor. From one floor to the next, the den is that foe. The straight edge, at 24 hours, falls in the shaman's band. The winged vampire and everyone after him stand past that edge, toward the corners. The lich's floor is the corner.

A claim stays a small patch around the den: 48 tiles for a pack, 64 for one large figure. The grave rat shows four. Plain slimes show three, one of each ordinary color. Wolves show four, boars three, goblins three. The ghoul and the imp show two. One large figure shows one: the wraith, the bear, each orc, each vampire, the zombie, the skeleton, a knight, the barrow king, and the lich. Sprites exist while a citizen is inside the claim. Marks on the ground stay in the chunk texture.

These sheets are not dens. Deer, hare, fox, and black grouse are hunt animals, and the fox has no attack. Farm animals are stock. The orange crab has no attack. The island humans and the three roguelike heroes are people, not dens. Eye stalks, jaws, spikes, tentacles, and the other cursed growths dress the poison plant's land. Dead trees and mushrooms dress the lich's land.

The night raid on the camp is separate and still comes to the fire. Camp-era nights stay wolves; boars join in the Village era and bears in the Town era. Those raids are not dens on this ladder.

The 2,000 by 1,500 land ends about 1,000 tiles from the camp, inside the first hour, so it has no wild den from this list. A den record on the day-scale land is written when the 6-tile reveal band first uncovers its chunk, and only when that chunk is past the foe's floor. Unseen land has no den.

## Areas

The open grounds in the archives are six. Island: sea, foam, sand, pebbles, grass, dirt, a dirt path, brown cliffs, a cave mouth. Undead: grey cracked earth, black cliffs, pits with a green rim, pebble patches. Cursed: pink flesh, red vein cliffs, pits. Fortress: grass, dirt, grey paving, a pit. Grey dungeon: slab, cobble, floors with a blue water rim. Roguelike rooms: grey stone floor, a darker cracked floor. There is no lava, no ash field, no snow field, and no desert of dunes. Snow bushes and a cactus exist, and they have no ground to stand on, so they stay unused. Sand is a shore, not a dune sea, and no foe claims it. The sea, the foam, and the pebbles are a coast that can cross the nearer bands. The crab stands on that sand as a prop.

Dungeon rooms, tables, chairs, and bookshelves are the inside of a door, not the open land. The game's own grass, river, reeds, marsh, mountains, ravine, thicket, and cave stay the home vale. A ploughed field tile is the game's farm beds. The archive crops are plants, not soil.

| Area | Walk | Ground | Decorations | Foes |
|---|---|---|---|---|
| Home vale | under 1 hour | The game's own land | Camp, pier, fields, today's trees | None. The night raid still comes to the fire |
| Slime marsh | 1 hour to 2 hours 8 minutes | Island grass, the game's marsh, shallow water and foam | Ferns, flowering bushes, reeds | Plain slime |
| Burrows | 2 hours 8 minutes to 4 hours 24 minutes | Dirt, beige rock outcrops | A few loose bones, small beige mushrooms | Grave rat, then imp |
| Hunting wood | 4 hours 24 minutes to 7 hours 48 minutes | Island grass, dirt path | Ball trees, plain green bushes. Deer, hare, and grouse are animals here, not foes | Small goblin, wolf, boar |
| Goblin camp and field | 7 hours 48 minutes to 11 hours 12 minutes | Grass and dirt. The field uses the game's farm beds | Autumn bushes, tan tents, crates, a campfire. Sunflower, onion, and berry bushes on the beds | Club goblin, hooded orc, scarecrow |
| First graves | 11 hours 12 minutes to 12 hours 20 minutes | Grey cracked earth | Grave stones, thorn bushes, dead trees, small blue mushrooms | Ghoul |
| Brown ruin | 12 hours 20 minutes to 14 hours 36 minutes | Grass, with brown masonry on it | Broken walls, an arch, rubble, a round tower | Sword goblin, banner goblin |
| White dead wood | 14 hours 36 minutes to 15 hours 44 minutes | Grey cracked earth, black cliff edges | White bare trees, bone piles, pale mushrooms | Wraith |
| Flesh pits | 15 hours 44 minutes to 16 hours 52 minutes | Pink flesh, red vein cliffs, pits | Eye stalks, a mound of eyes, jaws, red spines, tentacles, a meat flower, pustules | Poison plant |
| Burned rock | 16 hours 52 minutes to 18 hours | No ground matches. The nearest sheets are dark rock props | A burned black trunk, orange-spotted rocks, red and yellow crystals, a brazier | Flaming slime |
| Rocky wood | 18 hours to 20 hours 16 minutes | Dirt and brown cliffs. Loose grey rocks are props, not a floor | Fern trees, oval-leaf trees, black and orange mushrooms, cave mouths, a deer statue, two red-roofed yurts. Ents and tree idols can stand among the trees | Orc with one axe, bear |
| Warrior camp | 20 hours 16 minutes to 21 hours 24 minutes | The same dirt and cliffs | More yurts, weapon racks, loose axes, dragon bones as a prop | Orc warrior |
| Blue ruin | 21 hours 24 minutes to 22 hours 32 minutes | Grey paving, cobble, blue-grey masonry | Mossy walls, columns, luminous trees, a chest, wall torches | Vampire |
| Berserk camp | 22 hours 32 minutes to 23 hours 40 minutes | Dirt and cliffs again | Bones, horned skull trophies, the racks | Orc berserk |
| Ritual ground | 23 hours 40 minutes to 24 hours 48 minutes | Dirt, beige rock | Dark totems, a bone magic circle, a bone-ring fire, the caveman statue | Orc shaman. This band reaches the straight edge |
| High ruin | 24 hours 48 minutes to 27 hours 4 minutes | Fortress grey walls and paving, the small keep | A barred gate, arrow slits, a wooden door, a pit in the grass, white and luminous trees | Winged vampire, orc with two swords |
| Deep graves | 27 hours 4 minutes to 28 hours 12 minutes | Black cliffs, pits with a green rim | Skull doorway, reaching dead arms, piles of skulls, rib cages | Zombie |
| Fire waste | 28 hours 12 minutes to 29 hours 20 minutes | No ground matches | Red, yellow, and orange crystals, braziers, the bone-ring fire, orange-spotted rock | Fire spirit |
| Bone field | 29 hours 20 minutes to 30 hours 28 minutes | Grey cracked earth | The large bone piles, a human skeleton prop, scattered skulls | Skeleton |
| Keep | 30 hours 28 minutes to 32 hours 44 minutes | Fortress walls and the grey dungeon floor, inside the doors | Portcullis, chests, weapon racks, shields, barrels, a guillotine as a prop | Armored orc, then the nine knights on one floor |
| Barrow | 32 hours 44 minutes to 33 hours 52 minutes | Grey cracked earth, grave rows | Mounds, a standing stone, the skull doorway | Barrow king |
| Throne | 33 hours 52 minutes to the corner | Dead-land cliffs, the green glow along the pit rims | The spiked throne, the cairn with skulls and green smoke, green flames | Lich |

Three grounds are missing, so three foes stand on props.

- The imp has no brimstone and no lava. He stands on dirt and beige rock.
- The flaming slime and the fire spirit have no ash field and no lava. They stand on dark rocks, crystals, and fire props.
- The scarecrow's soil is the game's farm tile. The archive only supplies the crops on it.

The distance order also breaks two themes in half. The brown ruin, with living goblins, sits between the ghoul's graves and the wraith's dead wood. The bear, the unwinged vampire, and the zombie each sit between orc bands, so the rocky camp stops and starts again. The ground changes at those floors. It does not slide sideways to keep one theme together.

There is no useful tile cap after those three. A week from the edge is about a million tiles and stays cheap, because an empty region is a seed and a journey is one record. The 2,000 by 1,500 land remains the first size to build. Day-scale routes come after chunk-local coordinates and route fog.
