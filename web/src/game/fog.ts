import { CanvasSource, Container, Sprite, Texture } from 'pixi.js'
import { TILE } from './sheets'
import { LAND_CHUNK as CHUNK, type LandChunkRect } from './landChunk'
import { MAP_HEIGHT, MAP_LEFT, MAP_TOP, MAP_WIDTH } from './worldMap'
import type { ExploredChunk, TileArea } from '../api'

/** One fog sprite: stable `x`,`y` chunk indices, painted over the clipped tile rectangle. */
type FogChunk = Pick<LandChunkRect, 'x' | 'y' | 'originX' | 'originY' | 'width' | 'height'>

/** Base64 from the server. A bad string leaves that chunk fog instead of stopping the map. */
function decodeBits(encoded: string) {
  try {
    return atob(encoded)
  } catch {
    return ''
  }
}

/** Something that sees the land around it; coordinates and radius in tiles. */
export interface Viewer {
  x: number
  y: number
  radius: number
  /**
   * Buildings and sites: stamped into walk memory, but they must not keep fog chunks “live”
   * every tick or the whole village repaints and the map hitchs.
   */
  sticky?: boolean
}

const DARK: [number, number, number] = [12, 14, 20]
/** Explored land stays fully clear. Night still uses the world night overlay. */
const REMEMBERED_ALPHA = 0
/** One fog texel per tile — hard bumps, not a smooth disc. */
const FAR_PIXELS = 1
/** Memory at or above this counts as fully open when shading. */
const MEMORY_OPEN = 0.4
/** A point is in sight when it is lit at least this much (gameplay; fog paint does not use a disc). */
const IN_SIGHT = 0.5
/**
 * Outer band of the sight disc, in tiles. Inside is always open; in this rim each tile opens or stays
 * fog by a stable hash so the edge looks organic instead of a perfect stair-circle.
 */
const SIGHT_RIM = 1.75

/** Server exploration is baked here then maxed into walk memory so a publish never overwrites walk discs. */
let bakeServerScratch = new Uint8Array(0)

/** Stable 0…1 per tile — same rim bumps every visit. */
function rimRoll(tileX: number, tileY: number) {
  let value = Math.imul(tileX | 0, 0x27d4eb2d) ^ Math.imul(tileY | 0, 0x165667b1) ^ 0x9e3779b9
  value = Math.imul(value ^ (value >>> 16), 0x85ebca6b)
  value = Math.imul(value ^ (value >>> 13), 0xc2b2ae35)
  return ((value ^ (value >>> 16)) >>> 0) / 4294967296
}

/**
 * Whether fog memory opens this tile for a viewer. Gameplay `inSight` still uses the full disc;
 * only the painted/stamped rim is dithered.
 */
function fogOpensTile(tileX: number, tileY: number, viewer: Viewer) {
  const distance = Math.hypot(tileX + 0.5 - viewer.x, tileY + 0.5 - viewer.y)
  const inner = Math.max(0, viewer.radius - SIGHT_RIM)
  if (distance <= inner) return true
  if (distance > viewer.radius) return false
  const depth = (viewer.radius - distance) / SIGHT_RIM
  return rimRoll(tileX, tileY) < depth * depth
}
/**
 * Extra fog pixels around the camp picture canvas (minimap). Linear filtering otherwise blends the
 * rim with empty texture and draws a bright teal fringe where the vale ends.
 */
const EDGE_PAD = 1

/**
 * Fog of war over the map. One chunk-sprite layer covers camp land and far land alike.
 * Fog opens in whole tiles (server bits + tile under each walker), not a circular vision ring.
 */
