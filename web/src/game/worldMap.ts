import { plotPosition, type AnimalKind, type Catalog, type TileArea, type TilePoint, type VillageState, type Wilds } from '../api'
import { farBlocksKind, farKind } from './farWilds'
import { farRoute, firstHit } from './farRoute'
import { EXTRAS, battle, dungeon, factory, farm, rogue, town, type TileRef } from './sheets'

/**
 * The camp area (tiles 0…43 × 0…25) matches the server's coordinates; the wild land extends around it
 * into negative coordinates, so the whole map spans MAP_LEFT…MAP_RIGHT × MAP_TOP…MAP_BOTTOM.
 */
export const MAP_LEFT = -44
export const MAP_TOP = -32
export const MAP_WIDTH = 120
export const MAP_HEIGHT = 90
export const MAP_RIGHT = MAP_LEFT + MAP_WIDTH
export const MAP_BOTTOM = MAP_TOP + MAP_HEIGHT

/** One edge of the land, in tiles. The camp tile sits in the middle. A healthy walker takes one real hour to an edge. The picture of the camp stays MAP_*. */
export const LAND_SIDE = 12_000
export const LAND_LEFT = 15 - LAND_SIDE / 2
export const LAND_TOP = 13 - LAND_SIDE / 2
export const LAND_WIDTH = LAND_SIDE
export const LAND_HEIGHT = LAND_SIDE

const CORE_WIDTH = 44
const CORE_HEIGHT = 26

export const RIVER_LEFT = 30
export const RIVER_RIGHT = 33
export const ROAD_TOP = 12

/** A building takes 3×3 tiles; the row below it is its yard. Mirrors the server's plot size. */
export const PLOT_WIDTH = 3
export const PLOT_HEIGHT = 4

export const CAMPFIRE = { x: 15.5, y: 13.5 }

/** Tiles a building and its yard cover when its top-left tile is (`x`, `y`). */
export const footprint = (x: number, y: number): TileArea => ({ x, y, width: PLOT_WIDTH, height: PLOT_HEIGHT })

const overlaps = (a: TileArea, b: TileArea) =>
  a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height

/** Footprints of every building and open site. */
export function buildingFootprints(village: VillageState): TileArea[] {
  return [...village.storages, ...village.structures, ...village.sites].flatMap((building) => {
    const plot = plotPosition(building.plotId)
    return plot ? [footprint(plot.x, plot.y)] : []
  })
}

/**
 * Same check as the server: inside the build area, off reserved ground, clear of other buildings.
 * Trees do not stop a blueprint: its builders fell them first.
 */
export function canBuildAt(catalog: Catalog, village: VillageState, x: number, y: number, privateBuild = false) {
  const area = footprint(x, y)
  const kingdom = village.kingdomArea ?? catalog.buildArea
  const land = privateBuild ? {x:MAP_LEFT,y:MAP_TOP,width:MAP_WIDTH,height:MAP_HEIGHT} : kingdom
  const inside = x >= land.x && y >= land.y && x + area.width <= land.x + land.width && y + area.height <= land.y + land.height
  return (
    inside &&
    (!privateBuild || !overlaps(area,kingdom)) &&
    canReachBank(village,{x:x+area.width/2,y:y+area.height-.5}) &&
    (x >= 0 && y >= 0 && x + area.width <= 28 && y + area.height <= 26 ||
      Array.from({length:area.width*area.height},(_,i)=>({x:x+i%area.width,y:y+Math.floor(i/area.width)})).every(p=>
        !(village.wilds?.obstacles.some(o=>o.x===p.x&&o.y===p.y&&!o.cleared)))) &&
    !catalog.reservedAreas.some((reserved) => overlaps(reserved, area)) &&
    !buildingFootprints(village).some((taken) => overlaps(taken, area))
  )
}

/**
 * Same check as the server for a rope bridge: the ravine runs through every column of the footprint between its top
 * row (the north rim) and its bottom row (the south rim, the yard), and no other building is there.
 */
export function canBridgeAt(village: VillageState, x: number, y: number) {
  const ravine = village.wilds?.ravine ?? []
  for (let column = x; column < x + PLOT_WIDTH; column++) {
    const span = ravine.find((candidate) => candidate.x === column)
    if (!span || span.top <= y || span.top + span.height >= y + PLOT_HEIGHT) return false
  }
  const area = footprint(x, y)
  return !buildingFootprints(village).some((taken) => overlaps(taken, area))
}

/** Footprints of the finished rope bridges: walkers cross the ravine there. */
export const bridgeCrossings = (village: VillageState): TileArea[] =>
  village.structures.flatMap((structure) => {
    const plot = structure.kind === ROPE_BRIDGE ? plotPosition(structure.plotId) : null
    return plot ? [footprint(plot.x, plot.y)] : []
  })

export const ROPE_BRIDGE = 'RopeBridge'

/** True when a building or an open site covers the tile; its trees belong to the builders, not to wood gatherers. */
export const tileBuiltOver = (village: VillageState, x: number, y: number) =>
  buildingFootprints(village).some((taken) => overlaps(taken, { x, y, width: 1, height: 1 }))

/** Camp land up to the river whose trees come from the server. Mirrors the server's forest area. */
export const FOREST_AREA: TileArea = { x: 0, y: 0, width: 30, height: 26 }

export const inForestArea = (x: number, y: number) =>
  x >= FOREST_AREA.x && y >= FOREST_AREA.y && x < FOREST_AREA.x + FOREST_AREA.width && y < FOREST_AREA.y + FOREST_AREA.height

const TREE_TILES: Array<Omit<PlacedTile, 'x' | 'y'>> = [
  { tile: town(16), top: town(4) },
  { tile: town(28) },
  { tile: town(16), top: town(4) },
  { tile: town(28) },
  { tile: town(15), top: town(3) },
  { tile: town(27) },
  { tile: town(5) },
]

/** A tree from the server, whose look is picked from its tile so it stays the same between updates. */
export function treeAt(x: number, y: number): PlacedTile {
  let value = Math.imul(x, 0x27d4eb2d) ^ Math.imul(y, 0x165667b1)
  value = Math.imul(value ^ (value >>> 15), 0x85ebca6b)
  return { ...TREE_TILES[((value ^ (value >>> 13)) >>> 0) % TREE_TILES.length], x, y }
}

export interface PlacedTile {
  tile: TileRef
  x: number
  y: number
  /** Upper half of a two-tile sprite, drawn one tile above `tile`. */
  top?: TileRef
}

/** What covers a tile, for the minimap. */
export const Terrain = {
  Grass: 0,
  Tree: 1,
  Water: 2,
  Stone: 3,
  Bush: 4,
  Reeds: 5,
  Clearing: 6,
  Mountain: 7,
  /** Nobody walks into the ravine; a finished rope bridge carries walkers over it. */
  Ravine: 8,
  /** Thickets and rockfalls block the way until citizens clear them; cleared tiles become grass. */
  Thicket: 9,
  Rockfall: 10,
  /** Wet ground: walkers wade through it slowly, so their ways go around it when they can. */
  Marsh: 11,
  Cave: 12,
  /** The top of the lookout peak, and the trail up to it. */
  Lookout: 13,
  Trail: 14,
} as const
export type Terrain = (typeof Terrain)[keyof typeof Terrain]

/** Rocky peaks of the mountain ridges: bare grey ones and grey ones overgrown with moss. */
const MOUNTAIN_PEAKS = [rogue(1251), rogue(1252), rogue(1253)]
const MOSSY_PEAKS = [rogue(1308), rogue(1309), rogue(1310)]

export interface MapLayout {
  /** Rows from MAP_TOP, columns from MAP_LEFT. */
  ground: TileRef[][]
  decor: PlacedTile[]
  bushes: Array<{ x: number; y: number }>
  reeds: Array<{ x: number; y: number }>
  ponds: Array<{ x: number; y: number; radiusX: number; radiusY: number }>
  /** Mountain tiles with the peak drawn over each. */
  mountains: Array<{ x: number; y: number; peak: TileRef }>
  /** Wet tiles around the ponds. */
  marsh: Array<{ x: number; y: number }>
  /** Tiles of the trail up to the lookout. */
  trail: Array<{ x: number; y: number }>
  /** One entry per tile, row by row from the top-left corner of the map. */
  terrain: Uint8Array
}

