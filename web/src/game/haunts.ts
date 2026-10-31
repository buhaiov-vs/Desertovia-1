import { SPAWN_LEVELS, type RingLand, type SpawnPoint } from './spawnRings.ts'

/**
 * Lone foes haunting the land between the den rings, as the server's `Haunts` places them. The land is cut into
 * squares of `CELL` tiles; each square holds one foe at a seeded random tile. Its band is the highest ring whose floor
 * that tile reaches; the foe is of the band's level, or one level below or above it by `NEIGHBOUR` each.
 * A foe fights as a den with one protector of its level, standing on its own tile.
 */

/** Side of the square that holds one foe, in tiles. */
export const CELL = 64
/** Share of foes one level below the band, and again one level above it. */
export const NEIGHBOUR = 0.05
/** No foe stands this close to a den, in tiles, so it never joins a den fight. */
export const DEN_CLEARANCE = 40
/** Side of a foe's claim: it gives up a chase half this far from its tile. */
export const CLAIM = 32

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
