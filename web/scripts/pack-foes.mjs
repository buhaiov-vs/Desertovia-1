// Packs the chosen frames of the Craftpix archives in art/archieves into one atlas for PixiJS:
// src/assets/foes/foes.png and foes.json. The archives are not in git; run `npm run foes` where they are unpacked.
// Each entry is one animation. A sheet is cut into `cell`-sized frames; `row` picks one facing of a
// four-facing sheet, `rows: true` reads every row left to right, and `frames` keeps the first N. Without `cell`
// the whole file is one frame. Frames of one animation are cropped to their shared visible box, so they stay
// aligned, and keep the archive's own pixels: the game scales them when it draws them.
// The anchor is the middle of the cell at the bottom of the visible box, where the figure stands.
import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const here = dirname(fileURLToPath(import.meta.url))
const archive = resolve(here, '../../art/archieves')
const output = resolve(here, '../src/assets/foes')
const ATLAS_WIDTH = 2048
/** Transparent pixels between frames, so a zoomed view never samples a neighbour. */
const GAP = 2
/** Pixels fainter than this do not count toward the visible box. */
const VISIBLE_ALPHA = 8

const SLIMES = 'craftpix-net-424234-free-slime-sprite-sheets-pixel-art'
const BLOBS = 'craftpix-net-788364-free-slime-mobs-pixel-art-top-down-sprite-pack/PNG'
const ROGUE = 'craftpix-net-436971-free-top-down-roguelike-game-kit-pixel-art/3 Dungeon Enemies'
const ISLAND = 'craftpix-net-351300-free-island-adventure-pixel-top-down-minigame-kit/Characters'
const FANTASY = 'craftpix-net-339194-free-fantasy-enemies-pixel-art-sprite-pack'
const ORCS = 'craftpix-net-363992-free-top-down-orc-game-character-pixel-art/PNG'
const BIG_ORCS = 'craftpix-net-100504-free-orc-sprite-sheets-pixel-art/Craftpix_Orc'
const VAMPIRES = 'craftpix-net-208004-free-vampire-4-direction-pixel-character-sprite-pack/PNG'
const FORTRESS = 'craftpix-net-749505-free-medieval-fortress-top-down-pixel-minigame-kit'
const HUNT = 'craftpix-net-789196-free-top-down-hunt-animals-pixel-sprite-pack/PNG/With_Shadow'
const UNDEAD = 'craftpix-net-695666-free-undead-tileset-top-down-pixel-art/PNG'
const BUSHES = 'craftpix-net-141354-free-top-down-bushes-pixel-art/PNG/Assets'
const CRYSTALS = 'craftpix-net-106469-top-down-crystals-pixel-art/PNG/Assets'
const CURSED = 'craftpix-net-958568-free-cursed-land-top-down-pixel-art-tileset/PNG/Objects_separetely'
const CAVE = 'craftpix-net-266622-free-top-down-pixel-art-cave-objects/PNG/Objects_separately'
const ROCKY = 'craftpix-net-639143-free-rocky-area-objects-pixel-art/PNG/Objects_separately'
const RUINS = 'craftpix-net-934618-free-top-down-ruins-pixel-art/PNG/Assets'

/** Four-facing top-down sheets: the row whose figure looks right, like every other figure in the game. */
const SIDE_ROW = 2

