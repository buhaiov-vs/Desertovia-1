import { CanvasSource, Container, Sprite, Texture } from 'pixi.js'
import { TILE } from './sheets'
import { LAND_CHUNK as CHUNK, type LandChunkRect } from './landChunk'
import { CAMPFIRE, MAP_HEIGHT, MAP_LEFT, MAP_TOP, MAP_WIDTH } from './worldMap'
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
/** Land seen before but out of sight now is dimmed this much at night; land never seen is fully dark. */
const REMEMBERED_ALPHA = 0.5
/** Tiles over which sight fades out at its edge. */
const SIGHT_FADE = 1.5
/**
 * Far land is drawn much closer than the camp picture, so one pixel per tile becomes a stair-step.
 * Several pixels per tile let the sight disc follow the walker and fade instead of popping a whole tile.
 * Eight keeps each texel near two world pixels (TILE is 16) so linear stretch does not lattice.
 */
const FAR_PIXELS = 8
/** Tiles across which the far-land disc fades. The outer edge stays on the sight radius. */
const FAR_FADE = 2.5
/**
 * Tiles across which server-explored land fades into fog (distance to nearest unseen tile).
 * Written once into `farSeen` so paint never mixes a second tile map with the live disc.
 */
const SERVER_FADE = 2.5
/**
 * Same oval a new tribe knows on the server (`Exploration.CampRadiusX/Y`). Tile bits stair-step that
 * rim; fog memory stamps this smooth ellipse instead so the camp edge is not a grid.
 */
const CAMP_RADIUS_X = 19
const CAMP_RADIUS_Y = 17
/** One-time bake blur (fog texels) to round remaining path-tile stairs after soft fill. */
const SERVER_BAKE_BLUR = 5
/** A point is in sight when it is lit at least this much. */
const IN_SIGHT = 0.5

/** Reused when rounding a freshly baked chunk — not per frame. */
let bakeBlurScratch = new Float32Array(0)
/** Server soft is baked here then maxed into walk memory so blur never melts discs already walked. */
let bakeServerScratch = new Uint8Array(0)

/** Smooth falloff from 1 inside the disc to 0 at the radius, over `fade` tiles. */
function sightFalloff(distance: number, radius: number, fade: number) {
  const opened = (radius - distance) / fade
  if (opened <= 0) return 0
  if (opened >= 1) return 1
  return opened * opened * (3 - 2 * opened)
}
/**
 * Extra fog pixels around the camp picture canvas (minimap). Linear filtering otherwise blends the
 * rim with empty texture and draws a bright teal fringe where the vale ends.
 */
const EDGE_PAD = 1

