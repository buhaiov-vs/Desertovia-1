# Larger map

The map is one land, **12,000 by 12,000** tiles. The camp is in the middle. Each edge is one real hour from the camp, **6,000** tiles. A corner is about **1 hour 25 minutes**. There is no smaller land in front of this one. A citizen can build on seen ground anywhere on it, within 16 tiles of a burning fire.

Today's live land is still 120 by 90. Fog there is stored as the 32 by 32 chunks someone has seen. A missing chunk is fog. The live land, the version, and the server are unchanged. This document does not build the large land yet.

The screen now keeps a texture for each 32 by 32 block near the camera. Those blocks use the same coordinates as the fog chunks. Joining still builds the whole 120 by 90 once, so the picture stays the same. These checks stay on today's land: per-chunk generation, a delta village feed, nearby interest, and region pathfinding. They do not make a second map. Chunk-local coordinates and fog stored as visited chunks belong to this same land. A phone still draws one screen. The server still simulates only the places where someone is.

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

On this map the camp stays in the middle. Each edge is **6,000** tiles away. A corner is about **8,485** tiles.

| From | Tiles | On screen (26 px/s) | Server, healthy (0.6 s/tile) | Added to a work cycle (2 s/tile) |
|---|---:|---:|---:|---:|
| Any edge | 6,000 | 1 hour 2 minutes | 1 hour | 3 hours 20 minutes |
| A corner | 8,485 | 1 hour 27 minutes | 1 hour 25 minutes | 4 hours 43 minutes |

A wide screen shows about **34 tiles** across, so one edge is about **176 screens** from the camp.

A ten-minute walk is still one session. A half-hour work trip is not a normal job. Far activities are places you travel to, then work inside. The camp, a ruin, and a hunting ground each stay small. The distance between them is what grows.

## What is sent today

`villageUpdated` replaces the whole village state, for every viewer, at most once every 250 milliseconds.

The ground tiles are not in that message. The client builds all **10,800** tiles from the village id, plus a short wilds list (ravine, passes, thickets, cave, lookout). Wild trees outside the camp are drawn the same way.

The message does include every camp tree, the whole fog bitmap (1,350 bytes), every building, site, field, herd count, and every unit of every active battle.

Citizens are already incremental: the full list on join, then only the citizens who changed.

## Changes

### 1. Draw the land in chunks

This is in the client for the current land. A wide view keeps the blocks around the camera, plus one block of margin, and drops the rest. Each block is baked to one texture. Ponds, marsh, the ravine, and the rock under peaks stay one drawing, because there are few of them and they must keep today's pixels. The minimap still reads the whole land. A sprite is not created for every tile of this land. A block is drawn only when the view reaches it. The generator still walks the 120 by 90 once, which is what keeps the picture. That walk becomes per chunk before this land replaces today's land.

### 2. Generate each chunk from the seed

`buildLayout` and `Wilderness.Generate` walk the whole rectangle and place a fixed set of mountains and thickets. That cost grows with the area, and a feature's position depends on a scan of the entire land.

Each chunk must come from the village seed and the chunk coordinate alone. Rivers, ridges, and clearings stay continuous because neighboring chunks use the same function. Authored points (cave, lookout, a ruin) stay a short list, not a tile grid.

The client and the server use the same rectangle today (`MAP_WIDTH` / `MAP_HEIGHT` and `WorldMap.Land`). They keep one definition. The size becomes this 288,000 by 288,000 land only after generation is per chunk.

### 3. Split the village feed

The slow message keeps resources, research, housing, and weather.

Map changes go out as deltas: a tree felled, a node emptied, a building placed, one fog chunk revealed. A chunk is resent when it changes. A dense bitmap of this land is about **10.4 GB**. It is not sent. Visited chunks stay far smaller.

### 4. Full detail only nearby

A player receives entities near their own citizens and near the camera. A hunt in the far woods is one line until someone walks there.

A battle snapshot today includes every unit and goes to every viewer. On the large map, unit positions go to viewers near that fight. Everyone else receives the phase and the result.

### 5. Run activities by region

Work, raids, and dungeon runs are already separate activities. A node, camp, ruin, or caravan is an entity in one region. The server simulates that region when someone is there, or on a slow timer.

Pathfinding stays inside the region and uses crossings between regions. One grid does not cover this whole land.