/** Spots (in tiles) where citizens stand while working. Places match the server's map. */
export const WORKPLACES: Record<string, { x: number; y: number; spread: number }> = {
  forest: { x: 5, y: 8, spread: 2.5 },
  stones: { x: 10.5, y: 2.5, spread: 1.5 },
  berries: { x: 4.5, y: 18.5, spread: 2 },
  reeds: { x: 28.5, y: 18, spread: 1.2 },
  pier: { x: 30.6, y: 16.2, spread: 0.7 },
  ford: { x: 29, y: 14.5, spread: 0.6 },
  'gold-vein': { x: 40, y: 17.4, spread: 0.8 },
  hunting: { x: 9, y: 23, spread: 2.5 },
  'deer-woods': { x: -3, y: 21.5, spread: 2 },
  'bird-grove': { x: 21, y: 23.5, spread: 2 },
  camp: { x: CAMPFIRE.x, y: CAMPFIRE.y + 1, spread: 2.5 },
  gate: { x: 1.2, y: 13.4, spread: 0.6 },
  fortifications: { x:2.5,y:10.5,spread:.6 },
  project: { x: 28, y: 11.2, spread: 1.4 },
}

/** Workplace of tasks without a fixed spot, like roaming the land. Mirrors the server's map. */
export const WILDS = 'wilds'

/** Workplace of farm work: the beds of the village farms. Mirrors the server's catalog. */
export const FIELDS = 'fields'

/** Where roaming citizens drift between: the natural places around the camp. */
export const ROAMING_SPOTS = ['forest', 'stones', 'berries', 'reeds', 'hunting'].map((place) => WORKPLACES[place])

export const PRIEST_SPOT = { x: 13.6, y: 12.6 }

/** One tile, or several drawn over each other from the first: a gable's eave over the wall that shows through it. */
export type BuildingCell = TileRef | TileRef[]

/**
 * Finished buildings drawn from tiles, each with its own roof, walls and props so the Tsar can tell them apart.
 * Shelters and racks of the wild age and the open stage are drawn as shapes instead.
 */
export const BUILDING_TILES: Record<string, BuildingCell[][]> = {
  /** Orange gable, plaster walls. */
  Hut: [
    [rogue(1217), rogue(1222), rogue(1218)],
    [[rogue(873), rogue(1331)], [rogue(873), rogue(1276)], [rogue(873), rogue(1332)]],
    [rogue(869), [rogue(868), rogue(89)], rogue(871)],
  ],
  /** Cream gable with a round window, plaster walls, banners by an arched door. */
  TownHall: [
    [rogue(1210), rogue(1215), rogue(1211)],
    [[rogue(873), rogue(1324)], [rogue(873), rogue(1269), rogue(101)], [rogue(873), rogue(1325)]],
    [[rogue(869), rogue(222)], [rogue(868), rogue(32)], [rogue(871), rogue(222)]],
  ],
  /** Open shed under a long brown roof. */
  Woodyard: [
    [rogue(1231), rogue(1236), rogue(1232)],
    [[rogue(1235), rogue(1288)], [rogue(1236), rogue(1293)], [rogue(1237), rogue(1289)]],
    [[rogue(150), rogue(1117)], rogue(150), [rogue(150), rogue(1117)]],
  ],
  /** Battlemented grey stone wall with a dark gateway. */
  StoneYard: [
    [rogue(819), rogue(820), rogue(821)],
    [rogue(879), rogue(880), rogue(881)],
    [rogue(876), [rogue(875), rogue(150), rogue(994)], rogue(878)],
  ],
  /** Brown gable, timber walls, sacks of grain by the door. */
  Granary: [
    [rogue(1231), rogue(1236), rogue(1232)],
    [[rogue(894), rogue(1345)], [rogue(894), rogue(1290)], [rogue(894), rogue(1346)]],
    [[rogue(890), rogue(139)], [rogue(889), rogue(90)], [rogue(892), rogue(140)]],
  ],
  /** Cream gable, timber walls, a double door and a crate of goods. */
  Storehouse: [
    [rogue(1210), rogue(1215), rogue(1211)],
    [[rogue(894), rogue(1324)], [rogue(894), rogue(1269)], [rogue(894), rogue(1325)]],
    [[rogue(890), rogue(315)], [rogue(889), rogue(316)], [rogue(892), rogue(413)]],
  ],
  /** Long grey roof over blue stone, a barred door between heaps of gold. */
  Treasury: [
    [rogue(1224), rogue(1229), rogue(1225)],
    [[rogue(1228), rogue(1281)], [rogue(1229), rogue(1286)], [rogue(1230), rogue(1282)]],
    [[rogue(883), rogue(613)], [rogue(882), rogue(204)], [rogue(885), rogue(672)]],
  ],
  /** Three dry beds; wet soil and crops are drawn over them from the village's fields. */
  Farm: [
    [farm(60), farm(61), farm(63)],
    [farm(60), farm(61), farm(63)],
    [farm(60), farm(61), farm(63)],
  ],
  /** A dark shaft in the rock, rails coming out of it, ore and a cart beside them. */
  Mine: [
    [rogue(177), rogue(177), rogue(177)],
    [rogue(177), [rogue(150), rogue(994)], rogue(177)],
    [rogue(671), [rogue(150), rogue(1238)], rogue(69)],
  ],
  /** Grey gable over brick, an anvil and a glowing forge. */
  Smithy: [
    [rogue(1224), rogue(1229), rogue(1225)],
    [[rogue(120), rogue(1338)], [rogue(120), rogue(1283)], [rogue(120), rogue(1339)]],
    [[rogue(120), rogue(15)], [rogue(120), rogue(89)], [rogue(120), rogue(511)]],
  ],
  /** Long cream roof over plank walls, a window and a crate of tools. */
  Workshop: [
    [rogue(1210), rogue(1215), rogue(1211)],
    [[rogue(1214), rogue(1267)], [rogue(1215), rogue(1272)], [rogue(1216), rogue(1268)]],
    [[rogue(1003), rogue(331)], [rogue(1004), rogue(90)], [rogue(1005), rogue(414)]],
  ],
  /** Grey gable with a rose window over blue stone, tall arched windows. */
  Library: [
    [rogue(1224), rogue(1229), rogue(1225)],
    [[rogue(887), rogue(1338)], [rogue(887), rogue(1283), rogue(102)], [rogue(887), rogue(1339)]],
    [[rogue(883), rogue(159)], [rogue(882), rogue(32)], [rogue(885), rogue(159)]],
  ],
  /** Long orange roof over timber walls, a lit window and a barrel by the door. */
  Tavern: [
    [rogue(1217), rogue(1222), rogue(1218)],
    [[rogue(1221), rogue(1274)], [rogue(1222), rogue(1279)], [rogue(1223), rogue(1275)]],
    [[rogue(890), rogue(331)], [rogue(889), rogue(90)], [rogue(892), rogue(23)]],
  ],
  /** Orange gable over grey stone, a sack of flour and the oven. */
  Bakery: [
    [rogue(1217), rogue(1222), rogue(1218)],
    [[rogue(880), rogue(1331)], [rogue(880), rogue(1276)], [rogue(880), rogue(1332)]],
    [[rogue(876), rogue(141)], [rogue(875), rogue(89)], [rogue(878), rogue(453)]],
  ],
  /** A striped stall with goods on the counter, crates and sacks in front. */
  Market: [
    [rogue(68), rogue(11), rogue(125)],
    [rogue(296), rogue(240), rogue(353)],
    [rogue(413), rogue(141), rogue(24)],
  ],
  /** Battlemented plaster wall, war banners and an iron gate. */
  Barracks: [
    [rogue(812), rogue(813), rogue(814)],
    [[rogue(872), rogue(50)], rogue(873), [rogue(874), rogue(50)]],
    [rogue(869), [rogue(868), rogue(203)], rogue(871)],
  ],
  /** A ring of rough stones around a glowing moonstone. */
  Waystone: [
    [rogue(1252), rogue(1251), rogue(1253)],
    [rogue(1251), rogue(563), rogue(1252)],
    [rogue(1253), rogue(1252), rogue(1251)],
  ],
}

