import type { EnemyKind } from '../api'

/**
 * The thirty foe rings around the camp, as the server's `SpawnRings` places them, eight points each.
 * A point lands at a seeded random place in a wide band: from its floor up to `SPREAD` tiles further out,
 * and up to `SWAY` either side of its direction. When no such place is dry land, it falls back to the ring itself:
 * a point past the land is left out, so past the straight edge only the four corner points remain,
 * and a point on water, a mountain or a thicket slides along its ring to the nearest open tile that is still at least
 * that foe's floor away.
 */

export interface SpawnLevel {
  level: number
  name: string
  /** Not closer to the camp than this, in tiles. */
  floor: number
  /** The pack this level is balanced for: one protector has this share of the den's strength. A den holds `PROTECTORS_PER_DEN`. */
  protectors: number
  /** The look the game already draws for this foe; null while the foe has no picture. */
  kind: EnemyKind | null
}

export interface SpawnPoint {
  /** Level and direction, `"level:direction"`, the same id the server keeps for a destroyed den. */
  id: string
  level: number
  name: string
  kind: EnemyKind | null
  protectors: number
  /** Side of the claim around the den, in tiles. */
  claim: number
  /** The den tile. */
  x: number
  y: number
  /** A lone foe of `haunts.ts`, not a den: it has no den, and it stands on its own tile. */
  roams?: boolean
}

export const POINTS_ON_A_RING = 8
export const SPOTS = 8
/** Protectors standing at every den, each as strong as one of its level's pack. */
export const PROTECTORS_PER_DEN = 8
export const PACK_CLAIM = 48
export const SINGLE_CLAIM = 64
/** A den lands up to this many tiles past its floor; the next ring starts about 270 tiles out. */
export const SPREAD = 200
/** A den turns up to this far either side of its direction, in radians (18 degrees). */
export const SWAY = Math.PI / 10
/** Random places tried before the den falls back to its own ring. */
const SCATTER_TRIES = 6

/** Closest the cave may stand, in tiles. A healthy walker takes 6 minutes. Wood groves start here too. */
export const CAVE_NEAR = 600
/** Farthest the cave may stand, in tiles. A healthy walker takes 1 hour. */
export const CAVE_FAR = 6_000
const CAVE_TRIES = 48
/** The home picture. The cave is never placed inside it. Mirrors the server's vale. */
const VALE = { x: -44, y: -32, width: 120, height: 90 }

const level = (level: number, name: string, floor: number, protectors: number, kind: EnemyKind | null): SpawnLevel => ({
  level,
  name,
  floor,
  protectors,
  kind,
})

export const SPAWN_LEVELS: readonly SpawnLevel[] = [
  level(1, 'Plain slime', 300, 3, null),
  level(2, 'Grave rat', 570, 4, 'Rat'),
  level(3, 'Imp', 841, 2, 'Imp'),
  level(4, 'Small goblin', 1_111, 3, null),
  level(5, 'Wolf', 1_382, 4, 'Wolf'),
  level(6, 'Boar', 1_652, 3, 'Boar'),
  level(7, 'Goblin with a club', 1_923, 3, null),
  level(8, 'Hooded orc', 2_193, 1, null),
  level(9, 'Scarecrow', 2_464, 1, null),
  level(10, 'Ghoul', 2_734, 2, 'Ghoul'),
  level(11, 'Goblin with a sword', 3_005, 3, null),
  level(12, 'Banner goblin', 3_275, 3, null),
  level(13, 'Wraith', 3_546, 1, 'Wraith'),
  level(14, 'Poison plant', 3_816, 1, null),
  level(15, 'Flaming slime', 4_087, 1, null),
  level(16, 'Orc with one axe', 4_357, 1, null),
  level(17, 'Bear', 4_628, 1, 'Bear'),
  level(18, 'Orc warrior', 4_898, 1, null),
  level(19, 'Vampire', 5_169, 1, null),
  level(20, 'Orc berserk', 5_439, 1, null),
  level(21, 'Orc shaman', 5_710, 1, null),
  level(22, 'Winged vampire', 5_980, 1, null),
  level(23, 'Orc with two swords', 6_251, 1, null),
  level(24, 'Zombie', 6_521, 1, 'Zombie'),
  level(25, 'Fire spirit', 6_792, 1, null),
  level(26, 'Skeleton', 7_062, 1, 'Skeleton'),
  level(27, 'Armored orc', 7_333, 1, null),
  level(28, 'Fortress knight', 7_603, 1, null),
  level(29, 'Barrow king', 7_874, 1, 'BarrowKing'),
  level(30, 'Lich', 8_144, 1, null),
]

