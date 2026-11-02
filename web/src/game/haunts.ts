import { SPAWN_LEVELS, type RingLand, type SpawnPoint } from './spawnRings.ts'

/**
 * Lone foes haunting the land around the village and between the den rings, as the server's `Haunts` places them.
 * The land is cut into squares of `CELL` tiles; each square holds one foe at a seeded random tile. Its band is the
 * highest ring whose floor that tile reaches, and the first band inside the first ring; the foe is of the band's
 * level, or up to three levels above or two below it by `SHIFTS`.
 * A foe wanders around its tile (`wanderAt`) and fights as a den with one protector of its level, starting where it walked.
 */

/** Side of the square that holds one foe, in tiles: about two foes for every 64 × 64. */
export const CELL = 45
/** Levels off the band and the share of foes of each: the further off, the rarer. The other 76% are of the band. */
export const SHIFTS: ReadonlyArray<readonly [levels: number, share: number]> = [[3, 0.03], [2, 0.06], [1, 0.09], [-1, 0.05], [-2, 0.01]]
/** No foe stands this close to a den, in tiles, so it never joins a den fight. */
export const DEN_CLEARANCE = 40
/** Side of a foe's claim: it gives up a chase half this far from where the chase began. */
export const CLAIM = 32
/** A foe wanders this far from its tile at most, in tiles. */
export const WANDER_RADIUS = 8
/** Every this many seconds a foe sets off to a new spot of its wander. */
export const LEG_SECONDS = 24
/** Strolling pace, in tiles a second; a long leg is walked faster so it ends within three quarters of the leg. */
export const WANDER_SPEED = 0.5

/**
 * Where a foe is at `unixMs`, at rest, in tiles: leg k walks from the spot of leg k − 1 to the spot of leg k, then waits.
 * The same walk as the server's `Haunts.PositionAt`.
 */
export function wanderAt(seed: number, foe: SpawnPoint, unixMs: number): { x: number; y: number } {
  const seconds = unixMs / 1000
  const leg = Math.floor(seconds / LEG_SECONDS)
  const cellX = Math.floor(foe.x / CELL)
  const cellY = Math.floor(foe.y / CELL)
  const from = waypoint(seed, cellX, cellY, foe, leg - 1)
  const to = waypoint(seed, cellX, cellY, foe, leg)
  const length = Math.sqrt((to.x - from.x) * (to.x - from.x) + (to.y - from.y) * (to.y - from.y))
  const walk = Math.min(length / WANDER_SPEED, LEG_SECONDS * 0.75)
  const share = walk <= 0 ? 1 : Math.min(1, (seconds - leg * LEG_SECONDS) / walk)
  return { x: from.x + (to.x - from.x) * share, y: from.y + (to.y - from.y) * share }
}

function waypoint(seed: number, cellX: number, cellY: number, foe: SpawnPoint, leg: number) {
  const angle = roll(seed, cellX, cellY, 3 + 2 * leg) * 2 * Math.PI
  const radius = Math.sqrt(roll(seed, cellX, cellY, 4 + 2 * leg)) * WANDER_RADIUS
  return { x: foe.x + 0.5 + Math.cos(angle) * radius, y: foe.y + 0.5 + Math.sin(angle) * radius }
}

interface Rect {
  x: number
  y: number
  width: number
  height: number
}

/** The camp picture, in tiles: the server's `WorldMap.Vale`. */
const VALE = { x: -44, y: -32, width: 120, height: 90 }
/** No foe stands this close to the vale, in tiles: its wander plus the reach that wakes it (`DenGuards.AggroRange`). */
export const VALE_CLEARANCE = WANDER_RADIUS + 6

/** Distance from a point to the nearest tile of the vale, in tiles; 0 inside it. */
export function fromVale(x: number, y: number): number {
  const dx = Math.max(Math.max(VALE.x - x, 0), x - (VALE.x + VALE.width))
  const dy = Math.max(Math.max(VALE.y - y, 0), y - (VALE.y + VALE.height))
  return Math.sqrt(dx * dx + dy * dy)
}

