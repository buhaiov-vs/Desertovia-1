# Desertod — Sprite list

Every sprite the game needs, one row each, so each one can be generated as its own unique image. Today most of them reuse
tiles from the Kenney sheets, and many tiles stand for several different things.

## How to generate a sprite

The full prompt for one sprite is the **base style prompt** below, then the row's **Prompt** column. Add the
**negative prompt** if the image generator supports one.

### Base style prompt

```text
Pixel art game sprite for a cozy top-down village-building game set in an old Russian (Slavic) river village of the
Middle Ages: log houses, thatched roofs, birch forests, a monastery with onion domes, a Tsar, gusli music, kvass.
Style: crisp hand-placed pixel art on a strict pixel grid, in the spirit of the Kenney "Tiny Town" and "Tiny Dungeon"
packs. Flat colors with at most 2-3 shades per material, no gradients, no anti-aliasing, no dithering noise.
Limited warm earthy palette (about 24 colors in total): grass greens, wood browns, straw yellows, stone greys, clay
reds, river blues; one accent color per object at most. Light comes from the top-left; the shaded side is one step
darker. Outlines are 1 pixel, a darker shade of the object's own color, never pure black.
Single subject, centered, filling the canvas with 1 pixel of free margin, fully transparent background, no ground
shadow unless the prompt asks for one. Must read clearly at its real size and when scaled down to 60 percent.
Same scale, perspective, palette and lighting as every other sprite in the set.
```

### Negative prompt

```text
text, letters, numbers, watermark, signature, logo, frame, border, background scenery, floor, multiple objects,
photorealistic, 3D render, painterly, blur, soft edges, anti-aliasing, gradients, glow, lens flare, noise, dithering,
isometric diamond, fisheye, cropped subject, modern objects, plastic, neon colors, pure black outlines
```

### Requirements for every sprite

| Requirement | Value |
| --- | --- |
| Tile size | 16 × 16 pixels. Every size below is a whole number of tiles. |
| Canvas | Exactly the size named at the start of the row's prompt. |
| Background | Transparent PNG (alpha 0 around the subject). |
| Pixel grid | One art pixel is one image pixel. Generators that work at high resolution: generate at 32× or 64× (for example 1024 × 1024 for a 16 × 16 sprite) with clearly blocky pixels, then downscale with nearest-neighbor. Never use smooth resampling. |
| View | Top-down three-quarter view (we see the top and the south-facing front), the same as the Kenney Tiny sheets. Not isometric. |
| Icons | Read at 16 × 16 and at 1.5×–3× scale in the interface. One clear silhouette, no tiny details. |
| Buildings | Each building has its own size: a hut is small, a town hall or a castle is large. Sizes are whole tiles. The **footprint** is the ground the building covers (tiles × tiles); the **canvas** is the image, as wide as the footprint and as tall as the footprint plus whatever rises above it (roof, chimney, tower, dome). The bottom rows of the canvas are the footprint; the part above may overlap the tiles behind the building. Front faces south (down), the door on the bottom edge. See the size table in section 2. |
| Terrain | 16 × 16 and seamless: every edge must continue into the same tile placed next to it. |
| Two-tile props | 16 × 32 (one tile wide, two tiles high), trunk or base in the bottom tile. |
| Characters | 16 × 16, facing the viewer (south), feet on the bottom row, about 12-14 pixels tall. |
| Character colors | Citizens are recolored in the game, so draw these regions with these exact colors: skin `#f7c282` (light) and `#e19a65` (shade); hair `#bd6c4a` (light) and `#763b36` (dark); clothes `#c0cbdc` (light), `#8b9bb4` (middle), `#52607c` (dark). No other pixel may use these colors. `web/src/game/appearance.ts` maps these colors to the player's choices; update its `BODIES` list when the new sprites replace the Kenney ones. |
| Animations | One horizontal strip; every frame has the canvas size, frames left to right, no gaps. |
| File name | `kind-name.png` in kebab case, for example `item-iron-ingot.png`, `building-hut.png`, `icon-task-chop-wood.png`. |

## How to read this file

**Type**

| Type | Meaning |
| --- | --- |
| item | A good: shown in the inventory, in hands, in storage yards and in material piles. |
| building | A structure on the map, drawn in the world. |
| icon | A small picture in the interface or in a bubble above a citizen's head. |
| character | A person or an animal that moves on the map. |
| prop | A small object in the world: plants, pegs, signs, piles, project pieces. |
| terrain | Ground tiles: grass, water, stone. |
| effect | Animation or overlay: fire, rain, bubbles, outlines. |

**Currently drawn by.** The file and the tile used today. Tile references are written as `sheet(index)`:

| Sheet | File |
| --- | --- |
| `town` | `web/public/assets/kenney/tiny-town.png` |
| `dungeon` | `web/public/assets/kenney/tiny-dungeon.png` |
| `battle` | `web/public/assets/kenney/tiny-battle.png` |
| `farm` | `web/public/assets/kenney/tiny-farm.png` |
| `factory` | `web/public/assets/kenney/tiny-factory.png` |
| `rogue` | `web/public/assets/kenney/roguelike-rpg.png` |
| `extras` | `web/public/assets/extras.png` (made for this game) |

"Code-drawn" means it is drawn with shapes in code, not from a sprite. "Not drawn" means it has no picture today (text only, or not shown at all).

---

## 1. Resources (18)

