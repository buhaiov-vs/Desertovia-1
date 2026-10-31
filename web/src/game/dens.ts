import { Container, Graphics, Text } from 'pixi.js'
import type { DenSnapshot, EnemyKind } from '../api'
import { enemyFigure } from './enemies'
import { foeFigure, type FoeAnimation, type FoeLook } from './foeSprites'
import type { LandChunkRect, TileRect } from './landChunk'
import { TILE } from './sheets'
import { spotsAround, type SpawnPoint } from './spawnRings'

/** Den props are drawn this much larger than the heights in `DEN_PICTURES`. */
const PROP_SCALE = 1.35
/** Protectors and roaming foes are drawn this much larger than their own looks. */
const FOE_SCALE = 1.4
/** Protectors in one row in front of the den; more stand in a second, wider row. */
const ROW_SIZE = 4
/** The first row stands this far in front of the den, in tiles; each further row one tile more. */
const GUARD_RADIUS = 1.3

/** Where protector `index` of `count` stands on its row's arc, as the server's `DenGuards.Arc`. */
function guardArc(index: number, count: number): { angle: number; radius: number } {
  const row = Math.floor(index / ROW_SIZE)
  const inRow = Math.min(ROW_SIZE, count - row * ROW_SIZE)
  const share = inRow === 1 ? 0.5 : (index % ROW_SIZE) / (inRow - 1)
  const span = row === 0 ? (count > ROW_SIZE ? 0.5 : 0.7) : 0.8
  return { angle: Math.PI * (0.5 + (share - 0.5) * span), radius: GUARD_RADIUS + row }
}

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

/** How fast a drawn protector closes the gap to the server position, per second. */
const FOLLOW = 10
/** A protector flashes red this long after a blow, in seconds. */
const FLASH_SECONDS = 0.18
/** A striking protector lunges this long, in seconds. */
const LUNGE_SECONDS = 0.24
/** A tap this close to a protector picks it, in world pixels. */
const PICK_RADIUS = 14
const HIT_TINT = 0xff6a5a

/** A thin health bar centred on (x, y); null clears it. */
function drawBar(bar: Graphics, share: number | null, x: number, y: number) {
  bar.clear()
  if (share === null) return
  const width = 16
  bar
    .rect(Math.round(x - width / 2) - 1, Math.round(y) - 1, width + 2, 4)
    .fill({ color: 0x1a0f08, alpha: 0.85 })
    .rect(Math.round(x - width / 2), Math.round(y), Math.max(0, Math.round(width * Math.min(1, share))), 2)
    .fill({ color: share > 0.5 ? 0x6fcf4a : share > 0.25 ? 0xe0b83a : 0xd8432f })
}

interface Guard {
  view: Container
  bar: Graphics
  homeX: number
  homeY: number
  x: number
  y: number
  facing: number
  homeFacing: number
  flash: number
  lunge: number
  /** A roaming foe at rest walks this path, in world pixels, instead of standing at home. */
  wander?: () => { x: number; y: number }
}

interface Den {
  point: SpawnPoint
  views: Container[]
  guards: Guard[]
  mark: Container | null
  label: Container | null
}

/** A citizen fighting at a den, drawn at the server position. */
interface Fighter {
  x: number
  y: number
  bar: Graphics
}

/**
 * The foe rings on the land. The eight spots around each den are baked into the ground chunk under them.
 * A den the village has seen, near the camera, shows the prop of its area and its protectors in front of it.
 * The roaming foes between the rings are shown the same way, as dens of one protector with no prop.
 */
export class DenLayer {
  private points: SpawnPoint[] = []
  private readonly shown = new Map<string, Den>()
  private readonly layer: Container
  /** Rings and level numbers, drawn above the fog so a highlighted den stays visible. */
  private readonly marks: Container
  private highlighted = false
  private fights = new Map<string, DenSnapshot>()
  /** Snapshots whose events were played, so a re-render does not flash again. */
  private readonly played = new WeakSet<DenSnapshot>()
  private readonly fighters = new Map<string, Fighter>()
  /** The roaming foes in a part of the land, in tiles; they are figured like dens of one protector. */
  private haunts: ((rect: TileRect) => SpawnPoint[]) | null = null
  /** Where a roaming foe wanders at a server time, in tiles. */
  private wander: ((foe: SpawnPoint, unixMs: number) => { x: number; y: number }) | null = null
  /** Server time now, in Unix milliseconds, so the wander matches the one the server checks. */
  private now: () => number = () => Date.now()

