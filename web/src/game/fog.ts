import { CanvasSource, Container, Sprite, Texture } from 'pixi.js'
import { TILE } from './sheets'
import { LAND_CHUNK as CHUNK, type LandChunkRect } from './landChunk'
import { MAP_HEIGHT, MAP_LEFT, MAP_TOP, MAP_WIDTH } from './worldMap'
import type { ExploredChunk, TileArea } from '../api'

/** One fog sprite: stable `x`,`y` chunk indices, painted over the clipped tile rectangle. */
type FogChunk = Pick<LandChunkRect, 'x' | 'y' | 'originX' | 'originY' | 'width' | 'height'>

/** Something that sees the land around it; coordinates and radius in tiles. */
export interface Viewer {
  x: number
  y: number
  radius: number
  /**
   * Buildings and sites: stamped into walk memory, but they must not keep fog chunks “live”
   * every tick or the whole village repaints and the map hitches.
   */
  sticky?: boolean
}

/**
 * Unseen fog ink — cool night blue that sits with the map night wash (`0x0b1640`), not flat grey-black.
 * Grain nudges these channels per texel so the void is not a plastic slab.
 */
const DARK: [number, number, number] = [8, 12, 28]
/**
 * How much explored land outside live sight is dimmed at full night. Day (`darkness` 0) stays fully clear so
 * wilderness never greys in daylight; only night softens memory behind the hero.
 */
const REMEMBERED_ALPHA = 0.14
/** Fog texels per tile. Eight keeps each texel near two world pixels (TILE is 16). */
const FAR_PIXELS = 8
/** Fog texels along one side of a chunk. */
const STRIDE = CHUNK * FAR_PIXELS
const CELLS = STRIDE * STRIDE
/** Width, in tiles, of the soft rim walked memory leaves, centred on the sight radius. */
const MEMORY_FEATHER = 1.5
/**
 * Width, in tiles, of the glow at the edge of live sight. It ends exactly where the memory rim ends, so the
 * land does not close or harden when the explorer walks on — at night only the dim of memory returns.
 */
const SIGHT_FEATHER = 3
/** Memory at or above this counts as explored for gameplay; high enough that a soft rim never opens a closed tile. */
const MEMORY_OPEN = 0.6
/** A point is in sight when it is lit at least this much. */
const IN_SIGHT = 0.5
/** How far fog RGB may drift from `DARK` for grain (−n…n). */
const FOG_GRAIN = 7
/** Night must move this much before every fog chunk is repainted. */
const DARKNESS_STEP = 0.04
/** Walked chunks kept off screen (64 KiB each); older ones fall back to server exploration. */
const MAX_WALKED_CHUNKS = 256

/** Base64 from the server. A bad string leaves that chunk fog instead of stopping the map. */
function decodeBits(encoded: string) {
  try {
    return atob(encoded)
  } catch {
    return ''
  }
}

/** Stable −FOG_GRAIN…FOG_GRAIN for one texel, so the dark does not shimmer each frame. */
function fogGrain(x: number, y: number) {
  let value = Math.imul(x, 0x27d4eb2d) ^ Math.imul(y, 0x165667b1)
  value = Math.imul(value ^ (value >>> 16), 0x85ebca6b)
  return ((value >>> 0) % (FOG_GRAIN * 2 + 1)) - FOG_GRAIN
}

function writeInk(target: Uint8ClampedArray | Uint8Array, offset: number, grain: number) {
  target[offset] = Math.max(0, Math.min(255, DARK[0] + grain))
  target[offset + 1] = Math.max(0, Math.min(255, DARK[1] + Math.floor(grain * 0.6)))
  target[offset + 2] = Math.max(0, Math.min(255, DARK[2] + Math.floor(grain * 0.35)))
}

/** Fog colour of every texel in a chunk. Only alpha changes when fog repaints, so the colour is built once. */
const CHUNK_INK = (() => {
  const ink = new Uint8Array(CELLS * 3)
  for (let py = 0; py < STRIDE; py++) {
    for (let px = 0; px < STRIDE; px++) writeInk(ink, (py * STRIDE + px) * 3, fogGrain(px, py))
  }
  return ink
})()