The live budget is how many places are active at once. A few hundred entities near players fit the current process. Empty tiles do not.

### 6. Store fog by visited chunk

This is in the current map. The saved column is still one byte array, now headed `FCH1`. Each block is 32 by 32 tiles (128 bytes), or a complete flag and no bits when every tile in it has been seen. An old 1,350-byte bitmap is read once and written back as chunks. Reveal walks the route, about every 4 tiles, and only looks at the tiles within sight of that sample. The village message sends `exploredChunks`. It also still sends the old `explored` bitmap, because the published page decodes that string and throws if it is missing. The bitmap goes away when that page is replaced.

The client still paints fog on one canvas for the current land. That is the picture, not the store. On this map only visited chunks are stored. A dense bitmap is about **10.4 GB** per village, and the server must not load all of it to reveal a few tiles.

Sprite coordinates are ordinary numbers. One real day is about 2.3 million pixels from the camp. Ordinary positions cannot represent every pixel at that distance. Each chunk keeps its own origin.

## Order

1. Per-chunk generation, still at 120 by 90, so the picture matches today's map.
2. Chunked drawing at the same size. The ground of the current land is drawn this way.
3. Delta feed. Visited-chunk fog is already how the current map stores and sends what the village has seen.
4. Nearby interest for entities and battles.
5. Region activities and region pathfinding.
6. This map is the 288,000 by 288,000 land. The steps above are how that one land runs. There is no smaller land between today's 120 by 90 and this map.

Far work uses a journey to a place, then a local job. The 2-second work walk stays for distance inside that place.

## Days of travel

A walk uses the same pace: **0.6 seconds per tile**, healthy. The sprite at 26 pixels per second only plays while the figure is on screen. Watching the whole way is about 3 percent longer than the server clock, so one real day on screen is about **140,000 tiles** instead of **144,000**. The clock does not finish a fog march while the player is away. Quiet hands on that march spawn a faster pursuer, and a catch is a fall, not a finished trip.

One village day is **4 real hours**. A night is half of that day, **2 real hours**. One combined wave comes every night.

| Time to the camp, straight line | Tiles from the edge to the center |
|---|---:|
| 1 real hour | 6,000 |
| 2 real hours | 12,000 |
| 1 village day (4 hours) | 24,000 |
| 1 real day | 144,000 |
| 3 real days | 432,000 |
| 7 real days | 1,008,000 |

The map is that square. The camp stays in the middle. Each edge is one real day. A corner is about 34 hours.

A dense fog bitmap of that land is about **10.4 GB** per village. `Exploration.Seen` also copies the whole bitmap, and `Reveal` scans the rectangle around the entire trip. A diagonal day's walk makes that rectangle about 144,000 tiles on a side. This land uses three rules:

1. **A walk is the next step, on revealed ground.** The player orders a tile the village has already seen. The hero walks there. The order does not cross fog, and it does not reveal land by itself. There is no route the server finishes while the player is away from the fight. The client draws the hero only in the chunks around the camera.
2. **Coordinates are local to a chunk.** One real day is about 2.3 million pixels from the camp. A few days past that, ordinary sprite positions can no longer represent every pixel. Each chunk keeps its own origin.
3. **Fog is the chunks someone has seen.** A chunk is stored when a walker sees it or someone changes it. A walker sees **6 tiles** around them. Standing on the edge of known land reveals about 6 tiles past it, and the next order can go there. Scenery in the black is generated from the seed only when that band is revealed. Unseen land is not stored, and it is not a destination.

On today's map a click on fog does nothing. Building on unseen land stays refused. On the large map a click in the fog is allowed, and it is a march into the black, not a timer. The hero walks that way and reveals the 6 tiles they actually see. The march does not look ahead and does not plot a path around danger.

The march watches the hands. A mouse move or a key counts on a desk. On a phone the client must answer a presence ping. **15 seconds** with none of those, and a pursuer appears **12 tiles** ahead in the fog. The contact tile is the hero's tile at that moment. The check keeps running for the whole march. Breaking off one pursuer and going quiet again brings another. A tap at the edge and a device left on the table does not scout the late land.