Goods from `ResourceKind` (`src/Desertod.Domain/Catalog/ResourceKind.cs`). The same sprite is also shown scaled down as
stacked goods in storage yards, on construction sites and in project material piles.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| item | Wood | Bundle of logs and sticks. Gathered in the forest; the main building material; sawn into planks. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Wood` — `town(106)` | 16×16 item icon: three short chopped logs stacked in a small pyramid, light end grain rings visible on the front, brown bark. |
| item | Stone | Pile of rough grey stones. Gathered on the hillside; foundations, walls, monument, bridge. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Stone` — `farm(89)` | 16×16 item icon: small pile of three rough grey field stones, one large in front and two smaller behind. |
| item | Food | Basket of berries, meat and vegetables. Eaten by citizens; produced by foraging, hunting, fishing, farming, baking. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Food` — `town(29)` | 16×16 item icon: small woven bast basket filled with red berries, a turnip and a piece of meat. |
| item | Coal | Black lumps of coal. Mined; burned in the smithy to smelt iron and forge chains. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Coal` — `extras(0)` | 16×16 item icon: three angular lumps of coal, very dark blue-grey with small light glints on the edges. |
| item | Iron ore | Rusty-brown rock with metal flecks. Mined; smelted into iron ingots. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.IronOre` — `dungeon(41)` | 16×16 item icon: one chunky rust-brown rock with grey metallic flecks and orange rust spots. |
| item | Iron ingot | Grey metal bar. Smelted at the smithy; used for chains, mechanisms and deliveries. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.IronIngot` — `dungeon(102)` | 16×16 item icon: two trapezoid iron bars stacked crosswise, cool grey with a bright top edge. |
| item | Planks | Stack of sawn boards. Made at the workshop; floors, stage, storehouse, library. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Planks` — `town(73)` | 16×16 item icon: neat stack of four sawn light-brown boards seen from the side, straight edges, visible wood grain lines. |
| item | Leather | Tanned animal hide. From hunting; hung as the hut's door; stored in the storehouse. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Leather` — `dungeon(66)` | 16×16 item icon: stretched tanned hide in the classic animal-skin shape, tan with darker edges. |
| item | Gold | Gold nuggets or coins. Dug from the vein over the bridge, mined, or a rare expedition find; pays for the bell tower. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Gold` — `town(93)` | 16×16 item icon: three shiny lumpy gold nuggets with one bright white highlight pixel each. |
| item | Chain | Coil of iron chain. Forged by a master smith from iron and coal. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Chain` — `factory(115)` | 16×16 item icon: short iron chain of five oval links lying in an S curve, grey with light highlights. |
| item | Mechanism | Wooden gears and an iron axle. Built by an artisan from planks and iron. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Mechanism` — `factory(114)` | 16×16 item icon: one large wooden cog wheel meshing with a small iron cog on an iron axle. |
| item | Remedy | Small clay bottle of herbal medicine. Brewed by an alchemist at the library. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Remedy` — `dungeon(115)` | 16×16 item icon: small round clay bottle with a cork, a green herb leaf tied to its neck with string. |
| item | Research points | Knowledge: a birch-bark scroll with carved signs. Not a physical good, never stored; spent on discoveries. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.ResearchPoints` — `dungeon(65)` | 16×16 item icon: curled white birch-bark scroll with dark scratched symbols and a small glowing yellow spark above it. |
| item | Reeds | Bundle of cut river reeds (cattails). Cover for lean-tos and racks, thatch for roofs. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Reeds` — `farm(65)` | 16×16 item icon: bundle of long green-yellow reeds with brown cattail heads, tied in the middle with twine. |
| item | Feathers | A few bird feathers. From hunting birds; meant for arrows later. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Feathers` — `extras(2)` | 16×16 item icon: three crossed bird feathers, brown and grey with white tips. |
| item | Flax | Bundle of flax stalks with blue flowers. Grown on the farm; stored on the drying rack and in the storehouse. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Flax` — `farm(80)` | 16×16 item icon: bundle of thin straw-colored flax stalks tied at the middle, small light-blue flowers at the top. |
| item | Grain | Sack or sheaf of rye grain. Grown on the farm; baked into bread, brewed into vigor drinks. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.Grain` — `farm(68)` | 16×16 item icon: small burlap sack tied at the top, spilling golden rye grains, one rye ear sticking out. |
| item | Vigor drink | Clay mug or bottle of fizzy kvass "like a thunderstorm". Brewed at the tavern; gives energy back. | `web/src/game/worldMap.ts` `RESOURCE_ICONS.VigorDrink` — `dungeon(116)` | 16×16 item icon: wooden mug of dark brown kvass with white foam, a small yellow lightning bolt beside it. |

## 2. Buildings in the world (22)

Finished buildings. Definitions in `src/Desertod.Domain/Buildings/BuildingCatalog.cs` and
`src/Desertod.Domain/Storage/StorageCatalog.cs`. While a building is under construction the same sprite is shown faint and
"rising" from the ground.

### Building sizes

Buildings are not limited to one size: small wild-age structures take 2 × 2 tiles, big halls and later castles take 5 × 5 or
more. The sizes below are a proposal; change them freely, as long as they stay whole tiles.

**The game does not support this yet.** Today every building uses one fixed plot of 3 × 3 tiles plus a yard row below it.
Per-building footprints need code changes before these sprites can be used at their size:

- Server: `WorldMap.PlotWidth`, `WorldMap.PlotHeight`, `WorldMap.Footprint` and `WorldMap.Yard` in
  `src/Desertod.Domain/World/WorldMap.cs` become per building kind (for example a `Footprint` on `BuildingDefinition`), and
  placement checks use the building's own footprint.
- Client: `PLOT_WIDTH`, `PLOT_HEIGHT`, `footprint` and `canBuildAt` in `web/src/game/worldMap.ts`, and the placement ghost,
  site view, storage yard and farm beds in `web/src/game/VillageWorld.ts`.
- Existing villages keep their buildings on 3 × 3 plots; a bigger footprint may overlap neighbours, so either keep old
  buildings at their plot or migrate them.

| Building | Footprint (tiles) | Canvas (pixels) | Why |
| --- | --- | --- | --- |
| Heap | 2 × 2 | 32 × 32 | A fenced patch of ground. |
| Lean-to | 2 × 2 | 32 × 32 | Two people sleep under it. |
| Wood pile | 2 × 2 | 32 × 32 | One stack of logs. |
| Stone pile | 2 × 2 | 32 × 32 | One ring of stones. |
| Drying rack | 2 × 2 | 32 × 48 | Small, with tall poles. |
| Hut | 3 × 3 | 48 × 48 | The basic house, the reference size. |
| Town Hall | 5 × 4 | 80 × 80 | The village's main hall with a tall roof. |
| Woodyard | 4 × 3 | 64 × 48 | A long open shed. |
| Stone yard | 4 × 4 | 64 × 64 | A walled yard. |
| Granary | 3 × 3 | 48 × 64 | Raised on stilts, a high roof. |
| Storehouse | 4 × 3 | 64 × 48 | A long warehouse. |
| Treasury | 3 × 3 | 48 × 48 | Small and squat. |
| Farm | 3 × 3 | 48 × 48 | Three beds of three tiles; the bed count is a game rule (`FarmCatalog.BedsPerFarm`). |
| Mine | 3 × 3 | 48 × 48 | A shaft in a slope. |
| Smithy | 3 × 3 | 48 × 64 | Chimney rising above the roof. |
| Workshop | 4 × 3 | 64 × 48 | A long workshop. |
| Library | 3 × 3 | 48 × 64 | Tall and narrow. |
| Tavern | 4 × 3 | 64 × 64 | A big log house with a high roof. |
| Bakery | 3 × 3 | 48 × 48 | One oven under a roof. |
| Stage | 3 × 2 | 48 × 48 | A low platform with tall posts. |
| Market | 4 × 3 | 64 × 48 | A row of stalls. |
| Barracks | 5 × 4 | 80 × 80 | Fortified, with battlements. |
| Castle (future) | 7 × 6 or more | 112 × 128 or more | Example of a later-age building; not in the game yet. |

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| building | Heap | A fenced patch of ground with a messy pile of any goods; four short posts at the corners. First storage of the wild age. | `web/src/game/VillageWorld.ts` `wildView('Heap')` — code-drawn patch + `town(47)` posts | 32×32 building (footprint 2×2 tiles): patch of bare trampled earth with four short wooden posts at the corners joined by a low rope, an untidy pile of logs, stones and a basket in the middle. |
| building | Lean-to | Poles and reeds against the wind; two people sleep under it. | `web/src/game/VillageWorld.ts` `wildView('LeanTo')` — code-drawn | 32×32 building (footprint 2×2 tiles): primitive lean-to shelter, long wooden poles slanted from the ground on the left up to a crossbar on two forked posts on the right, covered with bundles of yellow reeds, open front with two straw bedrolls inside. |
| building | Wood pile | Stacked wood stays dry and does not crowd the heap. | `web/src/game/VillageWorld.ts` `wildView('WoodPile')` — stacked `town(106)` | 32×32 building (footprint 2×2 tiles): neat stack of split firewood logs between two pairs of upright stakes, end grain facing the viewer, a chopping block with an axe in front. |
| building | Stone pile | A ring of stones that holds more stones. | `web/src/game/VillageWorld.ts` `wildView('StonePile')` — scattered `farm(89)` | 32×32 building (footprint 2×2 tiles): low ring of large grey boulders enclosing a heap of smaller grey stones. |
| building | Drying rack | Meat and berries dry here instead of rotting in the heap. | `web/src/game/VillageWorld.ts` `wildView('DryingRack')` — code-drawn | 32×48 building (footprint 2×2 tiles, poles rise 1 tile above): wooden drying rack of two posts and a crossbar under a small reed roof, strips of red meat, a tan hide and bunches of berries hanging from it. |
| building | Hut | The first real house: stone base, timber frame, clay walls, thatched roof. Four people sleep well here. | `web/src/game/worldMap.ts` `BUILDING_TILES.Hut` — `rogue` tiles | 48×48 building (footprint 3×3 tiles): small Slavic peasant hut, low grey stone base, whitewashed clay walls between dark timber beams, thick golden thatched roof, hide hanging in the doorway, one small window. |
| building | Town Hall | Main village hall that stores any goods; cream gable with a round window, banners by an arched door. | `web/src/game/worldMap.ts` `BUILDING_TILES.TownHall` — `rogue` tiles | 80×80 building (footprint 5×4 tiles, roof rises 1 tile above): large two-storey log hall (terem) with a steep wooden shingle roof, carved wooden gable trim with a round window, wide arched double door, two red banners beside it. |
| building | Woodyard | A roofed yard for wood, planks and reeds. | `web/src/game/worldMap.ts` `BUILDING_TILES.Woodyard` — `rogue` tiles | 64×48 building (footprint 4×3 tiles): long open-sided timber shed on four posts under a long brown shingle roof, stacks of logs, planks and reed bundles under it. |
| building | Stone yard | Walled yard for stone, ore and coal. | `web/src/game/worldMap.ts` `BUILDING_TILES.StoneYard` — `rogue` tiles | 64×64 building (footprint 4×4 tiles): walled yard of dry-stacked grey stone with a wide dark gateway in the front, piles of stone, rust ore and black coal visible inside, small thatched lean roof at the back. |
| building | Granary | Raised floor and thick walls keep food from mice and rot. | `web/src/game/worldMap.ts` `BUILDING_TILES.Granary` — `rogue` tiles | 48×64 building (footprint 3×3 tiles, roof rises 1 tile above): log granary standing on short stone stilts, small door reached by a plank ramp, thatched roof, grain sacks leaning by the ramp. |
| building | Storehouse | Shelves for iron, leather and finer goods. | `web/src/game/worldMap.ts` `BUILDING_TILES.Storehouse` — `rogue` tiles | 64×48 building (footprint 4×3 tiles): long sturdy plank storehouse with a shingle roof and a wide double door with iron hinges, crates and a barrel outside. |
| building | Treasury | A strong room for gold. Needs a later age. | `web/src/game/worldMap.ts` `BUILDING_TILES.Treasury` — `rogue` tiles | 48×48 building (footprint 3×3 tiles): small squat stone building with thick walls, a grey slate roof, a heavy iron-barred door with a big lock, a glint of gold through a tiny barred window. |
| building | Farm | Fields to tend. | `web/src/game/worldMap.ts` `BUILDING_TILES.Farm` — `farm(60)`, `farm(61)`, `farm(63)` | 48×48 building (footprint 3×3 tiles): three horizontal rows of dry ploughed brown soil beds separated by narrow grass paths, a low wattle fence along the edges, no plants (crops are drawn on top). |
| building | Mine | A shaft for coal, ore and gold. | `web/src/game/worldMap.ts` `BUILDING_TILES.Mine` — `rogue` tiles | 48×48 building (footprint 3×3 tiles): dark mine entrance cut into a grey rocky hillside, framed by thick timber supports, wooden rails coming out with a small ore cart, a pile of ore beside it. |
| building | Smithy | Forge for iron and chains. | `web/src/game/worldMap.ts` `BUILDING_TILES.Smithy` — `rogue` tiles | 48×64 building (footprint 3×3 tiles, chimney rises 1 tile above): stone-and-timber smithy with an open front, a glowing orange forge and stone chimney inside, an anvil on a stump in front, grey shingle roof. |
| building | Workshop | Saws planks and builds mechanisms. | `web/src/game/worldMap.ts` `BUILDING_TILES.Workshop` — `rogue` tiles | 64×48 building (footprint 4×3 tiles): long plank workshop with a long cream-brown roof, a sawhorse with a log and a saw in front, a crate of tools, a small window. |
| building | Library | Chronicles, research and remedies. | `web/src/game/worldMap.ts` `BUILDING_TILES.Library` — `rogue` tiles | 48×64 building (footprint 3×3 tiles, roof rises 1 tile above): tall narrow stone building with a steep roof, tall arched windows, a round rose window in the gable, a small wooden cross-shaped finial. |
| building | Tavern | Talk and rest. | `web/src/game/worldMap.ts` `BUILDING_TILES.Tavern` — `rogue` tiles | 64×64 building (footprint 4×3 tiles, roof rises 1 tile above): big cozy log tavern with a long orange-brown roof, warm lit window, a barrel of kvass and a bench by the door, a hanging mug sign. |
| building | Bakery | A clay oven under a roof. Grain goes in, bread comes out. | `web/src/game/worldMap.ts` `BUILDING_TILES.Bakery` — `rogue` tiles | 48×48 building (footprint 3×3 tiles): open-fronted timber shelter on a stone base over a large domed clay oven with a glowing mouth, a flour sack and a tray of round loaves beside it. |
| building | Stage | Music lifts the spirit. | `web/src/game/VillageWorld.ts` `wildView('Stage')` — `town(73)`, `town(47)`, `battle(70)` | 48×48 building (footprint 3×2 tiles, posts rise 1 tile above): small raised plank stage with two carved wooden posts at the back corners carrying red and gold pennants, a three-step stair in front. |
| building | Market | Trade with other villages. Needs a later age. | `web/src/game/worldMap.ts` `BUILDING_TILES.Market` — `rogue` tiles | 64×48 building (footprint 4×3 tiles): row of two wooden market stalls with a red-and-white striped awning, goods on the counter (bread, pots, cloth), crates and sacks in front. |
| building | Barracks | Guards and patrols. Needs a later age. | `web/src/game/worldMap.ts` `BUILDING_TILES.Barracks` — `rogue` tiles | 80×80 building (footprint 5×4 tiles, towers rise 1 tile above): fortified log barracks with a battlemented palisade top, two war banners, a heavy iron-bound gate, a rack of spears by the door. |

## 3. Building icons (22)

Small sign of each building for the build panel, technology panel and votes.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| icon | Heap | Small fenced pile of mixed goods. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Heap` — `town(92)` | 16×16 icon: tiny untidy pile of a log, a stone and a sack between two short posts. |
| icon | Lean-to | Slanted reed shelter. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.LeanTo` — `farm(2)` | 16×16 icon: small triangular reed-covered lean-to seen from the front. |
| icon | Wood pile | Stacked logs. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.WoodPile` — `town(106)` | 16×16 icon: stack of log ends between two upright stakes. |
| icon | Stone pile | Ring of stones. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.StonePile` — `farm(89)` | 16×16 icon: ring of grey boulders around a small heap of stones. |
| icon | Drying rack | Rack with hanging meat. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.DryingRack` — `town(29)` | 16×16 icon: wooden frame with two strips of red meat hanging from the crossbar. |
| icon | Hut | Small thatched house. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Hut` — `town(83)` | 16×16 icon: tiny whitewashed hut with a thick golden thatched roof and a dark door. |
| icon | Town Hall | Hall with banners. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.TownHall` — `battle(52)` | 16×16 icon: log hall with a steep roof and a small red banner on top. |
| icon | Woodyard | Shed with logs under a roof. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Woodyard` — `town(127)` | 16×16 icon: open shed roof on two posts over a stack of logs. |
| icon | Stone yard | Walled yard with stones. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.StoneYard` — `town(115)` | 16×16 icon: low grey stone wall with a gateway and stones behind it. |
| icon | Granary | Raised barn with grain sacks. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Granary` — `farm(71)` | 16×16 icon: small log barn on stilts with a grain sack in front. |
| icon | Storehouse | Shelves with crates. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Storehouse` — `dungeon(89)` | 16×16 icon: two stacked wooden crates and a barrel. |
| icon | Treasury | Iron-bound chest of gold. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Treasury` — `town(93)` | 16×16 icon: open iron-bound wooden chest overflowing with gold. |
| icon | Farm | Field with crop rows. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Farm` — `farm(68)` | 16×16 icon: three green crop rows on brown soil behind a tiny fence. |
| icon | Mine | Shaft entrance with a cart. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Mine` — `factory(98)` | 16×16 icon: dark arched mine entrance with timber frame and a tiny ore cart. |
| icon | Smithy | Anvil with sparks. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Smithy` — `dungeon(74)` | 16×16 icon: black anvil with a hammer and three orange sparks. |
| icon | Workshop | Saw and planks. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Workshop` — `factory(123)` | 16×16 icon: hand saw lying across two planks. |
| icon | Library | Book shelf or scroll. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Library` — `dungeon(63)` | 16×16 icon: thick leather-bound chronicle book standing upright with a brass clasp. |
| icon | Tavern | Mug of kvass and a barrel. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Tavern` — `dungeon(82)` | 16×16 icon: foaming wooden mug in front of a small barrel. |
| icon | Bakery | Loaf of bread in an oven. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Bakery` — `farm(125)` | 16×16 icon: domed clay oven with a round golden loaf at its mouth. |
| icon | Stage | Gusli on a stage. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Stage` — `battle(70)` | 16×16 icon: small wing-shaped gusli (Russian zither) on a plank platform with a pennant. |
| icon | Market | Striped stall awning. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Market` — `farm(74)` | 16×16 icon: tiny market stall with a red-and-white striped awning. |
| icon | Barracks | Shield and spear. | `web/src/game/worldMap.ts` `BUILDING_SIGNS.Barracks` — `dungeon(104)` | 16×16 icon: round red wooden shield with a spear crossed behind it. |

## 4. Construction stages (optional, 62)

Each building is built in stages (`BuildingCatalog.cs`). Today a site shows the finished building rising behind a mask.
One sprite per stage would show real progress. One row per building; the stages are listed in building order. Generate each
stage separately with the same prompt and the stage's own line; each stage shows everything finished so far.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| building | Heap stages (1) | Ground: cleared patch with sticks. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Heap: cleared earth patch with a few sticks laid at the corners. |
| building | Lean-to stages (2) | Frame: leaning poles. Cover: reeds laid on the poles. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Lean-to. Stage 1 Frame: bare slanted poles on two forked posts. Stage 2 Cover: the same with reed bundles half laid on. |
| building | Wood pile stages (1) | Pile: first logs stacked. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Wood pile: stakes driven in and a first low row of logs. |
| building | Stone pile stages (1) | Ring: stones laid in a ring. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Stone pile: half-closed ring of boulders. |
| building | Drying rack stages (2) | Frame: tied poles. Cover: reed roof. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Drying rack. Stage 1 Frame: two posts and a tied crossbar. Stage 2 Cover: the frame with a reed roof, nothing hanging yet. |
| building | Hut stages (6) | Ground, Foundation (stone base), Frame (posts and beams), Walls (wattle and daub), Roof (thatch), Finish (hide door). | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Hut. Stage 1 Ground: cleared earth with stakes and string. Stage 2 Foundation: low rectangle of grey stones. Stage 3 Frame: timber posts and beams on the stones. Stage 4 Walls: woven wattle walls half daubed with clay. Stage 5 Roof: rafters half covered with thatch. Stage 6 Finish: complete hut without the hide door. |
| building | Woodyard stages (4) | Ground, Foundation, Frame, Roof. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Woodyard. Stage 1 Ground: cleared earth with stakes. Stage 2 Foundation: stone pads for four posts. Stage 3 Frame: four posts and beams. Stage 4 Roof: half-shingled roof. |
| building | Stone yard stages (4) | Ground, Walls, Frame, Roof. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Stone yard. Stage 1 Ground: cleared earth with stakes. Stage 2 Walls: knee-high stone walls. Stage 3 Frame: posts for the back roof. Stage 4 Roof: half-thatched back roof. |
| building | Granary stages (5) | Ground, Foundation, Frame, Walls, Roof. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Granary. Stage 1 Ground: stakes and string. Stage 2 Foundation: stone stilts. Stage 3 Frame: floor beams and posts on the stilts. Stage 4 Walls: log walls half up. Stage 5 Roof: rafters half thatched. |
| building | Storehouse stages (5) | Ground, Foundation, Frame, Floor (planks), Roof. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Storehouse. Stage 1 Ground: stakes and string. Stage 2 Foundation: stone base. Stage 3 Frame: posts and beams. Stage 4 Floor: plank floor laid inside the frame. Stage 5 Roof: half-shingled roof. |
| building | Farm stages (3) | Ground, Field (ploughed furrows), Fence. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Farm. Stage 1 Ground: grass cleared to bare earth with corner stakes. Stage 2 Field: fresh ploughed furrows. Stage 3 Fence: wattle fence half woven around the furrows. |
| building | Bakery stages (4) | Ground, Foundation, Oven (clay oven), Roof. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Bakery. Stage 1 Ground: stakes and string. Stage 2 Foundation: stone base. Stage 3 Oven: half-built clay dome on the base. Stage 4 Roof: posts and a half-thatched roof over the oven. |
| building | Mine stages (3) | Ground, Shaft (hole in rock), Frame (timber supports). | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Mine. Stage 1 Ground: marked rocky slope. Stage 2 Shaft: rough dark hole dug in the rock with rubble. Stage 3 Frame: first timber supports in the entrance. |
| building | Smithy stages (4) | Ground, Foundation, Forge (stone forge), Roof. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Smithy. Stage 1 Ground: stakes and string. Stage 2 Foundation: stone base. Stage 3 Forge: cold stone forge and chimney stub. Stage 4 Roof: posts and a half-covered roof. |
| building | Workshop stages (4) | Ground, Foundation, Frame, Roof. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Workshop. Stage 1 Ground: stakes and string. Stage 2 Foundation: stone base. Stage 3 Frame: posts and beams. Stage 4 Roof: half-covered roof. |
| building | Library stages (5) | Ground, Foundation, Frame, Floor, Roof. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Library. Stage 1 Ground: stakes and string. Stage 2 Foundation: stone base. Stage 3 Frame: tall posts and beams. Stage 4 Floor: plank floor inside. Stage 5 Roof: steep rafters half covered. |
| building | Tavern stages (5) | Ground, Foundation, Frame, Walls, Roof. | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Tavern. Stage 1 Ground: stakes and string. Stage 2 Foundation: stone base. Stage 3 Frame: posts and beams. Stage 4 Walls: log walls half up. Stage 5 Roof: half-thatched roof. |
| building | Stage stages (3) | Ground, Floor (planks), Frame (posts). | `web/src/game/VillageWorld.ts` `siteView` — masked finished sprite | Building under construction, same canvas size and footprint as the finished Stage. Stage 1 Ground: stakes and string. Stage 2 Floor: raised plank platform. Stage 3 Frame: back posts without pennants. |

