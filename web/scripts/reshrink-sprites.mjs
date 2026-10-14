// Rebuilds the game-size exports of art/generated at twice their size, as "name-2x.png" beside each export.
// The generated sources are drawn on a pixel grid of their own, often coarser than the export size, and shrinking
// them straight to the export size drops whole rows and columns. This finds the grid of each source, reads one
// colour per grid cell, and stretches that native image into the box the subject fills in the export, doubled.
// Run with `node scripts/reshrink-sprites.mjs [name filter]`, then `npm run sprites`.
import { existsSync, readFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const here = dirname(fileURLToPath(import.meta.url))
const art = resolve(here, '../../art/generated')
const CATALOGS = ['catalog.json', 'construction-catalog.json', 'pond-catalog.json', 'scenery-variants-catalog.json']
const SKIPPED = /^character-(citizen|tsar|elder)/
const SCALE = 2
/** Grid cells from 6 to 128 source pixels: art from about 170 down to 8 cells across a 1024 pixel source. */
const MIN_CELL = 6
const MAX_CELL = 128
const exports = new Set()
for (const catalog of CATALOGS) {
  for (const entry of JSON.parse(readFileSync(join(art, catalog), 'utf8'))) {
    if (!entry.file) continue
    const strip = entry.file.replace(/\.png$/, '')
    if (SKIPPED.test(strip.split('/').pop())) continue
    if (existsSync(join(art, `${strip}-frame-1.png`))) {
      for (let index = 1; existsSync(join(art, `${strip}-frame-${index}.png`)); index++) exports.add(`${strip}-frame-${index}`)
    } else {
      exports.add(strip)
    }
  }
}

const load = async (file) => {
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true })
  return { data, width: info.width, height: info.height }
}

const opaque = (image, x, y) => image.data[(y * image.width + x) * 4 + 3] >= 128

function bounds(image) {
  let left = image.width
  let top = image.height
  let right = -1
  let bottom = -1
  for (let y = 0; y < image.height; y++) {
    for (let x = 0; x < image.width; x++) {
      if (!opaque(image, x, y)) continue
      left = Math.min(left, x)
      right = Math.max(right, x)
      top = Math.min(top, y)
      bottom = Math.max(bottom, y)
    }
  }
  return right < 0 ? null : { left, top, width: right - left + 1, height: bottom - top + 1 }
}

function differs(image, a, b) {
  const d = image.data
  const alphaA = d[a + 3] >= 128
  if (alphaA !== d[b + 3] >= 128) return true
  if (!alphaA) return false
  return Math.abs(d[a] - d[b]) + Math.abs(d[a + 1] - d[b + 1]) + Math.abs(d[a + 2] - d[b + 2]) > 48
}

/** How many colour changes sit between pixel i - 1 and i along one axis, counted across the subject. */
function edgeProfile(image, box, horizontal) {
  const length = horizontal ? image.width : image.height
  const profile = new Float64Array(length + 1)
  const [from, to] = horizontal ? [box.top, box.top + box.height] : [box.left, box.left + box.width]
  for (let i = 1; i < length; i++) {
    let count = 0
    for (let j = from; j < to; j++) {
      const [x, y, px, py] = horizontal ? [i, j, i - 1, j] : [j, i, j, i - 1]
      if (differs(image, (y * image.width + x) * 4, (py * image.width + px) * 4)) count++
    }
    profile[i] = count
  }
  return profile
}

/**
 * Grid lines from `start` to `end`, one cell apart, each pulled onto the strongest colour change near it.
 * The generated grids drift by a few pixels across an image, so a single fixed spacing would slide off them.
 */