export class Fog {
  /** Camp picture bitmap for the minimap; not drawn on the world. */
  readonly canvas = document.createElement('canvas')
  /** Kept for destroy/layout; hidden — world fog is only `far`. */
  readonly sprite: Sprite
  /** Darkness for every land chunk near the camera, camp and wild together. */
  readonly far = new Container()
  private readonly farSprites = new Map<string, { sprite: Sprite; canvas: HTMLCanvasElement; context: CanvasRenderingContext2D; texture: Texture }>()
  /**
   * How strongly each pixel of a far chunk has been seen, 0 to 255.
   * Kept as the walkers move, so the land behind them stays open instead of snapping shut and popping back.
   */
  private readonly farSeen = new Map<string, Uint8Array>()
  /** Far chunks that held a live walker last cover; leaving the set freezes their fog once more. */
  private liveFar = new Set<string>()
  /** After a server sync, every on-screen far chunk must be painted again. */
  private farPaintDirty = true
  private farImage: ImageData | null = null
  private savedChunks: ExploredChunk[] = []
  /** Decoded exploration bits per chunk key — `chunkSees` used to `atob` on every tile probe. */
  private readonly decodedBits = new Map<string, string>()
  private viewers: Viewer[] = []
  /** Padded copy drawn on the map so filtering never samples transparent outside the vale. */
  private readonly display = document.createElement('canvas')
  private readonly texture: Texture
  private readonly context: CanvasRenderingContext2D
  private readonly displayContext: CanvasRenderingContext2D
  private readonly image: ImageData
  private readonly explored = new Uint8Array(MAP_WIDTH * MAP_HEIGHT)
  /** 0 out of sight, 1 in full sight — gameplay `inSight` only; world fog ignores this disc. */
  private readonly light = new Float32Array(MAP_WIDTH * MAP_HEIGHT)
  /** 0 at full daylight, when remembered land is not dimmed, 1 deep in the night. */
  private darkness = 1

  constructor() {
    this.canvas.width = MAP_WIDTH
    this.canvas.height = MAP_HEIGHT
    this.context = this.canvas.getContext('2d')!
    this.image = this.context.createImageData(MAP_WIDTH, MAP_HEIGHT)
    this.display.width = MAP_WIDTH + EDGE_PAD * 2
    this.display.height = MAP_HEIGHT + EDGE_PAD * 2
    this.displayContext = this.display.getContext('2d')!
    this.texture = new Texture({ source: new CanvasSource({ resource: this.display, scaleMode: 'nearest' }) })
    this.sprite = new Sprite(this.texture)
    this.sprite.position.set((MAP_LEFT - EDGE_PAD) * TILE, (MAP_TOP - EDGE_PAD) * TILE)
    this.sprite.scale.set(TILE)
    this.sprite.visible = false
    this.draw()
  }

  destroy() {
    this.texture.destroy(true)
    for (const entry of this.farSprites.values()) {
      entry.sprite.destroy()
      entry.texture.destroy(true)
    }
    this.farSprites.clear()
    this.farSeen.clear()
    this.decodedBits.clear()
  }

  /**
   * Merges visited chunks into the seen land. Exploration only grows: tiles the local walker already lit stay lit
   * when a village update arrives, so a publish cannot wipe the path and snap fog onto a stale corridor.
   */
  private lastExplored: string | null = null
  setChunks(chunks: ExploredChunk[]) {
    const signature = chunks.map(chunk => `${chunk.x},${chunk.y},${chunk.complete ? 1 : 0},${chunk.bits ?? ''}`).join(';')
    if (signature === this.lastExplored) return
    this.lastExplored = signature
    this.savedChunks = chunks
    this.decodedBits.clear()
    for (const chunk of chunks) {
      const originX = chunk.x * CHUNK
      const originY = chunk.y * CHUNK
      const key = `${chunk.x},${chunk.y}`
      const bits = chunk.bits ? decodeBits(chunk.bits) : ''
      if (chunk.bits) this.decodedBits.set(key, bits)
      for (let local = 0; local < CHUNK * CHUNK; local++) {
        const seen = chunk.complete || ((bits.charCodeAt(local >> 3) >> (local & 7)) & 1) === 1
        if (!seen) continue
        const index = this.index(originX + (local % CHUNK), originY + Math.floor(local / CHUNK))
        if (index >= 0) this.explored[index] = 1
      }
    }
    const length = CHUNK * FAR_PIXELS * CHUNK * FAR_PIXELS
    for (const key of [...this.farSeen.keys()]) {
      const [chunkX, chunkY] = key.split(',').map(Number)
      const cells = new Uint8Array(length)
      this.writeServerTiles(chunkX, chunkY, cells)
      this.farSeen.set(key, cells)
    }
    this.stampFar()
    this.farPaintDirty = true
    this.draw()
  }