## 5. Construction props (5)

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| prop | Survey peg | Short wooden stake marking a site's corners and lines still to build. | `web/src/game/VillageWorld.ts` `siteView` and projects — `town(47)` | 16×16 prop: one short pointed wooden stake driven into the ground with a strip of red cloth tied on top, small ground shadow. |
| prop | Construction signboard | Small wooden sign on a post: "work here". Placed next to every active site and project. | `web/src/game/VillageWorld.ts` projects — `town(83)` | 16×16 prop: small blank wooden board nailed to a post with a hammer hanging from a nail, small ground shadow. |
| terrain | Blueprint ground | Cleared, trampled earth of a building plot under construction. | `web/src/game/VillageWorld.ts` `siteView` — code-drawn brown rectangle | 16×16 seamless terrain tile: bare trampled light-brown earth with a few wood chips and footprints. |
| prop | Storage pallet | Wooden pallet in a storage yard; goods are stacked on it, one pallet per slot. | `web/src/game/VillageWorld.ts` `goodsYard` — code-drawn brown bar | 16×16 prop: low flat wooden pallet of three planks seen from the three-quarter view, empty, occupying the bottom half of the canvas. |
| effect | Placement outline | Outline that follows the pointer while the Tsar or a Governor places a blueprint; yellow with a plus when free, red when blocked. | `web/src/game/VillageWorld.ts` `showGhost` — code-drawn | Keep code-drawn. If drawn as a sprite: 48×64 effect: dashed parchment-yellow rectangle outline with rounded corners and a small plus sign in the middle, semi-transparent fill. |

## 6. Farming (26)

Crops from `src/Desertod.Domain/Farming/FarmCatalog.cs`. Each farm has three beds of three tiles. Plants are drawn on top
of the bed tiles, so plant sprites must leave the soil visible around them.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| terrain | Dry bed, left | Left end of a dry ploughed bed (light soil). | `web/src/game/worldMap.ts` `BUILDING_TILES.Farm` — `farm(60)` | 16×16 terrain tile: left end of a raised bed of dry light-brown ploughed soil with horizontal furrows, rounded left edge, grass path above and below. Tiles seamlessly with the middle piece on its right. |
| terrain | Dry bed, middle | Middle of a dry ploughed bed. | `web/src/game/worldMap.ts` `BUILDING_TILES.Farm` — `farm(61)` | 16×16 terrain tile: middle of a raised bed of dry light-brown ploughed soil with horizontal furrows, grass path above and below, seamless to the left and right. |
| terrain | Dry bed, right | Right end of a dry ploughed bed. | `web/src/game/worldMap.ts` `BUILDING_TILES.Farm` — `farm(63)` | 16×16 terrain tile: right end of a raised bed of dry light-brown ploughed soil with horizontal furrows, rounded right edge, grass path above and below. |
| terrain | Wet bed, left | Left end of a watered bed (dark soil). Shown while the bed is wet. | `web/src/game/worldMap.ts` `WET_BED` — `farm(48)` | 16×16 terrain tile: the same shape as the dry bed left end but with dark wet brown soil and two tiny water glints. |
| terrain | Wet bed, middle | Middle of a watered bed. | `web/src/game/worldMap.ts` `WET_BED` — `farm(49)` | 16×16 terrain tile: the same shape as the dry bed middle but with dark wet brown soil and a tiny water glint. |
| terrain | Wet bed, right | Right end of a watered bed. | `web/src/game/worldMap.ts` `WET_BED` — `farm(51)` | 16×16 terrain tile: the same shape as the dry bed right end but with dark wet brown soil and two tiny water glints. |
| prop | Turnip, sprout | Tiny turnip leaves. Growth below 40%. | `web/src/game/worldMap.ts` `CROP_STAGES.Turnip` — `farm(16)` | 16×16 prop: two tiny round green turnip seedling leaves in the center, transparent around them. |
| prop | Turnip, young | Leafy turnip tops. | `web/src/game/worldMap.ts` `CROP_STAGES.Turnip` — `farm(17)` | 16×16 prop: small clump of broad green turnip leaves, transparent around them. |
| prop | Turnip, ripe | Big leaves with a purple-white turnip showing. | `web/src/game/worldMap.ts` `CROP_STAGES.Turnip` — `farm(18)` | 16×16 prop: big clump of turnip leaves with the round white-and-purple top of a turnip showing at the base. |
| prop | Carrot, sprout | Tiny feathery carrot leaves. | `web/src/game/worldMap.ts` `CROP_STAGES.Carrot` — `farm(4)` | 16×16 prop: two tiny thin feathery carrot seedlings, transparent around them. |
| prop | Carrot, young | Feathery carrot tops. | `web/src/game/worldMap.ts` `CROP_STAGES.Carrot` — `farm(5)` | 16×16 prop: small bushy tuft of feathery bright-green carrot leaves. |
| prop | Carrot, ripe | Full tops with orange carrot shoulders showing. | `web/src/game/worldMap.ts` `CROP_STAGES.Carrot` — `farm(6)` | 16×16 prop: tall feathery carrot tops with an orange carrot shoulder showing at the base. |
| prop | Cabbage, sprout | Small round cabbage leaves. | `web/src/game/worldMap.ts` `CROP_STAGES.Cabbage` — `farm(52)` | 16×16 prop: three tiny round pale-green cabbage leaves. |
| prop | Cabbage, young | Loose cabbage head. | `web/src/game/worldMap.ts` `CROP_STAGES.Cabbage` — `farm(53)` | 16×16 prop: loose open rosette of pale-green cabbage leaves. |
| prop | Cabbage, ripe | Big tight green cabbage head. | `web/src/game/worldMap.ts` `CROP_STAGES.Cabbage` — `farm(54)` | 16×16 prop: big round tight cabbage head with outer leaves spread around it. |
| prop | Flax, sprout | Thin green flax shoots. | `web/src/game/worldMap.ts` `CROP_STAGES.Flax` — `farm(64)` (same as rye) | 16×16 prop: a few very thin blue-green flax shoots with narrow leaves. |
| prop | Flax, young | Tall thin flax stalks. | `web/src/game/worldMap.ts` `CROP_STAGES.Flax` — `farm(65)` (same as rye and reeds) | 16×16 prop: tall slender blue-green flax stalks with tiny narrow leaves, no flowers. |
| prop | Flax, ripe | Flax stalks with small blue flowers. | `web/src/game/worldMap.ts` `CROP_STAGES.Flax` — `farm(80)` | 16×16 prop: tall slender flax stalks topped with small sky-blue five-petal flowers. |
| prop | Rye, sprout | Short grass-like rye shoots. | `web/src/game/worldMap.ts` `CROP_STAGES.Rye` — `farm(64)` (same as flax) | 16×16 prop: short bright-green grass-like rye shoots in a row. |
| prop | Rye, young | Green rye stalks. | `web/src/game/worldMap.ts` `CROP_STAGES.Rye` — `farm(65)` (same as flax) | 16×16 prop: dense green rye stalks with unripe green ears. |
| prop | Rye, ripe | Golden rye with heavy ears. | `web/src/game/worldMap.ts` `CROP_STAGES.Rye` — `farm(66)` | 16×16 prop: dense golden rye stalks with heavy bent ears and long whiskers. |
| icon | Turnip, harvested | A pulled turnip. | `web/src/game/worldMap.ts` `CROP_ICONS.Turnip` — `farm(20)` (defined, not used yet) | 16×16 icon: one pulled turnip, white bottom and purple top, with green leaves. |
| icon | Carrot, harvested | A pulled carrot. | `web/src/game/worldMap.ts` `CROP_ICONS.Carrot` — `farm(8)` (defined, not used yet) | 16×16 icon: one orange carrot lying diagonally with green feathery top. |
| icon | Cabbage, harvested | A cabbage head. | `web/src/game/worldMap.ts` `CROP_ICONS.Cabbage` — `farm(56)` (defined, not used yet) | 16×16 icon: one round pale-green cabbage head. |
| icon | Flax, harvested | A bundle of flax. | `web/src/game/worldMap.ts` `CROP_ICONS.Flax` — `farm(80)` (defined, not used yet) | 16×16 icon: small sheaf of cut flax stalks with a few blue flowers. |
| icon | Rye, harvested | A sheaf of rye. | `web/src/game/worldMap.ts` `CROP_ICONS.Rye` — `farm(68)` (defined, not used yet) | 16×16 icon: golden rye sheaf tied with a straw band. |

## 7. Village projects (6 icons + 18 pieces)

Great works started by story quests (`src/Desertod.Domain/Projects/ProjectCatalog.cs`).

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| icon | Tsar's monument | Stone statue of the Tsar on a pedestal. | Not drawn (`web/src/ui/ProjectPanel.tsx` shows text) | 16×16 icon: small grey stone statue of a crowned Tsar with a sceptre on a square pedestal. |
| icon | Fishing pier | Planks on poles over water with a fishing rod. | Not drawn (`web/src/ui/ProjectPanel.tsx` shows text) | 16×16 icon: short plank pier on poles over blue water with a fishing rod leaning on it. |
| icon | Palisade | Row of sharpened logs. | Not drawn (`web/src/ui/ProjectPanel.tsx` shows text) | 16×16 icon: four sharpened upright logs side by side bound with rope. |
| icon | Tsar Bridge | Wooden bridge with railings over a river. | Not drawn (`web/src/ui/ProjectPanel.tsx` shows text) | 16×16 icon: arched wooden bridge with railings over a blue river. |
| icon | Great Granary | Large granary with a wide roof. | Not drawn (`web/src/ui/ProjectPanel.tsx` shows text) | 16×16 icon: wide log granary with a big thatched roof and two grain sacks. |
| icon | Bell Tower | Tower with a golden onion dome and a bell. | Not drawn (`web/src/ui/ProjectPanel.tsx` shows text) | 16×16 icon: white bell tower with a golden onion dome and a bronze bell in the open arch. |
| prop | Monument pedestal | Square stone pedestal; the first half of the monument build. | `web/src/game/worldMap.ts` `MONUMENT.pedestal` — `dungeon(54)` | 16×16 prop: square grey stone pedestal with a stepped base and a flat top, small ground shadow. |
| prop | Monument rough block | Roughly hewn stone block on the pedestal; the monument half built. | `web/src/game/worldMap.ts` `MONUMENT.rough` — `dungeon(65)` | 16×16 prop: rough uncut grey stone block, chisel marks, roughly the shape of a standing figure, standing on nothing (drawn above the pedestal). |
| prop | Tsar statue | Grey stone statue of the Tsar with a crown and a sceptre; the finished monument. | `web/src/game/worldMap.ts` `MONUMENT.statue` — `dungeon(86)` tinted grey | 16×16 prop: grey stone statue of a bearded Tsar in a long robe, wearing a crown, holding a sceptre, standing on nothing (drawn above the pedestal). |
| prop | Pier pole | Wooden pole standing in the river; pier before the planks. | `web/src/game/VillageWorld.ts` pier — `town(47)` | 16×16 prop: two thick wet wooden poles sticking out of water, small ripple rings at their base, transparent elsewhere. |
| prop | Pier deck | Plank deck tile over water. | `web/src/game/VillageWorld.ts` pier — `town(73)` | 16×16 prop: section of plank deck running left to right, three boards with nail dots, pole tops under the front edge, seamless left and right. |
| prop | Palisade stake, vertical | Sharpened logs along the west wall. | `web/src/game/worldMap.ts` `PALISADE` — `town(56)` | 16×16 prop: palisade section running top to bottom, seen from the side: sharpened log tips pointing up, seamless at the top and bottom edges. |
| prop | Palisade rail, horizontal | Sharpened logs along the north and south walls. | `web/src/game/worldMap.ts` `PALISADE` — `town(45)` | 16×16 prop: palisade section running left to right: three upright sharpened logs bound by a horizontal rope, seamless at the left and right edges. |
| prop | Palisade corner, north-west | Corner where the west wall meets the north wall. | `web/src/game/worldMap.ts` `PALISADE` — `town(44)` | 16×16 prop: palisade corner piece joining a wall going right and a wall going down, a thicker corner log. |
| prop | Palisade corner, south-west | Corner where the west wall meets the south wall. | `web/src/game/worldMap.ts` `PALISADE` — `town(68)` | 16×16 prop: palisade corner piece joining a wall going up and a wall going right, a thicker corner log. |
| prop | Palisade end | End of the north and south walls at the river. | `web/src/game/worldMap.ts` `PALISADE` — `town(82)` | 16×16 prop: palisade wall coming from the left ending in one thick post on the right. |
| prop | Palisade gate post, north | Thick gate post north of the gate opening. | `web/src/game/worldMap.ts` `PALISADE` — `town(71)` | 16×16 prop: palisade wall coming from the top ending in a tall thick gate post with a carved top, opening below. |
| prop | Palisade gate post, south | Thick gate post south of the gate opening. | `web/src/game/worldMap.ts` `PALISADE` — `town(47)` | 16×16 prop: tall thick gate post with a carved top, palisade wall continuing down from it, opening above. |
| prop | Bridge deck | Wooden plank deck over the river. | `web/src/game/VillageWorld.ts` bridge — `town(73)` | 16×16 prop: bridge deck of thick planks running top to bottom (across the deck), seamless left and right. |
| prop | Bridge railing, left | Railing end on the west bank. | `web/src/game/VillageWorld.ts` bridge — `town(80)` | 16×16 prop: start of a wooden bridge railing: a carved end post on the left and a rail running out to the right edge. |
| prop | Bridge railing, middle | Railing over the water. | `web/src/game/VillageWorld.ts` bridge — `town(81)` | 16×16 prop: wooden bridge railing section, top rail and two balusters, seamless left and right. |
| prop | Bridge railing, right | Railing end on the east bank. | `web/src/game/VillageWorld.ts` bridge — `town(82)` | 16×16 prop: end of a wooden bridge railing: rail coming from the left edge to a carved end post on the right. |
| building | Great Granary | Large granary, 4 × 3 tiles: more room for grain, wood and stone. | `web/src/game/worldMap.ts` `PROJECT_BUILDINGS.granary` — `town` tiles (same as monk cells) | 64×48 building: big long log granary on stone stilts, huge golden thatched roof, two wide doors, sacks, logs and stones stacked beside it. |
| building | Bell Tower | Stone bell tower, 3 × 4 tiles, with a golden onion dome; its ringing carries over both banks. | `web/src/game/worldMap.ts` `PROJECT_BUILDINGS['bell-tower']` — `town` tiles (same as church) + `web/src/game/VillageWorld.ts` `domeView('gold')` | 48×64 building: tall white stone bell tower, square base, open arched belfry with a bronze bell, topped with a golden onion dome and a gold Orthodox cross. |