/**
 * Server exploration is per tile. Tiles are blurred with their neighbours, then each texel samples the four
 * nearest tile centres, so explored land gets smooth contours instead of tile stairs.
 * `SAMPLE_CELL` indexes the blurred grid, which has one extra tile on every side.
 */
const SAMPLE_CELL = new Int32Array(STRIDE)
const SAMPLE_FRACTION = new Float32Array(STRIDE)
for (let texel = 0; texel < STRIDE; texel++) {
  const position = (texel + 0.5) / FAR_PIXELS - 0.5
  const cell = Math.floor(position)
  SAMPLE_CELL[texel] = cell + 1
  SAMPLE_FRACTION[texel] = position - cell
}
/** Blurred tile grid side: the chunk plus one tile each way. */
const BLURRED = CHUNK + 2
/** Raw tile grid side: the blur reads one more tile each way. */
const GRID = CHUNK + 4
/** Half-width of the ramp around the blurred contour; smaller is crisper. */
const CONTOUR_SOFTNESS = 0.3

function smooth(value: number) {
  const t = value <= 0 ? 0 : value >= 1 ? 1 : value
  return t * t * (3 - 2 * t)
}

interface ServerChunk {
  complete: boolean
  /** The base64 the server sent, kept to notice changes without decoding again. */
  source: string | null
  bits: string
}

interface FogSprite {
  sprite: Sprite
  canvas: HTMLCanvasElement
  context: CanvasRenderingContext2D
  texture: Texture
  /** Viewers that touched this chunk when it was last painted; unchanged means nothing to repaint. */
  sightKey: string | null
}

/**
 * Fog of war over the map. One chunk-sprite layer covers camp land and far land alike.
 * Memory keeps a soft rim forever; live sight is a softer glow inside it that clears night dimming.
 */
export class Fog {
  /** Camp picture bitmap for the minimap, one pixel per tile; not drawn on the world. */
  readonly canvas = document.createElement('canvas')
  /** Darkness for every land chunk near the camera, camp and wild together. */
  readonly far = new Container()
  private readonly sprites = new Map<string, FogSprite>()
  /** How strongly each texel of a chunk has been seen, 0 to 255. Only ever grows. */
  private readonly memory = new Map<string, Uint8Array>()
  /** Chunks with local walk memory, oldest first; the rest can be rebuilt from server exploration. */
  private readonly walked = new Set<string>()
  private server = new Map<string, ServerChunk>()
  /** Every on-screen chunk must be painted again: server sync or a night shift. */
  private farPaintDirty = true
  private fullImage: ImageData | null = null
  private readonly occupancy = new Uint8Array(GRID * GRID)
  private readonly blurred = new Float32Array(BLURRED * BLURRED)
  private viewers: Viewer[] = []
  private readonly context: CanvasRenderingContext2D
  private readonly image: ImageData
  private readonly explored = new Uint8Array(MAP_WIDTH * MAP_HEIGHT)
  private exploredDirty = true
  /** 0 out of sight, 1 in full sight — gameplay `inSight` only. */
  private readonly light = new Float32Array(MAP_WIDTH * MAP_HEIGHT)
  /** 0 at full daylight, when remembered land is not dimmed, 1 deep in the night. */
  private darkness = 1
  /** Night level every fog chunk is painted with, so neighbouring chunks never disagree. */
  private paintedDarkness = 1
  private drawnDarkness = -1
  /** Developer tools: the whole land counts as seen and no fog is drawn. Nothing is stamped into memory. */
  private revealed = false

  constructor() {
    this.canvas.width = MAP_WIDTH
    this.canvas.height = MAP_HEIGHT
    this.context = this.canvas.getContext('2d')!
    this.image = this.context.createImageData(MAP_WIDTH, MAP_HEIGHT)
    for (let row = 0; row < MAP_HEIGHT; row++) {
      for (let column = 0; column < MAP_WIDTH; column++) writeInk(this.image.data, (row * MAP_WIDTH + column) * 4, fogGrain(column, row))
    }
    this.draw()
  }