  constructor(layer: Container, marks: Container) {
    this.layer = layer
    this.marks = marks
  }

  /** Every den, explored or not. */
  all(): readonly SpawnPoint[] {
    return this.points
  }

  /** Developer view: a ring and the level on every den in sight, whether or not the fog is open. */
  setHighlighted(on: boolean) {
    this.highlighted = on
    for (const den of this.shown.values()) {
      if (den.mark) den.mark.visible = on
      if (den.label) den.label.visible = on
    }
  }

  setPoints(
    points: SpawnPoint[],
    haunts: ((rect: TileRect) => SpawnPoint[]) | null = null,
    wander: ((foe: SpawnPoint, unixMs: number) => { x: number; y: number }) | null = null,
  ) {
    for (const id of [...this.shown.keys()]) this.drop(id)
    this.points = points
    this.haunts = haunts
    this.wander = wander
  }

  setClock(now: () => number) {
    this.now = now
  }

  /** Dens the village has seen. */
  spotted(explored: (x: number, y: number) => boolean): SpawnPoint[] {
    return this.points.filter((point) => explored(point.x, point.y))
  }

  /** Lone foes standing in `rect`. Fog hides the ones the village has not seen, unless the developer marks are on. */
  standing(rect: TileRect, explored: (x: number, y: number) => boolean): SpawnPoint[] {
    const foes = this.haunts?.(rect) ?? []
    return this.highlighted ? foes : foes.filter((foe) => explored(foe.x, foe.y))
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
    const roaming = this.haunts?.({ x: left, y: top, width: right - left, height: bottom - top }) ?? []
    for (const point of [...this.points, ...roaming]) {
      if (point.x < left || point.x > right || point.y < top || point.y > bottom) continue
      if (!this.highlighted && !explored(point.x, point.y)) continue
      keep.add(point.id)
      if (this.shown.has(point.id)) continue
      this.shown.set(point.id, this.figures(point, camp))
      changed = true
    }
    for (const id of [...this.shown.keys()]) {
      if (keep.has(id)) continue
      this.drop(id)
      changed = true
    }
    return changed
  }

  /** The den fights the server runs now; a den missing from the list is at rest. */
  setFights(snapshots: readonly DenSnapshot[]) {
    this.fights = new Map(snapshots.map((snapshot) => [snapshot.denId, snapshot]))
    for (const snapshot of snapshots) {
      if (this.played.has(snapshot)) continue
      this.played.add(snapshot)
      const guards = this.shown.get(snapshot.denId)?.guards
      if (!guards) continue
      for (const event of snapshot.events) {
        const guard = guards[event.protector]
        if (!guard) continue
        if (event.kind === 'Hit') guard.flash = FLASH_SECONDS
        else if (event.kind === 'Struck') guard.lunge = LUNGE_SECONDS
      }
    }
    const heroes = new Set(snapshots.flatMap((snapshot) => (snapshot.hero ? [snapshot.hero.citizenId] : [])))
    for (const [id, fighter] of this.fighters) {
      if (heroes.has(id)) continue
      fighter.bar.destroy()
      this.fighters.delete(id)
    }
  }

  /** Where a citizen fighting at a den stands now, in world pixels; null when they are not fighting. */
  fighterAt(citizenId: string): { x: number; y: number } | null {
    return this.fighters.get(citizenId) ?? null
  }

