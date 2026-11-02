/**
 * Mountains, thickets and rivers outside the camp picture.
 * A course keeps a general heading, bends a little as it goes, and splits off thinner branches.
 * Every course is solid and at least three tiles wide, and here and there it swells gradually to a broad stretch.
 * A few mountain regions stand as solid massifs, with ranges running out of them. There are no gaps: a walker
 * goes around, digs a tunnel through a mountain, clears a thicket or bridges a river.
 * The same seed draws the same tiles. The server's FarWilds computes the same tiles. The camp picture
 * stays open, and each course keeps a stretch of open country around the camp.
 */

export const FAR_LAND_LEFT = 15 - 6_000
export const FAR_LAND_TOP = 13 - 6_000
export const FAR_LAND_SIDE = 12_000
export const FAR_VALE = { x: -44, y: -32, width: 120, height: 90 }

/** The land is split into squares of these sizes, so every kind of course is spread evenly across it. */
/** One massif per square. */
const MASSIF_REGION = 4000
/** One river per square. Rivers and ranges share one size of square, so both meet everywhere. */
const RIVER_REGION = 600
/** One range per square. */
const RANGE_REGION = 600
/** Thickets grow in most of the squares of this size. */
const THICKET_REGION = 800
/** Every course reaches at least this far either side of its middle tile, so it is at least three tiles across. */
const MIN_HALF_WIDTH = 1.5
/**
 * A course turns by one eighth of a circle at a time, at least this many steps apart, and never strays more than one
 * eighth from its heading, so it bends gently and never sharply.
 */
const TURN_GAP = 20
/** A course stops once it heads this close to the edge of the land. */
const EDGE_STOP = 8

const DX8 = [1, 1, 0, -1, -1, -1, 0, 1]
const DY8 = [0, -1, -1, -1, 0, 1, 1, 1]
const TURN = [0, 1, 7]

export type FarKind = 'open' | 'mountain' | 'thicket' | 'water'

/** How a course grows. Matches FarWilds.Shape. */
interface Shape {
  wobble: number
  forkEvery: number
  forkChance: number
  maxDepth: number
  childMin: number
  childSpan: number
  /** The widest half-width at each depth of branching. */
  swell: number[]
  /** Steps between one width and the next one the course eases toward. */
  period: number
  /** Up to this much is added to a broad stretch's half-width at each step, so its edge is wavy but has no loose tiles. */
  rough: number
}

const RANGE: Shape = { wobble: 0.15, forkEvery: 120, forkChance: 0.25, maxDepth: 1, childMin: 150, childSpan: 250, swell: [7.5, 3.5, 1.5], period: 150, rough: 0.9 }
const THICKET: Shape = { wobble: 0.3, forkEvery: 60, forkChance: 0.25, maxDepth: 1, childMin: 50, childSpan: 70, swell: [5, 2], period: 80, rough: 0.8 }
const RIVER: Shape = { wobble: 0.2, forkEvery: 100, forkChance: 0.3, maxDepth: 1, childMin: 150, childSpan: 200, swell: [2.5, 1, 0.5], period: 80, rough: 0 }

/** Stable 0…1 roll. `salt` keeps each decision on its own stream. Matches FarWilds.Roll. */
export function farWildRoll(seed: number, a: number, b: number, salt: number) {
  let value = (seed ^ Math.imul(a | 0, 0x27d4eb2d) ^ Math.imul(b | 0, 0x165667b1) ^ Math.imul(salt | 0, 0x9e3779b1)) >>> 0
  value = Math.imul(value ^ (value >>> 16), 0x85ebca6b) >>> 0
  value = Math.imul(value ^ (value >>> 13), 0xc2b2ae35) >>> 0
  return ((value ^ (value >>> 16)) >>> 0) / 4294967296
}

function inVale(x: number, y: number) {
  return x >= FAR_VALE.x && y >= FAR_VALE.y && x < FAR_VALE.x + FAR_VALE.width && y < FAR_VALE.y + FAR_VALE.height
}

function onLand(x: number, y: number) {
  return x >= FAR_LAND_LEFT && y >= FAR_LAND_TOP && x < FAR_LAND_LEFT + FAR_LAND_SIDE && y < FAR_LAND_TOP + FAR_LAND_SIDE
}

/** Campfire tile. Open country is measured from here. */
const CAMP_X = 15
const CAMP_Y = 13

function countryReach(seed: number, id: number, salt: number) {
  return 120 + Math.floor(farWildRoll(seed, id, salt, 91) * 160)
}

function clearOfCamp(x: number, y: number, reach: number) {
  const dx = x - CAMP_X
  const dy = y - CAMP_Y
  return dx * dx + dy * dy >= reach * reach && !inVale(x, y)
}

/** Signed 32-bit pack of a tile. Matches FarWilds.Key. */
function tileKey(x: number, y: number) {
  return ((x + 8000) << 16) | ((y + 8000) & 0xffff)
}

const courses = new Map<number, Map<number, FarKind>>()

