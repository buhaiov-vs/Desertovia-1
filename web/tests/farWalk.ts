import { FAR_LAND_LEFT, FAR_LAND_SIDE, FAR_LAND_TOP, farBlocksKind, farKind } from '../src/game/farWilds.ts'

/** Open far ground for a walk with nothing cleared, the way worldMap's farBlocked sees the land past the camp picture. */
export function farOpen(seed: number, from: { x: number; y: number }, to: { x: number; y: number }) {
  const startX = Math.floor(from.x)
  const startY = Math.floor(from.y)
  const goalX = Math.floor(to.x)
  const goalY = Math.floor(to.y)
  return (x: number, y: number) => {
    if (x === startX && y === startY) return true
    if (x < FAR_LAND_LEFT || y < FAR_LAND_TOP || x >= FAR_LAND_LEFT + FAR_LAND_SIDE || y >= FAR_LAND_TOP + FAR_LAND_SIDE) return false
    const kind = farKind(x, y, seed)
    if (x === goalX && y === goalY && (kind === 'thicket' || kind === 'mountain')) return true
    return !farBlocksKind(kind) && kind !== 'water'
  }
}