  /** The protector under a tap, of a den that is fighting someone. */
  pick(x: number, y: number): { denId: string; protector: number; heroId: string } | null {
    let best: { denId: string; protector: number; heroId: string; distance: number } | null = null
    for (const [denId, snapshot] of this.fights) {
      const guards = this.shown.get(denId)?.guards
      if (!snapshot.hero || !guards) continue
      for (const protector of snapshot.protectors) {
        const guard = guards[protector.index]
        if (!guard || protector.state === 'Dead') continue
        // A figure stands on its feet: aim at its middle.
        const distance = Math.hypot(guard.x - x, guard.y - 8 * FOE_SCALE - y)
        if (distance <= PICK_RADIUS * FOE_SCALE && (!best || distance < best.distance))
          best = { denId, protector: protector.index, heroId: snapshot.hero.citizenId, distance }
      }
    }
    return best ? { denId: best.denId, protector: best.protector, heroId: best.heroId } : null
  }

  /** Moves protectors and fighting citizens towards the server positions; dens at rest walk home, roaming foes wander. */
  update(seconds: number, sortDepth: boolean) {
    const follow = Math.min(1, seconds * FOLLOW)
    for (const [id, den] of this.shown) {
      const fight = this.fights.get(id)
      for (let index = 0; index < den.guards.length; index++) {
        const guard = den.guards[index]
        if (!guard) continue
        const protector = fight?.protectors[index]
        if (guard.wander && !protector) {
          // At rest a roaming foe's home is where its wander is now; it faces the way it strolls.
          const spot = guard.wander()
          if (Math.abs(spot.x - guard.homeX) > 0.01) guard.homeFacing = spot.x < guard.homeX ? -1 : 1
          guard.homeX = spot.x
          guard.homeY = spot.y
        }
        if (!protector && guard.flash === 0 && guard.lunge === 0 && guard.x === guard.homeX && guard.y === guard.homeY) continue
        guard.view.visible = protector?.state !== 'Dead'
        if (protector?.state === 'Dead') {
          guard.x = guard.homeX
          guard.y = guard.homeY
          guard.bar.clear()
          continue
        }
        const targetX = protector ? protector.x * TILE : guard.homeX
        const targetY = protector ? protector.y * TILE : guard.homeY
        const dx = targetX - guard.x
        guard.x += dx * follow
        guard.y += (targetY - guard.y) * follow
        if (Math.abs(dx) > 0.5) guard.facing = dx < 0 ? -1 : 1
        if (!protector && Math.hypot(targetX - guard.x, targetY - guard.y) < 0.3) {
          guard.x = targetX
          guard.y = targetY
          guard.facing = guard.homeFacing
        }
        guard.flash = Math.max(0, guard.flash - seconds)
        guard.lunge = Math.max(0, guard.lunge - seconds)
        const lunge = guard.lunge > 0 ? Math.sin((1 - guard.lunge / LUNGE_SECONDS) * Math.PI) * 3 : 0
        guard.view.scale.x = Math.abs(guard.view.scale.x) * guard.facing
        guard.view.position.set(guard.x + lunge * guard.facing, guard.y)
        this.tint(guard.view, guard.flash > 0 ? HIT_TINT : 0xffffff)
        if (sortDepth) guard.view.zIndex = guard.y
        const hurt = protector && protector.health < protector.maximumHealth
        drawBar(guard.bar, hurt ? protector.health / protector.maximumHealth : null, guard.x, guard.y - guard.view.height - 3)
      }
    }
    for (const fight of this.fights.values()) {
      const hero = fight.hero
      if (!hero) continue
      let fighter = this.fighters.get(hero.citizenId)
      if (!fighter) {
        const bar = new Graphics()
        bar.zIndex = Number.MAX_SAFE_INTEGER
        this.layer.addChild(bar)
        fighter = { x: hero.x * TILE, y: hero.y * TILE, bar }
        this.fighters.set(hero.citizenId, fighter)
      }
      fighter.x += (hero.x * TILE - fighter.x) * follow
      fighter.y += (hero.y * TILE - fighter.y) * follow
      drawBar(fighter.bar, hero.health / hero.maximumHealth, fighter.x, fighter.y - 30)
    }
  }

  private tint(view: Container, color: number) {
    for (const child of view.children) {
      if ('tint' in child) (child as Container & { tint: number }).tint = color
    }
    if ('tint' in view) (view as Container & { tint: number }).tint = color
  }