  destroy() {
    for (const entry of this.sprites.values()) this.dropSprite(entry)
    this.sprites.clear()
    this.memory.clear()
    this.walked.clear()
    this.server.clear()
  }

  /**
   * Merges visited chunks into the seen land. Exploration only grows: what the local walker already lit stays lit
   * when a village update arrives, so a publish cannot wipe the path or harden its soft edge.
   */
  setChunks(chunks: ExploredChunk[]) {
    const next = new Map<string, ServerChunk>()
    const changed: string[] = []
    for (const chunk of chunks) {
      const key = `${chunk.x},${chunk.y}`
      const source = chunk.bits ?? null
      const previous = this.server.get(key)
      if (previous && previous.complete === chunk.complete && previous.source === source) {
        next.set(key, previous)
        continue
      }
      next.set(key, { complete: chunk.complete, source, bits: source ? decodeBits(source) : '' })
      changed.push(key)
    }
    for (const key of this.server.keys()) if (!next.has(key)) changed.push(key)
    if (changed.length === 0) return
    this.server = next

    const rebake = new Set<string>()
    for (const key of changed) {
      const [chunkX, chunkY] = key.split(',').map(Number)
      this.markVale(chunkX, chunkY)
      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          const near = `${chunkX + dx},${chunkY + dy}`
          if (this.memory.has(near)) rebake.add(near)
        }
      }
    }
    for (const key of rebake) {
      const [chunkX, chunkY] = key.split(',').map(Number)
      this.bakeServer(chunkX, chunkY, this.memory.get(key)!)
    }
    this.farPaintDirty = true
    this.stampMemory()
    this.draw()
  }

  setRevealed(revealed: boolean) {
    if (this.revealed === revealed) return
    this.revealed = revealed
    this.far.visible = !revealed
    this.farPaintDirty = true
    this.exploredDirty = true
    this.draw()
  }

  /** Updates gameplay sight and stamps soft discs into fog memory. */
  update(viewers: Viewer[], darkness: number) {
    this.darkness = darkness
    if (Math.abs(darkness - this.paintedDarkness) > DARKNESS_STEP) this.farPaintDirty = true
    this.viewers = viewers
    this.light.fill(0)
    for (const viewer of viewers) {
      const left = Math.max(MAP_LEFT, Math.floor(viewer.x - viewer.radius))
      const right = Math.min(MAP_LEFT + MAP_WIDTH - 1, Math.ceil(viewer.x + viewer.radius))
      const top = Math.max(MAP_TOP, Math.floor(viewer.y - viewer.radius))
      const bottom = Math.min(MAP_TOP + MAP_HEIGHT - 1, Math.ceil(viewer.y + viewer.radius))
      const reach = viewer.radius * viewer.radius
      for (let y = top; y <= bottom; y++) {
        const dy = y + 0.5 - viewer.y
        for (let x = left; x <= right; x++) {
          const dx = x + 0.5 - viewer.x
          if (dx * dx + dy * dy > reach) continue
          const index = (y - MAP_TOP) * MAP_WIDTH + (x - MAP_LEFT)
          this.light[index] = 1
          if (this.explored[index] === 0) {
            this.explored[index] = 1
            this.exploredDirty = true
          }
        }
      }
    }
    this.stampMemory()
    this.draw()
  }

  /** Whether the tile under a point, in tiles, has been seen. */
  isExplored(x: number, y: number) {
    if (this.revealed) return true
    const index = this.index(x, y)
    if (index >= 0 && this.explored[index] === 1) return true
    if (this.memorySees(x, y)) return true
    if (this.serverSees(Math.floor(x), Math.floor(y))) return true
    return this.seenNow(x, y)
  }

  isAreaExplored(area: TileArea) {
    for (let y = area.y; y < area.y + area.height; y++) {
      for (let x = area.x; x < area.x + area.width; x++) if (!this.isExplored(x, y)) return false
    }
    return true
  }

  /** Whether someone sees the point, in tiles, right now. */
  inSight(x: number, y: number) {
    if (this.revealed) return true
    const index = this.index(x, y)
    if (index >= 0) return this.light[index] >= IN_SIGHT
    return this.seenNow(x, y)
  }

  /**
   * Darkness of each tile in a rectangle: 0 where someone is looking, the night dim on remembered land,
   * 1 on land nobody has seen. `out` has one entry per tile, row by row, and must be `width * height` long.
   */
  fillDarkness(x0: number, y0: number, width: number, height: number, out: Float32Array) {
    if (this.revealed) {
      out.fill(0, 0, width * height)
      return
    }
    const remembered = REMEMBERED_ALPHA * this.darkness
    for (let row = 0; row < height; row++) {
      for (let column = 0; column < width; column++) {
        out[row * width + column] = this.isExplored(x0 + column, y0 + row) ? remembered : 1
      }
    }
    for (const viewer of this.viewers) {
      const left = Math.max(x0, Math.floor(viewer.x - viewer.radius))
      const right = Math.min(x0 + width - 1, Math.ceil(viewer.x + viewer.radius))
      const top = Math.max(y0, Math.floor(viewer.y - viewer.radius))
      const bottom = Math.min(y0 + height - 1, Math.ceil(viewer.y + viewer.radius))
      const reach = viewer.radius * viewer.radius
      for (let y = top; y <= bottom; y++) {
        const dy = y + 0.5 - viewer.y
        for (let x = left; x <= right; x++) {
          const dx = x + 0.5 - viewer.x
          if (dx * dx + dy * dy <= reach) out[(y - y0) * width + (x - x0)] = 0
        }
      }
    }
  }

  /**
   * Paints darkness on land chunks near the camera. A chunk that scrolls away is dropped. A chunk is repainted
   * only when it is new, the night or server exploration moved, or the viewers over it moved.
   */
  cover(chunks: FogChunk[]) {
    const repaintAll = this.farPaintDirty
    if (repaintAll) this.paintedDarkness = this.darkness
    const wanted = new Set<string>()
    for (const chunk of chunks) {
      const key = `${chunk.x},${chunk.y}`
      wanted.add(key)
      let entry = this.sprites.get(key)
      let created = false
      if (!entry || entry.canvas.width !== chunk.width * FAR_PIXELS || entry.canvas.height !== chunk.height * FAR_PIXELS) {
        if (entry) this.dropSprite(entry)
        entry = this.makeSprite(key, chunk)
        created = true
      } else {
        entry.sprite.position.set(chunk.originX * TILE, chunk.originY * TILE)
      }
      const sightKey = this.sightKey(chunk)
      if (created || repaintAll || sightKey !== entry.sightKey) {
        this.paint(entry, chunk)
        entry.sightKey = sightKey
      }
    }
    for (const [key, entry] of this.sprites) {
      if (wanted.has(key)) continue
      this.dropSprite(entry)
      this.sprites.delete(key)
      if (!this.walked.has(key)) this.memory.delete(key)
    }
    this.farPaintDirty = false
  }

  private index(x: number, y: number) {
    const column = Math.floor(x) - MAP_LEFT
    const row = Math.floor(y) - MAP_TOP
    if (column < 0 || row < 0 || column >= MAP_WIDTH || row >= MAP_HEIGHT) return -1
    return row * MAP_WIDTH + column
  }

  /** Walk/server fog memory at a point — same buffer for camp and wild. */
  private memorySees(x: number, y: number) {
    const chunkX = Math.floor(Math.floor(x) / CHUNK)
    const chunkY = Math.floor(Math.floor(y) / CHUNK)
    const cells = this.memory.get(`${chunkX},${chunkY}`)
    if (!cells) return false
    const px = Math.min(STRIDE - 1, Math.max(0, Math.floor((x - chunkX * CHUNK) * FAR_PIXELS)))
    const py = Math.min(STRIDE - 1, Math.max(0, Math.floor((y - chunkY * CHUNK) * FAR_PIXELS)))
    return cells[py * STRIDE + px] >= MEMORY_OPEN * 255
  }

  private seenNow(x: number, y: number) {
    return this.viewers.some((viewer) => {
      const dx = x + 0.5 - viewer.x
      const dy = y + 0.5 - viewer.y
      return dx * dx + dy * dy <= viewer.radius * viewer.radius
    })
  }

  /** Server exploration of one tile — same for camp and wild. */
  private serverSees(x: number, y: number) {
    const chunkX = Math.floor(x / CHUNK)
    const chunkY = Math.floor(y / CHUNK)
    const chunk = this.server.get(`${chunkX},${chunkY}`)
    if (!chunk) return false
    if (chunk.complete || !chunk.bits) return chunk.complete
    const local = (y - chunkY * CHUNK) * CHUNK + (x - chunkX * CHUNK)
    return ((chunk.bits.charCodeAt(local >> 3) >> (local & 7)) & 1) === 1
  }

  /** Copies server exploration of one chunk into the camp picture, only where the chunk meets the vale. */
  private markVale(chunkX: number, chunkY: number) {
    const left = Math.max(MAP_LEFT, chunkX * CHUNK)
    const top = Math.max(MAP_TOP, chunkY * CHUNK)
    const right = Math.min(MAP_LEFT + MAP_WIDTH, (chunkX + 1) * CHUNK)
    const bottom = Math.min(MAP_TOP + MAP_HEIGHT, (chunkY + 1) * CHUNK)
    for (let y = top; y < bottom; y++) {
      for (let x = left; x < right; x++) {
        const index = (y - MAP_TOP) * MAP_WIDTH + (x - MAP_LEFT)
        if (this.explored[index] === 1 || !this.serverSees(x, y)) continue
        this.explored[index] = 1
        this.exploredDirty = true
      }
    }
  }

  /** Viewers that light this chunk, at texel precision; the chunk is repainted when this changes. */
  private sightKey(chunk: FogChunk) {
    let key = ''
    for (const viewer of this.viewers) {
      const reach = viewer.radius + MEMORY_FEATHER / 2
      if (viewer.x + reach < chunk.originX || viewer.x - reach > chunk.originX + chunk.width) continue
      if (viewer.y + reach < chunk.originY || viewer.y - reach > chunk.originY + chunk.height) continue
      key += `${Math.round(viewer.x * FAR_PIXELS)},${Math.round(viewer.y * FAR_PIXELS)},${viewer.radius};`
    }
    return key
  }

  /**
   * Soft memory disc at fog-texel resolution. Sticky viewers are stamped only with a full repaint;
   * their land does not move, and stamping every building each tick would cost the whole village.
   */
  private stampMemory() {
    const stampSticky = this.farPaintDirty
    for (const viewer of this.viewers) {
      if (viewer.sticky && !stampSticky) continue
      const outer = viewer.radius + MEMORY_FEATHER / 2
      const inner = Math.max(0, viewer.radius - MEMORY_FEATHER / 2)
      const outerSquared = outer * outer
      const innerSquared = inner * inner
      for (let chunkY = Math.floor((viewer.y - outer) / CHUNK); chunkY <= Math.floor((viewer.y + outer) / CHUNK); chunkY++) {
        for (let chunkX = Math.floor((viewer.x - outer) / CHUNK); chunkX <= Math.floor((viewer.x + outer) / CHUNK); chunkX++) {
          const key = `${chunkX},${chunkY}`
          const cells = this.ensureMemory(chunkX, chunkY)
          this.walked.delete(key)
          this.walked.add(key)
          const originX = chunkX * CHUNK
          const originY = chunkY * CHUNK
          const x0 = Math.max(0, Math.floor((viewer.x - outer - originX) * FAR_PIXELS))
          const y0 = Math.max(0, Math.floor((viewer.y - outer - originY) * FAR_PIXELS))
          const x1 = Math.min(STRIDE - 1, Math.ceil((viewer.x + outer - originX) * FAR_PIXELS))
          const y1 = Math.min(STRIDE - 1, Math.ceil((viewer.y + outer - originY) * FAR_PIXELS))
          for (let py = y0; py <= y1; py++) {
            const dy = originY + (py + 0.5) / FAR_PIXELS - viewer.y
            for (let px = x0; px <= x1; px++) {
              const dx = originX + (px + 0.5) / FAR_PIXELS - viewer.x
              const squared = dx * dx + dy * dy
              if (squared > outerSquared) continue
              const value = squared <= innerSquared ? 255 : Math.round(smooth((outer - Math.sqrt(squared)) / (outer - inner)) * 255)
              const index = py * STRIDE + px
              if (value > cells[index]) cells[index] = value
            }
          }
        }
      }
    }
    this.trimWalked()
  }

  /** Forgets the oldest off-screen walk memory past the cap; server exploration rebuilds it if needed. */
  private trimWalked() {
    if (this.walked.size <= MAX_WALKED_CHUNKS) return
    for (const key of this.walked) {
      if (this.walked.size <= MAX_WALKED_CHUNKS) return
      if (this.sprites.has(key)) continue
      this.walked.delete(key)
      this.memory.delete(key)
    }
  }

  private ensureMemory(chunkX: number, chunkY: number) {
    const key = `${chunkX},${chunkY}`
    const existing = this.memory.get(key)
    if (existing) return existing
    const cells = new Uint8Array(CELLS)
    this.bakeServer(chunkX, chunkY, cells)
    this.memory.set(key, cells)
    return cells
  }

  /** Raises memory to server exploration with smooth contours; never lowers what was walked. */
  private bakeServer(chunkX: number, chunkY: number, cells: Uint8Array) {
    const originX = chunkX * CHUNK
    const originY = chunkY * CHUNK
    const grid = this.occupancy
    let open = 0
    for (let gy = 0; gy < GRID; gy++) {
      for (let gx = 0; gx < GRID; gx++) {
        const seen = this.serverSees(originX + gx - 2, originY + gy - 2) ? 1 : 0
        grid[gy * GRID + gx] = seen
        open += seen
      }
    }
    if (open === 0) return
    if (open === GRID * GRID) {
      cells.fill(255)
      return
    }
    const field = this.blurred
    for (let by = 0; by < BLURRED; by++) {
      for (let bx = 0; bx < BLURRED; bx++) {
        const centre = (by + 1) * GRID + bx + 1
        field[by * BLURRED + bx] =
          (grid[centre - GRID - 1] + 2 * grid[centre - GRID] + grid[centre - GRID + 1] +
            2 * grid[centre - 1] + 4 * grid[centre] + 2 * grid[centre + 1] +
            grid[centre + GRID - 1] + 2 * grid[centre + GRID] + grid[centre + GRID + 1]) / 16
      }
    }
    for (let py = 0; py < STRIDE; py++) {
      const upper = SAMPLE_CELL[py] * BLURRED
      const lower = upper + BLURRED
      const fy = SAMPLE_FRACTION[py]
      for (let px = 0; px < STRIDE; px++) {
        const cell = SAMPLE_CELL[px]
        const fx = SAMPLE_FRACTION[px]
        const top = field[upper + cell] + (field[upper + cell + 1] - field[upper + cell]) * fx
        const bottom = field[lower + cell] + (field[lower + cell + 1] - field[lower + cell]) * fx
        const level = top + (bottom - top) * fy
        const value = Math.round(smooth((level - 0.5) / (2 * CONTOUR_SOFTNESS) + 0.5) * 255)
        const index = py * STRIDE + px
        if (value > cells[index]) cells[index] = value
      }
    }
  }

  private makeSprite(key: string, chunk: FogChunk): FogSprite {
    const canvas = document.createElement('canvas')
    canvas.width = chunk.width * FAR_PIXELS
    canvas.height = chunk.height * FAR_PIXELS
    const context = canvas.getContext('2d')!
    const texture = new Texture({ source: new CanvasSource({ resource: canvas, scaleMode: 'linear' }) })
    const sprite = new Sprite(texture)
    sprite.position.set(chunk.originX * TILE, chunk.originY * TILE)
    sprite.scale.set(TILE / FAR_PIXELS)
    this.far.addChild(sprite)
    const entry: FogSprite = { sprite, canvas, context, texture, sightKey: null }
    this.sprites.set(key, entry)
    return entry
  }

  private dropSprite(entry: FogSprite) {
    entry.sprite.destroy()
    entry.texture.destroy(true)
  }

  /** Image with the fog ink already in place; painting only writes alpha. */
  private imageFor(width: number, height: number, localX0: number, localY0: number) {
    const full = width === STRIDE && height === STRIDE
    if (full && this.fullImage) return this.fullImage
    const image = new ImageData(width, height)
    for (let row = 0; row < height; row++) {
      for (let column = 0; column < width; column++) {
        const from = ((localY0 + row) * STRIDE + localX0 + column) * 3
        const to = (row * width + column) * 4
        image.data[to] = CHUNK_INK[from]
        image.data[to + 1] = CHUNK_INK[from + 1]
        image.data[to + 2] = CHUNK_INK[from + 2]
      }
    }
    if (full) this.fullImage = image
    return image
  }

  /**
   * Fog alpha: unseen land opaque, remembered land dimmed by night, live sight clear. The live glow ends where
   * the memory rim ends, so walking on only brings the night dim back and never moves the edge.
   */
  private paint(entry: FogSprite, chunk: FogChunk) {
    const width = chunk.width * FAR_PIXELS
    const height = chunk.height * FAR_PIXELS
    const cells = this.ensureMemory(chunk.x, chunk.y)
    const localX0 = (chunk.originX - chunk.x * CHUNK) * FAR_PIXELS
    const localY0 = (chunk.originY - chunk.y * CHUNK) * FAR_PIXELS
    const image = this.imageFor(width, height, localX0, localY0)
    const data = image.data
    const remembered = REMEMBERED_ALPHA * Math.max(0, this.paintedDarkness)
    const alphaOf = new Uint8Array(256)
    for (let seen = 0; seen < 256; seen++) alphaOf[seen] = Math.round((1 - (seen / 255) * (1 - remembered)) * 255)
    for (let row = 0; row < height; row++) {
      const source = (localY0 + row) * STRIDE + localX0
      let target = row * width * 4 + 3
      for (let column = 0; column < width; column++) {
        data[target] = alphaOf[cells[source + column]]
        target += 4
      }
    }
    if (remembered > 0) {
      const worldLeft = chunk.originX
      const worldTop = chunk.originY
      for (const viewer of this.viewers) {
        const outer = viewer.radius + MEMORY_FEATHER / 2
        const inner = Math.max(0, outer - SIGHT_FEATHER)
        const x0 = Math.max(0, Math.floor((viewer.x - outer - worldLeft) * FAR_PIXELS))
        const y0 = Math.max(0, Math.floor((viewer.y - outer - worldTop) * FAR_PIXELS))
        const x1 = Math.min(width - 1, Math.ceil((viewer.x + outer - worldLeft) * FAR_PIXELS))
        const y1 = Math.min(height - 1, Math.ceil((viewer.y + outer - worldTop) * FAR_PIXELS))
        if (x0 > x1 || y0 > y1) continue
        const outerSquared = outer * outer
        const innerSquared = inner * inner
        for (let py = y0; py <= y1; py++) {
          const dy = worldTop + (py + 0.5) / FAR_PIXELS - viewer.y
          for (let px = x0; px <= x1; px++) {
            const dx = worldLeft + (px + 0.5) / FAR_PIXELS - viewer.x
            const squared = dx * dx + dy * dy
            if (squared > outerSquared) continue
            const glow = squared <= innerSquared ? 1 : smooth((outer - Math.sqrt(squared)) / (outer - inner))
            const index = (py * width + px) * 4 + 3
            const clear = Math.round(data[index] * (1 - glow))
            if (clear < data[index]) data[index] = clear
          }
        }
      }
    }
    entry.context.putImageData(image, 0, 0)
    entry.texture.source.update()
  }

  /** Camp picture for the minimap; redrawn only when exploration or the night changed. */
  private draw() {
    if (!this.exploredDirty && Math.abs(this.darkness - this.drawnDarkness) < 0.01) return
    const data = this.image.data
    const remembered = Math.round(REMEMBERED_ALPHA * this.darkness * 255)
    for (let index = 0; index < this.explored.length; index++)
      data[index * 4 + 3] = this.revealed ? 0 : this.explored[index] === 1 ? remembered : 255
    this.context.putImageData(this.image, 0, 0)
    this.drawnDarkness = this.darkness
    this.exploredDirty = false
  }
}
