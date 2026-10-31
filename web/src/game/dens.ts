import { Container, Graphics } from 'pixi.js'
import type { EnemyKind } from '../api'
import { enemyFigure } from './enemies'
import { foeFigure, type FoeAnimation, type FoeLook } from './foeSprites'
import type { LandChunkRect, TileRect } from './landChunk'
import { TILE } from './sheets'
import { spotsAround, type SpawnPoint } from './spawnRings'

/** Den props are drawn this much larger than the heights in `DEN_PICTURES`. */
const PROP_SCALE = 1.35
/** Protectors stand this far in front of the den, in tiles. */
const GUARD_RADIUS = 1.3
/** A lone protector stands this far from the den, in front on the camp side, in tiles. */
const LONE_GUARD = 1.2
/** Radius of the trodden patch under a den, in tiles; it reaches just past the spots. */
const PATCH_RADIUS = 2.7
/** Dens this far past the view keep their figures, so panning does not pop them in at the edge. */
const MARGIN_TILES = 32
/** A spot can sit across a chunk edge; it is drawn into every chunk it reaches. */
const SPOT_REACH = 0.5

/** One protector: an enemy the game already fights, or a figure of the archive atlas. */
type Protector = EnemyKind | FoeLook
type ProtectorOf = (index: number, direction: number) => Protector

interface DenPicture {
  /** Height of the den prop `den-<level>`, in world pixels. */
  prop: number
  protector: ProtectorOf
}

const look = (idle: FoeAnimation, height: number): ProtectorOf => () => ({ idle, height })
const known = (kind: EnemyKind): ProtectorOf => () => kind
const SLIMES: FoeAnimation[] = ['slime-blue', 'slime-green', 'slime-red']

/** The picture of each foe ring, by level. The prop comes from the area of that foe. */
const DEN_PICTURES: readonly DenPicture[] = [
  { prop: 16, protector: (index) => ({ idle: SLIMES[index % SLIMES.length], height: 10 }) },
  { prop: 36, protector: known('Rat') },
  { prop: 28, protector: known('Imp') },
  { prop: 14, protector: look('goblin-small', 12) },
  { prop: 26, protector: known('Wolf') },
  { prop: 22, protector: known('Boar') },
  { prop: 16, protector: look('goblin-club', 14) },
  { prop: 16, protector: look('orc-hooded', 19) },
  { prop: 18, protector: look('scarecrow', 20) },
  { prop: 16, protector: known('Ghoul') },
  { prop: 40, protector: look('goblin-sword', 13) },
  { prop: 28, protector: look('goblin-banner', 13) },
  { prop: 40, protector: known('Wraith') },
  { prop: 28, protector: look('poison-plant', 24) },
  { prop: 24, protector: look('slime-flaming', 18) },
  { prop: 36, protector: look('orc-axe', 22) },
  { prop: 32, protector: known('Bear') },
  { prop: 36, protector: look('orc-warrior', 30) },
  { prop: 44, protector: look('vampire', 22) },
  { prop: 20, protector: look('orc-berserk', 31) },
  { prop: 36, protector: look('orc-shaman', 31) },
  { prop: 40, protector: look('vampire-winged', 24) },
  { prop: 40, protector: look('orc-swords', 24) },
  { prop: 26, protector: known('Zombie') },
  { prop: 32, protector: look('fire-spirit', 26) },
  { prop: 24, protector: known('Skeleton') },
  { prop: 36, protector: look('orc-armored', 27) },
  { prop: 30, protector: (_, direction) => ({ idle: `knight-${(direction % 8) + 1}` as FoeAnimation, scale: 1.2 }) },
  { prop: 40, protector: known('BarrowKing') },
  { prop: 44, protector: look('lich', 40) },
]

interface Den {
  point: SpawnPoint
  views: Container[]
}