function gridLines(profile, start, end, cell) {
  const lines = [start]
  const reach = Math.max(1, Math.round(cell * 0.3))
  const threshold = Math.max(...profile.slice(start, end + 1)) * 0.15
  while (end - lines[lines.length - 1] > cell * 1.5) {
    const expected = Math.round(lines[lines.length - 1] + cell)
    let best = expected
    for (let i = expected - reach; i <= expected + reach; i++) {
      if (profile[i] > profile[best] || (profile[i] === profile[best] && Math.abs(i - expected) < Math.abs(best - expected))) best = i
    }
    lines.push(profile[best] >= threshold ? best : expected)
  }
  lines.push(end)
  return lines
}

/** About one cell of the source grid: grids are square, so a side measuring about twice the other has doubled runs. */
function cellSize(runX, runY) {
  const ratio = Math.max(runX, runY) / Math.min(runX, runY)
  return ratio > 1.4 ? Math.min(runX, runY) : (runX + runY) / 2
}

/** Lengths of the runs of one colour along rows (or columns) of the subject, a histogram by length. */
function runLengths(image, box, horizontal) {
  const histogram = new Float64Array(MAX_CELL * 4)
  const [outer, inner] = horizontal ? [box.height, box.width] : [box.width, box.height]
  for (let o = 0; o < outer; o++) {
    let length = 1
    for (let i = 1; i <= inner; i++) {
      const [x, y, px, py] = horizontal ? [box.left + i, box.top + o, box.left + i - 1, box.top + o] : [box.left + o, box.top + i, box.left + o, box.top + i - 1]
      const ended = i === inner || differs(image, (y * image.width + x) * 4, (py * image.width + px) * 4)
      if (!ended) {
        length++
        continue
      }
      if (opaque(image, px, py) && length < histogram.length) histogram[length]++
      length = 1
    }
  }
  return histogram
}

/** The run length that turns up most often, about one cell of the source grid. */
function commonRun(histogram) {
  let best = { cell: MIN_CELL, weight: 0 }
  for (let cell = MIN_CELL; cell <= MAX_CELL; cell++) {
    const reach = Math.max(1, Math.round(cell * 0.06))
    let weight = 0
    for (let i = cell - reach; i <= cell + reach; i++) weight += histogram[i]
    weight /= 2 * reach + 1
    if (weight > best.weight) best = { cell, weight }
  }
  let sum = 0
  let count = 0
  const reach = Math.max(1, Math.round(best.cell * 0.06))
  for (let i = best.cell - reach; i <= best.cell + reach; i++) {
    sum += i * histogram[i]
    count += histogram[i]
  }
  return sum / count
}

/** One colour per grid cell: the median of the opaque pixels in the middle of the cell, or nothing. */
function sampleGrid(image, linesX, linesY) {
  const columns = linesX.length - 1
  const rows = linesY.length - 1
  const native = { width: columns, height: rows, data: Buffer.alloc(columns * rows * 4) }
  const middle = (lines, index) => {
    const span = lines[index + 1] - lines[index]
    return [Math.round(lines[index] + span * 0.3), Math.max(Math.round(lines[index] + span * 0.3), Math.round(lines[index + 1] - span * 0.3) - 1)]
  }
  for (let row = 0; row < rows; row++) {
    for (let column = 0; column < columns; column++) {
      const [x0, x1] = middle(linesX, column)
      const [y0, y1] = middle(linesY, row)
      const channels = [[], [], []]
      let total = 0
      for (let y = y0; y <= y1; y++) {
        for (let x = x0; x <= x1; x++) {
          total++
          if (!opaque(image, x, y)) continue
          const offset = (y * image.width + x) * 4
          for (let c = 0; c < 3; c++) channels[c].push(image.data[offset + c])
        }
      }
      if (total === 0 || channels[0].length * 2 < total) continue
      const offset = (row * columns + column) * 4
      for (let c = 0; c < 3; c++) {
        const values = channels[c].sort((a, b) => a - b)
        native.data[offset + c] = values[values.length >> 1]
      }
      native.data[offset + 3] = 255
    }
  }
  return native
}