The pursuer is faster than the hero, so a chase the hero can always win does not exist. The hero takes 0.6 seconds a tile. The pursuer takes **0.48** seconds a tile, one quarter faster. It leaves when the hero is more than **24 tiles** from the contact tile. A hero who turns within about 14 seconds and runs back covers those 24 tiles while the pursuer is still short of a strike, and the pursuer goes. A hero who keeps the order walks into it and is caught in a few seconds. The pursuer leaves none of a point's goods. This is the run. It is not the fight inside a claim.

A catch lays the hero on that tile. Every good they carried drops there and stays for whoever picks it up. Another citizen who reaches the body cures them, and they get up then, at the healthy pace. If nobody comes, the hero wakes alone after **30 minutes**, at **1%** of health, and walks at **2.4 seconds** a tile, four times the healthy pace. The crawl home is the cost of the tap. A 10-minute walk back takes 40 minutes. A fall at the day edge is about four real days of crawling. The walk home has no new pursuer while it stays on revealed ground and out of a living claim.

A walk that stays on revealed ground and out of a living claim has no attack. Marching into the fog is the run above. The ground between claims is not a road, and that distance is not simulated as its own trip.

There is no road. A road is postponed. Its job, when it is designed, is to make farming faster, including at a patch that a den protects. The cost, the speed, and whether that speed is only the patch or the whole walk are not decided. They are not part of this map. Every walk takes the full 0.6 seconds a tile.

The player controls one citizen. A click on an enemy is an attack. The dungeon uses this same attack. A click on the ground is a walk. The controlled citizen can enter one claim, and the player clicks the enemies there. Each other citizen stops at the edge of a claim. Those citizens can walk and carry across revealed ground. Those citizens do not enter a claim, and they do not attack. One player can fight at one point.

Pushing the frontier is those watched steps. If the walker stays at the edge, the time to reveal a line is the same as walking it: **one real day** of scouting reaches **144,000 tiles** out. A known path that bends around fog is longer than the straight line, and each bend still has to be walked on revealed tiles.

## How far out each foe starts

On the square of 12,000 tiles, the camp is in the middle and each edge is one real hour away (6,000 tiles). A corner is about 1 hour 25 minutes. The order comes from the sprite descriptions, easiest first, including the archive sheets in `docs/local/pixel-sprite-archives.md`. A figure called weak or small, with no weapon, comes before a larger one. A tooth, a claw, a club, a blade, a spell, or armor comes later. Two weapons come after one. The crowned swordsman is near the end. The lich, seated on a throne with green flames and with no walk of his own, is last. Color variants of the same body share one floor. The fight numbers in the catalogs are not this order.

Missing enemy pictures are ignored for now. The lich does not walk. His throne picture stays.