/** A wet farm bed: darker soil over the dry one. */
export const WET_BED: TileRef[] = [farm(48), farm(49), farm(51)]

/** Growth stages of each crop in a farm bed: sprout, young plant, ripe plant. */
export const CROP_STAGES: Record<string, TileRef[]> = {
  Turnip: [farm(16), farm(17), farm(18)],
  Carrot: [farm(4), farm(5), farm(6)],
  Cabbage: [farm(52), farm(53), farm(54)],
  Flax: [farm(64), farm(65), farm(80)],
  Rye: [farm(64), farm(65), farm(66)],
}

/** What the crop looks like once gathered. */
export const CROP_ICONS: Record<string, TileRef> = {
  Turnip: farm(20),
  Carrot: farm(8),
  Cabbage: farm(56),
  Flax: farm(80),
  Rye: farm(68),
}

/** Below this growth a bed shows sprouts. */
const SPROUT_GROWTH = 0.4

/** Tile of a crop at its growth: sprout, young plant, or the ripe plant. */
export const cropTile = (crop: string, growth: number, ripe: boolean) =>
  CROP_STAGES[crop]?.[ripe ? 2 : growth < SPROUT_GROWTH ? 0 : 1]

/** Icon of each building in the interface. */
export const BUILDING_SIGNS: Record<string, TileRef> = {
  Heap: town(92),
  LeanTo: farm(2),
  WoodPile: town(106),
  StonePile: farm(89),
  DryingRack: town(29),
  Hut: town(83),
  Longhouse: town(83),
  Castle: town(84),
  TownHall: battle(52),
  Woodyard: town(127),
  StoneYard: town(115),
  Granary: farm(71),
  Storehouse: dungeon(89),
  Treasury: town(93),
  Farm: farm(68),
  Mine: factory(98),
  Smithy: dungeon(74),
  Workshop: factory(123),
  Library: dungeon(63),
  Tavern: dungeon(82),
  Bakery: farm(125),
  Hospital: town(84),
  Stage: battle(70),
  Market: farm(74),
  Barracks: dungeon(104),
  RopeBridge: town(73),
  Waystone: rogue(563),
}

/** The old monastery on the far bank: the Tsar Bridge is meant to reach it. Scenery, not part of the village. */
export const MONASTERY = {
  church: {
    x: 37,
    y: 7,
    tiles: [
      [town(48), town(49), town(50)],
      [town(60), town(61), town(62)],
      [town(76), town(77), town(79)],
      [town(88), town(90), town(88)],
    ] as TileRef[][],
  },
  cells: [
    { x: 34, y: 8 },
    { x: 40.5, y: 8 },
  ],
  cell: [
    [town(52), town(53), town(54)],
    [town(64), town(65), town(66)],
    [town(84), town(85), town(84)],
  ] as TileRef[][],
  area: { x: 34, y: 5, width: 10, height: 9 },
}

/** Where each catalog project is built when it is not the bridge. */
/** The Tsar's statue north of the campfire, between plots 1 and 2: a stone figure on a pedestal. */
export const MONUMENT = {
  id: 'tsar-monument',
  x: 16,
  y: 9,
  pedestal: dungeon(54),
  rough: dungeon(65),
  statue: dungeon(86),
  stone: 0xb9bec7,
}

/** The gold vein in the hills on the far bank, reached over the Tsar Bridge. Size mirrors the server's rules. */
export const VEIN_OUTCROP = { x: 39, y: 16, width: 3, height: 2, size: 150 }

/** Planks on poles from the west bank into the river, south of the road. */
export const PIER = { id: 'fishing-pier', x: RIVER_LEFT - 1, y: 16, length: 3 }

/**
 * Stakes around the camp land on the west, north and south; the river is the east wall. The west side is open at
 * the gate. Pieces are listed in building order: from the gate north and along the top, then south and along the bottom.
 */
export const PALISADE = (() => {
  const left = -1
  const top = -1
  const bottom = 26
  const end = 27
  const gate = { from: 12, to: 14 }
  const pieces: PlacedTile[] = []
  const side = (from: number, to: number, step: number, tile: (y: number) => TileRef) => {
    for (let y = from; step > 0 ? y <= to : y >= to; y += step) pieces.push({ tile: tile(y), x: left, y })
  }
  const rail = (y: number, corner: TileRef) => {
    pieces.push({ tile: corner, x: left, y })
    for (let x = left + 1; x <= end; x++) pieces.push({ tile: town(x === end ? 82 : 45), x, y })
  }
  side(gate.from - 1, top + 1, -1, (y) => town(y === gate.from - 1 ? 71 : 56))
  rail(top, town(44))
  side(gate.to + 1, bottom - 1, 1, (y) => town(y === gate.to + 1 ? 47 : 56))
  rail(bottom, town(68))
  return { id: 'palisade', pieces, yard: { x: 2.5, y: 10.5 } }
})()

/** Where builders stand and materials lie for projects drawn outside building plots. */
export const PROJECT_YARDS: Record<string, { x: number; y: number; spread: number }> = {
  [MONUMENT.id]: { x: MONUMENT.x + 0.5, y: MONUMENT.y + 3, spread: 1.5 },
  [PIER.id]: { x: PIER.x - 1, y: PIER.y + 1, spread: 1 },
  [PALISADE.id]: { x: PALISADE.yard.x, y: PALISADE.yard.y + 1, spread: 1.5 },
  'bell-tower': { x: 41.5, y: 5.5, spread: 1 },
  granary: { x: 26.5, y: 21, spread: 1.5 },
}

export const PROJECT_SITES: Record<string, { x: number; y: number }> = {
  granary: { x: 25, y: 22 },
  /** Behind the monastery on the far bank, standing free so the bell is seen from both banks. */
  'bell-tower': { x: 41, y: 1 },
}

/** Ground of each village project; while it is being built, a click there calls citizens to work on it. */
export const PROJECT_AREAS: Record<string, TileArea[]> = {
  'tsar-bridge': [
    { x: 26, y: 9, width: 4, height: 3 },
    { x: RIVER_LEFT, y: ROAD_TOP, width: RIVER_RIGHT - RIVER_LEFT + 1, height: 2 },
  ],
  [MONUMENT.id]: [{ x: MONUMENT.x - 1, y: MONUMENT.y - 1, width: 3, height: 4 }],
  [PIER.id]: [{ x: PIER.x - 2, y: PIER.y - 1, width: PIER.length + 2, height: 3 }],
  [PALISADE.id]: [
    { x: 0, y: 9, width: 4, height: 3 },
    ...PALISADE.pieces.map((piece) => ({ x: piece.x, y: piece.y, width: 1, height: 1 })),
  ],
  granary: [{ x: 25, y: 21, width: 4, height: 4 }],
  'bell-tower': [{ x: 40, y: 0, width: 5, height: 6 }],
}

/** Deck of the Tsar Bridge: the only way across the river once it is finished. */
export const BRIDGE_DECK: TileArea = { x: RIVER_LEFT, y: ROAD_TOP, width: RIVER_RIGHT - RIVER_LEFT + 1, height: 2 }

/**
 * Why a walk cannot be made: the spot is in water, on a mountain or in the ravine, the river lies between and no
 * bridge crosses it, mountains wall the spot off, or the ravine lies between and no rope bridge spans it.
 */
export type WalkBlock = 'water' | 'river' | 'mountain' | 'ravine'

/** How many times longer a step through marsh takes than one over dry ground. */
const MARSH_COST = 3