interface Tiles {
  x: number
  y: number
  width: number
  height: number
}

export interface RingLand {
  /** The campfire, in tiles. */
  camp: { x: number; y: number }
  land: Tiles
  /** Ground no den, foe or cave mouth stands on: water, mountains and thickets. */
  blocked?: (x: number, y: number, seed: number) => boolean
}

/** The dens of one village. The same seed always places the same dens. */
export function spawnPoints(seed: number, { camp, land, blocked = () => false }: RingLand): SpawnPoint[] {
  const points: SpawnPoint[] = []
  for (const ring of SPAWN_LEVELS) {
    for (let direction = 0; direction < POINTS_ON_A_RING; direction++) {
      const tile = place(seed, ring.level, ring.floor, direction, camp, land, blocked)
      if (!tile) continue
      points.push({
        id: `${ring.level}:${direction}`,
        level: ring.level,
        name: ring.name,
        kind: ring.kind,
        protectors: PROTECTORS_PER_DEN,
        claim: ring.protectors > 1 ? PACK_CLAIM : SINGLE_CLAIM,
        x: tile.x,
        y: tile.y,
      })
    }
  }
  return points
}

/**
 * The one cave mouth. Wood and stone stay where they are. The same seed always returns the same tile,
 * between a 6-minute walk and a 1-hour walk, outside the vale. Matches the server's `CaveSite.At`.
 */
export function caveTile(seed: number, land: RingLand): { x: number; y: number } {
  const dens = spawnPoints(seed, land)
  const { camp } = land
  for (let attempt = 0; attempt < CAVE_TRIES; attempt++) {
    const radius = caveRadius(seed, attempt)
    const angle = caveRoll(seed, attempt * 2 + 1) * 2 * Math.PI
    const x = Math.floor(camp.x + Math.cos(angle) * radius)
    const y = Math.floor(camp.y + Math.sin(angle) * radius)
    if (caveFree(seed, x, y, dens, land, CAVE_FAR)) return { x, y }
  }
  const fallback = caveRoll(seed, 1) * 2 * Math.PI
  for (let radius = CAVE_NEAR; radius < land.land.width / 2; radius += 50) {
    const x = Math.floor(camp.x + Math.cos(fallback) * radius)
    const y = Math.floor(camp.y + Math.sin(fallback) * radius)
    if (caveFree(seed, x, y, dens, land, land.land.width)) return { x, y }
  }
  return { x: Math.floor(camp.x + CAVE_NEAR), y: Math.floor(camp.y) }
}

function caveRadius(seed: number, attempt: number) {
  const span = CAVE_FAR * CAVE_FAR - CAVE_NEAR * CAVE_NEAR
  return Math.sqrt(CAVE_NEAR * CAVE_NEAR + caveRoll(seed, attempt * 2) * span)
}

function caveFree(
  seed: number,
  x: number,
  y: number,
  dens: readonly SpawnPoint[],
  land: RingLand,
  farthest: number,
) {
  const { camp, land: bounds, blocked = () => false } = land
  if (x < bounds.x || y < bounds.y || x + 1 > bounds.x + bounds.width || y + 1 > bounds.y + bounds.height) return false
  if (x >= VALE.x && y >= VALE.y && x + 1 <= VALE.x + VALE.width && y + 1 <= VALE.y + VALE.height) return false
  if (blocked(x, y, seed)) return false
  const distance = Math.hypot(x + 0.5 - camp.x, y + 0.5 - camp.y)
  if (distance < CAVE_NEAR || distance > farthest) return false
  return dens.every((den) => Math.hypot(den.x - x, den.y - y) >= den.claim / 2)
}

