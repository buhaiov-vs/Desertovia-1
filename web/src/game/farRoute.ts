import type { TilePoint } from '../api'

/**
 * The way across the land beyond the camp picture. Empty land is a straight line. Otherwise a capped, slightly
 * greedy A* finds the way around what is not open, and only its turns are kept. The server's OverlandPath computes
 * the same turns.
 */

/** How many far tiles one walk may search before its way counts as blocked. Matches OverlandPath.FarSearchLimit. */
export const FAR_SEARCH_LIMIT = 300_000
/** How strongly the far search leans toward the goal. Routes run at most this much longer than the shortest. */
const FAR_GREED = 1.2
const DIAGONAL = 1.4142135623730951

/** The turns from `from` to `to`, ending at `to`, or null when no way is found within {@link FAR_SEARCH_LIMIT} tiles. */
export function farRoute(from: TilePoint, to: TilePoint, open: (x: number, y: number) => boolean): TilePoint[] | null {
  if (clearLine(from, to, open)) return [to]
  const tiles = farSearch(Math.floor(from.x), Math.floor(from.y), Math.floor(to.x), Math.floor(to.y), open)
  if (!tiles) return null
  const points: TilePoint[] = [from, ...tiles.slice(1, -1).map((tile) => ({ x: tile.x + 0.5, y: tile.y + 0.5 })), to]
  return pullWay(points, open)
}

function farTileKey(x: number, y: number) {
  return ((x + 8000) << 16) | ((y + 8000) & 0xffff)
}

/**
 * Tiles from the start to the goal by a slightly greedy A*, or null past {@link FAR_SEARCH_LIMIT} tiles.
 * Pops in the same order as the server's FarSearch, so both agree on what can be reached.
 */
function farSearch(startX: number, startY: number, goalX: number, goalY: number, open: (x: number, y: number) => boolean) {
  type Entry = { key: number; score: number; cost: number }
  const cost = new Map<number, number>()
  const cameFrom = new Map<number, number>()
  const closed = new Set<number>()
  const heap: Entry[] = []
  const heuristic = (x: number, y: number) => {
    const dx = Math.abs(x - goalX)
    const dy = Math.abs(y - goalY)
    return FAR_GREED * (Math.max(dx, dy) + (DIAGONAL - 1) * Math.min(dx, dy))
  }
  // Lower score first; on a tie the node further along, then the lower key.
  const before = (a: Entry, b: Entry) =>
    a.score < b.score || (a.score === b.score && (a.cost > b.cost || (a.cost === b.cost && a.key < b.key)))
  const push = (entry: Entry) => {
    heap.push(entry)
    let child = heap.length - 1
    while (child > 0) {
      const parent = (child - 1) >> 1
      if (!before(heap[child], heap[parent])) break
      ;[heap[parent], heap[child]] = [heap[child], heap[parent]]
      child = parent
    }
  }
  const pop = () => {
    const top = heap[0]
    const last = heap.pop()!
    if (heap.length > 0) {
      heap[0] = last
      let parent = 0
      for (;;) {
        const left = parent * 2 + 1
        const right = left + 1
        let first = parent
        if (left < heap.length && before(heap[left], heap[first])) first = left
        if (right < heap.length && before(heap[right], heap[first])) first = right
        if (first === parent) break
        ;[heap[parent], heap[first]] = [heap[first], heap[parent]]
        parent = first
      }
    }
    return top
  }
  const start = farTileKey(startX, startY)
  const goal = farTileKey(goalX, goalY)
  cost.set(start, 0)
  push({ key: start, score: heuristic(startX, startY), cost: 0 })
  while (heap.length > 0) {
    const { key: node, cost: reached } = pop()
    if (closed.has(node)) continue
    closed.add(node)
    if (node === goal) break
    if (closed.size > FAR_SEARCH_LIMIT) return null
    const x = (node >> 16) - 8000
    const y = (node & 0xffff) - 8000
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        if ((dx === 0 && dy === 0) || !open(x + dx, y + dy)) continue
        if (dx !== 0 && dy !== 0 && (!open(x + dx, y) || !open(x, y + dy))) continue
        const next = farTileKey(x + dx, y + dy)
        const nextCost = reached + (dx !== 0 && dy !== 0 ? DIAGONAL : 1)
        if (closed.has(next)) continue
        const known = cost.get(next)
        if (known !== undefined && nextCost >= known) continue
        cost.set(next, nextCost)
        cameFrom.set(next, node)
        push({ key: next, score: nextCost + heuristic(x + dx, y + dy), cost: nextCost })
      }
    }
  }
  if (!closed.has(goal)) return null
  const tiles: Array<{ x: number; y: number }> = []
  for (let node = goal; ; node = cameFrom.get(node)!) {
    tiles.push({ x: (node >> 16) - 8000, y: (node & 0xffff) - 8000 })
    if (node === start) break
  }
  return tiles.reverse()
}