/**
 * The way on foot between two points, in tiles: the turns to take, ending at `to`. Water, mountains, the ravine,
 * thickets and rockfalls stop walkers, except the deck of a finished bridge and the `crossings` of finished rope
 * bridges. A walker may still walk off the tile they stand on, and up to the thicket or rockfall they are sent to clear.
 */
export function findWay(
  terrain: Uint8Array,
  from: TilePoint,
  to: TilePoint,
  bridge: boolean,
  crossings: TileArea[] = [],
  seed = 1,
  opened: ReadonlySet<string> = new Set(),
): TilePoint[] | WalkBlock {
  const startX = Math.floor(from.x)
  const startY = Math.floor(from.y)
  const goalX = Math.floor(to.x)
  const goalY = Math.floor(to.y)
  const inVale = (x: number, y: number) => x >= MAP_LEFT && x < MAP_RIGHT && y >= MAP_TOP && y < MAP_BOTTOM
  if (!inVale(startX, startY) || !inVale(goalX, goalY)) return farWay(terrain, from, to, seed, opened, bridge, crossings)
  const inside = (x: number, y: number) => x >= MAP_LEFT && x < MAP_RIGHT && y >= MAP_TOP && y < MAP_BOTTOM
  const index = (x: number, y: number) => (y - MAP_TOP) * MAP_WIDTH + (x - MAP_LEFT)
  const onDeck = (x: number, y: number) =>
    x >= BRIDGE_DECK.x && x < BRIDGE_DECK.x + BRIDGE_DECK.width && y >= BRIDGE_DECK.y && y < BRIDGE_DECK.y + BRIDGE_DECK.height
  const onCrossing = (x: number, y: number) =>
    crossings.some((area) => x >= area.x && x < area.x + area.width && y >= area.y && y < area.y + area.height)
  const passable = (x: number, y: number, climbing: boolean, bridging: boolean) => {
    if (!inside(x, y)) return false
    if (x === startX && y === startY) return true
    const kind = terrain[index(x, y)]
    if (kind === Terrain.Thicket || kind === Terrain.Rockfall) return x === goalX && y === goalY
    if (kind === Terrain.Water) return bridge && onDeck(x, y)
    if (kind === Terrain.Ravine) return bridging || onCrossing(x, y)
    return climbing || kind !== Terrain.Mountain
  }
  const walkable = (x: number, y: number) => passable(x, y, false, false)
  const stepCost = (x: number, y: number) => (terrain[index(x, y)] === Terrain.Marsh ? MARSH_COST : 1)

  if (!walkable(goalX, goalY)) {
    const kind = inside(goalX, goalY) ? terrain[index(goalX, goalY)] : Terrain.Water
    return kind === Terrain.Mountain ? 'mountain' : kind === Terrain.Ravine ? 'ravine' : 'water'
  }
  // A straight line never cuts through marsh or water, including a corner the samples used to skip.
  const dry = (x: number, y: number) =>
    walkable(x, y) && (stepCost(x, y) === 1 || (x === startX && y === startY) || (x === goalX && y === goalY))
  const clear = (a: TilePoint, b: TilePoint) => {
    let x = Math.floor(a.x)
    let y = Math.floor(a.y)
    const endX = Math.floor(b.x)
    const endY = Math.floor(b.y)
    const dx = b.x - a.x
    const dy = b.y - a.y
    const stepX = Math.sign(dx)
    const stepY = Math.sign(dy)
    const tDeltaX = dx === 0 ? Number.POSITIVE_INFINITY : Math.abs(1 / dx)
    const tDeltaY = dy === 0 ? Number.POSITIVE_INFINITY : Math.abs(1 / dy)
    let tMaxX = dx === 0 ? Number.POSITIVE_INFINITY : (stepX > 0 ? x + 1 - a.x : a.x - x) * tDeltaX
    let tMaxY = dy === 0 ? Number.POSITIVE_INFINITY : (stepY > 0 ? y + 1 - a.y : a.y - y) * tDeltaY
    if (!dry(x, y)) return false
    for (let guard = 0; (x !== endX || y !== endY) && guard < MAP_WIDTH + MAP_HEIGHT; guard++) {
      if (tMaxX < tMaxY) {
        x += stepX
        tMaxX += tDeltaX
      } else if (tMaxY < tMaxX) {
        y += stepY
        tMaxY += tDeltaY
      } else {
        if (!dry(x + stepX, y) || !dry(x, y + stepY)) return false
        x += stepX
        y += stepY
        tMaxX += tDeltaX
        tMaxY += tDeltaY
      }
      if (!dry(x, y)) return false
    }
    return x === endX && y === endY
  }
  if (clear(from, to)) return [to]

  const start = index(startX, startY)
  const goal = index(goalX, goalY)
  const cameFrom = searchTiles(start, goal, walkable, stepCost)
  if (!cameFrom) {
    if (searchTiles(start, goal, (x, y) => passable(x, y, true, false), stepCost)) return 'mountain'
    return searchTiles(start, goal, (x, y) => passable(x, y, false, true), stepCost) ? 'ravine' : 'river'
  }

  const centres: TilePoint[] = []
  for (let node = cameFrom[goal]; node !== start && node !== -1; node = cameFrom[node]) {
    centres.unshift({ x: (node % MAP_WIDTH) + MAP_LEFT + 0.5, y: Math.floor(node / MAP_WIDTH) + MAP_TOP + 0.5 })
  }
  const points = [from, ...centres, to]
  // Straightens the tile steps: from each turn, head for the farthest point still in plain sight.
  const way: TilePoint[] = []
  let at = 0
  while (at < points.length - 1) {
    let next = points.length - 1
    while (next > at + 1 && !clear(points[at], points[next])) next--
    way.push(points[next])
    at = next
  }
  return way
}

/**
 * A* over tiles, eight ways; a diagonal step may not cut the corner of a blocked tile, and a step costs its length
 * times the `stepCost` (at least 1) of the tile it enters. Returns the tile each tile was reached from, or null when the
 * goal cannot be reached.
 */
function searchTiles(
  start: number,
  goal: number,
  walkable: (x: number, y: number) => boolean,
  stepCost: (x: number, y: number) => number,
): Int32Array | null {
  const goalX = (goal % MAP_WIDTH) + MAP_LEFT
  const goalY = Math.floor(goal / MAP_WIDTH) + MAP_TOP
  const index = (x: number, y: number) => (y - MAP_TOP) * MAP_WIDTH + (x - MAP_LEFT)
  const cost = new Float64Array(MAP_WIDTH * MAP_HEIGHT).fill(Infinity)
  const cameFrom = new Int32Array(MAP_WIDTH * MAP_HEIGHT).fill(-1)
  const closed = new Uint8Array(MAP_WIDTH * MAP_HEIGHT)
  const heuristic = (x: number, y: number) => {
    const dx = Math.abs(x - goalX)
    const dy = Math.abs(y - goalY)
    return Math.max(dx, dy) + (Math.SQRT2 - 1) * Math.min(dx, dy)
  }
  const open: Array<{ node: number; score: number }> = []
  const push = (node: number, score: number) => {
    open.push({ node, score })
    let child = open.length - 1
    while (child > 0) {
      const parent = (child - 1) >> 1
      if (open[parent].score <= open[child].score) break
      ;[open[parent], open[child]] = [open[child], open[parent]]
      child = parent
    }
  }
  const pop = () => {
    const top = open[0]
    const last = open.pop()!
    if (open.length > 0) {
      open[0] = last
      let parent = 0
      for (;;) {
        const left = parent * 2 + 1
        const right = left + 1
        let smallest = parent
        if (left < open.length && open[left].score < open[smallest].score) smallest = left
        if (right < open.length && open[right].score < open[smallest].score) smallest = right
        if (smallest === parent) break
        ;[open[parent], open[smallest]] = [open[smallest], open[parent]]
        parent = smallest
      }
    }
    return top.node
  }

  cost[start] = 0
  push(start, heuristic((start % MAP_WIDTH) + MAP_LEFT, Math.floor(start / MAP_WIDTH) + MAP_TOP))
  while (open.length > 0 && !closed[goal]) {
    const node = pop()
    if (closed[node]) continue
    closed[node] = 1
    const x = (node % MAP_WIDTH) + MAP_LEFT
    const y = Math.floor(node / MAP_WIDTH) + MAP_TOP
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        if ((dx === 0 && dy === 0) || !walkable(x + dx, y + dy)) continue
        if (dx !== 0 && dy !== 0 && (!walkable(x + dx, y) || !walkable(x, y + dy))) continue
        const next = index(x + dx, y + dy)
        const nextCost = cost[node] + (dx !== 0 && dy !== 0 ? Math.SQRT2 : 1) * stepCost(x + dx, y + dy)
        if (closed[next] || nextCost >= cost[next]) continue
        cost[next] = nextCost
        cameFrom[next] = node
        push(next, nextCost + heuristic(x + dx, y + dy))
      }
    }
  }
  return closed[goal] ? cameFrom : null
}