/** A number in [0, 1). The same bits as the server's cave roll. */
function caveRoll(seed: number, draw: number) {
  let hash = (seed ^ 0xc0ffee01 ^ Math.imul(draw + 1, 0x9e3779b1)) >>> 0
  hash ^= hash >>> 16
  hash = Math.imul(hash, 0x85ebca6b) >>> 0
  hash ^= hash >>> 13
  hash = Math.imul(hash, 0xc2b2ae35) >>> 0
  hash ^= hash >>> 16
  return (hash >>> 0) / 4_294_967_296
}

/** Eight places around the den, two tiles out, starting east and turning clockwise. */
export function spotsAround(point: SpawnPoint): Array<{ x: number; y: number }> {
  return Array.from({ length: SPOTS }, (_, index) => {
    const angle = (index * Math.PI) / 4
    return { x: point.x + 0.5 + Math.cos(angle) * 2, y: point.y + 0.5 + Math.sin(angle) * 2 }
  })
}

function place(
  seed: number,
  level: number,
  floor: number,
  direction: number,
  camp: { x: number; y: number },
  land: Tiles,
  blocked: (x: number, y: number, seed: number) => boolean,
) {
  const onLand = (x: number, y: number) => x >= land.x && y >= land.y && x + 1 <= land.x + land.width && y + 1 <= land.y + land.height
  const angle = (direction * Math.PI) / 4
  for (let attempt = 0; attempt < SCATTER_TRIES; attempt++) {
    const sway = (roll(seed, level, direction, attempt * 2) * 2 - 1) * SWAY
    const radius = floor + roll(seed, level, direction, attempt * 2 + 1) * SPREAD
    const tile = near(camp, angle + sway, radius, floor)
    if (tile && onLand(tile.x, tile.y) && !blocked(tile.x, tile.y, seed)) return tile
  }
  const first = onRing(camp, angle, floor)
  if (!first || !onLand(first.x, first.y)) return null
  if (!blocked(first.x, first.y, seed)) return first
  for (let turn = 1; turn <= 8; turn++) {
    for (const sign of [1, -1]) {
      const slid = onRing(camp, angle + sign * turn * (Math.PI / 64), floor)
      if (!slid || !onLand(slid.x, slid.y) || blocked(slid.x, slid.y, seed)) continue
      return slid
    }
  }
  return null
}

function onRing(camp: { x: number; y: number }, angle: number, floor: number) {
  return near(camp, angle, floor, floor)
}

function near(camp: { x: number; y: number }, angle: number, radius: number, floor: number) {
  const x = Math.floor(camp.x + Math.cos(angle) * radius)
  const y = Math.floor(camp.y + Math.sin(angle) * radius)
  return Math.hypot(x + 0.5 - camp.x, y + 0.5 - camp.y) + 1 >= floor ? { x, y } : null
}

/** A number in [0, 1) fixed by the seed, the den and the draw: the same bits as the server's `Roll`. */
function roll(seed: number, level: number, direction: number, draw: number) {
  let hash = (seed ^ Math.imul(level, 0x9e3779b1) ^ Math.imul(direction + 1, 0x85ebca77) ^ Math.imul(draw + 1, 0xc2b2ae3d)) >>> 0
  hash ^= hash >>> 16
  hash = Math.imul(hash, 0x85ebca6b) >>> 0
  hash ^= hash >>> 13
  hash = Math.imul(hash, 0xc2b2ae35) >>> 0
  hash ^= hash >>> 16
  return (hash >>> 0) / 4_294_967_296
}
