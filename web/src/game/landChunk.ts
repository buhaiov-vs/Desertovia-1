/**
 * Blocks of the land, 32 tiles on a side. The same split as the server's fog chunks
 * (`Exploration.ChunkSize`): chunk 0 holds tiles 0…31, and negative tiles stay in their own blocks.
 */

export const LAND_CHUNK = 32

export interface TileRect {
  x: number
  y: number
  width: number
  height: number
}

/** One block clipped to the land. `originX`/`originY` are the first tile that is both in the block and on the land. */
export interface LandChunkRect {
  x: number
  y: number
  originX: number
  originY: number
  width: number
  height: number
}

export function chunkOf(tile: number) {
  return Math.floor(tile / LAND_CHUNK)
}

function clipped(chunkX: number, chunkY: number, land: TileRect): LandChunkRect | null {
  const blockX = chunkX * LAND_CHUNK
  const blockY = chunkY * LAND_CHUNK
  const originX = Math.max(blockX, land.x)
  const originY = Math.max(blockY, land.y)
  const right = Math.min(blockX + LAND_CHUNK, land.x + land.width)
  const bottom = Math.min(blockY + LAND_CHUNK, land.y + land.height)
  if (right <= originX || bottom <= originY) return null
  return { x: chunkX, y: chunkY, originX, originY, width: right - originX, height: bottom - originY }
}

/** Every block that meets the land, edge blocks clipped to it. Together they cover each tile once. */
export function chunksCovering(land: TileRect): LandChunkRect[] {
  if (land.width <= 0 || land.height <= 0) return []
  const chunks: LandChunkRect[] = []
  const x0 = chunkOf(land.x)
  const y0 = chunkOf(land.y)
  const x1 = chunkOf(land.x + land.width - 1)
  const y1 = chunkOf(land.y + land.height - 1)
  for (let y = y0; y <= y1; y++) {
    for (let x = x0; x <= x1; x++) {
      const chunk = clipped(x, y, land)
      if (chunk) chunks.push(chunk)
    }
  }
  return chunks
}

/**
 * Blocks that meet the view, plus `marginChunks` blocks past its edges, clipped to the land.
 * The screen asks for these; the rest of the land is not drawn.
 */
export function chunksNear(view: TileRect, land: TileRect, marginChunks = 1): LandChunkRect[] {
  const pad = LAND_CHUNK * marginChunks
  const left = Math.max(land.x, Math.floor(view.x - pad))
  const top = Math.max(land.y, Math.floor(view.y - pad))
  const right = Math.min(land.x + land.width, Math.ceil(view.x + view.width + pad))
  const bottom = Math.min(land.y + land.height, Math.ceil(view.y + view.height + pad))
  if (right <= left || bottom <= top) return []
  const chunks: LandChunkRect[] = []
  for (let y = chunkOf(top); y <= chunkOf(bottom - 1); y++) {
    for (let x = chunkOf(left); x <= chunkOf(right - 1); x++) {
      const chunk = clipped(x, y, land)
      if (chunk) chunks.push(chunk)
    }
  }
  return chunks
}