| Order | Foe | What the sprite says | Not closer than | Walk, healthy |
|---|---|---|---:|---:|
| 1 | Plain slime | Round blue, green, or red jelly, and the teal, dark-blue, and island green blobs. Eyes, no weapon | 300 | 3 minutes |
| 2 | Grave rat | "Fast, weak", a pack, body about 10 pixels | 570 | 5 minutes 42 seconds |
| 3 | Imp | "Small", about 9 pixels tall, stubby horns, no weapon | 841 | 8 minutes 25 seconds |
| 4 | Small goblin | Small green goblin, no weapon drawn | 1,111 | 11 minutes 7 seconds |
| 5 | Wolf | Low grey animal, 9 pixels tall, yellow eyes, no weapon | 1,382 | 13 minutes 49 seconds |
| 6 | Boar | Short tusks on a low heavy body | 1,652 | 16 minutes 31 seconds |
| 7 | Goblin with a club | Green goblin, wooden club | 1,923 | 19 minutes 14 seconds |
| 8 | Hooded orc | Green orc in a brown hood, a club | 2,193 | 21 minutes 56 seconds |
| 9 | Scarecrow | Straw hat, stick arms | 2,464 | 24 minutes 38 seconds |
| 10 | Ghoul | Hunched, about 11 pixels, long claws | 2,734 | 27 minutes 20 seconds |
| 11 | Goblin with a sword | Green goblin, a sword | 3,005 | 30 minutes 3 seconds |
| 12 | Banner goblin | Green goblin carrying a red-and-white banner | 3,275 | 32 minutes 45 seconds |
| 13 | Wraith | Floating, hooded, dark empty eyes, no weapon | 3,546 | 35 minutes 28 seconds |
| 14 | Poison plant | Dark purple bulb, leafy crown, poison puff and poison cloud | 3,816 | 38 minutes 10 seconds |
| 15 | Flaming slime | Dark cracked blob with yellow flames | 4,087 | 40 minutes 52 seconds |
| 16 | Orc with one axe | Smaller orc, leather and cloth, one axe | 4,357 | 43 minutes 34 seconds |
| 17 | Bear | Widest animal, pale claws, 11 pixels tall | 4,628 | 46 minutes 17 seconds |
| 18 | Orc warrior | Large orc, topknot, one axe | 4,898 | 48 minutes 59 seconds |
| 19 | Vampire | Pale, a cape, a spell flash, no wings | 5,169 | 51 minutes 41 seconds |
| 20 | Orc berserk | Large, tusks, bare chest, a cleaver in each hand | 5,439 | 54 minutes 23 seconds |
| 21 | Orc shaman | Large, grey tunic, wooden staff, two spell casts | 5,710 | 57 minutes 6 seconds |
| 22 | Winged vampire | Pale, white hair, red bat wings, red cape, a spell flash | 5,980 | 59 minutes 48 seconds |
| 23 | Orc with two swords | Blue-grey skin, a gold sword in each hand | 6,251 | 1 hour 2 minutes |
| 24 | Zombie | Standing green corpse, drawn twice as tall, 26 pixels, no weapon | 6,521 | 1 hour 5 minutes |
| 25 | Fire spirit | Yellow-orange flame with a face, a shot, a charge, and an explosion | 6,792 | 1 hour 7 minutes |
| 26 | Skeleton | The game's bone figure, 30 pixels tall, and the archive skeleton with a short blade | 7,062 | 1 hour 10 minutes |
| 27 | Armored orc | Tall black hair, red and gold armor, two swords | 7,333 | 1 hour 13 minutes |
| 28 | Fortress knight | Nine knights: sword and shield, crossbow, staff, spear, or halberd | 7,603 | 1 hour 16 minutes |
| 29 | Barrow king | "The boss", rusty gold crown, notched sword, a 16 by 24 frame | 7,874 | 1 hour 18 minutes |
| 30 | Lich | Seated on a spiked throne, blue robe, green eyes, green flames. No walk cycle | 8,144 | 1 hour 21 minutes |

The first floor is 3 minutes out, 300 tiles. Later floors stay about 270 tiles apart, which is about 2 minutes 42 seconds. Inside those 300 tiles there is no wild den. A den of a foe is never placed closer than that foe's floor. From one floor to the next, the den is that foe. The straight edge, at 1 hour, is the winged vampire's floor. Everyone after him stands past that edge, toward the corners. The lich's floor is short of the corner, and the throne band runs from there to the corner.

Each level is a ring of spawn points around the camp. The seed places **eight** points of that foe on the ring, one every eighth of the circle. A point's line is the straight line from that point to the camp. A point is kept when it lies inside the land and past that foe's floor. Levels out to the winged vampire have all eight points. Past the straight edge, the four points toward the middle of each edge fall outside the land. The four points toward the corners stay, through the lich.

A claim stays a small patch around the den: 48 tiles for a pack, 64 for one large figure. The creatures on a point are its protectors. The grave rat shows four. Plain slimes show three, one of each ordinary color. Wolves show four, boars three, goblins three. The ghoul and the imp show two. One large figure shows one: the wraith, the bear, each orc, each vampire, the zombie, the skeleton, a knight, the barrow king, and the lich. Sprites exist while a citizen is inside the claim. Marks on the ground stay in the chunk texture. Eight small spots stand around each spawn point.

These sheets are not dens. Deer, hare, fox, and black grouse are hunt animals, and the fox has no attack. Farm animals are stock. The orange crab has no attack. The island humans and the three roguelike heroes are people, not dens. Eye stalks, jaws, spikes, tentacles, and the other cursed growths dress the poison plant's land. Dead trees and mushrooms dress the lich's land.

One combined wave comes every night. The attackers of each point in that wave appear on the nearest fog on the straight line from that spawn point to the camp. They do not start the march at the spawn point. If that line is already revealed out to the point, they appear at the point. A night attacker leaves none of the point's goods. This attack replaces the old night pack of wolves, then boars, then bears.

