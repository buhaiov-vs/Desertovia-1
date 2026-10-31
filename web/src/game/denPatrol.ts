/**
 * Where a den's protectors walk, matching the server's `DenGuards.PatrolAt`.
 * Eight of them circle the den. The gap on the way in is `PATROL_RADIUS * sin(π/8)`,
 * which stays inside `SIGHT`, so the spots two tiles out cannot be reached without meeting one.
 * The poison plant and the lich do not walk: they stand at their own reach and still cover the middle.
 */

/** Protectors who can walk circle the den at this distance, in tiles. */
export const PATROL_RADIUS = 7
/** A protector sees a hero this close, in tiles, and calls the rest. */
export const SIGHT = 4
/** One lap of the ring, in seconds. */
export const PATROL_SECONDS = 40

/** A protector that cannot walk stands this far out, in tiles: its strike, so the middle stays in range. */
const POST: Readonly<Record<number, number>> = { 14: 3, 30: 5 }

export function standsStill(level: number): boolean {
  return level in POST
}

/** Where this den's ring sits, in tiles. */
export function postRadius(level: number): number {
  return POST[level] ?? PATROL_RADIUS
}

/**
 * Where protector `index` of `count` is at rest, in tiles.
 * `centerX` and `centerY` are the den's middle. The same round as the server.
 */
export function patrolAt(
  centerX: number,
  centerY: number,
  index: number,
  count: number,
  level: number,
  unixMs: number,
): { x: number; y: number } {
  const turn = standsStill(level) ? 0 : (unixMs / 1000 / PATROL_SECONDS) * Math.PI * 2
  const angle = turn + (index * Math.PI * 2) / Math.max(1, count)
  const radius = postRadius(level)
  return { x: centerX + Math.cos(angle) * radius, y: centerY + Math.sin(angle) * radius }
}
