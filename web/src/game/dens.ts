import { Container, Graphics } from 'pixi.js'
import { enemyContext } from './enemies'
import type { LandChunkRect, TileRect } from './landChunk'
import { TILE } from './sheets'
import { spotsAround, type SpawnPoint } from './spawnRings'

/** Protectors stand this far from the den, inside the ring of spots, in tiles. */
const GUARD_RADIUS = 1
/** Dens this far past the view keep their protectors, so panning does not pop them in at the edge. */
const MARGIN_TILES = 32
/** A spot can sit across a chunk edge; it is drawn into every chunk it reaches. */
const SPOT_REACH = 0.5

interface Den {
  point: SpawnPoint
  guards: Graphics[]
}

/**
 * The foe rings on the land. The eight spots around each den are baked into the ground chunk under them.
 * Protectors stand on a den the village has seen, near the camera, with the look the game already draws for
 * that foe. A foe with no picture yet has its spots and no figure.
 */
export class DenLayer {
  private points: SpawnPoint[] = []
  private readonly shown = new Map<string, Den>()
  private readonly layer: Container

  constructor(layer: Container) {
    this.layer = layer
  }

  setPoints(points: SpawnPoint[]) {
    for (const id of [...this.shown.keys()]) this.drop(id)
    this.points = points
  }

  /** Dens the village has seen, with or without a picture of their foe. */
  spotted(explored: (x: number, y: number) => boolean): SpawnPoint[] {
    return this.points.filter((point) => explored(point.x, point.y))
  }

  /** Draws the spots of every den that reaches this chunk into `batch`, whose (0, 0) is the chunk origin. */
  markGround(batch: Container, chunk: LandChunkRect) {
    const left = chunk.originX - SPOT_REACH
    const top = chunk.originY - SPOT_REACH
    const right = chunk.originX + chunk.width + SPOT_REACH
    const bottom = chunk.originY + chunk.height + SPOT_REACH
    let marks: Graphics | null = null
    for (const point of this.points) {
      if (point.x + 3 < left || point.x - 3 > right || point.y + 3 < top || point.y - 3 > bottom) continue
      for (const spot of spotsAround(point)) {
        if (spot.x < left || spot.x > right || spot.y < top || spot.y > bottom) continue
        marks ??= new Graphics()
        const x = (spot.x - chunk.originX) * TILE
        const y = (spot.y - chunk.originY) * TILE
        marks
          .ellipse(x, y, 5, 3)
          .fill({ color: 0x6b5236, alpha: 0.7 })
          .ellipse(x, y - 0.5, 3, 1.8)
          .fill({ color: 0x4a3a26, alpha: 0.65 })
      }
    }
    if (marks) batch.addChild(marks)
  }

  /** Shows protectors on seen dens near `view` and drops the rest. Returns whether anything changed. */
  sync(view: TileRect, explored: (x: number, y: number) => boolean, camp: { x: number; y: number }): boolean {
    const left = view.x - MARGIN_TILES
    const top = view.y - MARGIN_TILES
    const right = view.x + view.width + MARGIN_TILES
    const bottom = view.y + view.height + MARGIN_TILES
    let changed = false
    const keep = new Set<string>()
    for (const point of this.points) {
      if (!point.kind || point.x < left || point.x > right || point.y < top || point.y > bottom) continue
      if (!explored(point.x, point.y)) continue
      keep.add(point.id)
      if (this.shown.has(point.id)) continue
      this.shown.set(point.id, { point, guards: this.guards(point, camp) })
      changed = true
    }
    for (const id of [...this.shown.keys()]) {
      if (keep.has(id)) continue
      this.drop(id)
      changed = true
    }
    return changed
  }

  private guards(point: SpawnPoint, camp: { x: number; y: number }): Graphics[] {
    const kind = point.kind!
    const centerX = point.x + 0.5
    const centerY = point.y + 0.5
    const facing = camp.x < centerX ? -1 : 1
    const turn = (point.level * 0.9 + Number(point.id.split(':')[1]) * 0.35) % (Math.PI * 2)
    const guards: Graphics[] = []
    for (let index = 0; index < point.protectors; index++) {
      const angle = turn + (index * Math.PI * 2) / point.protectors
      const radius = point.protectors === 1 ? 0 : GUARD_RADIUS
      const view = new Graphics(enemyContext(kind))
      view.position.set(
        Math.round((centerX + Math.cos(angle) * radius) * TILE),
        Math.round((centerY + Math.sin(angle) * radius * 0.6) * TILE),
      )
      view.scale.x = facing
      view.zIndex = view.y
      view.cullable = true
      this.layer.addChild(view)
      guards.push(view)
    }
    return guards
  }

  private drop(id: string) {
    const den = this.shown.get(id)
    if (!den) return
    for (const guard of den.guards) guard.destroy()
    this.shown.delete(id)
  }
}