/** Keeps only the turns of a route, reaching further while the straight line stays clear. Matches OverlandPath.Pull. */
function pullWay(points: TilePoint[], open: (x: number, y: number) => boolean) {
  const way: TilePoint[] = []
  for (let at = 0; at < points.length - 1; ) {
    let good = at + 1
    let reach = 1
    while (good + reach < points.length && clearLine(points[at], points[good + reach], open)) {
      good += reach
      reach *= 2
    }
    for (reach = Math.floor(reach / 2); reach > 0; reach = Math.floor(reach / 2)) {
      if (good + reach < points.length && clearLine(points[at], points[good + reach], open)) good += reach
    }
    way.push(points[good])
    at = good
  }
  return way
}

/** Whether every tile the straight line crosses is open, including both sides of a corner it touches. */
function clearLine(a: TilePoint, b: TilePoint, open: (x: number, y: number) => boolean) {
  let x = Math.floor(a.x)
  let y = Math.floor(a.y)
  const endX = Math.floor(b.x)
  const endY = Math.floor(b.y)
  const dx = b.x - a.x
  const dy = b.y - a.y
  const stepX = Math.sign(dx)
  const stepY = Math.sign(dy)
  const tDeltaX = dx === 0 ? Number.POSITIVE_INFINITY : Math.abs(1 / dx)
  const tDeltaY = dy === 0 ? Number.POSITIVE_INFINITY : Math.abs(1 / dy)
  let tMaxX = dx === 0 ? Number.POSITIVE_INFINITY : (stepX > 0 ? x + 1 - a.x : a.x - x) * tDeltaX
  let tMaxY = dy === 0 ? Number.POSITIVE_INFINITY : (stepY > 0 ? y + 1 - a.y : a.y - y) * tDeltaY
  if (!open(x, y)) return false
  const steps = Math.abs(endX - x) + Math.abs(endY - y) + 2
  for (let guard = 0; (x !== endX || y !== endY) && guard < steps; guard++) {
    if (tMaxX < tMaxY) {
      x += stepX
      tMaxX += tDeltaX
    } else if (tMaxY < tMaxX) {
      y += stepY
      tMaxY += tDeltaY
    } else {
      if (!open(x + stepX, y) || !open(x, y + stepY)) return false
      x += stepX
      y += stepY
      tMaxX += tDeltaX
      tMaxY += tDeltaY
    }
    if (!open(x, y)) return false
  }
  return x === endX && y === endY
}

/** The first tile on the straight line that is not open, to say what stops the walk. */
export function firstHit(a: TilePoint, b: TilePoint, open: (x: number, y: number) => boolean) {
  const steps = Math.max(1, Math.ceil(Math.hypot(b.x - a.x, b.y - a.y)))
  for (let step = 1; step <= steps; step++) {
    const x = Math.floor(a.x + ((b.x - a.x) * step) / steps)
    const y = Math.floor(a.y + ((b.y - a.y) * step) / steps)
    if (!open(x, y)) return { x, y }
  }
  return { x: Math.floor(b.x), y: Math.floor(b.y) }
}
