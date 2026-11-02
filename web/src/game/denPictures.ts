import type { EnemyKind } from '../api'
import type { FoeAnimation, FoeLook } from './foeSprites'

/** Den props are drawn this much larger than the heights in `DEN_PICTURES`. */
export const PROP_SCALE = 1.35
/** Protectors and roaming foes of level 1 are drawn this much larger than their own looks. */
export const FOE_SCALE = 1.4
/** Level 1 stands this tall, in world pixels: a slime at `FOE_SCALE`. */
const LEVEL_ONE_HEIGHT = 10 * FOE_SCALE
/** Each later level stands this many world pixels taller than the one before. */
const LEVEL_STEP = 4

/** Drawn height of a foe of `level`, in world pixels. Level 1 keeps today's size. */
export function levelHeight(level: number) {
  return LEVEL_ONE_HEIGHT + Math.max(0, level - 1) * LEVEL_STEP
}

/** One protector: an enemy the game already fights, or a figure of the archive atlas. */
export type Protector = EnemyKind | FoeLook
type ProtectorOf = (index: number, direction: number) => Protector

export interface DenPicture {
  /** Height of the den prop `den-<level>`, in world pixels. */
  prop: number
  protector: ProtectorOf
}

const look = (idle: FoeAnimation, height: number): ProtectorOf => () => ({ idle, height })
const known = (kind: EnemyKind): ProtectorOf => () => kind
const SLIMES: FoeAnimation[] = ['slime-blue', 'slime-green', 'slime-red']

/** The picture of each foe ring, by level. The prop comes from the area of that foe. */
export const DEN_PICTURES: readonly DenPicture[] = [
  { prop: 16, protector: (index) => ({ idle: SLIMES[index % SLIMES.length], height: 10 }) },
  { prop: 36, protector: known('Rat') },
  { prop: 28, protector: known('Imp') },
  { prop: 16, protector: look('goblin-small', 12) },
  { prop: 26, protector: known('Wolf') },
  { prop: 22, protector: known('Boar') },
  { prop: 16, protector: look('goblin-club', 14) },
  { prop: 16, protector: look('orc-hooded', 19) },
  { prop: 18, protector: look('scarecrow', 20) },
  { prop: 16, protector: known('Ghoul') },
  { prop: 40, protector: look('goblin-sword', 13) },
  { prop: 28, protector: look('goblin-banner', 13) },
  { prop: 40, protector: known('Wraith') },
  { prop: 28, protector: look('poison-plant', 24) },
  { prop: 24, protector: look('slime-flaming', 18) },
  { prop: 36, protector: look('orc-axe', 22) },
  { prop: 32, protector: known('Bear') },
  { prop: 36, protector: look('orc-warrior', 30) },
  { prop: 44, protector: look('vampire', 22) },
  { prop: 20, protector: look('orc-berserk', 31) },
  { prop: 36, protector: look('orc-shaman', 31) },
  { prop: 40, protector: look('vampire-winged', 24) },
  { prop: 40, protector: look('orc-swords', 24) },
  { prop: 26, protector: known('Zombie') },
  { prop: 32, protector: look('fire-spirit', 26) },
  { prop: 24, protector: known('Skeleton') },
  { prop: 36, protector: look('orc-armored', 27) },
  { prop: 30, protector: (_, direction) => ({ idle: `knight-${(direction % 8) + 1}` as FoeAnimation, scale: 1.2 }) },
  { prop: 40, protector: known('BarrowKing') },
  { prop: 44, protector: look('lich', 40) },
]

/** The den level whose protectors are this enemy; every enemy kind guards exactly one. */
export function denLevelOf(kind: EnemyKind) {
  return DEN_PICTURES.findIndex((picture) => picture.protector(0, 0) === kind) + 1
}
