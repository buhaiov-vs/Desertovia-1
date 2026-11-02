import { farKind, farWildRoll, FAR_LAND_LEFT, FAR_LAND_SIDE, FAR_LAND_TOP, FAR_VALE } from './farWilds.ts'
import { POINTS_ON_A_RING, SPAWN_LEVELS, SPREAD, spawnPoints } from './spawnRings.ts'

/**
 * Coloured gathering patches in the open arcs between the dens of one foe ring.
 * The same seed places the same tiles as the server's WildGoods.
 */

export const WILD_HERBS = 'herbs'
export const WILD_FLINT = 'flint'
export const WILD_CLAY = 'clay'
export const WILD_SALT = 'salt'
export const WILD_RESIN = 'resin'
export const WILD_COPPER = 'copper'

export const WILD_PLACES = [WILD_HERBS, WILD_FLINT, WILD_CLAY, WILD_SALT, WILD_RESIN, WILD_COPPER] as const

export type WildPlace = (typeof WILD_PLACES)[number]

export const isWildPlace = (workplace: string): workplace is WildPlace =>
  (WILD_PLACES as readonly string[]).includes(workplace)

/** How far past a den's claim a patch must stand, in tiles. */
const CLEAR_OF_DEN = 10

/** Patches stay inside this distance from the camp, short of the land's edge. */
const REACH = 5_600

const CAMP = { x: 15.5, y: 13.5 }

export interface WildPatch {
  x: number
  y: number
  kind: WildPlace
  level: number
}

const cached = new Map<number, WildPatch[]>()

export function kindFor(level: number): WildPlace {
  if (level <= 3) return WILD_HERBS
  if (level <= 6) return WILD_FLINT
  if (level <= 9) return WILD_CLAY
  if (level <= 12) return WILD_SALT
  if (level <= 16) return WILD_RESIN
  return WILD_COPPER
}

export function wildPatches(seed: number): WildPatch[] {
  seed >>>= 0
  const ready = cached.get(seed)
  if (ready) return ready
  const built = build(seed)
  cached.set(seed, built)
  return built
}

/** The good on this tile, or null when the tile is empty ground. */
export function wildGoodAt(x: number, y: number, seed: number): WildPlace | null {
  for (const patch of wildPatches(seed)) {
    if (x >= patch.x && x < patch.x + 2 && y >= patch.y && y < patch.y + 2) return patch.kind
  }
  return null
}

function build(seed: number): WildPatch[] {
  const dens = spawnPoints(seed, {
    camp: CAMP,
    land: { x: FAR_LAND_LEFT, y: FAR_LAND_TOP, width: FAR_LAND_SIDE, height: FAR_LAND_SIDE },
    water: (x, y, ringSeed) => farKind(x, y, ringSeed) === 'water',
  })
  const patches: WildPatch[] = []
  for (const level of SPAWN_LEVELS) {
    const radius = level.floor + SPREAD / 2
    if (radius > REACH) break
    const kind = kindFor(level.level)
    for (let direction = 0; direction < POINTS_ON_A_RING; direction++) {
      const angle = (direction + 0.5) * Math.PI / 4
      const roll = farWildRoll(seed, level.level, direction, 1)
      const mid = radius + (roll - 0.5) * 36
      const placed =
        place(angle, mid, seed, dens, patches) ??
        place(angle, mid + 28, seed, dens, patches) ??
        place(angle, mid - 28, seed, dens, patches) ??
        place(angle, mid + 56, seed, dens, patches) ??
        place(angle, mid - 56, seed, dens, patches) ??
        place(angle, mid + 84, seed, dens, patches) ??
        place(angle, mid - 84, seed, dens, patches)
      if (placed) patches.push({ x: placed.x, y: placed.y, kind, level: level.level })
    }
  }
  return patches
}

function place(
  angle: number,
  radius: number,
  seed: number,
  dens: { x: number; y: number; claim: number }[],
  placed: WildPatch[],
): { x: number; y: number } | null {
  const x = Math.floor(CAMP.x + Math.cos(angle) * radius)
  const y = Math.floor(CAMP.y + Math.sin(angle) * radius)
  if (!fits(x, y, seed) || tooClose(x, y, dens) || overlaps(x, y, placed)) return null
  return { x, y }
}

function fits(x: number, y: number, seed: number) {
  for (let dy = 0; dy < 2; dy++) {
    for (let dx = 0; dx < 2; dx++) {
      const tileX = x + dx
      const tileY = y + dy
      if (!onLand(tileX, tileY) || inVale(tileX, tileY)) return false
      const kind = farKind(tileX, tileY, seed)
      if (kind === 'mountain' || kind === 'thicket' || kind === 'rockfall' || kind === 'water') return false
    }
  }
  return true
}

function onLand(x: number, y: number) {
  return x >= FAR_LAND_LEFT && y >= FAR_LAND_TOP && x < FAR_LAND_LEFT + FAR_LAND_SIDE && y < FAR_LAND_TOP + FAR_LAND_SIDE
}

function inVale(x: number, y: number) {
  return x >= FAR_VALE.x && y >= FAR_VALE.y && x < FAR_VALE.x + FAR_VALE.width && y < FAR_VALE.y + FAR_VALE.height
}

function tooClose(x: number, y: number, dens: { x: number; y: number; claim: number }[]) {
  for (const den of dens) {
    const reach = Math.floor(den.claim / 2) + CLEAR_OF_DEN
    const limit = reach * reach
    const left = den.x - x
    const top = den.y - y
    if (left * left + top * top < limit) return true
    const right = den.x - (x + 1)
    const bottom = den.y - (y + 1)
    if (right * right + bottom * bottom < limit) return true
  }
  return false
}

function overlaps(x: number, y: number, placed: WildPatch[]) {
  return placed.some((patch) => Math.abs(patch.x - x) < 2 && Math.abs(patch.y - y) < 2)
}