  /** Updates gameplay sight (full disc) and opens organic hard tiles into fog memory. */
  update(viewers: Viewer[], darkness: number) {
    this.darkness = darkness
    this.viewers = viewers
    this.light.fill(0)
    for (const viewer of viewers) {
      const left = Math.max(MAP_LEFT, Math.floor(viewer.x - viewer.radius))
      const right = Math.min(MAP_LEFT + MAP_WIDTH - 1, Math.ceil(viewer.x + viewer.radius))
      const top = Math.max(MAP_TOP, Math.floor(viewer.y - viewer.radius))
      const bottom = Math.min(MAP_TOP + MAP_HEIGHT - 1, Math.ceil(viewer.y + viewer.radius))
      for (let y = top; y <= bottom; y++) {
        for (let x = left; x <= right; x++) {
          const distance = Math.hypot(x + 0.5 - viewer.x, y + 0.5 - viewer.y)
          if (distance > viewer.radius) continue
          const index = (y - MAP_TOP) * MAP_WIDTH + (x - MAP_LEFT)
          this.light[index] = 1
          if (fogOpensTile(x, y, viewer)) this.explored[index] = 1
        }
      }
    }
    this.stampFar()
    this.draw()
  }

  private index(x: number, y: number) {
    const column = Math.floor(x) - MAP_LEFT
    const row = Math.floor(y) - MAP_TOP
    if (column < 0 || row < 0 || column >= MAP_WIDTH || row >= MAP_HEIGHT) return -1
    return row * MAP_WIDTH + column
  }

  /** Whether the tile under a point, in tiles, has been seen. */
  isExplored(x: number, y: number) {
    if (this.seenNow(x, y)) return true
    if (this.memorySees(x, y)) return true
    const index = this.index(x, y)
    if (index >= 0 && this.explored[index] === 1) return true
    return this.chunkSees(Math.floor(x), Math.floor(y))
  }

  /** Walk/server fog memory at a tile — same buffer for camp and wild. */
  private memorySees(x: number, y: number) {
    const tileX = Math.floor(x)
    const tileY = Math.floor(y)
    const chunkX = Math.floor(tileX / CHUNK)
    const chunkY = Math.floor(tileY / CHUNK)
    const cells = this.farSeen.get(`${chunkX},${chunkY}`)
    if (!cells) return false
    const stride = CHUNK * FAR_PIXELS
    const px = Math.min(stride - 1, Math.max(0, Math.floor((x - chunkX * CHUNK) * FAR_PIXELS)))
    const py = Math.min(stride - 1, Math.max(0, Math.floor((y - chunkY * CHUNK) * FAR_PIXELS)))
    return cells[py * stride + px] >= MEMORY_OPEN * 255
  }

  isAreaExplored(area: TileArea) {
    for (let y = area.y; y < area.y + area.height; y++) {
      for (let x = area.x; x < area.x + area.width; x++) if (!this.isExplored(x, y)) return false
    }
    return true
  }

  /** Whether someone sees the point, in tiles, right now. */
  inSight(x: number, y: number) {
    const index = this.index(x, y)
    if (index >= 0) return this.light[index] >= IN_SIGHT
    return this.seenNow(x, y)
  }