The bodies from a point are that point's creature. Their health is the health of one protector of that point. Add a body while the health is still short. A body is not split. At least one body comes from each point in the wave.

A living spawn point sends. A destroyed spawn point does not send. The night is one combined wave of every living point that is allowed to send.

Level 1 points are allowed from the start. While no point has been spotted, the wave is the living level 1 points. A point is spotted when the 6-tile reveal uncovers it. One spotted point of level N makes every point of level N+1 allowed to send. An allowed level stays allowed. A point of an allowed level can send even when the village has not seen that point. A destroyed spawn point does not come back by itself.

Each living point adds its own bodies on its own line. Two living points of the same level are the same enemy type. When the whole map is discovered and one point of every level still lives, the wave has one enemy type for each level. When every point of a level is destroyed, that enemy type leaves the wave.

One cave stands on the map. The village seed picks its tile, the same tile every time, between a 6-minute walk and a 1-hour walk. It is not a spawn point, and it is not in the home vale. Wood and stone stay where they are: the forest and the stone hill in the vale, wood groves from 6 minutes out, and stone from 20 minutes out. The cave sends every night when at least one spawn point is destroyed. It sends only the enemies of those destroyed points. It does not send when every spawn point still lives. Those enemies appear on the nearest fog on the straight line from the cave to the camp. They do not start the march at the cave. They leave the creature goods of the destroyed points.

Each edge is one day out. A level 1 point sits 10 minutes out, and all eight directions have one. A den record is written when the reveal band uncovers its chunk, and only when that chunk is past the foe's floor. Unseen land has no den.

## Wild goods

Goods that are gathered sit away from the camp, so a trip is the reason to go out. The walk to a patch is at least as long as one bout of work there. Coming home takes the same walk again. Staying for another bout spends the work again, and the walk home waits until the citizen leaves.

Berries are the near patch. They stand 300 tiles out, which is 3 minutes. Picking lasts 3 minutes. The walk home is another 3 minutes, or the picker stays and picks again. The scarecrow's beds, hours farther out, are a later garden. They are not this patch.

The other wild goods are farther than the berries, and the richer ones are past the 10-minute line, so the path to them crosses a spawn point.

| Good | Not closer than | Walk | Where |
|---|---:|---:|---|
| Berries | 300 | 3 minutes | Inside the quiet ring. One bout is 3 minutes |
| Reeds | 600 | 6 minutes | Still inside the quiet ring, by the water |
| Wood | 600 | 6 minutes | On the first foe line |
| Stone | 2,000 | 20 minutes | In the slime marsh |
| Birds | 4,000 | 40 minutes | Deeper in that marsh |
| Rabbits, deer, ore | the band that holds them | the walk to that band | Rabbits and deer in the hunting wood, ore in the burrows and the later rock |

Fish stay at the pier. Gold stays where that project stands. Crafted goods are made in the camp. They are not patches on the land.

## What a living point gives

The creatures that live on a point leave one good when they are slain. That good is the reason to keep the point. The same creature in a night raid leaves none of it. While the point stands, the locals return, so the good can be taken again. Ordinary patches, the berries and the wood and the stone, do not depend on the point.

To destroy one spawn point, kill every protector of that point. Then eight citizens stand at the same time, one on each of the eight small spots around that spawn point, for 3 seconds. The point is destroyed when every protector is dead and all eight spots have been occupied together for those 3 seconds. If a citizen leaves a spot before 3 seconds, that point is not destroyed. A destroyed spawn point does not come back by itself. No counter is shown. The stay is not an attack. The point then leaves the nights, and its good stops. A level that was already opened stays open.

A destroyed point of level N can be renewed. The cost is a unique resource from the level N+1 area. It is not always a plant. It can be a resource such as stone, and that stone needs a high Mining skill. It is not the creature good, and it is not a weapon. The amount is not set. The names are not set. The skill level is not set. A renewed point sends again, its protectors return, and its good returns. A lich point cannot be renewed.

A point never leaves a weapon. A sword, a spear, a bow, an arrow, and a shield are still made only from their camp recipes, after the technology that allows them. The tusk, the sinew, the claw, and the plate pay for that research. They are not the weapon.