## 8. Scenery (29)

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| terrain | Grass, plain | Plain meadow grass, the most common ground tile. | `web/src/game/worldMap.ts` `buildLayout` — `town(0)` | 16×16 seamless terrain tile: plain short meadow grass, two greens, very little detail so it can repeat thousands of times. |
| terrain | Grass, variant 1 | Grass with a few tufts. | `web/src/game/worldMap.ts` `buildLayout` — `town(1)` | 16×16 seamless terrain tile: the same meadow grass with two small darker grass tufts. |
| terrain | Grass, variant 2 | Grass with small flowers. | `web/src/game/worldMap.ts` `buildLayout` — `town(2)` | 16×16 seamless terrain tile: the same meadow grass with three tiny white and yellow flowers. |
| terrain | Stony ground | Grey stones in grass; where stones are gathered. | `web/src/game/worldMap.ts` `buildLayout` — `town(43)` | 16×16 seamless terrain tile: meadow grass with several loose grey stones of different sizes. |
| terrain | River, west bank | Water with the west shore. | `web/src/game/worldMap.ts` `buildLayout` — `battle(36)` | 16×16 terrain tile: river water on the right, a muddy grassy shore on the left with a light foam line where they meet, seamless top and bottom. |
| terrain | River, water | Open river water (animated ripples would be nice). | `web/src/game/worldMap.ts` `buildLayout` — `battle(37)` | 16×16 seamless terrain tile: open river water, mid blue with a few light ripple strokes running top to bottom. Optional: strip of 4 frames with the ripples moving down. |
| terrain | River, east bank | Water with the east shore. | `web/src/game/worldMap.ts` `buildLayout` — `battle(38)` | 16×16 terrain tile: river water on the left, a muddy grassy shore on the right with a light foam line where they meet, seamless top and bottom. |
| terrain | Pond water | Still pond water in the wild land. | `web/src/game/VillageWorld.ts` `buildTerrain` — code-drawn ellipses | 16×16 seamless terrain tile: still pond water, slightly greener blue than the river, one lily pad. |
| terrain | Pond shore | Light shallow edge of a pond. | `web/src/game/VillageWorld.ts` `buildTerrain` — code-drawn ellipses | Set of 16×16 terrain tiles for a pond edge (Wang autotile set of 16 tiles: every combination of water and grass at the four corners), light shallow water and sandy rim. |
| terrain | Mountain rock | Bare rock under the mountain ridges of the wild land; nobody walks on it. | `web/src/game/VillageWorld.ts` `buildTerrain` — code-drawn rectangles | 16×16 seamless terrain tile: bare grey-brown mountain rock with a few cracks and darker patches, no grass. |
| prop | Mountain peak | Rocky peak drawn twice the tile size; neighbouring peaks overlap into a ridge. Some are overgrown with moss. | `web/src/game/worldMap.ts` `MOUNTAIN_PEAKS` `rogue(1251–1253)`, `MOSSY_PEAKS` `rogue(1308–1310)` + `VillageWorld.ts` `peakView` | 32×32 prop, three variants plus three mossy ones: a jagged grey rock peak filling the bottom of the canvas, light faces on the left and shadow on the right, transparent around it; mossy versions with green patches on the slopes. |
| terrain | Trampled earth | Bare packed earth around the campfire. | `web/src/game/VillageWorld.ts` `buildTerrain` — code-drawn ellipses | 16×16 seamless terrain tile: packed brown earth with a few pebbles and ash specks. |
| prop | Tree, variant 1 | Tall tree, two tiles high. Felled for wood; grows back. | `web/src/game/worldMap.ts` `TREE_TILES` — `town(16)` + top `town(4)` | 16×32 prop: tall birch tree, white trunk with black marks, round light-green crown, small ground shadow. |
| prop | Tree, variant 2 | Small tree, one tile. | `web/src/game/worldMap.ts` `TREE_TILES` — `town(28)` | 16×16 prop: small young birch with a round light-green crown and a thin white trunk, small ground shadow. |
| prop | Tree, variant 3 | Tall tree of another kind (for example a pine), two tiles high. | `web/src/game/worldMap.ts` `TREE_TILES` — `town(15)` + top `town(3)` | 16×32 prop: tall dark-green spruce with layered drooping branches and a brown trunk, small ground shadow. |
| prop | Tree, variant 4 | Small tree of another kind, one tile. | `web/src/game/worldMap.ts` `TREE_TILES` — `town(27)` | 16×16 prop: small young spruce, dark-green cone shape, small ground shadow. |
| prop | Tree, variant 5 | Round bushy tree, one tile. | `web/src/game/worldMap.ts` `TREE_TILES` — `town(5)` | 16×16 prop: round bushy oak with a dark-green crown and a short thick trunk, small ground shadow. |
| prop | Tree stump | What is left after a tree is felled, until it grows back. | Not drawn (the tree just disappears) | 16×16 prop: low cut tree stump with light rings on the top and a few wood chips around it. |
| prop | Sapling | A young tree growing back on a felled spot. | Not drawn | 16×16 prop: tiny sapling with a thin stem and three small leaves next to an old stump. |
| prop | Berry bush | Green bush with red berries; where berries are foraged. | `web/src/game/VillageWorld.ts` `bushView` — code-drawn | 16×16 prop: round low green bush dotted with bright red berries, small ground shadow. |
| prop | Reeds | Clump of cattails on the river bank; cut for reeds. | `web/src/game/VillageWorld.ts` `reedView` — code-drawn | 16×16 prop: clump of four tall thin green reeds of different heights with brown cattail heads. |
| prop | Campfire stones and logs | Ring of stones with crossed logs; the heart of the camp. | `web/src/game/VillageWorld.ts` `campfireView` — code-drawn | 16×16 prop: oval ring of grey stones around two crossed burnt logs and glowing embers, no flames (flames are a separate effect). |
| effect | Campfire flames | Animated flickering flames on the campfire (several frames). | `web/src/game/VillageWorld.ts` `drawFlames` — code-drawn | Strip of 4 frames, each 16×16 effect: flickering campfire flames, red outer, orange middle, pale yellow core, about 10 pixels tall, base on the bottom row, each frame a slightly different flame shape. |
| prop | Gold vein rock | Grey rock outcrop on the far bank. | `web/src/game/worldMap.ts` `buildLayout` — `town(43)` | 16×16 seamless terrain tile: bare grey rocky ground with cracks, no grass. |
| prop | Gold nugget in rock | Gold glinting in the outcrop; disappears as the vein is dug. | `web/src/game/VillageWorld.ts` gold vein — `town(93)` scaled | 16×16 prop: a grey rock lump with a vein of bright gold and two glints, transparent around it. |
| prop | Depleted vein spot | Bare grey rock where a nugget was dug out. | `web/src/game/VillageWorld.ts` gold vein — `town(93)` desaturated | 16×16 prop: the same grey rock lump with an empty dug-out hollow and pick marks, no gold. |
| building | Monastery church | Old church on the far bank, 3 × 4 tiles, with a blue onion dome and cross. Scenery. | `web/src/game/worldMap.ts` `MONASTERY.church` — `town` tiles + `web/src/game/VillageWorld.ts` `domeView('blue')` | 48×64 building: old white stone Orthodox church, arched door and narrow windows, a drum topped with a blue onion dome with gold stars and a gold cross. |
| building | Monk cell | Small monastery house, 3 × 3 tiles; two stand by the church. | `web/src/game/worldMap.ts` `MONASTERY.cell` — `town` tiles | 48×48 building: small whitewashed monk's cell with a dark wooden roof, a tiny arched door and one small window, a wooden cross over the door. |
| prop | Onion dome | Onion dome with a cross; gold for the bell tower, blue for the church. | `web/src/game/VillageWorld.ts` `domeView` — code-drawn | 16×32 prop, two versions: an onion dome on a short drum with an Orthodox cross on top; version 1 gold, version 2 blue with small gold stars. |

## 9. Characters and animals (19)

All citizen bodies must use the recolor colors from the requirements table.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| character | Citizen, male style 1 | Male villager body. Hair, skin and outfit regions are recolored (6 skin tones, 8 hair colors, 8 outfit colors). | `web/src/game/appearance.ts` `BODIES.Male[0]` — `dungeon(85)` | 16×16 character: young Slavic peasant man, short hair, belted long shirt (kosovorotka) and trousers, bast shoes, arms at his sides. Skin, hair and clothes in the exact recolor colors. |
| character | Citizen, male style 2 | Male villager body, another cut of clothes. Recolorable. | `web/src/game/appearance.ts` `BODIES.Male[1]` — `dungeon(98)` | 16×16 character: bearded Slavic peasant man, shoulder-length hair, sleeveless vest over a shirt, trousers tucked into foot wraps. Skin, hair and clothes in the exact recolor colors. |
| character | Citizen, male style 3 | Male villager body, another cut of clothes. Recolorable. | `web/src/game/appearance.ts` `BODIES.Male[2]` — `dungeon(88)` | 16×16 character: stocky Slavic workman with short hair and a thick beard, sleeves rolled up, leather apron. Skin, hair and clothes in the exact recolor colors. |
| character | Citizen, male style 4 | Male villager with a hood or cap. Recolorable. | `web/src/game/appearance.ts` `BODIES.Male[3]` — `dungeon(86)` | 16×16 character: Slavic peasant man in a round felt cap with hair showing at the sides, long belted coat. Cap and coat in the clothes recolor colors, skin and hair in theirs. |
| character | Citizen, male style 5 | Male villager with a hood or cap, another color layout. Recolorable. | `web/src/game/appearance.ts` `BODIES.Male[4]` — `dungeon(112)` | 16×16 character: Slavic hunter-looking man with a hood over his head and a short cloak. Hood and cloak in the clothes recolor colors, face in skin colors, a fringe of hair in hair colors. |
| character | Citizen, female style 1 | Female villager in a dress. Recolorable. | `web/src/game/appearance.ts` `BODIES.Female[0]` — `dungeon(99)` | 16×16 character: young Slavic peasant woman with a long braid over her shoulder, sarafan dress over a white blouse. Skin, hair and dress in the exact recolor colors. |
| character | Citizen, female style 2 | Female villager with a headscarf. Recolorable. | `web/src/game/appearance.ts` `BODIES.Female[1]` — `dungeon(100)` | 16×16 character: Slavic peasant woman in a headscarf tied under the chin, long skirt and apron. Headscarf in the hair recolor colors, skirt in the clothes recolor colors. |
| character | Citizen, old look | Fixed sprites for citizens created before character creation. | `web/src/game/worldMap.ts` `VILLAGER_SPRITES` — `dungeon(84…112)` | No new sprite: map these citizens to the new recolorable bodies with fixed colors. |
| character | Tsar | The streamer's character: village leader, should stand out (for example a fur hat and staff). | `web/src/game/VillageWorld.ts` `setTsar` — same bodies as citizens, gold name label | 16×16 character: village leader (voivode) in a tall fur-trimmed hat and a long embroidered red caftan with gold trim, holding a tall staff. Face in the skin recolor colors, beard in the hair recolor colors. |
| character | Elder (priest) | Hooded old monk who stands by the campfire and gives advice. | `web/src/game/worldMap.ts` `PRIEST_SPRITE` — `dungeon(111)` | 16×16 character: old Orthodox monk in a black hooded robe and tall black hat (klobuk), long white beard, wooden cross on his chest, leaning on a crooked staff. Fixed colors, not recolored. |
| character | Citizen walk frames | Walking animation for every body style. | Not drawn (the sprite slides) | Strip of 4 frames, each 16×16 character: the given citizen style walking toward the viewer, legs and arms alternating, one-pixel body bob. Same colors as the still sprite. |
| character | Citizen work frames | Working poses: chopping, digging, carrying, sowing, fishing, building. | Not drawn | Strip of 2 frames per action, each 16×16 character: the given citizen style swinging an axe (chop), swinging a pickaxe (dig), holding a sack over the head (carry), scattering seed (sow), holding a rod (fish), hammering (build). |
| character | Citizen asleep | Lying or sitting asleep, with "Z" letters. | Not drawn (the citizen just stands still) | 16×16 character: the given citizen style curled up asleep on the ground on a straw mat, eyes closed. The "Z" is a separate effect. |
| character | Rabbit | Brown rabbit that hops in meadows; hunted for food and leather. | `web/src/game/animals.ts` `LOOKS.Rabbit` — code-drawn pixels | Strip of 3 frames, each 16×16 character, facing right: brown wild hare with long ears and a white tail; frame 1 sitting, frames 2-3 mid-hop. Body about 9 pixels long, feet on the bottom row. |
| character | Deer | Deer with antlers in the woods; hunted for a lot of food and leather. | `web/src/game/animals.ts` `LOOKS.Deer` — code-drawn pixels | Strip of 3 frames, each 16×16 character, facing right: red deer stag with small antlers, white belly and tail; frame 1 standing, frames 2-3 trotting. Body about 12 pixels long. |
| character | Bird | Small brown bird with a red breast in groves; hunted for feathers. | `web/src/game/animals.ts` `LOOKS.Bird` — code-drawn pixels | Strip of 3 frames, each 16×16 character, facing right: small brown bird with an orange-red breast (bullfinch-like); frame 1 perched, frames 2-3 wings up and down. Body about 6 pixels long. |
| character | Rabbit, carried | Caught rabbit held upside down above the hunter's head. | `web/src/game/animals.ts` `carry` — same pixels flipped | 16×16 character: dead hare hanging head down, held by the hind legs at the top, eyes closed. |
| character | Deer, carried | Caught deer carried by the hunter. | `web/src/game/animals.ts` `carry` — same pixels flipped | 16×16 character: dead deer slung across the shoulders seen from the front, legs hanging both sides, sized to sit on top of a 16×16 citizen. |
| character | Bird, carried | Caught bird held by the feet. | `web/src/game/animals.ts` `carry` — same pixels flipped | 16×16 character: two small dead birds hanging head down from a string, eyes closed. |