/** Natural places a click can send a citizen to; `spot` is the centre of the clicked tile. */
export type NatureKind = 'tree' | 'bush' | 'stone' | 'reeds' | 'water' | 'vein' | 'thicket' | 'rockfall' | 'cave' | 'coal-mine' | 'iron-mine' | 'moonstone-mine' | 'herbs' | 'flint' | 'clay' | 'salt' | 'resin' | 'copper'

/** Something on the map a citizen can be sent to work at. `area` is outlined while the choice is open. */
export type WorldTarget = { area: TileArea | null } & (
  | { kind: 'site'; plotId: string; building: string }
  | { kind: 'building'; plotId: string; building: string; storage: boolean }
  | { kind: 'project'; projectId: string }
  | { kind: 'campfire' }
  | { kind: 'animal'; prey: AnimalKind }
  | { kind: NatureKind; spot: TilePoint }
)

export const PROJECT_BUILDINGS: Record<string, TileRef[][]> = {
  granary: [
    [town(52), town(53), town(53), town(54)],
    [town(64), town(65), town(65), town(66)],
    [town(84), town(86), town(87), town(84)],
  ],
  'bell-tower': [
    [town(48), town(49), town(50)],
    [town(60), town(61), town(62)],
    [town(76), town(77), town(79)],
    [town(88), town(90), town(88)],
  ],
}

function random(seed: number) {
  let state = seed >>> 0
  return () => {
    state = (state * 1664525 + 1013904223) >>> 0
    return state / 4294967296
  }
}

export function seedFrom(text: string): number {
  let value = 2166136261
  for (let index = 0; index < text.length; index++) {
    value ^= text.charCodeAt(index)
    value = Math.imul(value, 16777619)
  }
  return value >>> 0
}

/** Smooth value noise in 0…1 with features about `scale` tiles wide. */
function valueNoise(seed: number, scale: number) {
  const corner = (x: number, y: number) => {
    // Integer hash with a full avalanche (MurmurHash3 finaliser), so neighbouring corners are unrelated.
    let value = (seed ^ Math.imul(x, 0x27d4eb2d) ^ Math.imul(y, 0x165667b1)) >>> 0
    value = Math.imul(value ^ (value >>> 16), 0x85ebca6b)
    value = Math.imul(value ^ (value >>> 13), 0xc2b2ae35)
    return ((value ^ (value >>> 16)) >>> 0) / 4294967296
  }
  const smooth = (t: number) => t * t * (3 - 2 * t)
  return (x: number, y: number) => {
    const fx = x / scale
    const fy = y / scale
    const x0 = Math.floor(fx)
    const y0 = Math.floor(fy)
    const tx = smooth(fx - x0)
    const ty = smooth(fy - y0)
    const top = corner(x0, y0) * (1 - tx) + corner(x0 + 1, y0) * tx
    const bottom = corner(x0, y0 + 1) * (1 - tx) + corner(x0 + 1, y0 + 1) * tx
    return top * (1 - ty) + bottom * ty
  }
}

const inCore = (x: number, y: number) => x >= 0 && x < CORE_WIDTH && y >= 0 && y < CORE_HEIGHT

/** Share of the ponds with marsh around them. */
const MARSH_PONDS = 0.5

/** Centre column of the river in a row: straight through the camp area, meandering beyond it. */
export function riverCenter(y: number, seed: number) {
  const straight = (RIVER_LEFT + RIVER_RIGHT) / 2
  const distance = y < 0 ? -y : y >= CORE_HEIGHT ? y - CORE_HEIGHT + 1 : 0
  if (distance === 0) return straight
  const phase = (seed % 1000) / 160
  const sway = Math.sin(distance * 0.11 + phase) * 7 + Math.sin(distance * 0.27 + phase * 2) * 2
  return straight + sway * Math.min(1, distance / 10)
}

const RIVER_WIDTH = 4

/** A river tile outside the camp picture. The camp picture's river is authored terrain. */
export function farWater(x: number, y: number, seed: number) {
  return farKind(x, y, seed) === 'water'
}

/** Stable 0…1 roll for one far tile, so ground and trees stay put between visits. */
function farRoll(seed: number, x: number, y: number) {
  let value = (seed ^ Math.imul(x | 0, 0x27d4eb2d) ^ Math.imul(y | 0, 0x165667b1)) >>> 0
  value = Math.imul(value ^ (value >>> 16), 0x85ebca6b)
  value = Math.imul(value ^ (value >>> 13), 0xc2b2ae35)
  return ((value ^ (value >>> 16)) >>> 0) / 4294967296
}

/**
 * Tiles from the campfire. A healthy walker takes 0.6 seconds a tile:
 * 300 is 3 minutes, 600 is 6 minutes, 2,000 is 20 minutes.
 */
function tilesFromCamp(x: number, y: number) {
  return Math.hypot(x + 0.5 - CAMPFIRE.x, y + 0.5 - CAMPFIRE.y)
}

/** Stone outcrops start past the camp meadow and then keep the same spacing. */
const STONE_OUTCROP_TILES = 80

function forestCover(seed: number, x: number, y: number) {
  return valueNoise(seed + 1, 9)(x, y) * 0.75 + valueNoise(seed + 2, 3)(x, y) * 0.25
}

/** A few stones on high ground, only once the walk has left the open country. */
function stoneOutcrop(x: number, y: number, forest: number, stone: number) {
  return tilesFromCamp(x, y) >= STONE_OUTCROP_TILES && stone > 0.9 && forest < 0.45
}

/**
 * The campfire stays open. Everywhere else the same groves run, inside the vale and past it.
 * `roll` is 0…1 for this tile.
 */
function wildTree(x: number, y: number, forest: number, roll: number) {
  if (tilesFromCamp(x, y) < 8) return false
  return forest > 0.64 ? roll < 0.18 : forest > 0.54 ? roll < 0.05 : roll < 0.012
}

/**
 * Ground outside the camp picture. Grass stays grass. Stone and trees use the same
 * rolls as the ground inside the picture, so the edge does not turn into empty land.
 */
export function farGroundTile(x: number, y: number, seed: number): TileRef {
  const kind = farKind(x, y, seed)
  if (kind === 'water') return battle(37)
  if (kind === 'mountain' || kind === 'rockfall') {
    const grass = farRoll(seed + 4, x, y)
    return town(grass < 0.06 ? 1 : grass < 0.09 ? 2 : 0)
  }
  const forest = forestCover(seed, x, y)
  const stone = valueNoise(seed + 3, 7)(x, y)
  if (stoneOutcrop(x, y, forest, stone)) return town(43)
  const roll = farRoll(seed + 4, x, y)
  return town(roll < 0.06 ? 1 : roll < 0.09 ? 2 : 0)
}

/** Whether a tree stands on a far-land tile. Same bands as the wild inside the vale. */
export function farHasTree(x: number, y: number, seed: number): boolean {
  const kind = farKind(x, y, seed)
  if (kind === 'water' || farBlocksKind(kind)) return false
  const forest = forestCover(seed, x, y)
  const stone = valueNoise(seed + 3, 7)(x, y)
  if (stoneOutcrop(x, y, forest, stone)) return false
  return wildTree(x, y, forest, farRoll(seed + 5, x, y))
}