Each good is the pile a technology eats before its research can start. The cycles and the crew do not begin until the village has turned that pile in. The pile is consumed. One slain local leaves one of the good. Destroy the point before the pile is in, and that supply stops at the point. The cave can still leave that good. Another living point of the same creature can still fill it. A renewed point can fill it again. Once the pile is turned in, the technology no longer needs the point.

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
| Brewing | 10 sap | Poison plant, one | 31 hours |
| Smelting | 10 ember | Flaming slime, one | 34 hours |
| Mechanics | 10 tooth | The nearest orc, the hooded one | 18 hours |
| Warfare | 10 claws, and 10 plate scraps | Bear, then a knight | 38 hours, then 63 hours |
| Bookkeeping | 10 dust | The nearer vampire | 43 hours |
| Baking | 10 salt | Zombie, one | 54 hours |
| Forging | 10 heart | Fire spirit, one | 56 hours |
| Carpentry | 10 bone | Skeleton, one | 59 hours |
| Music | 10 crown flakes | Barrow king, one | 65 hours |
| Vigor drinks | 10 green flames, and the sap already spent on brewing | Lich, one | 68 hours |

Weaving and weather lore take no creature good. Reeds and the sky still finish those two. Every other technology waits on a pile.

One slime load is the 10-minute walk out, 30 minutes while the pack of 3 refills the hands, the kills, and the 10-minute walk home. Twenty-seven of those loads are the 270. A rat load waits 20 minutes because four rats refill faster. A single figure waits 90 minutes for the other nine. The far rows are almost all walking: the lich is 33 hours out and 33 hours back, plus that 90-minute wait.

Warfare does not start until both piles are in. Vigor drinks do not start until the flame is in, and brewing has already consumed its own sap. A child technology still waits on its parent in the catalog. The pile does not replace that. The pile can be gathered before the parent is finished. A carrier can take each slime load home, so the fighter does not walk back between loads. The 270 jelly is then about 14 hours at the point, after the first 10-minute walk. The lich stays one fight and one walk of 68 hours. A second fighter cannot work a second point at the same time, because the player has one attack.

## Den fights, as built

The protectors now fight on the server. This section is what the code does; the numbers above are still the design.

A present citizen with no task who walks within 6 tiles of a den wakes all of its living protectors. The citizen stops there. The protectors chase and strike. They give up when the citizen is more than half the claim from the den: 24 tiles for a pack, 32 for a single foe. Then they walk home at 1.5 times their speed and heal fully once home. A citizen walks 1.67 tiles a second; packs walk 1.5 and single foes 1.3, so walking away always works. The poison plant (14) and the lich (30) do not walk; the wraith, the plant, the orc shaman, the fire spirit and the lich strike from 3 to 5 tiles.

The citizen strikes with their usual fight power, `CombatRules.Power`, once a second, or every 1.4 seconds with a bow and arrows from 5 tiles. Health is the raid health, 32 + 4 × Endurance. Armor takes its share off every blow. Without a picked protector the citizen strikes the nearest one in reach. A tap on a protector picks it; "come here" walks the citizen inside the fight.

HP left when the fight ends is kept on the citizen. It fills back to full over **30 real minutes** out of combat (linear). The next den starts from that regenerating pool, not from a free full bar. The map bar and the HUD heart stay visible until HP is full. A knock-out stores 0 HP and the usual wound timer.

A slain protector gives experience to Guarding and Strength (Dexterity with a bow) and maybe loot, which stays in the citizen's hands. It comes back 30 minutes later. A citizen who falls is wounded where they lie for 20 + 2 × (level − 1) minutes, and can be carried to the hospital as after a raid. A citizen who walked into the den on the way somewhere goes on once the fight ends.

Each level's whole den grows 10% in health and 2.8% in damage over the one before. A pack shares both among its protectors. A single foe strikes every 2 seconds, a pack member every 1.4.

| Level | Den health | Den damage a second |
|---:|---:|---:|
| 1 | 54 | 1.05 |
| 5 | 79 | 1.17 |
| 10 | 127 | 1.35 |
| 20 | 330 | 1.77 |
| 30 | 856 | 2.34 |

A brand-new citizen with bare hands (32 health, 2 damage) beats the slimes of level 1 with about 13 health left, and the wolves of level 5 with almost nothing left. They fall at the boars of level 6. A citizen with every stat at 10, a sharp sword and a shield beats the lich with about a third of their health left; one with 60 health, 14 damage and leather armor falls there.