## 10. Task icons (57)

Tasks from `src/Desertod.Domain/Catalog/GameCatalog.cs`. Shown in the task list, in the result panel, and in the bubble above a
working citizen. The 6 "Deliver" tasks could use the resource sprite with one shared "carry" badge instead of their own sprites.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| icon | Gather wood (`chop-wood`) | Axe in a log. Laborer gathers wood in the forest. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(127)` | 16×16 icon: woodcutter's axe stuck in a short log, two wood chips flying. |
| icon | Gather stones (`quarry-stone`) | Pickaxe over stones. Laborer gathers stones on the hillside. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(115)` | 16×16 icon: hand pick (wooden handle, iron head) above two grey stones. |
| icon | Forage berries (`forage`) | Hand picking red berries from a bush. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(29)` | 16×16 icon: hand picking a red berry from a small green berry sprig. |
| icon | Cut reeds (`cut-reeds`) | Sickle cutting cattails. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(88)` | 16×16 icon: curved sickle crossing two cattail reeds. |
| icon | Hunt rabbits (`hunt`) | Sling or snare with a rabbit. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(118)` | 16×16 icon: brown hare head in profile with a rope snare loop in front of it. |
| icon | Hunt deer (`track-game`) | Bow with a deer hoof print. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(118)` | 16×16 icon: wooden hunting bow beside a split deer hoof print. |
| icon | Hunt birds (`hunt-birds`) | Sling stone and a falling feather. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(119)` | 16×16 icon: leather sling with a stone and a small falling brown feather. |
| icon | Tend the fire (`tend-fire`) | Hand adding a log to a fire; raises morale. | `web/src/game/worldMap.ts` `TASK_ICONS` — `extras(1)` | 16×16 icon: hand laying a log onto small orange flames. |
| icon | Fish from the pier (`fish`) | Fishing rod with a fish. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(131)` | 16×16 icon: bent wooden fishing rod with a silver fish on the line. |
| icon | Dig for gold (`dig-gold`) | Shovel with a gold nugget. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(93)` | 16×16 icon: wooden shovel with a shiny gold nugget on the blade. |
| icon | Measure the river (`measure-river`) | Measuring pole with notches in water; gives research points. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(47)` | 16×16 icon: notched measuring pole standing in blue water with ripple lines. |
| icon | Carry to storage (`carry-to-storage`) | Person carrying a sack toward a pile. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(92)` | 16×16 icon: burlap sack with a small arrow pointing down into an open crate. |
| icon | Tend the fields (`tend-fields`) | Hoe between crop rows. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(66)` | 16×16 icon: wooden hoe over a small green sprout in soil. |
| icon | Sow turnips (`sow-turnips`) | Hand dropping seed with a turnip. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(21)` | 16×16 icon: small seed pouch dropping seeds, a tiny turnip in the corner. |
| icon | Sow carrots (`sow-carrots`) | Hand dropping seed with a carrot. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(9)` | 16×16 icon: small seed pouch dropping seeds, a tiny carrot in the corner. |
| icon | Sow cabbage (`sow-cabbage`) | Hand dropping seed with a cabbage. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(57)` | 16×16 icon: small seed pouch dropping seeds, a tiny cabbage in the corner. |
| icon | Sow flax (`sow-flax`) | Hand dropping seed with a blue flax flower. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(10)` | 16×16 icon: small seed pouch dropping seeds, a tiny blue flax flower in the corner. |
| icon | Sow rye (`sow-rye`) | Hand dropping seed with a rye ear. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(69)` | 16×16 icon: small seed pouch dropping seeds, a tiny golden rye ear in the corner. |
| icon | Water the fields (`water-fields`) | Wooden bucket pouring water. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(73)` | 16×16 icon: tilted wooden bucket pouring blue water drops onto a sprout. |
| icon | Harvest (`harvest`) | Sickle and a basket of crops. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(11)` | 16×16 icon: sickle leaning on a basket full of vegetables and rye ears. |
| icon | Read the chronicles (`read-books`) | Open book of chronicles. | `web/src/game/worldMap.ts` `TASK_ICONS` — `dungeon(63)` | 16×16 icon: open old book with red initial letters on the pages (no readable text). |
| icon | Chat at the tavern (`chat-tavern`) | Two mugs clinking. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(107)` | 16×16 icon: two wooden mugs of kvass clinking with foam splashing. |
| icon | Brew vigor drinks (`brew-vigor`) | Bubbling pot with a lightning spark. | `web/src/game/worldMap.ts` `TASK_ICONS` — `dungeon(116)` | 16×16 icon: small iron pot of bubbling brown brew with a yellow lightning spark rising from it. |
| icon | Deliver wood (`deliver-wood`) | Wood carried to a project. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(106)` | 16×16 icon: two logs on a carrying frame with a small arrow pointing right. |
| icon | Deliver stone (`deliver-stone`) | Stone carried to a project. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(89)` | 16×16 icon: wicker back basket with grey stones and a small arrow pointing right. |
| icon | Deliver reeds (`deliver-reeds`) | Reeds carried to a project. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(65)` | 16×16 icon: tied reed bundle with a small arrow pointing right. |
| icon | Deliver gold (`deliver-gold`) | Gold carried to a project. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(93)` | 16×16 icon: small leather pouch of gold nuggets with a small arrow pointing right. |
| icon | Deliver planks (`deliver-planks`) | Planks carried to a project. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(73)` | 16×16 icon: three planks tied with rope with a small arrow pointing right. |
| icon | Deliver iron (`deliver-iron`) | Iron ingots carried to a project. | `web/src/game/worldMap.ts` `TASK_ICONS` — `dungeon(102)` | 16×16 icon: two iron ingots on a small hand barrow with a small arrow pointing right. |
| icon | Build (`build`) | Hammer and a beam; work on a village project. | `web/src/game/worldMap.ts` `TASK_ICONS` — `factory(122)` | 16×16 icon: wooden mallet striking a beam joint, two small impact lines. |
| icon | Go on an expedition (`expedition`) | Backpack and walking stick leaving the gate. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(119)` | 16×16 icon: birch-bark backpack with a walking stick leaning on it. |
| icon | Roam the land (`roam`) | Footprints on grass. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(29)` | 16×16 icon: trail of three bast-shoe footprints across a green tuft. |
| icon | Tell stories by the fire (`tell-stories`) | Speech bubble over a fire. | `web/src/game/worldMap.ts` `TASK_ICONS` — `extras(1)` | 16×16 icon: small campfire with a parchment speech bubble containing three dots above it. |
| icon | Study plants (`study-plants`) | Magnifier or eye over a leaf. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(80)` | 16×16 icon: green herb leaf with a large open eye looking at it. |
| icon | Explore the land (`explore-land`) | Map with a dotted path. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(119)` | 16×16 icon: rolled-open parchment map with a red dotted path and an X. |
| icon | Read the sky (`read-sky`) | Eye under clouds; gives research points. | `web/src/game/worldMap.ts` `taskIcon` — no icon, falls back to hammer `factory(122)` | 16×16 icon: open eye looking up at a small white cloud and a half sun. |
| icon | Research a discovery (`research-technology`) | Scroll with a lightbulb-like spark. | `web/src/game/worldMap.ts` `TASK_ICONS` — `dungeon(63)` | 16×16 icon: birch-bark scroll with a bright yellow star-shaped spark above it. |
| icon | Mine coal (`mine-coal`) | Pickaxe with a coal lump. Miner. | `web/src/game/worldMap.ts` `TASK_ICONS` — `extras(0)` | 16×16 icon: pickaxe over a big black lump of coal. |
| icon | Mine iron ore (`mine-iron-ore`) | Pickaxe with ore. Miner. | `web/src/game/worldMap.ts` `TASK_ICONS` — `dungeon(41)` | 16×16 icon: pickaxe over a rust-brown ore rock with grey flecks. |
| icon | Smelt iron (`smelt-ingot`) | Crucible pouring into an ingot mold. Smith. | `web/src/game/worldMap.ts` `TASK_ICONS` — `dungeon(117)` | 16×16 icon: clay crucible pouring glowing orange metal into an ingot mold. |
| icon | Saw planks (`saw-planks`) | Saw cutting a log into boards. Carpenter. | `web/src/game/worldMap.ts` `TASK_ICONS` — `dungeon(118)` | 16×16 icon: hand saw half way through a log, a board falling away, sawdust dots. |
| icon | Bake bread (`bake-bread`) | Loaf on a baker's peel. Baker. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(125)` | 16×16 icon: wooden baker's peel holding a round golden rye loaf with a steam curl. |
| icon | Research (`research`) | Quill and scroll. Scholar. | `web/src/game/worldMap.ts` `TASK_ICONS` — `dungeon(63)` | 16×16 icon: goose quill writing on a parchment scroll with an ink pot. |
| icon | Survey the site (`survey`) | Measuring rod and plumb line. Engineer. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(83)` | 16×16 icon: striped measuring rod with a plumb bob hanging on a string. |
| icon | Supervise construction (`supervise`) | Pointing hand over a plan. Engineer. | `web/src/game/worldMap.ts` `TASK_ICONS` — `factory(125)` | 16×16 icon: pointing finger above a small blue building plan. |
| icon | Trade (`trade`) | Scales with coins. Merchant. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(74)` | 16×16 icon: brass balance scales with a coin on one pan and a sack on the other. |
| icon | Play the gusli (`perform`) | Gusli (Russian zither) with notes. Bard. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(95)` | 16×16 icon: wing-shaped wooden gusli with strings and two music notes. |
| icon | Patrol (`patrol`) | Spear and lantern. Guard. | `web/src/game/worldMap.ts` `TASK_ICONS` — `dungeon(104)` | 16×16 icon: spear with a small lit lantern hanging from it. |
| icon | Mine gold (`mine-gold`) | Pickaxe with a gold nugget deep in rock. Deep miner. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(93)` | 16×16 icon: pickaxe striking dark rock with a bright gold vein. |
| icon | Forge chains (`forge-chain`) | Hammer on an anvil with a chain. Master smith. | `web/src/game/worldMap.ts` `TASK_ICONS` — `factory(115)` | 16×16 icon: smith's hammer above an anvil with a glowing chain link on it. |
| icon | Build mechanisms (`build-mechanism`) | Gears and a wrench. Artisan. | `web/src/game/worldMap.ts` `TASK_ICONS` — `factory(114)` | 16×16 icon: wooden cog wheel with a small chisel crossing it. |
| icon | Long expedition (`explore`) | Compass and a far mountain. Scout. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(119)` | 16×16 icon: brass compass in front of a distant blue mountain peak. |
| icon | Draft plans (`design`) | Compass drawing on a blueprint. Architect. | `web/src/game/worldMap.ts` `TASK_ICONS` — `dungeon(65)` | 16×16 icon: drafting compass drawing an arc on a blue plan sheet. |
| icon | Brew remedies (`brew-remedy`) | Mortar, pestle and herbs. Alchemist. | `web/src/game/worldMap.ts` `TASK_ICONS` — `dungeon(115)` | 16×16 icon: stone mortar and pestle with green herb leaves. |
| icon | Lead a crew (`lead-crew`) | Raised hand over a group of hard hats. Foreman. | `web/src/game/worldMap.ts` `TASK_ICONS` — `factory(120)` | 16×16 icon: raised wooden staff above three small worker heads in caps. |
| icon | Negotiate (`negotiate`) | Handshake over a scroll. Envoy. | `web/src/game/worldMap.ts` `TASK_ICONS` — `battle(196)` | 16×16 icon: two hands shaking above a scroll with a red wax seal. |
| icon | Command (`command`) | Banner and sword. Captain. | `web/src/game/worldMap.ts` `TASK_ICONS` — `battle(70)` | 16×16 icon: red war banner on a pole crossed with a sword. |

## 11. Construction job icons (29)

Jobs on building sites (`BuildingCatalog.cs`, `Jobs`). Shown in the sites panel and above working citizens. The 6 "Bring"
jobs could use the resource sprite with one shared "carry" badge instead of their own sprites.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| icon | Bring sticks (`bring-sticks`) | Armful of thin sticks. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(2)` | 16×16 icon: armful of thin brown sticks tied with twine, small arrow pointing right. |
| icon | Bring logs (`bring-logs`) | Log on a shoulder. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(106)` | 16×16 icon: one long log on a shoulder strap, small arrow pointing right. |
| icon | Bring stones (`bring-stones`) | Stones in a carrying basket. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(89)` | 16×16 icon: wicker carrying basket with grey stones, small arrow pointing right. |
| icon | Bring reeds (`bring-reeds`) | Bundle of reeds on a back. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(65)` | 16×16 icon: large reed bundle with a carrying rope, small arrow pointing right. |
| icon | Bring hides (`bring-hides`) | Folded hide. | `web/src/game/worldMap.ts` `TASK_ICONS` — `dungeon(66)` | 16×16 icon: folded tan hide tied with a cord, small arrow pointing right. |
| icon | Bring planks (`bring-planks`) | Planks on a shoulder. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(73)` | 16×16 icon: two long planks carried at an angle, small arrow pointing right. |
| icon | Fell the trees (`fell-trees`) | Axe and a falling tree; trees standing on the plot. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(127)` | 16×16 icon: small tree tipping over next to a stump with an axe. |
| icon | Clear the ground (`clear-ground`) | Rake or shovel over rubble. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(86)` | 16×16 icon: wooden rake pulling small stones and grass tufts off bare earth. |
| icon | Mark the outline (`mark-outline`) | Stakes with a string between them. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(47)` | 16×16 icon: two stakes in the ground with a taut string between them. |
| icon | Lean the poles (`lean-poles`) | Two poles leaned together. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(2)` | 16×16 icon: three poles leaned together in a tent shape. |
| icon | Lay the cover (`lay-cover`) | Reeds laid over poles. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(65)` | 16×16 icon: reed bundle being laid across two slanted poles. |
| icon | Stack the pile (`stack-pile`) | Hands stacking logs. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(106)` | 16×16 icon: hand placing a log on top of a small stack of logs. |
| icon | Lay the stone ring (`lay-ring`) | Stones set in a circle. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(77)` | 16×16 icon: hand setting the last stone into a circle of stones. |
| icon | Tie the rack (`tie-rack`) | Rope knot on a pole joint. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(47)` | 16×16 icon: two poles crossed and lashed with a rope knot. |
| icon | Lay stones (`lay-stones`) | Stone foundation course being laid. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(77)` | 16×16 icon: row of square foundation stones with a trowel. |
| icon | Hew beams (`hew-beams`) | Adze squaring a log. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(127)` | 16×16 icon: adze squaring the side of a log, chips flying. |
| icon | Raise posts (`raise-posts`) | Upright post being lifted. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(47)` | 16×16 icon: tall wooden post standing up with a small upward arrow. |
| icon | Tie the frame (`tie-frame`) | Timber frame corner with rope. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(44)` | 16×16 icon: corner of a timber frame, post and beam joined and lashed. |
| icon | Mix clay (`mix-clay`) | Trough of clay and straw. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(124)` | 16×16 icon: wooden trough of red-brown clay with straw sticking out and a stirring stick. |
| icon | Weave and daub walls (`weave-walls`) | Wattle fence smeared with clay. | `web/src/game/worldMap.ts` `taskIcon` — no icon, falls back to hammer `factory(122)` | 16×16 icon: square of woven wattle, half of it smeared with clay. |
| icon | Bundle thatch (`bundle-thatch`) | Tied bundle of straw or reeds. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(68)` | 16×16 icon: tight bundle of golden straw tied at both ends. |
| icon | Thatch the roof (`thatch-roof`) | Thatched roof corner. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(97)` | 16×16 icon: corner of a roof with layered golden thatch and a straw bundle being added. |
| icon | Hang the door (`hang-door`) | Hide door on a doorframe. | `web/src/game/worldMap.ts` `taskIcon` — no icon, falls back to hammer `factory(122)` | 16×16 icon: wooden doorframe with a tan hide hanging in it. |
| icon | Plough the field (`plough-field`) | Wooden plough in furrows. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(129)` | 16×16 icon: wooden scratch plough (sokha) cutting a furrow in brown soil. |
| icon | Dig the shaft (`dig-shaft`) | Shovel at a hole in rock. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(115)` | 16×16 icon: shovel at the edge of a dark hole in grey rock. |
| icon | Build the forge (`build-forge`) | Stone forge with bellows. | `web/src/game/worldMap.ts` `TASK_ICONS` — `dungeon(74)` | 16×16 icon: small stone forge hearth with leather bellows beside it. |
| icon | Build the oven (`build-oven`) | Domed clay oven. | `web/src/game/worldMap.ts` `TASK_ICONS` — `farm(125)` | 16×16 icon: half-built domed clay oven with a hand adding clay. |
| icon | Lay the floor (`lay-floor`) | Planks being nailed down. | `web/src/game/worldMap.ts` `TASK_ICONS` — `town(73)` | 16×16 icon: floor boards with a hammer driving in a nail. |
| icon | Lend a hand (`lend-a-hand`) | Open helping hand; counts toward the crew without material. | `web/src/game/worldMap.ts` `TASK_ICONS` — `factory(122)` | 16×16 icon: two hands reaching toward each other, one helping the other up. |

## 12. Roles (22)

Titles from `GameCatalog.Roles`. Shown in the character sheet, under names, and in task requirements. Journeyman icons have a
plain border; master icons could get a small gold star in the corner.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| icon | Laborer | Plain work gloves. Basic role, everyone starts here. | Not drawn (text in `web/src/ui/CharacterSheet.tsx`) | 16×16 icon: pair of worn brown work mittens. |
| icon | Miner | Pickaxe and helmet lamp. Journeyman, needs Mining. | Not drawn (text) | 16×16 icon: pickaxe crossed with a small oil lamp. |
| icon | Smith | Hammer and anvil. Journeyman, needs Smelting. | Not drawn (text) | 16×16 icon: smith's hammer resting on a small anvil. |
| icon | Carpenter | Saw and plane. Journeyman, needs Carpentry. | Not drawn (text) | 16×16 icon: hand saw crossed with a wooden plane. |
| icon | Baker | Loaf and baker's cap. Journeyman, needs Baking. | Not drawn (text) | 16×16 icon: round rye loaf with a white baker's cap behind it. |
| icon | Hunter | Bow and arrow. Journeyman. | Not drawn (text) | 16×16 icon: wooden bow with a feathered arrow nocked. |
| icon | Farmer | Sickle and wheat. Journeyman. | Not drawn (text) | 16×16 icon: sickle crossed with a golden rye ear. |
| icon | Scholar | Quill and scroll. Journeyman, needs Writing. | Not drawn (text) | 16×16 icon: goose quill over a rolled scroll. |
| icon | Engineer | Measuring rod and compass. Journeyman. | Not drawn (text) | 16×16 icon: measuring rod crossed with a drafting compass. |
| icon | Merchant | Coin purse. Journeyman. | Not drawn (text) | 16×16 icon: fat leather coin purse with a coin peeking out. |
| icon | Bard | Gusli. Journeyman, needs Music. | Not drawn (text) | 16×16 icon: wing-shaped gusli with a music note. |
| icon | Guard | Spear and round shield. Journeyman. | Not drawn (text) | 16×16 icon: round wooden shield with an iron boss and a spear behind it. |
| icon | Synoptic | Weather vane with a cloud; forecasts are famously wrong. Journeyman, needs Weather lore. | Not drawn (text) | 16×16 icon: rooster weather vane pointing the wrong way at a small cloud, a tiny question mark shape made of a curl. |
| icon | Deep miner | Pickaxe with a gold nugget, deep tunnel. Master. | Not drawn (text) | 16×16 icon: pickaxe in front of a dark tunnel with a gold nugget, small gold star in the corner. |
| icon | Master smith | Hammer with a chain, glowing anvil. Master, needs Forging. | Not drawn (text) | 16×16 icon: glowing anvil with a chain and a hammer, small gold star in the corner. |
| icon | Artisan | Gears and fine tools. Master, needs Mechanics. | Not drawn (text) | 16×16 icon: two meshing gears with a fine chisel, small gold star in the corner. |
| icon | Scout | Spyglass and map. Master. | Not drawn (text) | 16×16 icon: brass spyglass lying on a map, small gold star in the corner. |
| icon | Architect | Blueprint and drafting compass. Master. | Not drawn (text) | 16×16 icon: blue plan of a church with a drafting compass, small gold star in the corner. |
| icon | Alchemist | Flask with bubbling liquid. Master, needs Alchemy. | Not drawn (text) | 16×16 icon: round glass flask of bubbling green liquid, small gold star in the corner. |
| icon | Foreman | Megaphone or raised staff over a crew. Master. | Not drawn (text) | 16×16 icon: raised foreman's staff with a red ribbon, small gold star in the corner. |
| icon | Envoy | Sealed letter with a ribbon. Master. | Not drawn (text) | 16×16 icon: folded letter with a red wax seal and a ribbon, small gold star in the corner. |
| icon | Captain | Helmet with a plume and a banner. Master. | Not drawn (text) | 16×16 icon: pointed Slavic helmet with a red plume, small gold star in the corner. |

## 13. Technologies (18)

Discoveries from `src/Desertod.Domain/Technologies/TechnologyCatalog.cs`. Today each one shows the icon of the building it
is researched at.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| icon | Stone tools | Sharp stone bound to a handle. Camp era. | `web/src/ui/TechnologyPanel.tsx`, `web/src/ui/PollPanel.tsx` — Stone pile icon `farm(89)` | 16×16 icon: knapped flint blade lashed to a wooden handle with sinew. |
| icon | Herb lore | Bunch of herbs and roots. Camp era. | Same — Drying rack icon `town(29)` | 16×16 icon: bunch of green herbs and a forked root tied with twine. |
| icon | Weaving | Woven reed mat and cord. Camp era. | Same — Lean-to icon `farm(2)` | 16×16 icon: small square of woven reed mat with a coil of twisted cord. |
| icon | Tracking | Hoof prints and a broken twig. Camp era. | Same — Drying rack icon `town(29)` | 16×16 icon: two hoof prints in mud next to a broken twig. |
| icon | Agriculture | Seed sprouting from soil. Village era. | Same — Granary icon `farm(71)` | 16×16 icon: seed in brown soil with a green sprout and two leaves. |
| icon | Carpentry | Plane shaving a beam. Village era. | Same — Woodyard icon `town(127)` | 16×16 icon: wooden plane on a beam with a curled shaving. |
| icon | Mining | Pickaxe at a tunnel mouth. Village era. | Same — Stone yard icon `town(115)` | 16×16 icon: pickaxe leaning at a timber-framed tunnel mouth. |
| icon | Writing | Birch bark with carved letters. Village era. | Same — Hut icon `town(83)` | 16×16 icon: piece of white birch bark with scratched dark marks and a bone stylus. |
| icon | Brewing | Barrel of kvass and honey. Village era. | Same — Granary icon `farm(71)` | 16×16 icon: small barrel with a honey pot on top. |
| icon | Baking | Clay oven with a loaf. Village era. | Same — Granary icon `farm(71)` | 16×16 icon: round loaf with a wheat ear and a steam curl. |
| icon | Weather lore | Cloud, wind and an aching knee. Village era. | Same — Granary icon `farm(71)` | 16×16 icon: small cloud with wind lines and a raindrop. |
| icon | Smelting | Furnace with molten metal. Town era. | Same — Mine icon `factory(98)` | 16×16 icon: small clay furnace with a glowing orange opening and molten metal running out. |
| icon | Music | Gusli strings and notes. Town era. | Same — Tavern icon `dungeon(82)` | 16×16 icon: three music notes over gusli strings. |
| icon | Bookkeeping | Tally sticks and an abacus. Town era. | Same — Library icon `dungeon(63)` | 16×16 icon: wooden abacus (schoty) with a notched tally stick. |
| icon | Mechanics | Gear, lever and axle. Town era. | Same — Workshop icon `factory(123)` | 16×16 icon: gear wheel on an axle with a lever. |
| icon | Alchemy | Distilling flask over a flame. Town era. | Same — Library icon `dungeon(63)` | 16×16 icon: glass retort over a small flame with a drop falling. |
| icon | Forging | Folded iron on an anvil. Town era. | Same — Smithy icon `dungeon(74)` | 16×16 icon: glowing orange iron bar folded on an anvil. |
| icon | Vigor drinks | Mug with lightning, coffee beans and sleepless eyes. Town era. | Same — Tavern icon `dungeon(82)` | 16×16 icon: mug with a lightning bolt, two coffee beans and a pair of wide-open tired eyes above it. |

## 14. Eras (4)

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| icon | Wild | A lone campfire under the trees. | Not drawn (text in `web/src/ui/TechnologyPanel.tsx`) | 16×16 icon: small campfire under a tall spruce. |
| icon | Camp | Lean-to and drying rack. | Not drawn (text) | 16×16 icon: reed lean-to next to a drying rack. |
| icon | Village | Huts and a farm. | Not drawn (text) | 16×16 icon: two thatched huts behind a small field. |
| icon | Town | Stone buildings and a church dome. | Not drawn (text) | 16×16 icon: stone wall with a gate and a small onion dome behind it. |

## 15. Stats (5)

From `src/Desertod.Domain/Stats/Stat.cs`. Each stat gets one strong color so it reads as a set.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| icon | Strength | Flexed arm or a lifted boulder. | Not drawn (text in `web/src/ui/CharacterSheet.tsx`) | 16×16 icon: red flexed arm. |
| icon | Dexterity | Open hand catching something. | Not drawn (text) | 16×16 icon: green open hand catching a small ball. |
| icon | Intelligence | Head with a spark, or an open book. | Not drawn (text) | 16×16 icon: blue head silhouette with a yellow spark. |
| icon | Charisma | Smiling face with a speech bubble. | Not drawn (text) | 16×16 icon: purple speech bubble with a small heart. |
| icon | Endurance | Heart with a shield, or a walking boot. | Not drawn (text) | 16×16 icon: orange heart in front of a small shield. |

## 16. Skills (18)

Crafts from `src/Desertod.Domain/Skills/Skill.cs`. Same subject as the matching task or role, drawn in a round badge so
skills are recognizable as a set.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| icon | Woodcutting | Axe. | Not drawn (text in `web/src/ui/CharacterSheet.tsx`) | 16×16 icon: round parchment badge with an axe. |
| icon | Quarrying | Pickaxe and stone. | Not drawn (text) | 16×16 icon: round parchment badge with a pick over a stone. |
| icon | Gathering | Basket of berries and herbs. | Not drawn (text) | 16×16 icon: round parchment badge with a berry basket. |
| icon | Hunting | Bow. | Not drawn (text) | 16×16 icon: round parchment badge with a bow. |
| icon | Fishing | Fish on a hook. | Not drawn (text) | 16×16 icon: round parchment badge with a fish on a hook. |
| icon | Farming | Hoe and sprout. | Not drawn (text) | 16×16 icon: round parchment badge with a hoe and a sprout. |
| icon | Mining | Mine cart. | Not drawn (text) | 16×16 icon: round parchment badge with an ore cart. |
| icon | Smithing | Anvil. | Not drawn (text) | 16×16 icon: round parchment badge with an anvil. |
| icon | Carpentry | Saw. | Not drawn (text) | 16×16 icon: round parchment badge with a saw. |
| icon | Baking | Bread loaf. | Not drawn (text) | 16×16 icon: round parchment badge with a loaf. |
| icon | Scholarship | Book and quill. | Not drawn (text) | 16×16 icon: round parchment badge with a book and quill. |
| icon | Building | Hammer and beam. | Not drawn (text) | 16×16 icon: round parchment badge with a mallet and beam. |
| icon | Hauling | Sack on a back. | Not drawn (text) | 16×16 icon: round parchment badge with a sack. |
| icon | Entertaining | Gusli or a jester's bell. | Not drawn (text) | 16×16 icon: round parchment badge with a gusli and a music note. |
| icon | Exploring | Compass. | Not drawn (text) | 16×16 icon: round parchment badge with a compass rose. |
| icon | Trading | Scales. | Not drawn (text) | 16×16 icon: round parchment badge with balance scales. |
| icon | Guarding | Shield. | Not drawn (text) | 16×16 icon: round parchment badge with a round shield. |
| icon | Weatherlore | Weather vane and cloud. | Not drawn (text) | 16×16 icon: round parchment badge with a cloud and a weather vane. |

## 17. Quests (14)

From `src/Desertod.Domain/Quests/VillageQuest.cs` and `StoryQuests.cs`. Tribe needs (Hunger, Cold, Storage) are warnings;
story quests are goals.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| icon | Hunger | Empty bowl; someone worked with no food in storage. | Not drawn (text in `web/src/ui/QuestPanel.tsx`) | 16×16 icon: empty wooden bowl and spoon with a small red warning border. |
| icon | Cold | Shivering figure under a tree; more citizens than beds. | Not drawn (text) | 16×16 icon: blue snowflake over a small tree, with a red warning border. |
| icon | Storage | Overflowing pile; gathered goods did not fit. | Not drawn (text) | 16×16 icon: crate overflowing with logs falling out, with a red warning border. |
| icon | Fire | First campfire being lit. Story 1. | Not drawn (text) | 16×16 icon: two sticks with the first small flame between them. |
| icon | Shelter | Lean-to. Story 2. | Not drawn (text) | 16×16 icon: reed lean-to with a person sleeping inside. |
| icon | Stores | Basket of food. Story 3. | Not drawn (text) | 16×16 icon: full basket of food next to a drying rack. |
| icon | First house | Hut with smoke from the roof. Story 4. | Not drawn (text) | 16×16 icon: thatched hut with a curl of smoke from the roof. |
| icon | Monument | Tsar statue. Story 5. | Not drawn (text) | 16×16 icon: crowned Tsar statue on a pedestal. |
| icon | Fishing spot | Pier with a fish. Story 6. | Not drawn (text) | 16×16 icon: plank pier with a fish jumping beside it. |
| icon | Palisade | Wall of stakes, wolf eyes outside. Story 7. | Not drawn (text) | 16×16 icon: sharpened stake wall with two yellow wolf eyes in the dark behind it. |
| icon | Tsar Bridge | Bridge to the monastery. Story 8. | Not drawn (text) | 16×16 icon: wooden bridge with a small onion dome on the far side. |
| icon | Monks' gold | Gold nugget and a monk's hood. Story 9. | Not drawn (text) | 16×16 icon: black monk's hood beside a gold nugget. |
| icon | Monastery bell | Bell under a golden dome. Story 10. | Not drawn (text) | 16×16 icon: bronze bell under a golden onion dome, sound lines. |
| icon | Great Granary | Large granary full of sacks. Story 11. | Not drawn (text) | 16×16 icon: large granary with sacks piled at its door. |

## 18. Weather and time (5)

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| icon | Sunny | Sun; today's sky in the top bar. | `web/src/index.css` `.weather-icon` — yellow CSS circle | 16×16 icon: round yellow sun with short rays. |
| icon | Rain | Rain cloud; today's sky in the top bar. | `web/src/index.css` `.weather-chip.raining .weather-icon` — grey CSS shape | 16×16 icon: grey cloud with three blue raindrops falling. |
| effect | Raindrop | Falling drop for the rain overlay on the map. | `web/src/game/VillageWorld.ts` `drawRain` — code-drawn lines | 4×8 effect: thin light-blue diagonal raindrop streak, 1 pixel wide, slightly transparent. |
| icon | Day | Sun for the day and night toggle. | Not drawn (text in `web/src/ui/Hud.tsx`) | 16×16 icon: sun rising over a hill. |
| icon | Night | Moon for the day and night toggle. | Not drawn (text in `web/src/ui/Hud.tsx`) | 16×16 icon: pale crescent moon with two small stars. |

## 19. Status (17)

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| icon | Energy | Lightning bolt or flame; spent by every cycle, regained by sleeping. | Not drawn (bar and text in `web/src/ui/CharacterSheet.tsx`, `web/src/ui/Hud.tsx`) | 16×16 icon: yellow lightning bolt. |
| icon | **New** Morale | Happy face or a raised fist; village spirit. Shown before the morale number (0-100, no percent sign) in the top bar. | `web/src/ui/Hud.tsx` `TopBar` — text "☺" placeholder | 16×16 icon: smiling sun-like face. |
| icon | Renown | Laurel or a star medal; reward for helping the village. | Not drawn (text in `web/src/ui/CharacterSheet.tsx`) | 16×16 icon: gold medal with a star on a red ribbon. |
| icon | Streak | Chain of flames; renewing cycles on time. | Not drawn (text) | 16×16 icon: orange flame with three small linked dots under it. |
| icon | Experience / level up | Upward arrow with a star. | Not drawn (text in `web/src/ui/TaskDonePanel.tsx`) | 16×16 icon: green upward arrow with a small star. |
| icon | Rare find | Sparkling gold nugget from an expedition. | Not drawn (text in `web/src/ui/TaskDonePanel.tsx`) | 16×16 icon: gold nugget with four sparkles around it. |
| icon | Idle | Citizen standing with nothing to do. | Not drawn | 16×16 icon: three grey dots in a speech bubble. |
| icon | Working | Small turning gear or hammer. | Not drawn (the task icon is shown instead) | 16×16 icon: small hammer with motion lines. |
| icon | Awaiting renewal | Hourglass; the cycle ended and waits for the player. | Not drawn (text in `web/src/ui/TaskList.tsx`) | 16×16 icon: wooden hourglass with sand run out. |
| icon | Dormant | Closed eyes or a grey moon; subscription lapsed. | Not drawn | 16×16 icon: grey sleeping face with closed eyes and a small padlock. |
| icon | Asleep | "Z Z" above a head. | Not drawn | 16×16 effect: three white "Z" shapes rising diagonally, getting bigger. |
| icon | Sleeps under the trees | Tree with a shivering person; worst rest. | Not drawn | 16×16 icon: small tree with a curled-up person and blue shiver lines. |
| icon | Sleeps in a lean-to | Lean-to with a bedroll. | Not drawn | 16×16 icon: reed lean-to with a straw bedroll. |
| icon | Sleeps in a hut | Hut with a lit window; full rest. | Not drawn | 16×16 icon: hut with a warm lit window and a crescent moon. |
| icon | Storage full | Box with an exclamation mark; citizens wait with goods in hands. | Not drawn (a crowd at the building is the only hint) | 16×16 icon: full crate with a red exclamation mark. |
| icon | Tsar badge | Crown or a fur hat; marks the streamer. | `web/src/index.css` `.badge` — text pill | 16×16 icon: tall fur-trimmed red voivode hat. |
| icon | Forecast | Weather vane with a question mark; a Synoptic's announcement. | Not drawn (text in `web/src/ui/WeatherChip.tsx`) | 16×16 icon: rooster weather vane with half sun and half cloud behind it. |

## 20. Interface (22)

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| icon | Tab: Village | Village roofs. | Not drawn (text, `web/src/ui/menuTabs.ts`) | 16×16 icon: two small hut roofs side by side. |
| icon | Tab: Character | Person silhouette. | Not drawn (text, `web/src/ui/menuTabs.ts`) | 16×16 icon: head-and-shoulders silhouette of a peasant in a cap. |
| icon | Tab: Tasks | Checklist on birch bark. | Not drawn (text, `web/src/ui/menuTabs.ts`) | 16×16 icon: birch-bark sheet with three tick marks. |
| icon | Tab: Build | Hammer and saw. | Not drawn (text, `web/src/ui/menuTabs.ts`) | 16×16 icon: hammer crossed with a saw. |
| icon | Tab: Technologies | Scroll with a spark. | Not drawn (text, `web/src/ui/menuTabs.ts`) | 16×16 icon: scroll with a yellow star-shaped spark. |
| icon | Tab: Settings | Wooden gear. | Not drawn (text, `web/src/ui/menuTabs.ts`) | 16×16 icon: wooden cog wheel. |
| icon | Close | Cross. | `web/src/ui/*.tsx` — text "✕" | 16×16 icon: dark brown X made of two crossed sticks. |
| icon | Done | Check mark. | `web/src/ui/SitesPanel.tsx`, `web/src/ui/WatchPopup.tsx` — text "✓" | 16×16 icon: green check mark. |
| icon | Menu | Three lines or a scroll. | Not drawn (text in `web/src/ui/Hud.tsx`) | 16×16 icon: three horizontal wooden bars. |
| icon | Inventory / Storage | Bag or chest. | Not drawn (text in `web/src/ui/Hud.tsx`) | 16×16 icon: tied burlap bag. |
| icon | Join | Open door with a person. | Not drawn (text in `web/src/ui/Hud.tsx`) | 16×16 icon: open wooden door with a small arrow going in. |
| icon | Results | Scroll with a tick. | Not drawn (text in `web/src/ui/Hud.tsx`) | 16×16 icon: scroll with a green tick. |
| icon | Collapse | Arrow up. | Not drawn (text in `web/src/ui/Hud.tsx`) | 16×16 icon: brown chevron pointing up. |
| icon | Expand | Arrow down. | Not drawn (text in `web/src/ui/Hud.tsx`) | 16×16 icon: brown chevron pointing down. |
| icon | Chat | Speech bubble. | Not drawn (text in `web/src/ui/ChatPanel.tsx`) | 16×16 icon: two overlapping parchment speech bubbles. |
| icon | Invite | Letter with a seal. | Not drawn (text in `web/src/ui/InviteCard.tsx`) | 16×16 icon: folded letter with a red wax seal. |
| icon | Notifications on | Bell. | Not drawn (text in `web/src/ui/NotificationToggle.tsx`) | 16×16 icon: small bronze bell with two sound lines. |
| icon | Notifications off | Crossed bell. | Not drawn (text in `web/src/ui/NotificationToggle.tsx`) | 16×16 icon: small bronze bell with a red diagonal line across it. |
| icon | Login: Twitch | Official Twitch logo (use the brand asset, do not generate). | Not drawn (text in `web/src/ui/LoginPanel.tsx`) | Do not generate: use the official brand asset. |
| icon | Login: Google | Official Google logo (use the brand asset, do not generate). | Not drawn (text in `web/src/ui/LoginPanel.tsx`) | Do not generate: use the official brand asset. |
| icon | Login: Telegram | Official Telegram logo (use the brand asset, do not generate). | Not drawn (text in `web/src/ui/LoginPanel.tsx`) | Do not generate: use the official brand asset. |
| icon | Login: Email | Envelope. | Not drawn (text in `web/src/ui/LoginPanel.tsx`) | 16×16 icon: closed parchment envelope. |

## 21. Effects (7)

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| effect | Speech bubble | Parchment-colored bubble with a tail; citizens and the Elder talk. | `web/src/game/VillageWorld.ts` `say` — code-drawn | Keep code-drawn (it stretches to fit the text). If drawn as a sprite: 24×24 nine-slice effect: parchment `#fff8e7` bubble with a 2-pixel dark brown `#3a2a1a` border, rounded corners, tail at the bottom middle. |
| effect | Task bubble | Small square bubble above a citizen's head holding the task icon or carried good. | `web/src/game/VillageWorld.ts` `iconBubble` — code-drawn | 16×16 effect: empty rounded square parchment `#fff5dc` frame with a 1-pixel dark brown border and a small tail at the bottom, center left empty for the icon. |
| effect | Chimney smoke | Puffs of smoke rising from huts, bakery, smithy and the campfire. | Not drawn | Strip of 4 frames, each 16×16 effect: small grey-white smoke puff rising and fading from the bottom to the top. |
| effect | Forge sparks | Sparks flying at the smithy while someone works. | Not drawn | Strip of 4 frames, each 16×16 effect: a few orange and yellow spark pixels bursting outward and fading. |
| effect | Selection outline | Yellow outline around the clicked tree, building or spot while the player chooses what to do. | `web/src/game/VillageWorld.ts` `setSelection` — code-drawn | Keep code-drawn (it fits any area). |
| effect | Walk target marker | Marker on the ground where the Tsar or a citizen is walking to. | Not drawn | Strip of 2 frames, each 16×16 effect: small parchment-yellow ring on the ground with a downward arrow bobbing above it. |
| effect | Ripe sparkle | Glint over a ripe farm bed, ready to harvest. | Not drawn | Strip of 3 frames, each 16×16 effect: small four-point white-yellow star twinkling (small, big, small). |

## 22. Obstacles (28)

Planned land that stands in the walkers' way, beyond the mountains of section 8. Marsh only slows walkers. Thickets and
rockfalls block until citizens clear them; the ravine blocks until the Tsar or a Governor places a rope bridge and citizens build
it. The cave and the lookout sit in the mountain ridges. None of these is drawn yet.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| terrain | Marsh ground | Soggy ground around ponds and the river in the wild; walkers wade through it slowly. | Not drawn (planned) | 16×16 seamless terrain tile: soggy dark olive-green ground with two small puddles of brown-green water and a few darker wet patches. |
| terrain | Marsh pool | Open marsh water between the soggy patches. | Not drawn (planned) | 16×16 seamless terrain tile: still murky green-brown water with a few floating duckweed specks and one small lily pad. |
| prop | Sedge tussock | Clump of marsh sedge dotted over the marsh. | Not drawn (planned) | 16×16 prop: round clump of thin pale-green and straw-yellow sedge blades rising from a small dark wet mound, transparent around it. |
| effect | Marsh splash | Splash at a walker's feet while they wade through the marsh. | Not drawn (planned) | Strip of 3 frames, each 16×16 effect: a small ring of muddy green water drops splashing up from the bottom row and falling back. |
| prop | Thicket, variant 1 | Dense brambles that nobody walks through until they are cut. | Not drawn (planned) | 16×16 prop: tangled dense blackthorn bush filling the tile, dark green leaves, brown thorny branches poking out, a few black berries. |
| prop | Thicket, variant 2 | Dense hazel and briar scrub. | Not drawn (planned) | 16×16 prop: dense low hazel scrub woven with thorny briar stems, mid-green leaves, a few small pink briar flowers. |
| prop | Thicket, variant 3 | Thicket with dry dead stems. | Not drawn (planned) | 16×16 prop: dense thorny thicket with grey dead stems sticking up out of dark green leaves. |
| prop | Cleared thicket | What is left once the thicket is cut: stubble and a pile of brush. | Not drawn (planned) | 16×16 prop: short cut stems in trampled grass and a small pile of cut brown branches, transparent around it. |
| icon | Clear the thicket (`clear-thicket`) | Billhook cutting thorny branches. Laborer cuts a path through a thicket. | Not drawn (planned) | 16×16 icon: curved iron billhook with a wooden handle slicing through a thorny brown branch. |
| prop | Rockfall, variant 1 | Big boulders fallen into a mountain pass; they block it until broken up. | Not drawn (planned) | 16×16 prop: two large rough grey boulders jammed together with smaller stones at their feet, cracks on the faces, filling the tile. |
| prop | Rockfall, variant 2 | One huge boulder in the pass. | Not drawn (planned) | 16×16 prop: one huge rounded grey boulder with a lighter top-left face and moss on its shaded side, pebbles around it. |
| prop | Rockfall, variant 3 | Heap of broken rock in the pass. | Not drawn (planned) | 16×16 prop: tall jumbled heap of angular grey rock slabs leaning on each other. |
| prop | Broken rockfall | Rubble left once the boulders are broken and carried off; walkable. | Not drawn (planned) | 16×16 prop: scattered flat grey rock chips and gravel on bare ground, low, transparent around it. |
| icon | Break the rockfall (`break-rockfall`) | Sledgehammer striking a boulder. Laborer breaks up a rockfall and gets stone. | Not drawn (planned) | 16×16 icon: heavy wooden-handled sledgehammer striking a grey boulder, a crack and two chips flying off. |
| terrain | Ravine, north rim | Grass ending at the northern edge of the ravine, the far wall dropping away below. | Not drawn (planned) | 16×16 terrain tile, seamless left and right: meadow grass in the top third ending at a ragged rock edge, below it a lit grey-brown cliff face falling into darkness at the bottom edge. |
| terrain | Ravine, depth | The bottom of the ravine, too deep to see. | Not drawn (planned) | 16×16 seamless terrain tile: near-black blue-grey chasm with a few faint rock ledges and pebbles catching a little light. |
| terrain | Ravine, south rim | Grass at the southern edge of the ravine; the near wall is in shadow. | Not drawn (planned) | 16×16 terrain tile, seamless left and right: darkness at the top edge, a thin strip of shadowed rock lip, then meadow grass in the bottom two-thirds. |
| building | Rope bridge | Plank bridge on ropes across the ravine, 3 × 4 tiles; the Tsar or a Governor places it, citizens build it. | Not drawn (planned) | 48×64 building (footprint 3×4 tiles): a narrow wooden plank bridge running from the bottom edge to the top edge across a dark ravine, thick rope hand-lines on both sides sagging slightly, two stout wooden posts at each end with ropes tied around them; the ravine shows dark under the planks in the middle rows, grass at the top and bottom rows. |
| icon | Rope bridge | Planks on ropes. | Not drawn (planned) | 16×16 icon: short sagging plank bridge with two rope hand-lines between two wooden posts. |
| building | Rope bridge stages (2) | Posts (posts driven in on both rims, ropes stretched across), Ropes and half the planks. | Not drawn (planned) | Building under construction, same canvas size and footprint as the finished Rope bridge. Stage 1 Posts: two posts on each rim with bare ropes stretched across the dark ravine. Stage 2 Planks: the ropes with planks laid over the lower half, a few loose planks stacked on the near rim. |
| icon | Stretch the ropes (`stretch-ropes`) | Coil of rope being pulled taut between two posts. Construction job of the rope bridge. | Not drawn (planned) | 16×16 icon: coil of thick hemp rope with its end pulled taut to a wooden post. |
| icon | Lay the deck (`lay-deck`) | A plank being laid across two ropes. Last construction job of the rope bridge. | Not drawn (planned) | 16×16 icon: one wooden plank laid crosswise over two parallel ropes, a mallet resting beside it. |
| prop | Cave mouth | Dark opening in a mountain ridge; laborers dig stone in it and sometimes find coal or iron ore. | Not drawn (planned) | 32×32 prop: rough natural arch opening in a grey rock face, deep darkness inside, a few rocks at the entrance and two small glints of ore on the inner wall, the rock face fills the top half and sides. |
| prop | Worked cave mouth | The cave once citizens have dug in it: timber props and a lantern. | Not drawn (planned) | 32×32 prop: the same cave arch with two rough timber props holding the entrance, a small lantern hanging on one, a little heap of broken stone outside. |
| icon | Dig in the cave (`dig-cave`) | Pickaxe before a cave arch. Laborer digs stone, sometimes coal or ore. | Not drawn (planned) | 16×16 icon: small iron pickaxe in front of a dark rocky cave arch with one orange ore glint. |
| prop | Lookout peak | A rocky peak with a way up; from the top a climber sees far over the land. | Not drawn (planned) | 32×32 prop: a rocky grey peak with a narrow zig-zag trail of flat stone steps climbing its south face to a small flat top, a few tufts of grass on the ledges. |
| prop | Lookout cairn | A stone cairn with a ribbon on a pole, raised on the lookout once someone has climbed it. | Not drawn (planned) | 16×32 prop: small cairn of stacked flat grey stones with a thin wooden pole rising from it and a red ribbon fluttering at the top. |
| terrain | Mountain trail | Flat stone steps up the lookout; the only mountain tile walkers may climb. | Not drawn (planned) | 16×16 terrain tile: three flat grey stone steps rising from the bottom to the top edge, rough rock on both sides, seamless top and bottom. |

## 23. Dungeon: Dead Lands (24)

The first dungeon, a 48 × 32 tile map of dead land the Tsar leads the heroes into
(`src/Desertod.Domain/Dungeons/DungeonCatalog.cs`). Everything is code-drawn today in `web/src/game/DungeonWorld.ts` and
`web/src/game/enemies.ts`. Every row is **New**: generate it. Foes face right like the raiders of section 9; the client
mirrors them.

| Type | Name | Description | Currently drawn by | Prompt |
| --- | --- | --- | --- | --- |
| terrain | **New** Dead earth (3 variants) | Bare grey-brown ground covering most of the map (`Ground`). | `web/src/game/DungeonWorld.ts` `drawTile` — code-drawn | Three 16×16 seamless terrain tiles: dry cracked grey-brown earth `#4a4238` with dark grit specks; variant 2 with a thin crack, variant 3 with a few pebbles. No grass. |
| terrain | **New** Withered grass | Patches of dead grass between the graves (`DeadGrass`). | `web/src/game/DungeonWorld.ts` `drawTile` — code-drawn | 16×16 seamless terrain tile: the dead earth with four or five tufts of dry straw-yellow `#8a7a50` grass bent over, no green. |
| terrain | **New** Bone-strewn ground | Old bones lying in the dirt (`Bones`); walkable. | `web/src/game/DungeonWorld.ts` `drawTile` — code-drawn | 16×16 terrain tile: the dead earth with a scattered thigh bone, two ribs and a small cracked skull, bones off-white `#d8d0b8`. |
| terrain | **New** Entrance path | Trodden track at the west edge where the heroes come in (`Entrance`). | `web/src/game/DungeonWorld.ts` `drawTile` — code-drawn | 16×16 terrain tile, seamless left and right: packed lighter brown path `#6a5e4c` with wheel ruts along it and grit at the edges. |
| prop | **New** Dead tree (2 variants) | Bare blackened tree; blocks walkers (`DeadTree`). | `web/src/game/DungeonWorld.ts` `drawTile` — code-drawn | Two 16×16 props: a leafless dead tree with a dark grey-brown trunk and crooked bare branches reaching up, a small shadow at the foot; variant 2 split by lightning with one branch hanging. Transparent around it. |
| prop | **New** Grave | Leaning headstone with a cross; blocks walkers (`Grave`). | `web/src/game/DungeonWorld.ts` `drawTile` — code-drawn | 16×16 prop: a weathered grey rounded headstone leaning slightly, a carved cross, moss at its foot, a low mound of earth in front. |
| prop | **New** Broken fence | Rotten graveyard fence; blocks walkers (`Fence`). | `web/src/game/DungeonWorld.ts` `drawTile` — code-drawn | 16×16 prop, seamless left and right: two dark rotten wooden rails on crooked posts, one rail broken and hanging. |
| prop | **New** Barrow mound | Earth mound of an old chieftain's grave; blocks walkers (`Mound`). | `web/src/game/DungeonWorld.ts` `drawTile` — code-drawn | 16×16 prop: a round grassless earth mound with a small grey standing stone on top. |
| prop | **New** Grey boulder | Big boulder; blocks walkers (`Rock`). | `web/src/game/DungeonWorld.ts` `drawTile` — code-drawn | 16×16 prop: a large rounded grey boulder with a lighter top-left face and a dark crack, filling most of the tile. |
| character | **New** Grave rat | Fast, weak rat that comes in packs. | `web/src/game/enemies.ts` `LOOKS.Rat` — code-drawn pixels | Strip of 4 frames, each 16×16 character, facing right: a mangy grey-brown rat with red eyes and a bald pink tail; frames 1-3 scurrying, frame 4 lying dead on its back. Body about 10 pixels long. |
| character | **New** Imp | Small hopping red devil. | `web/src/game/enemies.ts` `LOOKS.Imp` — code-drawn pixels | Strip of 4 frames, each 16×16 character, facing right: a small red imp with stubby black horns, yellow eyes, bat wings and a pointed tail; frames 1-3 hopping, frame 4 lying slain. About 9 pixels tall. |
| character | **New** Ghoul | Hunched corpse-eater with long claws. | `web/src/game/enemies.ts` `LOOKS.Ghoul` — code-drawn pixels | Strip of 4 frames, each 16×16 character, facing right: a hunched grey-green ghoul in rags with yellow eyes, long bone-white claws reaching forward; frames 1-3 shambling, frame 4 collapsed. About 11 pixels tall. |
| character | **New** Wraith | Floating pale ghost. | `web/src/game/enemies.ts` `LOOKS.Wraith` — code-drawn pixels | Strip of 4 frames, each 16×16 character, facing right: a floating pale blue-grey hooded ghost with dark empty eyes and a ragged trailing hem, semi-transparent edges; frames 1-3 bobbing, frame 4 fading into wisps. |
| character | **New** Barrow King | The boss: a crowned skeleton lord with a sword. | `web/src/game/enemies.ts` `LOOKS.BarrowKing` — code-drawn pixels | Strip of 4 frames, each 16×24 character, facing right: a tall skeleton king with a rusty gold crown, green glowing eyes, a tattered dark green cloak and a notched sword; frames 1-3 striding, frame 4 crumbled into bones with the crown on top. |
| icon | **New** Class: Brawler | Bare fists; anyone can fight this way. | Not drawn (text in `web/src/ui/Dungeon.tsx`) | 16×16 icon: a clenched fist with a cloth wrap around the knuckles. |
| icon | **New** Class: Spearman | Fights with a spear from storage. | Not drawn (text) | 16×16 icon: a wooden spear with an iron head, diagonal. |
| icon | **New** Class: Swordsman | Fights with a sword from storage. | Not drawn (text) | 16×16 icon: an iron sword with a leather-wrapped hilt, point up. |
| icon | **New** Class: Archer | Shoots with a bow and arrows. | Not drawn (text) | 16×16 icon: a curved yew bow with an arrow nocked. |
| icon | **New** Class: Shieldbearer | Holds a shield; more health, softer blows. | Not drawn (text) | 16×16 icon: a round wooden shield with an iron boss and rim. |
| icon | **New** Class: Healer | Heals allies with a remedy. | Not drawn (text) | 16×16 icon: a small cloth satchel with a red cross and a sprig of herbs. |
| icon | **New** Class: Voivode | The Tsar at the head of the host. | Not drawn (text) | 16×16 icon: a gold Russian crown with fur trim and a small cross on top. |
| effect | **New** Skull bubble | Over a fallen hero running for the exit. | `web/src/game/DungeonWorld.ts` `drawSkullBubble` — code-drawn | 16×16 effect: parchment `#fff8e7` speech bubble with a dark brown border and a tail at the bottom middle, a small black skull inside. |
| effect | **New** Arrow in flight | An archer's shot between hero and foe. | `web/src/game/DungeonWorld.ts` `arrow` — code-drawn line | 16×16 effect: a short arrow flying right, wooden shaft, iron tip, white fletching, centred. |
| icon | **New** Dead Lands | The dungeon in the Tsar's panel and the village banner. | Not drawn (text in `web/src/ui/Dungeon.tsx`) | 16×16 icon: a dead tree beside a leaning headstone under a pale green moon. |

---

## Totals

| Section | Count |
| --- | --- |
| Resources | 18 |
| Buildings in the world | 22 |
| Building icons | 22 |
| Construction stages (optional) | 62 sprites in 18 rows |
| Construction props | 5 |
| Farming | 26 |
| Village projects | 24 |
| Scenery | 29 |
| Characters and animals | 19 (plus animation frames) |
| Task icons | 57 |
| Construction job icons | 29 |
| Roles | 22 |
| Technologies | 18 |
| Eras | 4 |
| Stats | 5 |
| Skills | 18 |
| Quests | 14 |
| Weather and time | 5 |
| Status | 17 |
| Interface | 22 |
| Effects | 7 |
| Obstacles | 28 |
| Dungeon: Dead Lands | 24 rows (27 sprites with variants, plus animation frames) |

About 437 sprites without the optional construction stages and animation frames. Planned content from
[roadmap.md](roadmap.md) (House, Manor, arrows, decrees, Renown cosmetics, wolves) is not listed yet.