  /**
   * Darkness of each tile in a rectangle, 0 where someone is looking and 1 on land nobody has seen.
   * `out` has one entry per tile, row by row, and must already be `width * height` long.
   */
  fillDarkness(x0: number, y0: number, width: number, height: number, out: Float32Array) {
    const remembered = REMEMBERED_ALPHA * this.darkness
    for (let row = 0; row < height; row++) {
      const y = y0 + row
      for (let column = 0; column < width; column++) {
        const x = x0 + column
        out[row * width + column] = this.isExplored(x, y) ? remembered : 1
      }
    }
    for (const viewer of this.viewers) {
      const left = Math.max(x0, Math.floor(viewer.x - viewer.radius))
      const right = Math.min(x0 + width - 1, Math.ceil(viewer.x + viewer.radius))
      const top = Math.max(y0, Math.floor(viewer.y - viewer.radius))
      const bottom = Math.min(y0 + height - 1, Math.ceil(viewer.y + viewer.radius))
      for (let y = top; y <= bottom; y++) {
        for (let x = left; x <= right; x++) {
          if (Math.hypot(x + 0.5 - viewer.x, y + 0.5 - viewer.y) > viewer.radius) continue
          const index = (y - y0) * width + (x - x0)
          if (remembered < out[index]) out[index] = remembered
        }
      }
    }
  }

  /**
   * Paints darkness on land chunks near the camera (camp picture and far land use the same sprites).
   * A chunk that scrolls away is dropped. Only chunks under a walker, newly created, leaving a
   * walker's sight, or dirtied by a server sync are repainted.
   */
  cover(chunks: FogChunk[]) {
    const wanted = new Set<string>()
    const nextLive = new Set<string>()
    for (const chunk of chunks) {
      const key = `${chunk.x},${chunk.y}`
      wanted.add(key)
      let entry = this.farSprites.get(key)
      let created = false
      if (
        !entry ||
        entry.canvas.width !== chunk.width * FAR_PIXELS ||
        entry.canvas.height !== chunk.height * FAR_PIXELS
      ) {
        if (entry) {
          entry.sprite.destroy()
          entry.texture.destroy(true)
          this.farSprites.delete(key)
        }
        entry = this.makeFar(chunk)
        created = true
      } else {
        entry.sprite.position.set(chunk.originX * TILE, chunk.originY * TILE)
      }
      const area = { x: chunk.originX, y: chunk.originY, width: chunk.width, height: chunk.height }
      const isLive = this.chunkMeetsViewers(area)
      if (isLive) nextLive.add(key)
      const leaving = this.liveFar.has(key) && !isLive
      if (created || isLive || leaving || this.farPaintDirty) {
        this.paintFar(key, entry, area)
      }
    }
    for (const [key, entry] of this.farSprites) {
      if (wanted.has(key)) continue
      entry.sprite.destroy()
      entry.texture.destroy(true)
      this.farSprites.delete(key)
    }
    this.liveFar = nextLive
    this.farPaintDirty = false
  }

  /** Whether a moving walker reaches this fog sprite (repaint while they cross it). */
  private chunkMeetsViewers(chunk: TileArea) {
    for (const viewer of this.viewers) {
      if (viewer.sticky) continue
      if (viewer.x + viewer.radius < chunk.x) continue
      if (viewer.x - viewer.radius > chunk.x + chunk.width) continue
      if (viewer.y + viewer.radius < chunk.y) continue
      if (viewer.y - viewer.radius > chunk.y + chunk.height) continue
      return true
    }
    return false
  }

  private seenNow(x: number, y: number) {
    return this.viewers.some((viewer) => Math.hypot(x + 0.5 - viewer.x, y + 0.5 - viewer.y) <= viewer.radius)
  }

