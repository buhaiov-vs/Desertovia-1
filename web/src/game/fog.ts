import { CanvasSource, Sprite, Texture } from 'pixi.js'
import { TILE } from './sheets'
import { MAP_HEIGHT, MAP_LEFT, MAP_TOP, MAP_WIDTH } from './worldMap'
import type { TileArea } from '../api'

/** Something that sees the land around it; coordinates and radius in tiles. */
export interface Viewer {
  x: number
  y: number
  radius: number
}

const DARK: [number, number, number] = [12, 14, 20]
/** Land seen before but out of sight now is dimmed this much at night; land never seen is fully dark. */
const REMEMBERED_ALPHA = 0.5
/** Tiles over which sight fades out at its edge. */
const SIGHT_FADE = 1.5
/** A point is in sight when it is lit at least this much. */
const IN_SIGHT = 0.5

/**
 * Fog of war over the map, one pixel per tile, stretched with smoothing so its edges are soft. The land the tribe
 * has seen comes from the server and is shared by all players; walkers on this map add what they see as they go.
 */
export class Fog {
  readonly canvas = document.createElement('canvas')
  readonly sprite: Sprite
  private readonly texture: Texture
  private readonly context: CanvasRenderingContext2D
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
    this.texture = new Texture({ source: new CanvasSource({ resource: this.canvas, scaleMode: 'linear' }) })
    this.sprite = new Sprite(this.texture)
    this.sprite.position.set(MAP_LEFT * TILE, MAP_TOP * TILE)
    this.sprite.scale.set(TILE)
    this.draw()
  }

  destroy() {
    this.texture.destroy(true)
  }

  /** Adds the land the server says the tribe has seen; nothing seen is ever forgotten. */
  setExplored(base64: string) {
    const bits = atob(base64)
    for (let index = 0; index < this.explored.length; index++) {
      if ((bits.charCodeAt(index >> 3) >> (index & 7)) & 1) this.explored[index] = 1
    }
    this.draw()
  }

  /** Lights the land around the viewers; whatever they see is explored from then on. */
  update(viewers: Viewer[], darkness: number) {
    this.darkness = darkness
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
    return index >= 0 && this.explored[index] === 1
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
    return index >= 0 && this.light[index] >= IN_SIGHT
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
    this.texture.source.update()
  }
}
