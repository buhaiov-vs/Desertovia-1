import type { TilePoint, TripWalk } from '../api'

/** Route points matching domain `Trip.Route`: from, optional turns, then to. */
function routeOf(walk: TripWalk): TilePoint[] {
  const from = { x: walk.fromX, y: walk.fromY }
  const to = { x: walk.toX, y: walk.toY }
  if (!walk.way || walk.way.length === 0) return [from, to]
  const points = [from, ...walk.way.map((point) => ({ x: point.x, y: point.y }))]
  const last = points[points.length - 1]
  if (Math.hypot(last.x - to.x, last.y - to.y) > 1e-6) points.push(to)
  return points
}

function pathLength(route: TilePoint[]) {
  let length = 0
  for (let index = 0; index < route.length - 1; index++) {
    length += Math.hypot(route[index + 1].x - route[index].x, route[index + 1].y - route[index].y)
  }
  return length
}

/**
 * Where a walker stands at `nowMs` — same wall clock as server `Trip.PositionAt` / fog reveal.
 * After a background tab the ticker may have slept; this still lands on the revealed corridor.
 */
export function walkPositionAt(walk: TripWalk, nowMs: number): TilePoint {
  const route = routeOf(walk)
  const to = { x: walk.toX, y: walk.toY }
  if (route.length < 2) return to
  const setOut = Date.parse(walk.setOutAt)
  const arrives = Date.parse(walk.arrivesAt)
  const total = (arrives - setOut) / 1000
  const share = total <= 0 ? 1 : Math.min(1, Math.max(0, (nowMs - setOut) / 1000 / total))
  const target = pathLength(route) * share
  let walked = 0
  for (let index = 0; index < route.length - 1; index++) {
    const leg = Math.hypot(route[index + 1].x - route[index].x, route[index + 1].y - route[index].y)
    if (walked + leg >= target || index === route.length - 2) {
      const along = leg <= 0 ? 1 : Math.min(1, Math.max(0, (target - walked) / leg))
      return {
        x: route[index].x + (route[index + 1].x - route[index].x) * along,
        y: route[index].y + (route[index + 1].y - route[index].y) * along,
      }
    }
    walked += leg
  }
  return to
}

export function walkActive(walk: TripWalk | null | undefined, nowMs: number) {
  if (!walk) return false
  return nowMs < Date.parse(walk.arrivesAt)
}