/** What a far-land tile is, for overviews. A cleared thicket or rockfall is open grass. */
export function farTerrain(x: number, y: number, seed: number, opened?: { has(key: string): boolean }): Terrain {
  if (opened?.has(`${x},${y}`)) return Terrain.Grass
  const kind = farKind(x, y, seed)
  if (kind === 'water') return Terrain.Water
  if (kind === 'mountain') return Terrain.Mountain
  if (kind === 'rockfall') return Terrain.Rockfall
  if (kind === 'thicket') return Terrain.Thicket
  const forest = forestCover(seed, x, y)
  const stone = valueNoise(seed + 3, 7)(x, y)
  if (stoneOutcrop(x, y, forest, stone)) return Terrain.Stone
  return farHasTree(x, y, seed) ? Terrain.Tree : Terrain.Grass
}

/** A peak on every far mountain tile. The six mountain sprites, never the stone ground tile. */
export function farPeak(x: number, y: number, seed: number): TileRef | null {
  if (farKind(x, y, seed) !== 'mountain') return null
  const looks = [...MOUNTAIN_PEAKS, ...MOSSY_PEAKS]
  return looks[Math.floor(farRoll(seed + 6, x, y) * looks.length)]
}

/**
 * A walk that leaves the camp picture. Empty land is a straight line. Otherwise a capped route search finds the
 * way around mountains, thickets and water, and only its turns are kept. Matches the server's OverlandPath.
 */
function farWay(
  terrain: Uint8Array,
  from: TilePoint,
  to: TilePoint,
  seed: number,
  opened: ReadonlySet<string>,
  bridge: boolean,
  crossings: TileArea[],
): TilePoint[] | WalkBlock {
  const onLand = (x: number, y: number) =>
    x >= LAND_LEFT && x < LAND_LEFT + LAND_WIDTH && y >= LAND_TOP && y < LAND_TOP + LAND_HEIGHT
  const inVale = (x: number, y: number) => x >= MAP_LEFT && x < MAP_RIGHT && y >= MAP_TOP && y < MAP_BOTTOM
  const startX = Math.floor(from.x)
  const startY = Math.floor(from.y)
  const goalX = Math.floor(to.x)
  const goalY = Math.floor(to.y)
  const blocked = (x: number, y: number) => farBlocked(terrain, x, y, seed, goalX, goalY, opened, bridge, crossings)
  const stop = (x: number, y: number): WalkBlock => {
    if (!inVale(x, y)) return farKind(x, y, seed) === 'water' ? 'water' : 'mountain'
    const kind = terrain[(y - MAP_TOP) * MAP_WIDTH + (x - MAP_LEFT)]
    return kind === Terrain.Mountain || kind === Terrain.Thicket ? 'mountain' : kind === Terrain.Ravine ? 'ravine' : 'water'
  }
  if (!onLand(goalX, goalY)) return 'water'
  if (blocked(goalX, goalY)) return stop(goalX, goalY)
  const open = (x: number, y: number) => (x === startX && y === startY) || (onLand(x, y) && !blocked(x, y))
  const way = farRoute(from, to, open)
  if (way) return way
  const hit = firstHit(from, to, open)
  return stop(hit.x, hit.y)
}

function farBlocked(
  terrain: Uint8Array,
  x: number,
  y: number,
  seed: number,
  goalX: number,
  goalY: number,
  opened: ReadonlySet<string>,
  bridge: boolean,
  crossings: TileArea[],
) {
  if (x >= MAP_LEFT && x < MAP_RIGHT && y >= MAP_TOP && y < MAP_BOTTOM) {
    const kind = terrain[(y - MAP_TOP) * MAP_WIDTH + (x - MAP_LEFT)]
    if (x === goalX && y === goalY && (kind === Terrain.Thicket || kind === Terrain.Rockfall)) return false
    if (kind === Terrain.Water) {
      return !(bridge && x >= BRIDGE_DECK.x && x < BRIDGE_DECK.x + BRIDGE_DECK.width && y >= BRIDGE_DECK.y && y < BRIDGE_DECK.y + BRIDGE_DECK.height)
    }
    if (kind === Terrain.Ravine) {
      return !crossings.some((area) => x >= area.x && x < area.x + area.width && y >= area.y && y < area.y + area.height)
    }
    return kind === Terrain.Mountain || kind === Terrain.Thicket || kind === Terrain.Rockfall
  }
  if (opened.has(`${x},${y}`)) return false
  const kind = farKind(x, y, seed)
  if (x === goalX && y === goalY && (kind === 'thicket' || kind === 'rockfall')) return false
  return farBlocksKind(kind) || farWater(x, y, seed)
}

/**
 * The land around the first campfire. Clearings stay open for the camp, the road and the workplaces.
 * Woods and stones use the same rolls as the land beyond the picture. The ravine, the cave and the lookout
 * come from the server's `wilds`. `seed` is the village, so every viewer sees the same land.
 */
