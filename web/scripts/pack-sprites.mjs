// Packs the game-size exports of art/generated into one atlas for PixiJS: src/assets/sprites.png and sprites.json.
// Frames are named after their export file, without the folder, the extension and a trailing "-v2".
// An export with numbered frames beside it ("name-frame-1.png", ...) is packed as those frames and listed as an animation.
// The atlas holds every frame at twice its size in world pixels, from the "name-2x.png" files of reshrink-sprites.mjs.
// Run with `npm run sprites` after adding or regenerating art.
import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { basename, dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const here = dirname(fileURLToPath(import.meta.url))
const art = resolve(here, '../../art/generated')
const output = resolve(here, '../src/assets')
const CATALOGS = ['catalog.json', 'construction-catalog.json', 'pond-catalog.json', 'scenery-variants-catalog.json']
/** Citizens, the Tsar and the Elder wait for skins that keep the recolouring of the character creator. */
const SKIPPED = /^character-(citizen|tsar|elder)/
const ATLAS_WIDTH = 1024
/** Atlas pixels per world pixel. */
const SCALE = 2
/** Transparent pixels between frames, so a zoomed view never samples a neighbour. */
const GAP = 2

const frameName = (file) => basename(file, '.png').replace(/-v2$/, '')

const files = new Map()
const animations = {}
for (const catalog of CATALOGS) {
  for (const entry of JSON.parse(readFileSync(join(art, catalog), 'utf8'))) {
    if (!entry.file || SKIPPED.test(frameName(entry.file))) continue
    const strip = entry.file.replace(/\.png$/, '')
    if (existsSync(join(art, `${strip}-frame-1.png`))) {
      const frames = []
      for (let index = 1; existsSync(join(art, `${strip}-frame-${index}.png`)); index++) {
        frames.push(`${strip}-frame-${index}.png`)
      }
      animations[frameName(entry.file)] = frames.map(frameName)
      for (const frame of frames) files.set(frameName(frame), frame)
    } else {
      files.set(frameName(entry.file), entry.file)
    }
  }
}

const images = await Promise.all(
  [...files].map(async ([name, file]) => {
    const doubled = file.replace(/\.png$/, '-2x.png')
    if (!existsSync(join(art, doubled))) throw new Error(`${doubled} is missing: run node scripts/reshrink-sprites.mjs`)
    const { data, info } = await sharp(join(art, doubled)).ensureAlpha().raw().toBuffer({ resolveWithObject: true })
    return { name, data, width: info.width, height: info.height }
  }),
)

// Shelves: tallest first, left to right, a new shelf when the row is full.
images.sort((a, b) => b.height - a.height || b.width - a.width || a.name.localeCompare(b.name))
let x = 0
let y = 0
let shelf = 0
for (const image of images) {
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
for (const image of [...images].sort((a, b) => a.name.localeCompare(b.name))) {
  const size = { w: image.width, h: image.height }
  frames[image.name] = { frame: { x: image.x, y: image.y, ...size }, sourceSize: size, spriteSourceSize: { x: 0, y: 0, ...size } }
}

await sharp({ create: { width: ATLAS_WIDTH, height, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
  .composite(images.map((image) => ({ input: image.data, raw: { width: image.width, height: image.height, channels: 4 }, left: image.x, top: image.y })))
  .png({ compressionLevel: 9, palette: false })
  .toFile(join(output, 'sprites.png'))

const atlas = { frames, animations, meta: { image: 'sprites.png', format: 'RGBA8888', size: { w: ATLAS_WIDTH, h: height }, scale: SCALE } }
writeFileSync(join(output, 'sprites.json'), `${JSON.stringify(atlas, null, 1)}\n`)
console.log(`${images.length} frames, ${Object.keys(animations).length} animations, ${ATLAS_WIDTH}x${height}`)
