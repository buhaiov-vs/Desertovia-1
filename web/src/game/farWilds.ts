/**
 * Mountains, thickets and rivers outside the camp picture.
 * A course keeps a general heading, bends a little as it goes, and splits off thinner branches.
 * Most of its length is one tile wide, and here and there it swells gradually to a broad stretch.
 * A few mountain regions stand as solid massifs, with ranges running out of them.
 * Every other pass across a range is an open saddle and the rest are rockfall to clear. A massif is cut by
 * alternating open valleys and rockfall lines, rivers and thickets have open gaps, and where two courses meet the
 * meeting is open, so the whole land can be walked.
 * The same seed draws the same tiles. The server's FarWilds computes the same tiles. The camp picture
 * stays open, and each course keeps a stretch of open country around the camp.
 */

export const FAR_LAND_LEFT = 15 - 6_000
export const FAR_LAND_TOP = 13 - 6_000
export const FAR_LAND_SIDE = 12_000
export const FAR_VALE = { x: -44, y: -32, width: 120, height: 90 }

const RIVER_COURSES = 12
const MASSIFS = 8
/** Ranges grow one per square of this size. */
const RANGE_REGION = 3000
/** Thickets grow in most of the squares of this size. */
const THICKET_REGION = 1000
/** Valleys and rockfall passes cut a massif along lines this far apart. */
const MASSIF_PASS_SPACING = 40
/** A course cuts a pass, ford or gap once this much of its cross-section has grown since the last one. */
const GATE_LOAD = 400
/** A course stops once it heads this close to the edge of the land. */
const EDGE_STOP = 8

const DX8 = [1, 1, 0, -1, -1, -1, 0, 1]
const DY8 = [0, -1, -1, -1, 0, 1, 1, 1]
const TURN = [0, 1, 7, 2, 6, 3, 5, 4]

export type FarKind = 'open' | 'mountain' | 'thicket' | 'rockfall' | 'water'

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
  /** Share of tiles left out along the outer edge of a wide stretch. */
  edge: number
  /** Share of tiles left out anywhere. */
  skip: number
}

const RANGE: Shape = { wobble: 0.15, forkEvery: 80, forkChance: 0.6, maxDepth: 2, childMin: 150, childSpan: 250, swell: [7.5, 3.5, 1.5], period: 150, edge: 0.4, skip: 0 }
const THICKET: Shape = { wobble: 0.3, forkEvery: 40, forkChance: 0.6, maxDepth: 1, childMin: 50, childSpan: 70, swell: [5, 2], period: 80, edge: 0.35, skip: 0.1 }
const RIVER: Shape = { wobble: 0.2, forkEvery: 80, forkChance: 0.6, maxDepth: 2, childMin: 150, childSpan: 200, swell: [2.5, 1, 0.5], period: 80, edge: 0, skip: 0 }

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

/** An open saddle, valley or ford. It reads as open ground, and no later course paints over it. */
type Painted = FarKind | 'saddle'

const courses = new Map<number, Map<number, Painted>>()