Slain protectors are kept in memory only. A server restart brings every protector back, and a fight under way at a restart is lost: the citizen stands where they stopped, unhurt, with nothing gained. Only a citizen whose player is present wakes a den, and a citizen at work does not. The Tsar does not wake dens.

## The shape of the land

The ring stays the rule for who lives there. A foe is never closer than its floor, and the ground changes at the next floor. The river, the coast, and the ridges do not move a floor. They are the reason one direction is not the same walk as another.

Three features are enough. Each is a function of the seed and the coordinate, so a chunk can draw its piece without looking at the chunk beside it.

The camp's river runs on north and south out of the vale. It is the same water, four tiles wide, with a slow bend. It blocks a step. The vale keeps today's bridge. This map adds no new bridge and no new ford. A new crossing would change the walk times. Fish stay at the pier. Gold stays where that project stands. A point that would sit in the water slides along its ring to the nearest dry tile, and that tile is still at least the foe's floor away.

The sea is a bay on the west. Its nearest sand is the ten-minute line. Sand, then pebbles, then foam, then water that blocks. The sea fills the west beyond that shore and crosses the later bands, so the west edge is water, not a day of grass. A western den stands on the inland edge of the sand, still no closer than its own floor. The crab is a prop on the sand. Nothing is hunted in the sea, and nothing sails. The east edge is one day away. The slime line is 10 minutes out, on the dry ground inland of the sand and on the grass in the other directions.

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

The night is one wave toward the camp. Each point in that wave appears on the nearest fog on the line from that point to the camp. The buildings remain at a dead fire, but nobody can rest there or add a building until the fire burns again.

## Areas

The open grounds in the archives are six. Island: sea, foam, sand, pebbles, grass, dirt, a dirt path, brown cliffs, a cave mouth. Undead: grey cracked earth, black cliffs, pits with a green rim, pebble patches. Cursed: pink flesh, red vein cliffs, pits. Fortress: grass, dirt, grey paving, a pit. Grey dungeon: slab, cobble, floors with a blue water rim. Roguelike rooms: grey stone floor, a darker cracked floor. There is no lava, no ash field, no snow field, and no desert of dunes. Snow bushes and a cactus exist, and they have no ground to stand on, so they stay unused. Sand is the western bay, not a dune sea, and no foe claims it. The crab stands on that sand as a prop.

Dungeon rooms, tables, chairs, and bookshelves are the inside of a door, not the open land. The game's own grass, river, reeds, marsh, mountains, ravine, and thicket stay the home vale. The cave stands at the seed's place, outside that picture. A ploughed field tile is the game's farm beds. The archive crops are plants, not soil.