  private chunkSees(x: number, y: number) {
    const chunkX = Math.floor(x / CHUNK)
    const chunkY = Math.floor(y / CHUNK)
    const key = `${chunkX},${chunkY}`
    const chunk = this.savedChunks.find((item) => item.x === chunkX && item.y === chunkY)
    if (!chunk) return false
    if (chunk.complete || !chunk.bits) return chunk.complete
    let bits = this.decodedBits.get(key)
    if (bits === undefined) {
      bits = decodeBits(chunk.bits)
      this.decodedBits.set(key, bits)
    }
    const local = (y - chunkY * CHUNK) * CHUNK + (x - chunkX * CHUNK)
    return ((bits.charCodeAt(local >> 3) >> (local & 7)) & 1) === 1
  }

  /**
   * Server exploration only — same for camp and wild. Local vale `explored` must not open fog memory
   * alone, or the camp picture rectangle shades differently from the land around it.
   */
  private serverSees(x: number, y: number) {
    return this.chunkSees(Math.floor(x), Math.floor(y))
  }

  private makeFar(chunk: FogChunk) {
    const canvas = document.createElement('canvas')
    canvas.width = chunk.width * FAR_PIXELS
    canvas.height = chunk.height * FAR_PIXELS
    const context = canvas.getContext('2d')!
    const texture = new Texture({ source: new CanvasSource({ resource: canvas, scaleMode: 'nearest' }) })
    const sprite = new Sprite(texture)
    sprite.position.set(chunk.originX * TILE, chunk.originY * TILE)
    sprite.scale.set(TILE / FAR_PIXELS)
    this.far.addChild(sprite)
    const entry = { sprite, canvas, context, texture }
    this.farSprites.set(`${chunk.x},${chunk.y}`, entry)
    return entry
  }

  /**
   * Opens whole tiles in sight. Interior of the disc is solid; the outer rim dither-opens by tile hash
   * so the frontier looks organic (still binary open/closed — no soft grey ring).
   */
  private stampFar() {
    const stampSticky = this.farPaintDirty
    for (const viewer of this.viewers) {
      if (viewer.sticky && !stampSticky) continue
      const left = Math.floor(viewer.x - viewer.radius)
      const right = Math.ceil(viewer.x + viewer.radius)
      const top = Math.floor(viewer.y - viewer.radius)
      const bottom = Math.ceil(viewer.y + viewer.radius)
      for (let ty = top; ty <= bottom; ty++) {
        for (let tx = left; tx <= right; tx++) {
          if (!fogOpensTile(tx, ty, viewer)) continue
          this.openTile(tx, ty)
        }
      }
    }
  }

  private openTile(tileX: number, tileY: number) {
    const chunkX = Math.floor(tileX / CHUNK)
    const chunkY = Math.floor(tileY / CHUNK)
    const cells = this.ensureFar(chunkX, chunkY)
    const stride = CHUNK * FAR_PIXELS
    const localX = tileX - chunkX * CHUNK
    const localY = tileY - chunkY * CHUNK
    if (localX < 0 || localY < 0 || localX >= CHUNK || localY >= CHUNK) return
    const x0 = localX * FAR_PIXELS
    const y0 = localY * FAR_PIXELS
    for (let py = y0; py < y0 + FAR_PIXELS; py++) {
      cells.fill(255, py * stride + x0, py * stride + x0 + FAR_PIXELS)
    }
  }

  private ensureFar(chunkX: number, chunkY: number) {
    const key = `${chunkX},${chunkY}`
    const length = CHUNK * FAR_PIXELS * CHUNK * FAR_PIXELS
    const existing = this.farSeen.get(key)
    if (existing && existing.length === length) return existing
    const cells = new Uint8Array(length)
    this.writeServerTiles(chunkX, chunkY, cells)
    this.farSeen.set(key, cells)
    return cells
  }