function build(seed: number) {
  const map = new Map<number, Painted>()
  const paint = (x: number, y: number, kind: FarKind) => {
    if (!onLand(x, y) || inVale(x, y)) return
    const key = tileKey(x, y)
    const prev = map.get(key)
    if (prev === 'saddle' || prev === 'rockfall') return
    if (prev === 'mountain' && kind !== 'rockfall') return
    if (kind === 'thicket' && prev) return
    if (kind === 'water' && prev) return
    map.set(key, kind)
  }
  const open = (x: number, y: number) => {
    if (onLand(x, y) && !inVale(x, y)) map.set(tileKey(x, y), 'saddle')
  }
  const disk = (x: number, y: number, radius: number, kind: FarKind, shape: Shape, salt: number, reach: number) => {
    const span = Math.floor(radius)
    const inner = (radius - 1) * (radius - 1)
    for (let oy = -span; oy <= span; oy++) {
      for (let ox = -span; ox <= span; ox++) {
        const distance = ox * ox + oy * oy
        if (distance > radius * radius) continue
        const tx = x + ox
        const ty = y + oy
        if (radius >= 1.5 && distance > inner && farWildRoll(seed, tx, ty, salt + 81) < shape.edge) continue
        if (shape.skip > 0 && farWildRoll(seed, tx, ty, salt + 80) < shape.skip) continue
        if (!clearOfCamp(tx, ty, reach)) continue
        paint(tx, ty, kind)
      }
    }
  }
  const start = (id: number, salt: number, room: number) => {
    const span = FAR_LAND_SIDE - 400
    const x = FAR_LAND_LEFT + 200 + Math.floor(farWildRoll(seed, id, salt, 1) * span)
    const y = FAR_LAND_TOP + 200 + Math.floor(farWildRoll(seed, id, salt, 2) * span)
    if (!clearOfCamp(x, y, countryReach(seed, id, salt) + room)) return null
    return { x, y }
  }
  const cell = (index: number, salt: number, region: number) => {
    const regions = FAR_LAND_SIDE / region
    const column = index % regions
    const row = Math.floor(index / regions)
    const x = FAR_LAND_LEFT + column * region + 30 + Math.floor(farWildRoll(seed, index, salt, 1) * (region - 60))
    const y = FAR_LAND_TOP + row * region + 30 + Math.floor(farWildRoll(seed, index, salt, 2) * (region - 60))
    if (!clearOfCamp(x, y, countryReach(seed, index, salt))) return null
    return { x, y }
  }
  /** Thin most of the way, easing up to a broad stretch now and then, narrowing at both ends. A river widens downstream. */
  const halfWidth = (id: number, salt: number, step: number, length: number, kind: FarKind, depth: number, shape: Shape) => {
    const knot = Math.floor(step / shape.period)
    const t = step / shape.period - knot
    const from = farWildRoll(seed, id, knot, salt + 62)
    const to = farWildRoll(seed, id, knot + 1, salt + 62)
    const swell = from + (to - from) * (t * t * (3 - 2 * t))
    const widest = shape.swell[depth]
    if (kind === 'water') return widest * (0.15 + 0.85 * step / length) * (0.6 + 0.4 * swell)
    const ends = Math.min(1, step / 24, (length - step) / 24)
    return widest * swell * swell * ends
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
    const gateEvery = 50 + Math.floor(farWildRoll(seed, id, salt, 4) * 30)
    let load = 0
    let sinceGate = 0
    let gates = 0
    let crossingAt = -1
    let emerged = false
    let px = x
    let py = y
    for (let step = 0; step < length; step++) {
      if (!onLand(px, py) || !clearOfCamp(px, py, reach)) break
      const radius = halfWidth(id, salt, step, length, kind, depth, shape)
      load += 2 * radius + 1
      sinceGate++
      if (step > 6 && (load >= GATE_LOAD || sinceGate >= gateEvery)) {
        load = 0
        sinceGate = 0
        const mark = kind !== 'mountain' || gates % 2 === 1 ? open : (gx: number, gy: number) => paint(gx, gy, 'rockfall')
        gates++
        mark(px, py)
        const spoke = Math.floor(radius * 1.5) + 2
        for (let side = 0; side < 8; side += 2) {
          for (let n = 1; n <= spoke; n++) mark(px + DX8[side] * n, py + DY8[side] * n)
        }
      }
      // Where a course runs into another, an open cross joins the land on every side of the meeting.
      const span = Math.floor(radius)
      const aheadX = px + DX8[dir] * (span + 2)
      const aheadY = py + DY8[dir] * (span + 2)
      const ahead = map.get(tileKey(aheadX, aheadY))
      if (ahead === undefined) emerged = true
      else if (emerged && step > 12 && step > crossingAt && ahead !== 'saddle') {
        crossingAt = step + 2 * span + 24
        open(aheadX, aheadY)
        for (let side = 0; side < 8; side += 2) {
          for (let n = 1; n <= span + 12; n++) open(aheadX + DX8[side] * n, aheadY + DY8[side] * n)
        }
      }
      disk(px, py, radius, kind, shape, salt, reach)
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
      if (drift >= 2 && drift <= 6 && sway < 0.6) dir = (dir + (drift <= 4 ? 7 : 1)) % 8
      else if (sway < shape.wobble) {
        const side = farWildRoll(seed, id, step, salt + 51) < 0.5 ? 1 : 7
        dir = (dir + side) % 8
      }
      // A course ends short of the edge of the land rather than running along it, so it never walls off the edge.
      if (!onLand(px + DX8[dir] * EDGE_STOP, py + DY8[dir] * EDGE_STOP)) break
      let moved = false
      for (let turn = 0; turn < 8; turn++) {
        const nd = (dir + TURN[turn]) % 8
        const nx = px + DX8[nd]
        const ny = py + DY8[nd]
        if (!onLand(nx, ny) || !clearOfCamp(nx, ny, reach)) continue
        dir = nd
        px = nx
        py = ny
        if (DX8[nd] !== 0 && DY8[nd] !== 0) paint(px - DX8[nd], py, kind)
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
  /** Turns the massif's mountain tiles on one line into an open valley, or into rockfall that opens once it is cleared. */
  const pass = (x: number, y: number, dx: number, dy: number, length: number, valley: boolean) => {
    for (let n = 0; n <= length; n++) {
      const key = tileKey(x + dx * n, y + dy * n)
      if (map.get(key) === 'mountain') map.set(key, valley ? 'saddle' : 'rockfall')
    }
  }

  for (let index = 0; index < MASSIFS; index++) {
    const radius = 50 + Math.floor(farWildRoll(seed, index, 130, 6) * 60)
    const origin = start(index, 130, radius * 2)
    if (!origin) continue
    const reach = countryReach(seed, index, 130)
    const lumps = 14 + Math.floor(farWildRoll(seed, index, 130, 7) * 10)
    for (let lump = 0; lump < lumps; lump++) {
      const ox = Math.floor((farWildRoll(seed, index, lump, 131) * 2 - 1) * radius * 0.75)
      const oy = Math.floor((farWildRoll(seed, index, lump, 132) * 2 - 1) * radius * 0.75)
      const size = radius * (0.25 + farWildRoll(seed, index, lump, 133) * 0.25)
      disk(origin.x + ox, origin.y + oy, size, 'mountain', RANGE, 130, reach)
    }
    const arms = 2 + Math.floor(farWildRoll(seed, index, 130, 8) * 2)
    for (let arm = 0; arm < arms; arm++) {
      const id = index * 8 + arm
      const length = 400 + Math.floor(farWildRoll(seed, id, 140, 5) * 500)
      course(id, 140, origin.x, origin.y, length, 'mountain', reach, RANGE)
    }
    const left = origin.x - radius * 2
    const top = origin.y - radius * 2
    const firstX = left + Math.floor(farWildRoll(seed, index, 130, 9) * MASSIF_PASS_SPACING)
    const firstY = top + Math.floor(farWildRoll(seed, index, 130, 10) * MASSIF_PASS_SPACING)
    for (let x = firstX, line = 0; x <= origin.x + radius * 2; x += MASSIF_PASS_SPACING, line++) pass(x, top, 0, 1, radius * 4, line % 2 === 1)
    for (let y = firstY, line = 0; y <= origin.y + radius * 2; y += MASSIF_PASS_SPACING, line++) pass(left, y, 1, 0, radius * 4, line % 2 === 1)
  }
  const rangeSquares = (FAR_LAND_SIDE / RANGE_REGION) ** 2
  for (let index = 0; index < rangeSquares; index++) {
    if (farWildRoll(seed, index, 70, 6) < 0.15) continue
    const origin = cell(index, 70, RANGE_REGION)
    if (!origin) continue
    const length = 1200 + Math.floor(farWildRoll(seed, index, 70, 5) * 1200)
    course(index, 70, origin.x, origin.y, length, 'mountain', countryReach(seed, index, 70), RANGE)
  }
  for (let index = 0; index < RIVER_COURSES; index++) {
    const origin = start(index, 110, 0)
    if (!origin) continue
    const radius = 7 + Math.floor(farWildRoll(seed, index, 110, 6) * 4)
    const reach = countryReach(seed, index, 110)
    for (let dy = -radius; dy <= radius; dy++) {
      for (let dx = -radius; dx <= radius; dx++) {
        if (dx * dx + dy * dy <= radius * radius && clearOfCamp(origin.x + dx, origin.y + dy, reach)) {
          paint(origin.x + dx, origin.y + dy, 'water')
        }
      }
    }
    const toEdge = farWildRoll(seed, index, 110, 7) < 0.5
    const length = toEdge ? 2500 : 900 + Math.floor(farWildRoll(seed, index, 110, 8) * 800)
    course(index, 110, origin.x, origin.y, length, 'water', reach, RIVER)
  }
  const thicketSquares = (FAR_LAND_SIDE / THICKET_REGION) ** 2
  for (let index = 0; index < thicketSquares; index++) {
    if (farWildRoll(seed, index, 90, 6) < 0.4) continue
    const origin = cell(index, 90, THICKET_REGION)
    if (!origin) continue
    const length = 200 + Math.floor(farWildRoll(seed, index, 90, 5) * 300)
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

/** Mountain, thicket, rockfall, river or open ground. The camp picture is always open. */
export function farKind(x: number, y: number, seed: number): FarKind {
  if (!onLand(x, y) || inVale(x, y)) return 'open'
  const kind = mapFor(seed).get(tileKey(x, y))
  return kind === undefined || kind === 'saddle' ? 'open' : kind
}

/** A mountain, thicket or rockfall blocks a walk until it is cleared. A river is water, not a clearing. */
export function farBlocksKind(kind: FarKind) {
  return kind === 'mountain' || kind === 'thicket' || kind === 'rockfall'
}