const ENTRIES = [
  // Foes of the dens that had no picture.
  { name: 'slime-blue', file: `${SLIMES}/Blue_Slime/Idle.png`, cell: [128, 128] },
  { name: 'slime-green', file: `${SLIMES}/Green_Slime/Idle.png`, cell: [128, 128] },
  { name: 'slime-red', file: `${SLIMES}/Red_Slime/Idle.png`, cell: [128, 128] },
  { name: 'goblin-small', file: `${ROGUE}/2/S_Idle.png`, cell: [32, 32] },
  { name: 'goblin-club', file: `${ISLAND}/Character_7/Idle.png`, cell: [32, 32] },
  { name: 'orc-hooded', file: `${ISLAND}/Character_9/Idle.png`, cell: [32, 32] },
  { name: 'scarecrow', file: `${ISLAND}/Character_8/Idle.png`, cell: [32, 32] },
  { name: 'goblin-sword', file: `${ROGUE}/3/S_Idle.png`, cell: [32, 32] },
  { name: 'goblin-banner', file: `${ROGUE}/4/S_Idle.png`, cell: [32, 32] },
  { name: 'poison-plant', file: `${FANTASY}/Plent/Idle.png`, cell: [128, 128] },
  { name: 'slime-flaming', file: `${BLOBS}/Slime3/With_shadow/Slime3_Idle_with_shadow.png`, cell: [64, 64], row: SIDE_ROW },
  { name: 'orc-axe', file: `${ORCS}/Orc1/With_shadow/orc1_idle_with_shadow.png`, cell: [64, 64], row: SIDE_ROW },
  { name: 'orc-warrior', file: `${BIG_ORCS}/Orc_Warrior/Idle.png`, cell: [96, 96] },
  { name: 'vampire', file: `${VAMPIRES}/Vampires1/With_shadow/Vampires1_Idle_with_shadow.png`, cell: [64, 64], row: SIDE_ROW },
  { name: 'orc-berserk', file: `${BIG_ORCS}/Orc_Berserk/Idle.png`, cell: [96, 96] },
  { name: 'orc-shaman', file: `${BIG_ORCS}/Orc_Shaman/Idle.png`, cell: [96, 96] },
  { name: 'vampire-winged', file: `${VAMPIRES}/Vampires3/With_shadow/Vampires3_Idle_with_shadow.png`, cell: [64, 64], row: SIDE_ROW },
  { name: 'orc-swords', file: `${ORCS}/Orc2/With_shadow/orc2_idle_with_shadow.png`, cell: [64, 64], row: SIDE_ROW },
  { name: 'fire-spirit', file: `${FANTASY}/Fire_Spirit/Idle.png`, cell: [128, 128] },
  { name: 'orc-armored', file: `${ORCS}/Orc3/With_shadow/orc3_idle_with_shadow.png`, cell: [64, 64], row: SIDE_ROW },
  ...[1, 2, 3, 4, 5, 6, 7, 8].map((knight) => ({
    name: `knight-${knight}`,
    file: `${FORTRESS}/Characters/Character_${knight}/Idle.png`,
    cell: [32, 32],
  })),
  { name: 'lich', file: `${UNDEAD}/Animation5.png`, cell: [96, 112], rows: true },
  // Enemies that already fight, now drawn from the archives.
  { name: 'rat', file: `${ROGUE}/1/S_Idle.png`, cell: [32, 32] },
  { name: 'rat-walk', file: `${ROGUE}/1/S_Walk.png`, cell: [32, 32] },
  { name: 'boar', file: `${HUNT}/Boar/Boar_Idle_with_shadow.png`, cell: [32, 32], row: SIDE_ROW },
  { name: 'boar-walk', file: `${HUNT}/Boar/Boar_Walk_with_shadow.png`, cell: [32, 32], row: SIDE_ROW },
  { name: 'skeleton', file: `${FANTASY}/Skeleton/Idle.png`, cell: [128, 128] },
  { name: 'skeleton-walk', file: `${FANTASY}/Skeleton/Walk.png`, cell: [128, 128] },
  // One prop on each den, from the area of that foe.
  { name: 'den-1', file: `${BUSHES}/Fern1_1.png` },
  { name: 'den-2', file: `${ROCKY}/Cave_entrance1_ground_shadow.png` },
  { name: 'den-3', file: `${ROCKY}/Cave_entrance2_ground_shadow.png` },
  { name: 'den-4', file: `${BUSHES}/Bush_simple1_1.png` },
  { name: 'den-5', file: `${ROCKY}/Cave_entrance3_grass_shadow.png` },
  { name: 'den-6', file: `${ROCKY}/Oval_rock1_grass_shadow.png` },
  { name: 'den-7', file: `${FORTRESS}/Tileset/Fire.png`, cell: [32, 32] },
  { name: 'den-8', file: `${BUSHES}/Autumn_bush1.png` },
  { name: 'den-9', file: `${BUSHES}/Bush_red_flowers1.png` },
  { name: 'den-10', file: `${UNDEAD}/Objects_separately/Grave_shadow1_5.png` },
  { name: 'den-11', file: `${RUINS}/Brown_ruins1.png` },
  { name: 'den-12', file: `${RUINS}/Brown_ruins3.png` },
  { name: 'den-13', file: `${UNDEAD}/Objects_separately/Dead_tree_shadow1_1.png` },
  { name: 'den-14', file: `${CURSED}/Many_eyes_plant_shadow1_1.png` },
  { name: 'den-15', file: `${CRYSTALS}/Red_crystal1.png` },
  { name: 'den-16', file: `${ROCKY}/Yurt1_ground_shadow.png` },
  { name: 'den-17', file: `${ROCKY}/Rock_statue_deer_grass_shadow.png` },
  { name: 'den-18', file: `${ROCKY}/Yurt2_ground_shadow.png` },
  { name: 'den-19', file: `${RUINS}/Blue-gray_ruins1.png` },
  { name: 'den-20', file: `${ROCKY}/Dragon_bones_body_ground_shadow.png` },
  { name: 'den-21', file: `${CAVE}/128/Dark_totem_dark_shadow2.png` },
  { name: 'den-22', file: `${RUINS}/White_ruins1.png` },
  { name: 'den-23', file: `${RUINS}/Brown-gray_ruins1.png` },
  { name: 'den-24', file: `${UNDEAD}/Objects_separately/Scull_door_shadow1.png` },
  { name: 'den-25', file: `${CAVE}/128/Gates_dark_shadow3.png` },
  { name: 'den-26', file: `${UNDEAD}/Objects_separately/Pile_sculls_shadow1.png` },
  { name: 'den-27', file: `${UNDEAD}/Objects_separately/Ruin_shadow1_1.png` },
  { name: 'den-28', file: `${FORTRESS}/Tileset/Gate.png`, cell: [80, 80], frames: 1 },
  { name: 'den-29', file: `${UNDEAD}/Objects_separately/Lich_shadow1.png` },
  { name: 'den-30', file: `${UNDEAD}/Animation4.png`, cell: [80, 112], rows: true },
]