/**
 * The foe rings on the land. The eight spots around each den are baked into the ground chunk under them.
 * A den the village has seen, near the camera, shows the prop of its area and its protectors in front of it.
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

  /** Dens the village has seen. */
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
    const reach = PATCH_RADIUS + 1
    for (const point of this.points) {
      if (point.x + reach < chunk.originX || point.x - reach > chunk.originX + chunk.width) continue
      if (point.y + reach < chunk.originY || point.y - reach > chunk.originY + chunk.height) continue
      // The chunk texture clips the patch at its frame, so every chunk it reaches draws all of it.
      marks ??= new Graphics()
      const centerX = (point.x + 0.5 - chunk.originX) * TILE
      const centerY = (point.y + 0.5 - chunk.originY) * TILE
      marks
        .circle(centerX, centerY, PATCH_RADIUS * TILE)
        .fill({ color: 0x3a2414, alpha: 0.38 })
        .circle(centerX, centerY, PATCH_RADIUS * TILE)
        .stroke({ color: 0x9e2a1e, alpha: 0.75, width: 2 })
        .circle(centerX, centerY, PATCH_RADIUS * TILE * 0.55)
        .fill({ color: 0x2a170c, alpha: 0.3 })
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

  /** Shows the figures of seen dens near `view` and drops the rest. Returns whether anything changed. */
  sync(view: TileRect, explored: (x: number, y: number) => boolean, camp: { x: number; y: number }): boolean {
    const left = view.x - MARGIN_TILES
    const top = view.y - MARGIN_TILES
    const right = view.x + view.width + MARGIN_TILES
    const bottom = view.y + view.height + MARGIN_TILES
    let changed = false
    const keep = new Set<string>()
    for (const point of this.points) {
      if (point.x < left || point.x > right || point.y < top || point.y > bottom) continue
      if (!explored(point.x, point.y)) continue
      keep.add(point.id)
      if (this.shown.has(point.id)) continue
      this.shown.set(point.id, { point, views: this.figures(point, camp) })
      changed = true
    }
    for (const id of [...this.shown.keys()]) {
      if (keep.has(id)) continue
      this.drop(id)
      changed = true
    }
    return changed
  }

  private figures(point: SpawnPoint, camp: { x: number; y: number }): Container[] {
    const picture = DEN_PICTURES[point.level - 1]
    if (!picture) return []
    const centerX = (point.x + 0.5) * TILE
    const centerY = (point.y + 0.5) * TILE
    const views: Container[] = []
    const prop = foeFigure({ idle: `den-${point.level}` as FoeAnimation, height: Math.round(picture.prop * PROP_SCALE) })
    if (prop) views.push(this.place(prop, centerX, centerY))

    const direction = Number(point.id.split(':')[1])
    const facing = camp.x * TILE < centerX ? -1 : 1
    for (let index = 0; index < point.protectors; index++) {
      const protector = picture.protector(index, direction)
      const figure = typeof protector === 'string' ? enemyFigure(protector) : foeFigure(protector)
      if (!figure) continue
      // Protectors stand on an arc in front of the prop, so the prop never covers them; a lone one stands aside so it does not cover the prop.
      const lone = facing < 0 ? Math.PI * 0.75 : Math.PI * 0.25
      const angle = point.protectors === 1 ? lone : Math.PI * (0.15 + (0.7 * index) / (point.protectors - 1))
      const radius = point.protectors === 1 ? LONE_GUARD : GUARD_RADIUS
      figure.scale.x = facing
      views.push(this.place(figure, centerX + Math.cos(angle) * radius * TILE, centerY + Math.sin(angle) * radius * 0.6 * TILE))
    }
    return views
  }

  private place(view: Container, x: number, y: number) {
    view.position.set(Math.round(x), Math.round(y))
    view.zIndex = view.y
    view.cullable = true
    this.layer.addChild(view)
    return view
  }

  private drop(id: string) {
    const den = this.shown.get(id)
    if (!den) return
    for (const view of den.views) view.destroy({ children: true })
    this.shown.delete(id)
  }
}