/**
 * The foe of one square; null when its tile is off the land, near a den or near the vale, or when water, a mountain or a
 * thicket lies within its wander, so it never stands on or walks into one. Between the vale
 * and the first ring the foes are of the first band, so level 1 foes roam around the village.
 */
export function hauntAt(seed: number, cellX: number, cellY: number, { camp, land, blocked = () => false }: RingLand, dens: readonly SpawnPoint[]): SpawnPoint | null {
  const x = cellX * CELL + Math.floor(roll(seed, cellX, cellY, 0) * CELL)
  const y = cellY * CELL + Math.floor(roll(seed, cellX, cellY, 1) * CELL)
  if (x < land.x || y < land.y || x + 1 > land.x + land.width || y + 1 > land.y + land.height) return null
  const reach = WANDER_RADIUS + 1
  for (let dy = -reach; dy <= reach; dy++) {
    for (let dx = -reach; dx <= reach; dx++) {
      if (dx * dx + dy * dy <= reach * reach && blocked(x + dx, y + dy, seed)) return null
    }
  }
  const centerX = x + 0.5
  const centerY = y + 0.5
  const distance = Math.sqrt((centerX - camp.x) * (centerX - camp.x) + (centerY - camp.y) * (centerY - camp.y))
  let band = 1
  while (band < SPAWN_LEVELS.length && SPAWN_LEVELS[band].floor <= distance) band++
  if (fromVale(centerX, centerY) < VALE_CLEARANCE) return null
  for (const den of dens) {
    const dx = den.x + 0.5 - centerX
    const dy = den.y + 0.5 - centerY
    if (Math.sqrt(dx * dx + dy * dy) < DEN_CLEARANCE) return null
  }
  const ring = SPAWN_LEVELS[levelOf(band, roll(seed, cellX, cellY, 2)) - 1]
  return { id: `h:${cellX}:${cellY}`, level: ring.level, name: ring.name, kind: ring.kind, protectors: 1, claim: CLAIM, x, y, roams: true }
}

/** The level of a foe of `band` for a `mix` in [0, 1), within the known levels: the server's `Haunts.LevelOf`. */
export function levelOf(band: number, mix: number): number {
  let share = 0
  for (const [levels, chance] of SHIFTS) {
    share += chance
    if (mix < share) return Math.min(SPAWN_LEVELS.length, Math.max(1, band + levels))
  }

  return band
}

/** Foes whose tile lies inside `rect`, in tiles. */
export function hauntsIn(seed: number, rect: Rect, ring: RingLand, dens: readonly SpawnPoint[]): SpawnPoint[] {
  const found: SpawnPoint[] = []
  for (let cellY = Math.floor(rect.y / CELL); cellY <= Math.floor((rect.y + rect.height) / CELL); cellY++) {
    for (let cellX = Math.floor(rect.x / CELL); cellX <= Math.floor((rect.x + rect.width) / CELL); cellX++) {
      const foe = hauntAt(seed, cellX, cellY, ring, dens)
      if (foe && foe.x >= rect.x && foe.x < rect.x + rect.width && foe.y >= rect.y && foe.y < rect.y + rect.height) found.push(foe)
    }
  }
  return found
}

/** A number in [0, 1) fixed by the seed, the square and the draw: the same bits as the server's `Roll`. */
function roll(seed: number, cellX: number, cellY: number, draw: number) {
  let hash = (seed ^ Math.imul(cellX, 0x27d4eb2f) ^ Math.imul(cellY, 0x165667b1) ^ Math.imul(draw + 1, 0x9e3779b1)) >>> 0
  hash ^= hash >>> 15
  hash = Math.imul(hash, 0x85ebca6b) >>> 0
  hash ^= hash >>> 13
  hash = Math.imul(hash, 0xc2b2ae35) >>> 0
  hash ^= hash >>> 16
  return (hash >>> 0) / 4_294_967_296
}
