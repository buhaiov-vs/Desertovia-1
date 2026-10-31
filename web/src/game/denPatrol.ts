/**
 * Where a den's protectors walk, matching the server's `DenGuards.PatrolAt`.
 * Eight of them keep posts around the den and take a few steps here and there. Stepping into
 * `PATROL_RADIUS` of the middle wakes the den even when no one is looking that way.
 * The poison plant and the lich do not walk: they stand at their own reach and still cover the middle.
 */

/** How far from the den a walking protector may stray, in tiles. */
export const PATROL_RADIUS = 7
/** Walking protectors mill this far from their own post, in tiles. */
export const PATROL_STRIDE = 2.5
/** Share of `PATROL_RADIUS` where the eight posts sit, so a stride still stays in the disk. */
export const PATROL_POST_SHARE = 0.65
/** A protector sees a hero this close, in tiles, and calls the rest. */
export const SIGHT = 4
/** Every this many seconds a walking protector sets off to a new spot by its post. */
export const PATROL_LEG_SECONDS = 6
/** Strolling pace while milling the den, in tiles a second. */
export const PATROL_SPEED = 0.65

/** A protector that cannot walk stands this far out, in tiles: its strike, so the middle stays in range. */
const POST: Readonly<Record<number, number>> = { 14: 3, 30: 5 }

export function standsStill(level: number): boolean {
  return level in POST
}

/** Where this den's wake disk sits, in tiles. Rooted posts stand at their own reach. */
export function postRadius(level: number): number {
  return POST[level] ?? PATROL_RADIUS
}

/**
 * Where protector `index` of `count` is at rest, in tiles.
 * `centerX` and `centerY` are the den's middle. The same walk as the server.
 */
export function patrolAt(
  centerX: number,
  centerY: number,
  index: number,
  count: number,
  level: number,
  unixMs: number,
): { x: number; y: number } {
  const posts = Math.max(1, count)
  const postAngle = (index * Math.PI * 2) / posts
  if (standsStill(level)) {
    const rooted = postRadius(level)
    return { x: centerX + Math.cos(postAngle) * rooted, y: centerY + Math.sin(postAngle) * rooted }
  }

  const post = {
    x: centerX + Math.cos(postAngle) * PATROL_RADIUS * PATROL_POST_SHARE,
    y: centerY + Math.sin(postAngle) * PATROL_RADIUS * PATROL_POST_SHARE,
  }
  const seconds = unixMs / 1000
  const leg = Math.floor(seconds / PATROL_LEG_SECONDS)
  const tileX = Math.floor(centerX)
  const tileY = Math.floor(centerY)
  const from = waypoint(tileX, tileY, index, post, leg - 1)
  const to = waypoint(tileX, tileY, index, post, leg)
  const length = Math.hypot(to.x - from.x, to.y - from.y)
  const walk = Math.min(length / PATROL_SPEED, PATROL_LEG_SECONDS * 0.75)
  const share = walk <= 0 ? 1 : Math.min(1, (seconds - leg * PATROL_LEG_SECONDS) / walk)
  return { x: from.x + (to.x - from.x) * share, y: from.y + (to.y - from.y) * share }
}

function waypoint(tileX: number, tileY: number, index: number, post: { x: number; y: number }, leg: number) {
  const angle = roll(tileX, tileY, index, 1 + 2 * leg) * Math.PI * 2
  const radius = Math.sqrt(roll(tileX, tileY, index, 2 + 2 * leg)) * PATROL_STRIDE
  return { x: post.x + Math.cos(angle) * radius, y: post.y + Math.sin(angle) * radius }
}

/** A number in [0, 1) fixed by the den tile, the protector and the draw: the same bits as the server's `PatrolRoll`. */
function roll(tileX: number, tileY: number, index: number, draw: number) {
  let hash = (Math.imul(tileX, 0x27d4eb2f) ^ Math.imul(tileY, 0x165667b1) ^ Math.imul(index + 1, 0x9e3779b1) ^ Math.imul(draw + 1, 0x85ebca6b)) >>> 0
  hash ^= hash >>> 15
  hash = Math.imul(hash, 0x85ebca6b) >>> 0
  hash ^= hash >>> 13
  hash = Math.imul(hash, 0xc2b2ae35) >>> 0
  hash ^= hash >>> 16
  return (hash >>> 0) / 4_294_967_296
}