async function cut(entry) {
  const { data, info } = await sharp(join(archive, entry.file)).ensureAlpha().raw().toBuffer({ resolveWithObject: true })
  const [cellWidth, cellHeight] = entry.cell ?? [info.width, info.height]
  const columns = Math.floor(info.width / cellWidth)
  const rows = entry.rows ? Math.floor(info.height / cellHeight) : 1
  const firstRow = entry.row ?? 0
  if ((firstRow + rows) * cellHeight > info.height) throw new Error(`${entry.name}: row ${firstRow} is outside ${entry.file}`)
  let cells = []
  for (let row = firstRow; row < firstRow + rows; row++) for (let column = 0; column < columns; column++) cells.push({ left: column * cellWidth, top: row * cellHeight })
  const visible = (cell, x, y) => data[((cell.top + y) * info.width + cell.left + x) * 4 + 3] > VISIBLE_ALPHA
  const empty = (cell) => {
    for (let y = 0; y < cellHeight; y++) for (let x = 0; x < cellWidth; x++) if (visible(cell, x, y)) return false
    return true
  }
  cells = cells.filter((cell) => !empty(cell))
  if (entry.frames) cells = cells.slice(0, entry.frames)
  if (cells.length === 0) throw new Error(`${entry.name}: no visible frame in ${entry.file}`)

  let minX = cellWidth, minY = cellHeight, maxX = -1, maxY = -1
  for (const cell of cells)
    for (let y = 0; y < cellHeight; y++)
      for (let x = 0; x < cellWidth; x++)
        if (visible(cell, x, y)) {
          minX = Math.min(minX, x)
          maxX = Math.max(maxX, x)
          minY = Math.min(minY, y)
          maxY = Math.max(maxY, y)
        }
  const width = maxX - minX + 1
  const height = maxY - minY + 1
  const anchor = { x: Math.min(1, Math.max(0, (cellWidth / 2 - minX) / width)), y: 1 }
  return cells.map((cell, index) => {
    const pixels = Buffer.alloc(width * height * 4)
    for (let y = 0; y < height; y++) {
      const from = ((cell.top + minY + y) * info.width + cell.left + minX) * 4
      data.copy(pixels, y * width * 4, from, from + width * 4)
    }
    return { name: `${entry.name}-${index + 1}`, animation: entry.name, data: pixels, width, height, anchor }
  })
}

const images = (await Promise.all(ENTRIES.map(cut))).flat()

// Shelves: tallest first, left to right, a new shelf when the row is full.
const placed = [...images].sort((a, b) => b.height - a.height || b.width - a.width || a.name.localeCompare(b.name))
let x = 0
let y = 0
let shelf = 0
for (const image of placed) {
  if (x + image.width > ATLAS_WIDTH) {
    x = 0
    y += shelf + GAP
    shelf = 0
  }
  image.x = x
  image.y = y
  x += image.width + GAP
  shelf = Math.max(shelf, image.height)
}
const height = y + shelf

const frames = {}
const animations = {}
for (const image of images) {
  const size = { w: image.width, h: image.height }
  frames[image.name] = { frame: { x: image.x, y: image.y, ...size }, sourceSize: size, spriteSourceSize: { x: 0, y: 0, ...size }, anchor: image.anchor }
  ;(animations[image.animation] ??= []).push(image.name)
}

mkdirSync(output, { recursive: true })
await sharp({ create: { width: ATLAS_WIDTH, height, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
  .composite(images.map((image) => ({ input: image.data, raw: { width: image.width, height: image.height, channels: 4 }, left: image.x, top: image.y })))
  .png({ compressionLevel: 9, palette: false })
  .toFile(join(output, 'foes.png'))

const atlas = { frames, animations, meta: { image: 'foes.png', format: 'RGBA8888', size: { w: ATLAS_WIDTH, h: height }, scale: 1 } }
writeFileSync(join(output, 'foes.json'), `${JSON.stringify(atlas, null, 1)}\n`)
console.log(`${images.length} frames, ${Object.keys(animations).length} animations, ${ATLAS_WIDTH}x${height}`)