  /** Server-explored tiles open solid in memory — same for camp and wild. */
  private writeServerTiles(chunkX: number, chunkY: number, cells: Uint8Array) {
    const pixels = FAR_PIXELS
    const stride = CHUNK * pixels
    const length = stride * stride
    if (bakeServerScratch.length < length) bakeServerScratch = new Uint8Array(length)
    else bakeServerScratch.fill(0, 0, length)
    const baked = bakeServerScratch
    const originX = chunkX * CHUNK
    const originY = chunkY * CHUNK
    let wrote = false
    for (let localY = 0; localY < CHUNK; localY++) {
      for (let localX = 0; localX < CHUNK; localX++) {
        if (!this.serverSees(originX + localX, originY + localY)) continue
        const x0 = localX * pixels
        const y0 = localY * pixels
        for (let py = y0; py < y0 + pixels; py++) {
          baked.fill(255, py * stride + x0, py * stride + x0 + pixels)
        }
        wrote = true
      }
    }
    if (!wrote) return
    for (let index = 0; index < length; index++) {
      if (baked[index] > cells[index]) cells[index] = baked[index]
    }
  }

  private paintFar(
    key: string,
    entry: { canvas: HTMLCanvasElement; context: CanvasRenderingContext2D; texture: Texture },
    chunk: TileArea,
  ) {
    const pixels = FAR_PIXELS
    const width = chunk.width * pixels
    const height = chunk.height * pixels
    const [chunkX, chunkY] = key.split(',').map(Number)
    const seen = this.ensureFar(chunkX, chunkY)
    const stride = CHUNK * pixels
    const localX0 = Math.round((chunk.x - chunkX * CHUNK) * pixels)
    const localY0 = Math.round((chunk.y - chunkY * CHUNK) * pixels)
    if (!this.farImage || this.farImage.width !== width || this.farImage.height !== height) {
      this.farImage = entry.context.createImageData(width, height)
    }
    const image = this.farImage
    const remembered = REMEMBERED_ALPHA * this.darkness
    for (let row = 0; row < height; row++) {
      for (let column = 0; column < width; column++) {
        const index = (row * width + column) * 4
        image.data[index] = DARK[0]
        image.data[index + 1] = DARK[1]
        image.data[index + 2] = DARK[2]
        const open = seen[(localY0 + row) * stride + (localX0 + column)] >= MEMORY_OPEN * 255
        image.data[index + 3] = Math.round((open ? remembered : 1) * 255)
      }
    }
    entry.context.putImageData(image, 0, 0)
    entry.texture.source.update()
  }

  private draw() {
    const data = this.image.data
    for (let index = 0; index < this.explored.length; index++) {
      const open = this.explored[index] === 1
      const pixel = index * 4
      data[pixel] = DARK[0]
      data[pixel + 1] = DARK[1]
      data[pixel + 2] = DARK[2]
      data[pixel + 3] = Math.round((open ? REMEMBERED_ALPHA * this.darkness : 1) * 255)
    }
    this.context.putImageData(this.image, 0, 0)
    this.displayContext.clearRect(0, 0, this.display.width, this.display.height)
    this.displayContext.drawImage(this.canvas, EDGE_PAD, EDGE_PAD)
    const pad = this.displayContext.getImageData(0, 0, this.display.width, this.display.height)
    const width = this.display.width
    const height = this.display.height
    const sample = (x: number, y: number) => {
      const cx = Math.min(MAP_WIDTH - 1, Math.max(0, x - EDGE_PAD))
      const cy = Math.min(MAP_HEIGHT - 1, Math.max(0, y - EDGE_PAD))
      return ((cy * MAP_WIDTH + cx) * 4) + 3
    }
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        if (x >= EDGE_PAD && y >= EDGE_PAD && x < EDGE_PAD + MAP_WIDTH && y < EDGE_PAD + MAP_HEIGHT) continue
        const source = sample(x, y)
        const index = (y * width + x) * 4
        pad.data[index] = DARK[0]
        pad.data[index + 1] = DARK[1]
        pad.data[index + 2] = DARK[2]
        pad.data[index + 3] = data[source]
      }
    }
    this.displayContext.putImageData(pad, 0, 0)
    this.texture.source.update()
  }
}