/**
 * Fog of war over the map. One chunk-sprite layer covers camp land and far land alike, so the old
 * rectangular camp-picture fog edge does not appear. The small `canvas` stays for the minimap only.
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
  /** Far chunks that held a live sight disc last cover; leaving the set freezes their fog once more. */
  private liveFar = new Set<string>()
  /** After a server sync, every on-screen far chunk must be painted again. */
  private farPaintDirty = true
  /** Reused by `paintFar` so a zoomed-out view does not allocate megabytes every fog tick. */
  private farLight = new Float32Array(0)
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
  /** 0 out of sight, 1 in full sight. */
  private readonly light = new Float32Array(MAP_WIDTH * MAP_HEIGHT)
  /** Darkness of each tile before blurring, 0 to 1. */
  private readonly alpha = new Float32Array(MAP_WIDTH * MAP_HEIGHT)
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
    this.texture = new Texture({ source: new CanvasSource({ resource: this.display, scaleMode: 'linear' }) })
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
   * when a village update arrives, so a publish cannot wipe the path disc and snap fog onto a stale corridor.
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
    // Raise soft fog memory with new server bits; never replace walk discs with a second tile layer.
    for (const [key, cells] of this.farSeen) {
      const [chunkX, chunkY] = key.split(',').map(Number)
      this.writeServerSoft(chunkX, chunkY, cells)
    }
    this.farPaintDirty = true
    this.draw()
  }

  /** Lights the land around the viewers; whatever they see is explored from then on. */
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
          const lit = Math.min(1, (viewer.radius - Math.hypot(x + 0.5 - viewer.x, y + 0.5 - viewer.y)) / SIGHT_FADE)
          if (lit <= 0) continue
          const index = (y - MAP_TOP) * MAP_WIDTH + (x - MAP_LEFT)
          this.explored[index] = 1
          if (lit > this.light[index]) this.light[index] = lit
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
    const index = this.index(x, y)
    if (index >= 0) return this.explored[index] === 1
    if (this.seenNow(x, y)) return true
    return this.chunkSees(Math.floor(x), Math.floor(y))
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
        const index = this.index(x, y)
        const seen = index >= 0 ? this.explored[index] === 1 : this.chunkSees(x, y)
        out[row * width + column] = seen ? remembered : 1
      }
    }
    for (const viewer of this.viewers) {
      const left = Math.max(x0, Math.floor(viewer.x - viewer.radius))
      const right = Math.min(x0 + width - 1, Math.ceil(viewer.x + viewer.radius))
      const top = Math.max(y0, Math.floor(viewer.y - viewer.radius))
      const bottom = Math.min(y0 + height - 1, Math.ceil(viewer.y + viewer.radius))
      for (let y = top; y <= bottom; y++) {
        for (let x = left; x <= right; x++) {
          const lit = Math.min(1, (viewer.radius - Math.hypot(x + 0.5 - viewer.x, y + 0.5 - viewer.y)) / SIGHT_FADE)
          if (lit <= 0) continue
          const index = (y - y0) * width + (x - x0)
          const alpha = remembered * (1 - lit)
          if (alpha < out[index]) out[index] = alpha
        }
      }
    }
  }

  /**
   * Paints darkness on land chunks near the camera (camp picture and far land use the same sprites).
   * A chunk that scrolls away is dropped. Only chunks under a walker, newly created, leaving a
   * walker's sight, or dirtied by a server sync are repainted — zooming out used to redraw every
   * on-screen far chunk at 8 pixels per tile every fog tick and made the map lag.
   *
   * Keys are chunk indices (`LandChunkRect.x/y`), not clipped origins, so panning cannot recreate
   * sprites and wipe walk memory. Paint reads one soft buffer (`farSeen`) plus live light — no
   * second server-tile layer that melted under the disc then snapped back with a hitch.
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
      // Paint when new, under a sight disc, just left a disc (clear live light), or after sync.
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

  /** Whether a moving sight disc (walker or campfire) reaches this fog sprite. */
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

  /** Server or local explored bit for any tile — camp and wild use the same check. */
  private tileSeen(x: number, y: number) {
    const index = this.index(x, y)
    if (index >= 0 && this.explored[index] === 1) return true
    return this.chunkSees(x, y)
  }

  private makeFar(chunk: FogChunk) {
    const canvas = document.createElement('canvas')
    canvas.width = chunk.width * FAR_PIXELS
    canvas.height = chunk.height * FAR_PIXELS
    const context = canvas.getContext('2d')!
    const texture = new Texture({ source: new CanvasSource({ resource: canvas, scaleMode: 'linear' }) })
    const sprite = new Sprite(texture)
    sprite.position.set(chunk.originX * TILE, chunk.originY * TILE)
    sprite.scale.set(TILE / FAR_PIXELS)
    this.far.addChild(sprite)
    const entry = { sprite, canvas, context, texture }
    this.farSprites.set(`${chunk.x},${chunk.y}`, entry)
    return entry
  }

  /** Writes the smooth discs the walkers see now into the far-land memory, and keeps what they already crossed. */
  private stampFar() {
    const pixels = FAR_PIXELS
    const stride = CHUNK * pixels
    // Sticky building discs only when memory is dirty — every-tick stamps over the whole village were costly.
    const stampSticky = this.farPaintDirty
    for (const viewer of this.viewers) {
      if (viewer.sticky && !stampSticky) continue
      const left = viewer.x - viewer.radius
      const top = viewer.y - viewer.radius
      const right = viewer.x + viewer.radius
      const bottom = viewer.y + viewer.radius
      for (let chunkY = Math.floor(top / CHUNK); chunkY <= Math.floor(bottom / CHUNK); chunkY++) {
        for (let chunkX = Math.floor(left / CHUNK); chunkX <= Math.floor(right / CHUNK); chunkX++) {
          const cells = this.ensureFar(chunkX, chunkY)
          const originX = chunkX * CHUNK
          const originY = chunkY * CHUNK
          const x0 = Math.max(0, Math.floor((left - originX) * pixels))
          const y0 = Math.max(0, Math.floor((top - originY) * pixels))
          const x1 = Math.min(stride - 1, Math.ceil((right - originX) * pixels))
          const y1 = Math.min(stride - 1, Math.ceil((bottom - originY) * pixels))
          for (let py = y0; py <= y1; py++) {
            for (let px = x0; px <= x1; px++) {
              const x = originX + (px + 0.5) / pixels
              const y = originY + (py + 0.5) / pixels
              const lit = Math.round(sightFalloff(Math.hypot(x - viewer.x, y - viewer.y), viewer.radius, FAR_FADE) * 255)
              if (lit <= 0) continue
              const index = py * stride + px
              if (lit > cells[index]) cells[index] = lit
            }
          }
        }
      }
    }
  }

  private ensureFar(chunkX: number, chunkY: number) {
    const key = `${chunkX},${chunkY}`
    const length = CHUNK * FAR_PIXELS * CHUNK * FAR_PIXELS
    const existing = this.farSeen.get(key)
    if (existing && existing.length === length) return existing
    const cells = new Uint8Array(length)
    this.writeServerSoft(chunkX, chunkY, cells)
    this.farSeen.set(key, cells)
    return cells
  }

  /** Smooth open 0–1 for the starting camp oval (not the stair-stepped tile mask). */
  private campOvalOpen(x: number, y: number) {
    const nx = (x - CAMPFIRE.x) / CAMP_RADIUS_X
    const ny = (y - CAMPFIRE.y) / CAMP_RADIUS_Y
    const r = Math.hypot(nx, ny)
    if (r >= 1) return 0
    const fade = SERVER_FADE / Math.min(CAMP_RADIUS_X, CAMP_RADIUS_Y)
    if (r <= 1 - fade) return 1
    const opened = (1 - r) / fade
    return opened * opened * (3 - 2 * opened)
  }

  /**
   * Soft server exploration into the same buffer walk discs use. Camp uses a smooth ellipse;
   * other explored land fills interior tiles and fades only on the frontier, then a one-time blur
   * rounds leftover tile stairs. Blur runs on a scratch buffer so walk discs are not melted.
   */
  private writeServerSoft(chunkX: number, chunkY: number, cells: Uint8Array) {
    const pixels = FAR_PIXELS
    const stride = CHUNK * pixels
    const length = stride * stride
    if (bakeServerScratch.length < length) bakeServerScratch = new Uint8Array(length)
    else bakeServerScratch.fill(0, 0, length)
    const baked = bakeServerScratch
    const originX = chunkX * CHUNK
    const originY = chunkY * CHUNK
    const reach = Math.ceil(SERVER_FADE) + 1
    let wrote = false
    for (let py = 0; py < stride; py++) {
      for (let px = 0; px < stride; px++) {
        const amount = Math.round(this.campOvalOpen(originX + (px + 0.5) / pixels, originY + (py + 0.5) / pixels) * 255)
        if (amount <= 0) continue
        baked[py * stride + px] = amount
        wrote = true
      }
    }
    for (let localY = 0; localY < CHUNK; localY++) {
      for (let localX = 0; localX < CHUNK; localX++) {
        const tileX = originX + localX
        const tileY = originY + localY
        if (!this.tileSeen(tileX, tileY)) continue
        // Camp oval already covers starting land; skip tile stairs there.
        if (this.campOvalOpen(tileX + 0.5, tileY + 0.5) > 0) continue
        let edge = false
        for (let dy = -1; dy <= 1 && !edge; dy++) {
          for (let dx = -1; dx <= 1; dx++) {
            if (dx === 0 && dy === 0) continue
            if (!this.tileSeen(tileX + dx, tileY + dy)) {
              edge = true
              break
            }
          }
        }
        const x0 = localX * pixels
        const y0 = localY * pixels
        if (!edge) {
          for (let py = y0; py < y0 + pixels; py++) {
            baked.fill(255, py * stride + x0, py * stride + x0 + pixels)
          }
          wrote = true
          continue
        }
        for (let py = y0; py < y0 + pixels; py++) {
          for (let px = x0; px < x0 + pixels; px++) {
            const x = originX + (px + 0.5) / pixels
            const y = originY + (py + 0.5) / pixels
            let nearest = SERVER_FADE
            for (let dy = -reach; dy <= reach; dy++) {
              for (let dx = -reach; dx <= reach; dx++) {
                if (dx === 0 && dy === 0) continue
                if (this.tileSeen(tileX + dx, tileY + dy)) continue
                const left = tileX + dx
                const top = tileY + dy
                const cx = Math.min(left + 1, Math.max(left, x))
                const cy = Math.min(top + 1, Math.max(top, y))
                const distance = Math.hypot(x - cx, y - cy)
                if (distance < nearest) nearest = distance
              }
            }
            let amount = 255
            if (nearest < SERVER_FADE) {
              const opened = nearest / SERVER_FADE
              amount = Math.round(opened * opened * (3 - 2 * opened) * 255)
            }
            const index = py * stride + px
            if (amount > baked[index]) baked[index] = amount
            wrote = true
          }
        }
      }
    }
    if (!wrote) return
    this.blurSeenChunk(baked, stride, SERVER_BAKE_BLUR)
    for (let index = 0; index < length; index++) {
      if (baked[index] > cells[index]) cells[index] = baked[index]
    }
  }

  /** Box-blur a baked fog chunk once so path-tile rims are not stair-steps. */
  private blurSeenChunk(cells: Uint8Array, stride: number, radius: number) {
    if (radius <= 0) return
    const length = cells.length
    if (bakeBlurScratch.length < length) bakeBlurScratch = new Float32Array(length)
    const tmp = bakeBlurScratch
    const span = radius * 2 + 1
    const height = (length / stride) | 0
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < stride; x++) {
        let sum = 0
        for (let dx = -radius; dx <= radius; dx++) {
          sum += cells[y * stride + Math.min(stride - 1, Math.max(0, x + dx))]
        }
        tmp[y * stride + x] = sum / span
      }
    }
    for (let x = 0; x < stride; x++) {
      for (let y = 0; y < height; y++) {
        let sum = 0
        for (let dy = -radius; dy <= radius; dy++) {
          sum += tmp[Math.min(height - 1, Math.max(0, y + dy)) * stride + x]
        }
        cells[y * stride + x] = Math.round(sum / span)
      }
    }
  }

  /**
   * Darkness from remembered open amount and live light, 0 clear to 1 full night.
   * One continuous ramp — the old `seen > 0 ? half-night : full` branch jumped at the first lit
   * texel and the blur turned that step into concentric bands.
   */
  private farShade(seen: number, light: number) {
    const known = Math.max(seen, light)
    if (known <= 0) return 1
    const remembered = REMEMBERED_ALPHA * this.darkness
    return 1 - known + known * (1 - light) * remembered
  }

  private paintFar(
    key: string,
    entry: { canvas: HTMLCanvasElement; context: CanvasRenderingContext2D; texture: Texture },
    chunk: TileArea,
  ) {
    const pixels = FAR_PIXELS
    const width = chunk.width * pixels
    const height = chunk.height * pixels
    const count = width * height
    const [chunkX, chunkY] = key.split(',').map(Number)
    const seen = this.ensureFar(chunkX, chunkY)
    const stride = CHUNK * pixels
    const localX0 = Math.round((chunk.x - chunkX * CHUNK) * pixels)
    const localY0 = Math.round((chunk.y - chunkY * CHUNK) * pixels)
    if (this.farLight.length < count) this.farLight = new Float32Array(count)
    else this.farLight.fill(0, 0, count)
    const light = this.farLight
    for (const viewer of this.viewers) {
      if (viewer.sticky) continue
      const left = viewer.x - viewer.radius
      const top = viewer.y - viewer.radius
      const right = viewer.x + viewer.radius
      const bottom = viewer.y + viewer.radius
      const x0 = Math.max(0, Math.floor((left - chunk.x) * pixels))
      const y0 = Math.max(0, Math.floor((top - chunk.y) * pixels))
      const x1 = Math.min(width - 1, Math.ceil((right - chunk.x) * pixels))
      const y1 = Math.min(height - 1, Math.ceil((bottom - chunk.y) * pixels))
      for (let py = y0; py <= y1; py++) {
        for (let px = x0; px <= x1; px++) {
          const x = chunk.x + (px + 0.5) / pixels
          const y = chunk.y + (py + 0.5) / pixels
          const lit = sightFalloff(Math.hypot(x - viewer.x, y - viewer.y), viewer.radius, FAR_FADE)
          if (lit <= 0) continue
          const index = py * width + px
          if (lit > light[index]) light[index] = lit
        }
      }
    }
    if (!this.farImage || this.farImage.width !== width || this.farImage.height !== height) {
      this.farImage = entry.context.createImageData(width, height)
    }
    const image = this.farImage
    for (let row = 0; row < height; row++) {
      for (let column = 0; column < width; column++) {
        const index = (row * width + column) * 4
        image.data[index] = DARK[0]
        image.data[index + 1] = DARK[1]
        image.data[index + 2] = DARK[2]
        const soft = seen[(localY0 + row) * stride + (localX0 + column)] / 255
        const cell = row * width + column
        image.data[index + 3] = Math.round(
          Math.min(1, Math.max(0, this.farShade(soft, light[cell]))) * 255,
        )
      }
    }
    entry.context.putImageData(image, 0, 0)
    entry.texture.source.update()
  }

  private draw() {
    const alpha = this.alpha
    for (let index = 0; index < this.explored.length; index++) {
      alpha[index] = this.explored[index] ? REMEMBERED_ALPHA * this.darkness * (1 - this.light[index]) : 1
    }
    // A 3×3 blur rounds off the tile steps that stretching alone leaves along the edges.
    const data = this.image.data
    for (let row = 0; row < MAP_HEIGHT; row++) {
      for (let column = 0; column < MAP_WIDTH; column++) {
        let sum = 0
        let weights = 0
        for (let dy = -1; dy <= 1; dy++) {
          const y = row + dy
          if (y < 0 || y >= MAP_HEIGHT) continue
          for (let dx = -1; dx <= 1; dx++) {
            const x = column + dx
            if (x < 0 || x >= MAP_WIDTH) continue
            const weight = (dx === 0 ? 2 : 1) * (dy === 0 ? 2 : 1)
            sum += alpha[y * MAP_WIDTH + x] * weight
            weights += weight
          }
        }
        const index = (row * MAP_WIDTH + column) * 4
        data[index] = DARK[0]
        data[index + 1] = DARK[1]
        data[index + 2] = DARK[2]
        data[index + 3] = Math.round((sum / weights) * 255)
      }
    }
    this.context.putImageData(this.image, 0, 0)
    // Clamp the rim into the pad so linear filtering never samples empty texels at the vale edge.
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