function build(seed: number) {
  const map = new Map<number, FarKind>()
  const paint = (x: number, y: number, kind: FarKind) => {
    if (!onLand(x, y) || inVale(x, y)) return
    const key = tileKey(x, y)
    const prev = map.get(key)
    if (prev === 'mountain') return
    if (kind === 'thicket' && prev) return
    if (kind === 'water' && prev) return
    map.set(key, kind)
  }
  const disk = (x: number, y: number, radius: number, kind: FarKind, reach: number) => {
    const span = Math.floor(radius)
    for (let oy = -span; oy <= span; oy++) {
      for (let ox = -span; ox <= span; ox++) {
        const distance = ox * ox + oy * oy
        if (distance > radius * radius) continue
        const tx = x + ox
        const ty = y + oy
        if (!clearOfCamp(tx, ty, reach)) continue
        paint(tx, ty, kind)
      }
    }
  }
  /** A random spot in one square of the land, or null when it would crowd the camp. */
  const cell = (index: number, salt: number, region: number, room = 0) => {
    const regions = FAR_LAND_SIDE / region
    const column = index % regions
    const row = Math.floor(index / regions)
    const x = FAR_LAND_LEFT + column * region + 30 + Math.floor(farWildRoll(seed, index, salt, 1) * (region - 60))
    const y = FAR_LAND_TOP + row * region + 30 + Math.floor(farWildRoll(seed, index, salt, 2) * (region - 60))
    if (!clearOfCamp(x, y, countryReach(seed, index, salt) + room)) return null
    return { x, y }
  }
  /** Three tiles across most of the way, easing up to a broad stretch now and then. A river widens downstream. */
  const halfWidth = (id: number, salt: number, step: number, length: number, kind: FarKind, depth: number, shape: Shape) => {
    const knot = Math.floor(step / shape.period)
    const t = step / shape.period - knot
    const from = farWildRoll(seed, id, knot, salt + 62)
    const to = farWildRoll(seed, id, knot + 1, salt + 62)
    const swell = from + (to - from) * (t * t * (3 - 2 * t))
    const widest = shape.swell[depth]
    if (kind === 'water') return Math.max(MIN_HALF_WIDTH, widest * (0.15 + 0.85 * step / length) * (0.6 + 0.4 * swell))
    const ends = Math.min(1, step / 24, (length - step) / 24)
    return Math.max(MIN_HALF_WIDTH, widest * swell * swell * ends)
  }
  const grow = (
    id: number,
    salt: number,
    x: number,
    y: number,
    length: number,
    kind: FarKind,
    depth: number,
    dir: number,
    reach: number,
    shape: Shape,
    childMin: number,
    childSpan: number,
    forkChance: number,
  ) => {
    let home = dir
    let px = x
    let py = y
    let turnedAt = 0
    let emerged = false
    for (let step = 0; step < length; step++) {
      if (!onLand(px, py) || !clearOfCamp(px, py, reach)) break
      const middle = halfWidth(id, salt, step, length, kind, depth, shape)
      const radius = middle >= MIN_HALF_WIDTH + 1 ? middle + farWildRoll(seed, id, step, salt + 81) * shape.rough : middle
      // A course never runs into another one, and a branch never turns back into its parent: once it is clear of the
      // ground it started on, it ends where the tile just ahead of it is taken.
      const span = Math.floor(radius)
      const ahead = map.get(tileKey(px + DX8[dir] * (span + 2), py + DY8[dir] * (span + 2)))
      if (ahead === undefined) emerged = true
      else if (emerged) break
      disk(px, py, radius, kind, reach)
      if (depth < shape.maxDepth && step > 5 && step % shape.forkEvery === 0 && farWildRoll(seed, id, step, salt + 70) < forkChance) {
        const off = farWildRoll(seed, id, step, salt + 71) < 0.5 ? 1 : 7
        const childDir = (dir + off) % 8
        const childLen = childMin + Math.floor(farWildRoll(seed, id, step, salt + 73) * childSpan)
        const childId = (id + (step + 1) * 1024 + (depth + 1) * 262144) | 0
        grow(childId, salt, px, py, childLen, kind, depth + 1, childDir, reach, shape, Math.max(8, childMin >> 1), Math.max(8, childSpan >> 1), forkChance * 0.45)
      }
      if (step > 0 && step % 240 === 0 && farWildRoll(seed, id, step, salt + 52) < 0.4) {
        home = (home + (farWildRoll(seed, id, step, salt + 53) < 0.5 ? 1 : 7)) % 8
      }
      const drift = (dir - home + 8) % 8
      const sway = farWildRoll(seed, id, step, salt + 50)
      if (step - turnedAt >= TURN_GAP) {
        if (drift !== 0 && sway < 0.5) {
          dir = (dir + (drift <= 4 ? 7 : 1)) % 8
          turnedAt = step
        } else if (drift === 0 && sway < shape.wobble) {
          dir = (dir + (farWildRoll(seed, id, step, salt + 51) < 0.5 ? 1 : 7)) % 8
          turnedAt = step
        }
      }
      // A course ends short of the edge of the land rather than running along it, so it never walls off the edge.
      if (!onLand(px + DX8[dir] * EDGE_STOP, py + DY8[dir] * EDGE_STOP)) break
      // Only straight on, or one eighth of a circle to either side; anything sharper ends the course.
      let moved = false
      for (let turn = 0; turn < 3; turn++) {
        const nd = (dir + TURN[turn]) % 8
        const nx = px + DX8[nd]
        const ny = py + DY8[nd]
        if (!onLand(nx, ny) || !clearOfCamp(nx, ny, reach)) continue
        if (nd !== dir) turnedAt = step
        dir = nd
        px = nx
        py = ny
        moved = true
        break
      }
      if (!moved) break
    }
  }
  const course = (id: number, salt: number, x: number, y: number, length: number, kind: FarKind, reach: number, shape: Shape) => {
    const dir = Math.floor(farWildRoll(seed, id, salt, 3) * 8)
    grow(id, salt, x, y, length, kind, 0, dir, reach, shape, shape.childMin, shape.childSpan, shape.forkChance)
  }

  const massifSquares = (FAR_LAND_SIDE / MASSIF_REGION) ** 2
  for (let index = 0; index < massifSquares; index++) {
    const radius = 20 + Math.floor(farWildRoll(seed, index, 130, 6) * 20)
    const origin = cell(index, 130, MASSIF_REGION, radius * 2)
    if (!origin) continue
    const reach = countryReach(seed, index, 130)
    const lumps = 14 + Math.floor(farWildRoll(seed, index, 130, 7) * 10)
    for (let lump = 0; lump < lumps; lump++) {
      const ox = Math.floor((farWildRoll(seed, index, lump, 131) * 2 - 1) * radius * 0.75)
      const oy = Math.floor((farWildRoll(seed, index, lump, 132) * 2 - 1) * radius * 0.75)
      const size = radius * (0.25 + farWildRoll(seed, index, lump, 133) * 0.25)
      disk(origin.x + ox, origin.y + oy, size, 'mountain', reach)
    }
    const arms = 2 + Math.floor(farWildRoll(seed, index, 130, 8) * 2)
    for (let arm = 0; arm < arms; arm++) {
      const id = index * 8 + arm
      const length = 400 + Math.floor(farWildRoll(seed, id, 140, 5) * 500)
      course(id, 140, origin.x, origin.y, length, 'mountain', reach, RANGE)
    }
  }
  const rangeSquares = (FAR_LAND_SIDE / RANGE_REGION) ** 2
  for (let index = 0; index < rangeSquares; index++) {
    const origin = cell(index, 70, RANGE_REGION)
    if (!origin) continue
    const length = 500 + Math.floor(farWildRoll(seed, index, 70, 5) * 500)
    course(index, 70, origin.x, origin.y, length, 'mountain', countryReach(seed, index, 70), RANGE)
  }
  const riverSquares = (FAR_LAND_SIDE / RIVER_REGION) ** 2
  for (let index = 0; index < riverSquares; index++) {
    const origin = cell(index, 110, RIVER_REGION)
    if (!origin) continue
    const radius = 4 + Math.floor(farWildRoll(seed, index, 110, 6) * 3)
    const reach = countryReach(seed, index, 110)
    for (let dy = -radius; dy <= radius; dy++) {
      for (let dx = -radius; dx <= radius; dx++) {
        if (dx * dx + dy * dy <= radius * radius && clearOfCamp(origin.x + dx, origin.y + dy, reach)) {
          paint(origin.x + dx, origin.y + dy, 'water')
        }
      }
    }
    const length = 400 + Math.floor(farWildRoll(seed, index, 110, 8) * 400)
    course(index, 110, origin.x, origin.y, length, 'water', reach, RIVER)
  }
  const thicketSquares = (FAR_LAND_SIDE / THICKET_REGION) ** 2
  for (let index = 0; index < thicketSquares; index++) {
    if (farWildRoll(seed, index, 90, 6) < 0.15) continue
    const origin = cell(index, 90, THICKET_REGION)
    if (!origin) continue
    const length = 250 + Math.floor(farWildRoll(seed, index, 90, 5) * 200)
    course(index, 90, origin.x, origin.y, length, 'thicket', countryReach(seed, index, 90), THICKET)
  }
  return map
}

function mapFor(seed: number) {
  const key = seed >>> 0
  let map = courses.get(key)
  if (!map) {
    map = build(key)
    courses.set(key, map)
  }
  return map
}

/** Mountain, thicket, river or open ground. The camp picture is always open. */
export function farKind(x: number, y: number, seed: number): FarKind {
  if (!onLand(x, y) || inVale(x, y)) return 'open'
  return mapFor(seed).get(tileKey(x, y)) ?? 'open'
}

/** Water, a mountain or a thicket: ground no den, foe or cave mouth stands on. */
export function farTaken(x: number, y: number, seed: number) {
  return farKind(x, y, seed) !== 'open'
}

/** A mountain blocks a walk until a tunnel is dug through it, a thicket until it is cleared. A river is water, not a clearing. */
export function farBlocksKind(kind: FarKind) {
  return kind === 'mountain' || kind === 'thicket'
}
