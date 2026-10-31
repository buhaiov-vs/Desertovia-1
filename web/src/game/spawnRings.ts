import type { EnemyKind } from '../api'

/**
 * The thirty foe rings around the camp, as the server's `SpawnRings` places them, eight points each.
 * A point lands at a seeded random place in a wide band: from its floor up to `SPREAD` tiles further out,
 * and up to `SWAY` either side of its direction. When no such place is dry land, it falls back to the ring itself:
 * a point past the land is left out, so past the straight edge only the four corner points remain,
 * and a point on water slides along its ring to the nearest dry tile that is still at least that foe's floor away.
 */

export interface SpawnLevel {
  level: number
  name: string
  /** Not closer to the camp than this, in tiles. */
  floor: number
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
export const PACK_CLAIM = 48
export const SINGLE_CLAIM = 64
/** A den lands up to this many tiles past its floor; the next ring starts 541 tiles out. */
export const SPREAD = 400
/** A den turns up to this far either side of its direction, in radians (18 degrees). */
export const SWAY = Math.PI / 10
/** Random places tried before the den falls back to its own ring. */
const SCATTER_TRIES = 6

const level = (level: number, name: string, floor: number, protectors: number, kind: EnemyKind | null): SpawnLevel => ({
  level,
  name,
  floor,
  protectors,
  kind,
})

export const SPAWN_LEVELS: readonly SpawnLevel[] = [
  level(1, 'Plain slime', 600, 3, null),
  level(2, 'Grave rat', 1_141, 4, 'Rat'),
  level(3, 'Imp', 1_682, 2, 'Imp'),
  level(4, 'Small goblin', 2_223, 3, null),
  level(5, 'Wolf', 2_764, 4, 'Wolf'),
  level(6, 'Boar', 3_305, 3, 'Boar'),
  level(7, 'Goblin with a club', 3_846, 3, null),
  level(8, 'Hooded orc', 4_387, 1, null),
  level(9, 'Scarecrow', 4_928, 1, null),
  level(10, 'Ghoul', 5_469, 2, 'Ghoul'),
  level(11, 'Goblin with a sword', 6_010, 3, null),
  level(12, 'Banner goblin', 6_551, 3, null),
  level(13, 'Wraith', 7_092, 1, 'Wraith'),
  level(14, 'Poison plant', 7_633, 1, null),
  level(15, 'Flaming slime', 8_174, 1, null),
  level(16, 'Orc with one axe', 8_715, 1, null),
  level(17, 'Bear', 9_256, 1, 'Bear'),
  level(18, 'Orc warrior', 9_797, 1, null),
  level(19, 'Vampire', 10_338, 1, null),
  level(20, 'Orc berserk', 10_879, 1, null),
  level(21, 'Orc shaman', 11_420, 1, null),
  level(22, 'Winged vampire', 11_961, 1, null),
  level(23, 'Orc with two swords', 12_502, 1, null),
  level(24, 'Zombie', 13_043, 1, 'Zombie'),
  level(25, 'Fire spirit', 13_584, 1, null),
  level(26, 'Skeleton', 14_125, 1, 'Skeleton'),
  level(27, 'Armored orc', 14_666, 1, null),
  level(28, 'Fortress knight', 15_207, 1, null),
  level(29, 'Barrow king', 15_748, 1, 'BarrowKing'),
  level(30, 'Lich', 16_289, 1, null),
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
  /** Water that blocks a den. The land past the camp picture is dry today. */
  water?: (x: number, y: number, seed: number) => boolean
}

/** The dens of one village. The same seed always places the same dens. */
export function spawnPoints(seed: number, { camp, land, water = () => false }: RingLand): SpawnPoint[] {
  const points: SpawnPoint[] = []
  for (const ring of SPAWN_LEVELS) {
    for (let direction = 0; direction < POINTS_ON_A_RING; direction++) {
      const tile = place(seed, ring.level, ring.floor, direction, camp, land, water)
      if (!tile) continue
      points.push({
        id: `${ring.level}:${direction}`,
        level: ring.level,
        name: ring.name,
        kind: ring.kind,
        protectors: ring.protectors,
        claim: ring.protectors > 1 ? PACK_CLAIM : SINGLE_CLAIM,
        x: tile.x,
        y: tile.y,
      })
    }
  }
  return points
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
  water: (x: number, y: number, seed: number) => boolean,
) {
  const onLand = (x: number, y: number) => x >= land.x && y >= land.y && x + 1 <= land.x + land.width && y + 1 <= land.y + land.height
  const angle = (direction * Math.PI) / 4
  for (let attempt = 0; attempt < SCATTER_TRIES; attempt++) {
    const sway = (roll(seed, level, direction, attempt * 2) * 2 - 1) * SWAY
    const radius = floor + roll(seed, level, direction, attempt * 2 + 1) * SPREAD
    const tile = near(camp, angle + sway, radius, floor)
    if (tile && onLand(tile.x, tile.y) && !water(tile.x, tile.y, seed)) return tile
  }
  const first = onRing(camp, angle, floor)
  if (!first || !onLand(first.x, first.y)) return null
  if (!water(first.x, first.y, seed)) return first
  for (let turn = 1; turn <= 8; turn++) {
    for (const sign of [1, -1]) {
      const slid = onRing(camp, angle + sign * turn * (Math.PI / 64), floor)
      if (!slid || !onLand(slid.x, slid.y) || water(slid.x, slid.y, seed)) continue
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