  private figures(point: SpawnPoint, camp: { x: number; y: number }): Den {
    const picture = DEN_PICTURES[point.level - 1]
    const den: Den = { point, views: [], guards: [], mark: null, label: null }
    if (!picture) return den
    const centerX = (point.x + 0.5) * TILE
    const centerY = (point.y + 0.5) * TILE
    const views = den.views
    const facing = camp.x * TILE < centerX ? -1 : 1
    if (point.roams) {
      // A roaming foe: no den, one figure walking the same wander the server checks.
      const [, cellX, cellY] = point.id.split(':').map(Number)
      const figure = this.protectorFigure(picture, 0, Math.abs(cellX + cellY))
      if (!figure) return den
      const walk = this.wander
      const wander = walk
        ? () => {
            const spot = walk(point, this.now())
            return { x: spot.x * TILE, y: spot.y * TILE }
          }
        : undefined
      const start = wander?.() ?? { x: centerX, y: centerY }
      figure.scale.x = facing * FOE_SCALE
      views.push(this.place(figure, start.x, start.y))
      const bar = new Graphics()
      bar.zIndex = Number.MAX_SAFE_INTEGER
      this.layer.addChild(bar)
      views.push(bar)
      den.guards[0] = { view: figure, bar, homeX: start.x, homeY: start.y, x: start.x, y: start.y, facing, homeFacing: facing, flash: 0, lunge: 0, wander }
      return den
    }
    const ring = new Graphics()
      .circle(0, 0, 3 * TILE)
      .stroke({ color: 0xffe14a, width: 3, alpha: 0.95 })
      .circle(0, 0, 1.4 * TILE)
      .stroke({ color: 0xff3b30, width: 2 })
    den.mark = this.placeMark(ring, centerX, centerY)
    views.push(den.mark)
    const caption = new Text({
      text: String(point.level),
      style: { fontFamily: 'sans-serif', fontSize: 15, fontWeight: '700', fill: 0xfff4e0, stroke: { color: 0x1b1206, width: 4 } },
    })
    caption.anchor.set(0.5, 1)
    den.label = this.placeMark(caption, centerX, centerY - 3 * TILE - 2)
    views.push(den.label)
    const prop = foeFigure({ idle: `den-${point.level}` as FoeAnimation, height: Math.round(picture.prop * PROP_SCALE) })
    if (prop) views.push(this.place(prop, centerX, centerY))

    const direction = Number(point.id.split(':')[1])
    for (let index = 0; index < point.protectors; index++) {
      const figure = this.protectorFigure(picture, index, direction)
      if (!figure) continue
      // Protectors stand in rows on arcs in front of the prop, so the prop never covers them; a lone one stands aside
      // so it does not cover the prop. The same places as the server's `DenGuards.Homes`.
      const lone = facing < 0 ? Math.PI * 0.75 : Math.PI * 0.25
      const { angle, radius } = point.protectors === 1 ? { angle: lone, radius: LONE_GUARD } : guardArc(index, point.protectors)
      figure.scale.x = facing * FOE_SCALE
      const homeX = centerX + Math.cos(angle) * radius * TILE
      const homeY = centerY + Math.sin(angle) * radius * 0.6 * TILE
      views.push(this.place(figure, homeX, homeY))
      const bar = new Graphics()
      bar.zIndex = Number.MAX_SAFE_INTEGER
      this.layer.addChild(bar)
      views.push(bar)
      den.guards[index] = { view: figure, bar, homeX, homeY, x: homeX, y: homeY, facing, homeFacing: facing, flash: 0, lunge: 0 }
    }
    return den
  }

  private protectorFigure(picture: DenPicture, index: number, direction: number) {
    const protector = picture.protector(index, direction)
    const figure = typeof protector === 'string' ? enemyFigure(protector) : foeFigure(protector)
    figure?.scale.set(FOE_SCALE)
    return figure
  }

  private placeMark(view: Container, x: number, y: number) {
    view.position.set(Math.round(x), Math.round(y))
    view.visible = this.highlighted
    view.zIndex = y
    this.marks.addChild(view)
    return view
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