/** The native image cropped to what it draws. */
function trim(native) {
  const box = bounds(native)
  if (!box) return native
  const data = Buffer.alloc(box.width * box.height * 4)
  for (let y = 0; y < box.height; y++) {
    native.data.copy(data, y * box.width * 4, ((box.top + y) * native.width + box.left) * 4, ((box.top + y) * native.width + box.left + box.width) * 4)
  }
  return { width: box.width, height: box.height, data }
}

const report = []
const filter = process.argv[2] ?? ''
for (const strip of [...exports].filter((name) => name.includes(filter)).sort()) {
  const exportFile = join(art, `${strip}.png`)
  const sourceFile = join(art, `${strip}-source.png`)
  const exported = await load(exportFile)
  const target = { width: exported.width * SCALE, height: exported.height * SCALE, data: Buffer.alloc(exported.width * exported.height * SCALE * SCALE * 4) }
  const placed = bounds(exported)
  let native = null
  if (placed && existsSync(sourceFile)) {
    const source = await load(sourceFile)
    const box = bounds(source)
    if (box) {
      const cell = cellSize(commonRun(runLengths(source, box, true)), commonRun(runLengths(source, box, false)))
      const linesX = gridLines(edgeProfile(source, box, true), box.left, box.left + box.width, cell)
      const linesY = gridLines(edgeProfile(source, box, false), box.top, box.top + box.height, cell)
      native = trim(sampleGrid(source, linesX, linesY))
    }
  }
  if (native) {
    const rect = { left: placed.left * SCALE, top: placed.top * SCALE, width: placed.width * SCALE, height: placed.height * SCALE }
    for (let y = 0; y < rect.height; y++) {
      for (let x = 0; x < rect.width; x++) {
        const from = (Math.floor(((y + 0.5) * native.height) / rect.height) * native.width + Math.floor(((x + 0.5) * native.width) / rect.width)) * 4
        native.data.copy(target.data, ((rect.top + y) * target.width + rect.left + x) * 4, from, from + 4)
      }
    }
    report.push({ strip, native: `${native.width}x${native.height}`, placed: `${placed.width}x${placed.height}`, loss: Math.max(native.width / placed.width, native.height / placed.height) })
  } else {
    for (let y = 0; y < target.height; y++) {
      for (let x = 0; x < target.width; x++) {
        const from = (Math.floor(y / SCALE) * exported.width + Math.floor(x / SCALE)) * 4
        exported.data.copy(target.data, (y * target.width + x) * 4, from, from + 4)
      }
    }
    report.push({ strip, native: 'no source', placed: placed ? `${placed.width}x${placed.height}` : 'empty', loss: 0 })
  }
  await sharp(target.data, { raw: { width: target.width, height: target.height, channels: 4 } }).png({ compressionLevel: 9 }).toFile(join(art, `${strip}-2x.png`))
}

if (process.env.REVIEW_SHEET) {
  const shown = report.slice(0, 40)
  const cell = 160
  const columns = 4
  const tiles = await Promise.all(
    shown.flatMap(({ strip }, index) =>
      [`${strip}.png`, `${strip}-2x.png`].map(async (file, side) => ({
        input: await sharp(join(art, file)).resize(cell - 16, cell - 16, { kernel: 'nearest', fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } }).png().toBuffer(),
        left: ((index % columns) * 2 + side) * cell + 8,
        top: Math.floor(index / columns) * cell + 8,
      })),
    ),
  )
  await sharp({ create: { width: columns * 2 * cell, height: Math.ceil(shown.length / columns) * cell, channels: 4, background: { r: 236, g: 226, b: 200, alpha: 1 } } })
    .composite(tiles)
    .png()
    .toFile(process.env.REVIEW_SHEET)
}

report.sort((a, b) => b.loss - a.loss)
for (const line of report) console.log(`${line.loss.toFixed(2).padStart(5)}  ${line.native.padEnd(9)} into ${line.placed.padEnd(7)} ${line.strip}`)
const damaged = report.filter((line) => line.loss > 1.15).length
console.log(`${report.length} sprites, ${damaged} had lost more than 15% of their rows or columns in the old export`)