export function buildLayout(seed: number, wilds: Wilds | null): MapLayout {
  const next = random(seed)
  const detailNoise = valueNoise(seed + 2, 3)
  const stoneNoise = valueNoise(seed + 3, 7)
  const ground: TileRef[][] = []
  const decor: PlacedTile[] = []
  const bushes: MapLayout['bushes'] = []
  const reeds: MapLayout['reeds'] = []
  const ponds: MapLayout['ponds'] = []
  const mountains: MapLayout['mountains'] = []
  const terrain = new Uint8Array(MAP_WIDTH * MAP_HEIGHT)
  const index = (x: number, y: number) => (y - MAP_TOP) * MAP_WIDTH + (x - MAP_LEFT)
  const inside = (x: number, y: number) => x >= MAP_LEFT && x < MAP_RIGHT && y >= MAP_TOP && y < MAP_BOTTOM
  const occupied = new Set<number>()
  const mark = (x: number, y: number, width = 1, height = 1, kind?: Terrain) => {
    for (let row = y; row < y + height; row++) {
      for (let column = x; column < x + width; column++) {
        if (!inside(column, row)) continue
        occupied.add(index(column, row))
        if (kind !== undefined) terrain[index(column, row)] = kind
      }
    }
  }
  const setGround = (x: number, y: number, tile: TileRef) => {
    if (inside(x, y)) ground[y - MAP_TOP][x - MAP_LEFT] = tile
  }

  for (let y = MAP_TOP; y < MAP_BOTTOM; y++) {
    const row: TileRef[] = []
    for (let x = MAP_LEFT; x < MAP_RIGHT; x++) {
      const roll = next()
      row.push(town(roll < 0.06 ? 1 : roll < 0.09 ? 2 : 0))
    }
    ground.push(row)
  }

  // The cave mouth and the lookout stay. Hills, hedges and rockfalls do not stand around the camp.
  const trail: MapLayout['trail'] = []
  if (wilds) {
    for (const span of wilds.ravine) {
      mark(span.x, span.top - 1)
      mark(span.x, span.top, 1, span.height, Terrain.Ravine)
      mark(span.x, span.top + span.height)
    }
    const { caveX, caveY, lookoutX, lookoutY } = wilds
    mark(caveX, caveY, 1, 1, Terrain.Cave)
    mark(caveX - 1, caveY + 1, 3, 1)
    mark(lookoutX, lookoutY, 1, 1, Terrain.Lookout)
    mark(lookoutX, lookoutY + 1, 1, 1, Terrain.Trail)
    trail.push({ x: lookoutX, y: lookoutY + 1 })
    mark(lookoutX - 1, lookoutY + 2, 3, 1)
  }

  // The river runs the whole height of the map.
  for (let y = MAP_TOP; y < MAP_BOTTOM; y++) {
    const left = Math.round(riverCenter(y, seed) - 1.5)
    setGround(left, y, battle(36))
    setGround(left + 1, y, battle(37))
    setGround(left + 2, y, battle(37))
    setGround(left + 3, y, battle(38))
    mark(left, y, 4, 1, Terrain.Water)
    // Reeds grow on the west bank wherever the land is low.
    if (!inCore(left, y) && detailNoise(left, y) > 0.55 && !occupied.has(index(left - 1, y))) {
      mark(left - 1, y)
    }
  }

  // Ponds out in the wild, never in the camp area.
  for (let attempt = 0; attempt < 40 && ponds.length < 9; attempt++) {
    const x = MAP_LEFT + 4 + Math.floor(next() * (MAP_WIDTH - 8))
    const y = MAP_TOP + 4 + Math.floor(next() * (MAP_HEIGHT - 8))
    const radiusX = 2 + next() * 3.5
    const radiusY = 1.5 + next() * 2.5
    const clear = (cx: number, cy: number) => !inCore(cx, cy) && !occupied.has(index(cx, cy))
    let fits = true
    for (let row = Math.floor(y - radiusY - 1); row <= y + radiusY + 1 && fits; row++) {
      for (let column = Math.floor(x - radiusX - 1); column <= x + radiusX + 1 && fits; column++) {
        if (!inside(column, row) || !clear(column, row)) fits = false
      }
    }
    if (!fits) continue
    ponds.push({ x: x + 0.5, y: y + 0.5, radiusX, radiusY })
    for (let row = Math.floor(y - radiusY); row <= y + radiusY; row++) {
      for (let column = Math.floor(x - radiusX); column <= x + radiusX; column++) {
        if (((column - x) / radiusX) ** 2 + ((row - y) / radiusY) ** 2 <= 1) mark(column, row, 1, 1, Terrain.Water)
      }
    }
    // A ring of reeds around some ponds.
    if (next() < 0.6) {
      for (let angle = 0; angle < Math.PI * 2; angle += 0.5) {
        const rx = x + 0.5 + Math.cos(angle) * (radiusX + 0.6)
        const ry = y + 0.5 + Math.sin(angle) * (radiusY + 0.6)
        if (next() < 0.7) {
          reeds.push({ x: rx, y: ry })
          mark(Math.floor(rx), Math.floor(ry), 1, 1, Terrain.Reeds)
        }
      }
    }
  }

  // Marsh spreads around some of the ponds where the ground is low.
  const marsh: MapLayout['marsh'] = []
  for (const pond of ponds) {
    if (next() >= MARSH_PONDS) continue
    const reachX = pond.radiusX + 2.5
    const reachY = pond.radiusY + 2.5
    for (let row = Math.floor(pond.y - reachY); row <= pond.y + reachY; row++) {
      for (let column = Math.floor(pond.x - reachX); column <= pond.x + reachX; column++) {
        if (!inside(column, row) || inCore(column, row) || occupied.has(index(column, row))) continue
        if (((column + 0.5 - pond.x) / reachX) ** 2 + ((row + 0.5 - pond.y) / reachY) ** 2 > 1) continue
        if (detailNoise(column, row) < 0.4) continue
        marsh.push({ x: column, y: row })
        mark(column, row, 1, 1, Terrain.Marsh)
      }
    }
  }

  // The old berry rolls stay, so the ponds and the bank keep their places. The bushes themselves do not.
  for (let y = 16; y <= 20; y++) {
    for (let x = 1; x <= 7; x++) next()
  }

  // The old bank rolls stay, so the meadows beyond them keep their places. The reeds themselves went with the river.
  for (let y = 14; y <= 22; y++) {
    const pierRow = Math.abs(y - PIER.y) <= 1
    next()
    if (next() < 0.6 && !pierRow) {
      /* The bank reeds left with the river. The roll stays so the meadows keep their places. */
    }
    mark(RIVER_LEFT - 2, y, 2, 1)
  }

  // Camp clearing with the bare plot east of it, and the trail to the gate. Mirrors the server's bare ground.
  mark(12, 10, 7, 7, Terrain.Clearing)
  mark(MONUMENT.x - 1, MONUMENT.y - 1, 3, 4, Terrain.Clearing)
  mark(0, 12, 12, 3, Terrain.Clearing)
  mark(17, 12, PLOT_WIDTH, PLOT_HEIGHT, Terrain.Clearing)
  const monastery = MONASTERY.area
  mark(monastery.x, monastery.y, monastery.width, monastery.height, Terrain.Clearing)
  const bellTower = PROJECT_SITES['bell-tower']
  mark(bellTower.x - 3, bellTower.y - 1, 7, 6, Terrain.Clearing)
  mark(RIVER_RIGHT + 1, ROAD_TOP, MAP_RIGHT - RIVER_RIGHT - 1, 2, Terrain.Clearing)
  mark(VEIN_OUTCROP.x - 1, VEIN_OUTCROP.y - 1, VEIN_OUTCROP.width + 2, VEIN_OUTCROP.height + 2, Terrain.Clearing)
  const plantTree = (x: number, y: number) => {
    const look = TREE_TILES[Math.floor(next() * TREE_TILES.length)]
    // Trees of the forest area come from the server; the roll and the mark still happen so the wild keeps its look.
    if (inForestArea(x, y)) return mark(x, y)
    decor.push({ ...look, x, y })
    mark(x, y, 1, 1, Terrain.Tree)
  }

  // The same groves and outcrops as the land beyond the picture. Clearings stay bare.
  for (let y = MAP_TOP; y < MAP_BOTTOM; y++) {
    for (let x = MAP_LEFT; x < MAP_RIGHT; x++) {
      if (occupied.has(index(x, y))) continue
      const forest = forestCover(seed, x, y)
      const stone = stoneNoise(x, y)
      const roll = farRoll(seed + 5, x, y)
      if (stoneOutcrop(x, y, forest, stone)) {
        setGround(x, y, town(43))
        mark(x, y, 1, 1, Terrain.Stone)
      } else if (wildTree(x, y, forest, roll)) {
        plantTree(x, y)
      } else if (forest > 0.4 && forest < 0.5 && roll > 0.97) {
        bushes.push({ x: x + 0.5, y: y + 0.6 })
        mark(x, y, 1, 1, Terrain.Bush)
      }
    }
  }

  // The river and the ravine were part of the picture. The ground under them is grass, and it can be walked.
  if (wilds) {
    for (const span of wilds.ravine) {
      for (let row = span.top; row < span.top + span.height; row++) {
        if (inside(span.x, row) && terrain[index(span.x, row)] === Terrain.Ravine) terrain[index(span.x, row)] = 0
      }
    }
  }
  for (let y = MAP_TOP; y < MAP_BOTTOM; y++) {
    const left = Math.round(riverCenter(y, seed) - 1.5)
    for (let column = 0; column < RIVER_WIDTH; column++) {
      const x = left + column
      if (!inside(x, y) || terrain[index(x, y)] !== Terrain.Water) continue
      terrain[index(x, y)] = 0
      setGround(x, y, town(0))
    }
  }

  return { ground, decor, bushes, reeds, ponds, mountains, marsh, trail, terrain }
}

/** Villager sprites from Tiny Dungeon (the hooded monk, 111, is reserved for the elder). */
export const VILLAGER_SPRITES = [85, 86, 87, 88, 96, 97, 98, 99, 100, 112, 84]
export const PRIEST_SPRITE = 111

const HAMMER = factory(122)
const PICKAXE = town(115)
const STICK = farm(2)
const ROCKS = farm(89)
const REEDS = farm(65)
const POST = town(47)

