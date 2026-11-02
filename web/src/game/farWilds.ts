/**
 * Mountains, thickets and rivers outside the camp picture.
 * A ridge keeps a general heading, bends a little as it goes, and splits off shorter spurs, the way a
 * range forks. Thickets are clumps along that same growth. The same seed draws the same tiles.
 * The server's FarWilds computes the same tiles. The camp picture stays open, and each course keeps
 * a stretch of open country around the camp. One ridge and one thicket stand in every square of the land.
 */

export const FAR_LAND_LEFT = 15 - 6_000
export const FAR_LAND_TOP = 13 - 6_000
export const FAR_LAND_SIDE = 12_000
export const FAR_VALE = { x: -44, y: -32, width: 120, height: 90 }

/** How far along an obstacle a walk looks for a gap. */
export const FAR_OPENING_REACH = 240

const RIVER_COURSES = 8
/** The land is split into these squares. Each square grows its own ridge and thicket. */
const REGION = 750
const REGIONS = FAR_LAND_SIDE / REGION

const DX = [1, 0, -1, 0]
const DY = [0, -1, 0, 1]
const DX8 = [1, 1, 0, -1, -1, -1, 0, 1]
const DY8 = [0, -1, -1, -1, 0, 1, 1, 1]
const TURN = [0, 1, 7, 2, 6, 3, 5, 4]

export type FarKind = 'open' | 'mountain' | 'thicket' | 'rockfall' | 'water'

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
    if (prev === 'rockfall') return
    if (prev === 'mountain' && kind !== 'rockfall') return
    if (kind === 'thicket' && prev) return
    if (kind === 'water' && prev) return
    map.set(key, kind)
  }
  const start = (id: number, salt: number) => {
    const span = FAR_LAND_SIDE - 400
    const x = FAR_LAND_LEFT + 200 + Math.floor(farWildRoll(seed, id, salt, 1) * span)
    const y = FAR_LAND_TOP + 200 + Math.floor(farWildRoll(seed, id, salt, 2) * span)
    if (!clearOfCamp(x, y, countryReach(seed, id, salt))) return null
    return { x, y }
  }
  const cell = (index: number, salt: number) => {
    const column = index % REGIONS
    const row = Math.floor(index / REGIONS)
    const x = FAR_LAND_LEFT + column * REGION + 30 + Math.floor(farWildRoll(seed, index, salt, 1) * (REGION - 60))
    const y = FAR_LAND_TOP + row * REGION + 30 + Math.floor(farWildRoll(seed, index, salt, 2) * (REGION - 60))
    if (!clearOfCamp(x, y, countryReach(seed, index, salt))) return null
    return { x, y }
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
    home: number,
    reach: number,
    wobble: number,
    forkEvery: number,
    forkChance: number,
    maxDepth: number,
    childMin: number,
    childSpan: number,
  ) => {
    let px = x
    let py = y
    for (let step = 0; step < length; step++) {
      if (!onLand(px, py) || !clearOfCamp(px, py, reach)) break
      const bulk = kind === 'water' ? (farWildRoll(seed, id, step, salt + 61) < 0.18 ? 1 : 0)
        : kind === 'thicket' ? (depth === 0 ? 2 : 1)
        : depth === 0 ? 1 + (farWildRoll(seed, px, py, salt + 60) < 0.4 ? 1 : 0)
        : depth === 1 ? 1 : 0
      const skip = kind === 'thicket' ? 0.34 : 0
      const gateEvery = 36 + Math.floor(farWildRoll(seed, id, salt, 4) * 12)
      const gating = kind === 'mountain' && depth === 0 && step > 6 && step % gateEvery === 0
      if (gating) {
        paint(px, py, 'rockfall')
        for (let spoke = 0; spoke < 8; spoke += 2) {
          for (let n = 1; n <= bulk + 2; n++) paint(px + DX8[spoke] * n, py + DY8[spoke] * n, 'rockfall')
        }
      }
      for (let oy = -bulk; oy <= bulk; oy++) {
        for (let ox = -bulk; ox <= bulk; ox++) {
          if (ox * ox + oy * oy > bulk * bulk) continue
          if (skip > 0 && farWildRoll(seed, px + ox, py + oy, salt + 80) < skip) continue
          paint(px + ox, py + oy, gating ? 'rockfall' : kind)
        }
      }
      if (depth < maxDepth && step > 5 && step % forkEvery === 0 && farWildRoll(seed, id, step, salt + 70) < forkChance) {
        const off = farWildRoll(seed, id, step, salt + 71) < 0.5 ? 2 : 6
        const childDir = (dir + off) % 8
        const childLen = childMin + Math.floor(farWildRoll(seed, id, step, salt + 73) * childSpan)
        const childId = (id + (step + 1) * 1024 + (depth + 1) * 262144) | 0
        grow(childId, salt, px, py, childLen, kind, depth + 1, childDir, childDir, reach, wobble, forkEvery, forkChance * 0.45, maxDepth, Math.max(8, childMin >> 1), Math.max(8, childSpan >> 1))
      }
      const drift = (dir - home + 8) % 8
      const sway = farWildRoll(seed, id, step, salt + 50)
      if (drift >= 3 && drift <= 5 && sway < 0.75) dir = (dir + (drift <= 4 ? 7 : 1)) % 8
      else if (sway < wobble) {
        const side = farWildRoll(seed, id, step, salt + 51) < 0.5 ? 1 : 7
        dir = (dir + side) % 8
      }
      let moved = false
      for (let turn = 0; turn < 8; turn++) {
        const nd = (dir + TURN[turn]) % 8
        const nx = px + DX8[nd]
        const ny = py + DY8[nd]
        if (!onLand(nx, ny) || !clearOfCamp(nx, ny, reach)) continue
        dir = nd
        px = nx
        py = ny
        if (DX8[nd] !== 0 && DY8[nd] !== 0) paint(px - DX8[nd], py, gating ? 'rockfall' : kind)
        moved = true
        break
      }
      if (!moved) break
    }
  }

  const squares = REGIONS * REGIONS
  for (let index = 0; index < squares; index++) {
    const origin = cell(index, 70)
    if (!origin) continue
    const length = 78 + Math.floor(farWildRoll(seed, index, 70, 5) * 36)
    const dir = Math.floor(farWildRoll(seed, index, 70, 3) * 8)
    grow(index, 70, origin.x, origin.y, length, 'mountain', 0, dir, dir, countryReach(seed, index, 70), 0.55, 8, 0.9, 2, 18, 26)
  }
  for (let index = 0; index < RIVER_COURSES; index++) {
    const origin = start(index, 110)
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
    const length = toEdge ? 1400 : 420 + Math.floor(farWildRoll(seed, index, 110, 8) * 380)
    const dir = Math.floor(farWildRoll(seed, index, 110, 3) * 8)
    grow(index, 110, origin.x, origin.y, length, 'water', 0, dir, dir, countryReach(seed, index, 110), 0.42, 90, 0.55, 1, 36, 40)
  }
  for (let index = 0; index < squares; index++) {
    const origin = cell(index, 90)
    if (!origin) continue
    const length = 28 + Math.floor(farWildRoll(seed, index, 90, 5) * 18)
    const dir = Math.floor(farWildRoll(seed, index, 90, 3) * 8)
    grow(index, 90, origin.x, origin.y, length, 'thicket', 0, dir, dir, countryReach(seed, index, 90), 0.7, 6, 0.85, 2, 10, 12)
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
  return mapFor(seed).get(tileKey(x, y)) ?? 'open'
}

