import { SPAWN_LEVELS, type RingLand, type SpawnPoint } from './spawnRings.ts'

/**
 * Lone foes haunting the land between the den rings, as the server's `Haunts` places them. The land is cut into
 * squares of `CELL` tiles; each square holds one foe at a seeded random tile. Its band is the highest ring whose floor
 * that tile reaches; the foe is of the band's level, or one level below or above it by `NEIGHBOUR` each.
 * A foe wanders around its tile (`wanderAt`) and fights as a den with one protector of its level, starting where it walked.
 */

/** Side of the square that holds one foe, in tiles. */
export const CELL = 64
/** Share of foes one level below the band, and again one level above it. */
export const NEIGHBOUR = 0.05
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

/** The foe of one square; null when its tile is closer to the camp than the first ring, off the land, on water or near a den. */
export function hauntAt(seed: number, cellX: number, cellY: number, { camp, land, water = () => false }: RingLand, dens: readonly SpawnPoint[]): SpawnPoint | null {
  const x = cellX * CELL + Math.floor(roll(seed, cellX, cellY, 0) * CELL)
  const y = cellY * CELL + Math.floor(roll(seed, cellX, cellY, 1) * CELL)
  if (x < land.x || y < land.y || x + 1 > land.x + land.width || y + 1 > land.y + land.height || water(x, y, seed)) return null
  const centerX = x + 0.5
  const centerY = y + 0.5
  const distance = Math.sqrt((centerX - camp.x) * (centerX - camp.x) + (centerY - camp.y) * (centerY - camp.y))
  let band = 0
  while (band < SPAWN_LEVELS.length && SPAWN_LEVELS[band].floor <= distance) band++
  if (band === 0) return null
  for (const den of dens) {
    const dx = den.x + 0.5 - centerX
    const dy = den.y + 0.5 - centerY
    if (Math.sqrt(dx * dx + dy * dy) < DEN_CLEARANCE) return null
  }
  const mix = roll(seed, cellX, cellY, 2)
  const level = mix < NEIGHBOUR ? Math.max(1, band - 1) : mix < 2 * NEIGHBOUR ? Math.min(SPAWN_LEVELS.length, band + 1) : band
  const ring = SPAWN_LEVELS[level - 1]
  return { id: `h:${cellX}:${cellY}`, level: ring.level, name: ring.name, kind: ring.kind, protectors: 1, claim: CLAIM, x, y, roams: true }
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