export const RESOURCE_ICONS: Record<string, TileRef> = {
  Wood: town(106),
  Stone: ROCKS,
  Food: town(29),
  Coal: EXTRAS.coal,
  IronOre: dungeon(41),
  IronIngot: dungeon(102),
  Planks: town(73),
  Leather: dungeon(66),
  Gold: town(93),
  Chain: factory(115),
  Mechanism: factory(114),
  Remedy: dungeon(115),
  ResearchPoints: dungeon(65),
  Reeds: REEDS,
  Feathers: EXTRAS.feather,
  Flax: farm(80),
  Grain: farm(68),
  Porridge: town(29),
  Stew: town(29),
  VigorDrink: dungeon(116),
  Spear: dungeon(130),
  Bow: town(119),
  Arrows: EXTRAS.feather,
  Sword: dungeon(104),
  Shield: dungeon(101),
  LeatherArmor: dungeon(66),
  WoodenAxe: town(127),
  StoneAxe: town(127),
  IronAxe: town(127),
  SteelAxe: town(127),
  WoodenPickaxe: PICKAXE,
  StonePickaxe: PICKAXE,
  IronPickaxe: PICKAXE,
  SteelPickaxe: PICKAXE,
  Moonstone: rogue(563),
  Flint: ROCKS,
  Clay: farm(89),
  Brick: town(73),
  Salt: town(93),
  Herbs: farm(80),
  CopperOre: dungeon(41),
  Copper: dungeon(102),
  Resin: town(29),
  Parchment: rogue(896),
  TeleportScroll: rogue(898),
}

export const TASK_ICONS: Record<string, TileRef> = {
  'chop-wood': town(127),
  'quarry-stone': PICKAXE,
  forage: town(29),
  'cut-reeds': farm(88),
  hunt: town(118),
  'hunt-birds': town(119),
  'tend-fire': EXTRAS.campfire,
  'carry-to-storage': town(92),
  'track-game': town(118),
  'tend-fields': farm(66),
  'sow-turnips': farm(21),
  'sow-carrots': farm(9),
  'sow-cabbage': farm(57),
  'sow-flax': farm(10),
  'sow-rye': farm(69),
  'water-fields': farm(73),
  harvest: farm(11),
  'read-books': dungeon(63),
  'chat-tavern': town(107),
  'deliver-wood': RESOURCE_ICONS.Wood,
  'deliver-stone': RESOURCE_ICONS.Stone,
  'deliver-planks': RESOURCE_ICONS.Planks,
  'deliver-reeds': RESOURCE_ICONS.Reeds,
  'deliver-gold': RESOURCE_ICONS.Gold,
  fish: town(131),
  'measure-river': POST,
  'dig-gold': RESOURCE_ICONS.Gold,
  'clear-thicket': town(127),
  'break-rockfall': PICKAXE,
  'dig-cave': PICKAXE,
  'dig-coal': RESOURCE_ICONS.Coal,
  'dig-iron': RESOURCE_ICONS.IronOre,
  'deliver-iron': RESOURCE_ICONS.IronIngot,
  build: HAMMER,
  expedition: town(119),
  roam: town(29),
  'tell-stories': EXTRAS.campfire,
  'study-plants': farm(80),
  'explore-land': town(119),
  'research-technology': dungeon(63),
  'mine-coal': RESOURCE_ICONS.Coal,
  'mine-iron-ore': RESOURCE_ICONS.IronOre,
  'smelt-ingot': dungeon(117),
  'saw-planks': dungeon(118),
  'bake-bread': farm(125),
  research: dungeon(63),
  survey: town(83),
  supervise: factory(125),
  trade: farm(74),
  perform: town(95),
  patrol: dungeon(104),
  'mine-gold': RESOURCE_ICONS.Gold,
  'forge-chain': RESOURCE_ICONS.Chain,
  'build-mechanism': RESOURCE_ICONS.Mechanism,
  explore: town(119),
  design: dungeon(65),
  'brew-remedy': dungeon(115),
  'cook-porridge': RESOURCE_ICONS.Food,
  'cook-stew': RESOURCE_ICONS.Food,
  'brew-vigor': RESOURCE_ICONS.VigorDrink,
  'lead-crew': factory(120),
  negotiate: battle(196),
  command: battle(70),
  'keep-watch': dungeon(101),
  repair: HAMMER,
  'make-poultice': RESOURCE_ICONS.Remedy,
  'make-spears': RESOURCE_ICONS.Spear,
  'sew-armor': RESOURCE_ICONS.LeatherArmor,
  'make-bows': RESOURCE_ICONS.Bow,
  'make-arrows': RESOURCE_ICONS.Arrows,
  'forge-sword': RESOURCE_ICONS.Sword,
  'sharpen-sword': RESOURCE_ICONS.Sword,
  'sharpen-spear': RESOURCE_ICONS.Spear,
  'sharpen-arrows': RESOURCE_ICONS.Arrows,
  'make-shields': RESOURCE_ICONS.Shield,
  'make-wooden-axe': RESOURCE_ICONS.WoodenAxe,
  'make-wooden-pickaxe': RESOURCE_ICONS.WoodenPickaxe,
  'make-stone-axe': RESOURCE_ICONS.StoneAxe,
  'make-stone-pickaxe': RESOURCE_ICONS.StonePickaxe,
  'forge-iron-axe': RESOURCE_ICONS.IronAxe,
  'forge-iron-pickaxe': RESOURCE_ICONS.IronPickaxe,
  'forge-steel-axe': RESOURCE_ICONS.SteelAxe,
  'forge-steel-pickaxe': RESOURCE_ICONS.SteelPickaxe,
  'bring-sticks': STICK,
  'bring-logs': RESOURCE_ICONS.Wood,
  'bring-stones': RESOURCE_ICONS.Stone,
  'bring-reeds': RESOURCE_ICONS.Reeds,
  'bring-hides': RESOURCE_ICONS.Leather,
  'bring-planks': RESOURCE_ICONS.Planks,
  'fell-trees': town(127),
  'clear-ground': farm(86),
  'mark-outline': POST,
  'lean-poles': STICK,
  'lay-cover': REEDS,
  'stack-pile': RESOURCE_ICONS.Wood,
  'lay-ring': farm(77),
  'tie-rack': POST,
  'lay-stones': farm(77),
  'hew-beams': town(127),
  'raise-posts': POST,
  'tie-frame': town(44),
  'mix-clay': farm(124),
  'bundle-thatch': farm(68),
  'thatch-roof': farm(97),
  'plough-field': town(129),
  'dig-shaft': PICKAXE,
  'build-forge': dungeon(74),
  'build-oven': farm(125),
  'lay-floor': RESOURCE_ICONS.Planks,
  'stretch-ropes': REEDS,
  'lay-deck': RESOURCE_ICONS.Planks,
  'lend-a-hand': HAMMER,
  'chip-moonstone': RESOURCE_ICONS.Moonstone,
  'gather-herbs': RESOURCE_ICONS.Herbs,
  'gather-flint': RESOURCE_ICONS.Flint,
  'dig-clay': RESOURCE_ICONS.Clay,
  'scrape-salt': RESOURCE_ICONS.Salt,
  'tap-resin': RESOURCE_ICONS.Resin,
  'dig-copper': RESOURCE_ICONS.CopperOre,
  'knapp-arrows': RESOURCE_ICONS.Arrows,
  'pitch-bow': RESOURCE_ICONS.Bow,
  'lay-bricks': RESOURCE_ICONS.Brick,
  'fire-bricks': RESOURCE_ICONS.Brick,
  'smelt-copper': RESOURCE_ICONS.Copper,
  'wire-mechanism': RESOURCE_ICONS.Mechanism,
  'brew-herbs': RESOURCE_ICONS.Remedy,
  'salt-food': RESOURCE_ICONS.Food,
  'scrape-parchment': RESOURCE_ICONS.Parchment,
  'write-teleport-scroll': RESOURCE_ICONS.TeleportScroll,
  'bring-moonstones': RESOURCE_ICONS.Moonstone,
  'carve-runes': rogue(900),
}

/** Icon for a task or a construction job. Labor jobs without their own icon show a hammer. */
export const taskIcon = (id: string) => TASK_ICONS[id] ?? HAMMER

/** The river no longer divides the land, so a spot on it is not waiting for the bridge. */
export function canReachBank(village: VillageState, point: TilePoint) {
  return Boolean(village) && Number.isFinite(point.x + point.y)
}