| Area | Walk | Ground | Decorations | Foes |
|---|---|---|---|---|
| Home vale | under 10 minutes | Today's land, then grass out to the berry circle, the river north and south, and the western sand at the slime line | Camp, pier, fields, thinning trees. Eight berry patches at 3 minutes, reed beds at 6 minutes on the banks and the bay | None by day. The night is one wave. Before a point is spotted, the wave is the living level 1 points |
| Slime marsh | 10 minutes to 1 hour 18 minutes | Island grass, the game's marsh, shallow water and foam | Ferns, flowering bushes, reeds. Wood, stone, and birds are gathered here | Plain slime |
| Burrows | 1 hour 18 minutes to 3 hours 34 minutes | Dirt, beige rock outcrops | A few loose bones, small beige mushrooms | Grave rat, then imp. Ore starts here |
| Hunting wood | 3 hours 34 minutes to 6 hours 58 minutes | Island grass, dirt path | Ball trees, plain green bushes. Deer, hare, and grouse are animals here, not foes | Small goblin, wolf, boar. Rabbits and deer are hunted here |
| Goblin camp and field | 6 hours 58 minutes to 10 hours 22 minutes | Grass and dirt. The field uses the game's farm beds | Autumn bushes, tan tents, crates, a campfire. Sunflower, onion, and berry bushes on the beds | Club goblin, hooded orc, scarecrow |
| First graves | 10 hours 22 minutes to 11 hours 30 minutes | Grey cracked earth | Grave stones, thorn bushes, dead trees, small blue mushrooms | Ghoul |
| Brown ruin | 11 hours 30 minutes to 13 hours 46 minutes | Grass, with brown masonry on it | Broken walls, an arch, rubble, a round tower | Sword goblin, banner goblin |
| White dead wood | 13 hours 46 minutes to 14 hours 54 minutes | Grey cracked earth, black cliff edges | White bare trees, bone piles, pale mushrooms | Wraith |
| Flesh pits | 14 hours 54 minutes to 16 hours 2 minutes | Pink flesh, red vein cliffs, pits | Eye stalks, a mound of eyes, jaws, red spines, tentacles, a meat flower, pustules | Poison plant |
| Burned rock | 16 hours 2 minutes to 17 hours 10 minutes | No ground matches. The nearest sheets are dark rock props | A burned black trunk, orange-spotted rocks, red and yellow crystals, a brazier | Flaming slime |
| Rocky wood | 17 hours 10 minutes to 19 hours 26 minutes | Dirt and brown cliffs. Loose grey rocks are props, not a floor | Fern trees, oval-leaf trees, black and orange mushrooms, cave mouths, a deer statue, two red-roofed yurts. Ents and tree idols can stand among the trees | Orc with one axe, bear |
| Warrior camp | 19 hours 26 minutes to 20 hours 34 minutes | The same dirt and cliffs | More yurts, weapon racks, loose axes, dragon bones as a prop | Orc warrior |
| Blue ruin | 20 hours 34 minutes to 21 hours 42 minutes | Grey paving, cobble, blue-grey masonry | Mossy walls, columns, luminous trees, a chest, wall torches | Vampire |
| Berserk camp | 21 hours 42 minutes to 22 hours 50 minutes | Dirt and cliffs again | Bones, horned skull trophies, the racks | Orc berserk |
| Ritual ground | 22 hours 50 minutes to 23 hours 58 minutes | Dirt, beige rock | Dark totems, a bone magic circle, a bone-ring fire, the caveman statue | Orc shaman. This band reaches the straight edge |
| High ruin | 23 hours 58 minutes to 26 hours 14 minutes | Fortress grey walls and paving, the small keep | A barred gate, arrow slits, a wooden door, a pit in the grass, white and luminous trees | Winged vampire, orc with two swords |
| Deep graves | 26 hours 14 minutes to 27 hours 22 minutes | Black cliffs, pits with a green rim | Skull doorway, reaching dead arms, piles of skulls, rib cages | Zombie |
| Fire waste | 27 hours 22 minutes to 28 hours 30 minutes | No ground matches | Red, yellow, and orange crystals, braziers, the bone-ring fire, orange-spotted rock | Fire spirit |
| Bone field | 28 hours 30 minutes to 29 hours 38 minutes | Grey cracked earth | The large bone piles, a human skeleton prop, scattered skulls | Skeleton |
| Keep | 29 hours 38 minutes to 31 hours 54 minutes | Fortress walls and the grey dungeon floor, inside the doors | Portcullis, chests, weapon racks, shields, barrels, a guillotine as a prop | Armored orc, then the nine knights on one floor |
| Barrow | 31 hours 54 minutes to 33 hours 2 minutes | Grey cracked earth, grave rows | Mounds, a standing stone, the skull doorway | Barrow king |
| Throne | 33 hours 2 minutes to the corner | Dead-land cliffs, the green glow along the pit rims | The spiked throne, the cairn with skulls and green smoke, green flames | Lich |

Three grounds are missing, so three foes stand on props.

- The imp has no brimstone and no lava. He stands on dirt and beige rock.
- The flaming slime and the fire spirit have no ash field and no lava. They stand on dark rocks, crystals, and fire props.
- The scarecrow's soil is the game's farm tile. The archive only supplies the crops on it.

The distance order also breaks two themes in half. The brown ruin, with living goblins, sits between the ghoul's graves and the wraith's dead wood. The bear, the unwinged vampire, and the zombie each sit between orc bands, so the rocky camp stops and starts again. The ground changes at those floors. It does not slide sideways to keep one theme together.

There is no useful tile cap after those three. A week of travel is about a million tiles and stays cheap, because an empty region is a seed and a journey is one record. This map is the 288,000 by 288,000 land. Chunk-local coordinates and fog stored as visited chunks are part of it.