/** A mountain, thicket or rockfall blocks a walk until it is cleared. A river is water, not a clearing. */
export function farBlocksKind(kind: FarKind) {
  return kind === 'mountain' || kind === 'thicket' || kind === 'rockfall'
}

/**
 * The nearest open tile at a gap or at the end of the obstacle, within {@link FAR_OPENING_REACH}
 * steps along it. Grass beside a solid stretch is not a gap. A cleared rockfall, and the end of a river, are.
 */
/** Gaps and ends of the obstacle, nearest first. A walk tries the next one when the nearest still crosses the wall. */
export function farOpenings(
  x: number,
  y: number,
  blocked: (x: number, y: number) => boolean,
  reach = FAR_OPENING_REACH,
): Array<{ x: number; y: number }> {
  if (!blocked(x, y)) return []
  const queue: Array<{ x: number; y: number; step: number }> = [{ x, y, step: 0 }]
  const seen = new Set<number>([tileKey(x, y)])
  let minX = queue[0]
  let maxX = queue[0]
  let minY = queue[0]
  let maxY = queue[0]
  const passes: Array<{ x: number; y: number; step: number }> = []
  for (let head = 0; head < queue.length && head < reach; head++) {
    const here = queue[head]
    if (here.x < minX.x || (here.x === minX.x && here.step < minX.step)) minX = here
    if (here.x > maxX.x || (here.x === maxX.x && here.step < maxX.step)) maxX = here
    if (here.y < minY.y || (here.y === minY.y && here.step < minY.step)) minY = here
    if (here.y > maxY.y || (here.y === maxY.y && here.step < maxY.step)) maxY = here
    for (let dir = 0; dir < 4; dir++) {
      const nx = here.x + DX[dir]
      const ny = here.y + DY[dir]
      if (blocked(nx, ny)) {
        const key = tileKey(nx, ny)
        if (seen.has(key)) continue
        seen.add(key)
        queue.push({ x: nx, y: ny, step: here.step + 1 })
        continue
      }
      const across = dir % 2 === 0
        ? blocked(nx, ny - 1) && blocked(nx, ny + 1)
        : blocked(nx - 1, ny) && blocked(nx + 1, ny)
      if (across) passes.push({ x: nx, y: ny, step: here.step + 1 })
    }
  }
  const found: Array<{ x: number; y: number; step: number }> = []
  const gaps = new Set<number>()
  const offer = (gap: { x: number; y: number; step: number }) => {
    if (blocked(gap.x, gap.y)) return
    const key = tileKey(gap.x, gap.y)
    if (gaps.has(key)) return
    gaps.add(key)
    found.push(gap)
  }
  const tips: Array<{ x: number; y: number; step: number }> = []
  for (const here of queue) {
    let touch = 0
    for (let dir = 0; dir < 4; dir++) if (blocked(here.x + DX[dir], here.y + DY[dir])) touch++
    if (touch <= 1) tips.push(here)
  }
  const ends = tips.length > 0 ? tips : [minX, maxX, minY, maxY]
  for (const tip of ends) {
    for (let dir = 0; dir < 4; dir++) offer({ x: tip.x + DX[dir], y: tip.y + DY[dir], step: tip.step + 1 })
  }
  for (const pass of passes) offer(pass)
  found.sort((a, b) => a.step - b.step)
  return found.map((gap) => ({ x: gap.x, y: gap.y }))
}

export function farOpening(
  x: number,
  y: number,
  blocked: (x: number, y: number) => boolean,
  reach = FAR_OPENING_REACH,
): { x: number; y: number } | null {
  return farOpenings(x, y, blocked, reach)[0] ?? null
}

