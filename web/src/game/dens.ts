import { Container, Graphics, Text } from 'pixi.js'
import type { DenSnapshot } from '../api'
import { denFoeFigure } from './enemies'
import { DEN_PICTURES, FOE_SCALE, levelHeight, PROP_SCALE } from './denPictures'
import { foeFigure, type FoeAnimation } from './foeSprites'
import type { LandChunkRect, TileRect } from './landChunk'
import { TILE } from './sheets'
import { patrolAt } from './denPatrol'
import { spotsAround, type SpawnPoint } from './spawnRings'

/** Radius of the trodden patch under a den, in tiles; it reaches just past the spots. */
const PATCH_RADIUS = 2.7
/** Dens this far past the view keep their figures, so panning does not pop them in at the edge. */
const MARGIN_TILES = 32
/** A spot can sit across a chunk edge; it is drawn into every chunk it reaches. */
const SPOT_REACH = 0.5

/** How fast a drawn protector closes the gap to the server position, per second. */
const FOLLOW = 10
/** A protector flashes red this long after a blow, in seconds. */
const FLASH_SECONDS = 0.18
/** A striking protector lunges this long, in seconds. */
const LUNGE_SECONDS = 0.24
/** A tap this close to a protector picks it, in world pixels, before zoom. */
const PICK_RADIUS = 14
/** A tap this close to the den prop also picks the nearest protector, in world pixels. */
const DEN_CENTER_REACH = 2.6 * TILE
/** A tap must cover at least this many screen pixels, so a zoomed-out den stays clickable. */
const MIN_PICK_SCREEN = 36
const HIT_TINT = 0xff6a5a

/** A thin health bar centred on (x, y); null clears it. `width` follows a taller foe. */
function drawBar(bar: Graphics, share: number | null, x: number, y: number, width = 16) {
  bar.clear()
  if (share === null) return
  bar
    .rect(Math.round(x - width / 2) - 1, Math.round(y) - 1, width + 2, 4)
    .fill({ color: 0x1a0f08, alpha: 0.85 })
    .rect(Math.round(x - width / 2), Math.round(y), Math.max(0, Math.round(width * Math.min(1, share))), 2)
    .fill({ color: share > 0.5 ? 0x6fcf4a : share > 0.25 ? 0xe0b83a : 0xd8432f })
}

/** Gold ring around a left-clicked protector, matching the raid foe selection mark. */
function drawMark(bar: Graphics, guard: { x: number; y: number; height: number }, on: boolean) {
  if (!on) return
  const radius = Math.max(14, guard.height * 0.55)
  bar.circle(guard.x, guard.y - guard.height * 0.45, radius).stroke({ color: 0xffd75b, width: 2 })
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
  /** A roaming foe at rest walks this path, in world pixels, instead of standing at home. A den's protector walks its ring the same way. */
  wander?: () => { x: number; y: number }
  /** The path is a steady round, so the figure sits on it instead of chasing a moving point. */
  orbit?: boolean
  /** Drawn height in world pixels. The bar, the tap and the lunge follow it. */
  height: number
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
 * A den the village has seen, near the camera, shows the prop of its area and its protectors walking around it.
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
  /** The signed-in citizen: their bar is drawn on their figure, above the name. */
  private viewerId: string | null = null
  /** The roaming foes in a part of the land, in tiles; they are figured like dens of one protector. */
  private haunts: ((rect: TileRect) => SpawnPoint[]) | null = null
  /** Where a roaming foe wanders at a server time, in tiles. */
  private wander: ((foe: SpawnPoint, unixMs: number) => { x: number; y: number }) | null = null
  /** Server time now, in Unix milliseconds, so the wander matches the one the server checks. */
  private now: () => number = () => Date.now()
  /** Protector the player left-clicked or inspected; drawn with a gold ring. */
  private selected: { denId: string; protector: number } | null = null

  constructor(layer: Container, marks: Container) {
    this.layer = layer
    this.marks = marks
  }

  /** Marks one protector, or clears the mark when `null`. */
  select(target: { denId: string; protector: number } | null) {
    this.selected = target
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

  /** The signed-in citizen. Their health is drawn on the figure, so this layer leaves that bar clear. */
  setViewer(citizenId: string | null) {
    this.viewerId = citizenId
  }

  /** Where a citizen fighting at a den stands now, in world pixels; null when they are not fighting. */
  fighterAt(citizenId: string): { x: number; y: number } | null {
    return this.fighters.get(citizenId) ?? null
  }

  /** Health of the citizen fighting a den; null when they are not in one. */
  heroVitals(citizenId: string): { health: number; maximumHealth: number } | null {
    for (const fight of this.fights.values()) {
      const hero = fight.hero
      if (hero?.citizenId === citizenId) return { health: hero.health, maximumHealth: hero.maximumHealth }
    }
    return null
  }

  /**
   * The protector or roaming foe under a tap. `heroId` is set when somebody is already fighting there.
   * `scale` is the world zoom: a zoomed-out view keeps a minimum screen-sized target so dens stay clickable.
   * A tap on the den prop itself picks the nearest living protector of that den.
   */
  pick(
    x: number,
    y: number,
    scale = 1,
  ): { denId: string; protector: number; heroId: string | null; point: SpawnPoint } | null {
    const minReach = MIN_PICK_SCREEN / Math.max(0.05, scale)
    let best: { denId: string; protector: number; heroId: string | null; point: SpawnPoint; distance: number } | null = null
    for (const [denId, den] of this.shown) {
      const fight = this.fights.get(denId)
      const heroId = fight?.hero?.citizenId ?? null
      for (let index = 0; index < den.guards.length; index++) {
        const guard = den.guards[index]
        if (!guard?.view.visible) continue
        const protector = fight?.protectors.find((one) => one.index === index)
        if (protector?.state === 'Dead') continue
        // A figure stands on its feet: aim at its middle. A taller level is a larger tap.
        const reach = Math.max(PICK_RADIUS * FOE_SCALE, guard.height * 0.55, minReach)
        const distance = Math.hypot(guard.x - x, guard.y - guard.height * 0.45 - y)
        if (distance <= reach && (!best || distance < best.distance))
          best = { denId, protector: index, heroId, point: den.point, distance }
      }
    }
    if (best) return { denId: best.denId, protector: best.protector, heroId: best.heroId, point: best.point }

    let centerHit: { denId: string; protector: number; heroId: string | null; point: SpawnPoint; distance: number } | null = null
    const centerReach = Math.max(DEN_CENTER_REACH, minReach)
    for (const [denId, den] of this.shown) {
      if (den.point.roams) continue
      const centerX = (den.point.x + 0.5) * TILE
      const centerY = (den.point.y + 0.5) * TILE
      const toCenter = Math.hypot(centerX - x, centerY - y)
      if (toCenter > centerReach) continue
      const fight = this.fights.get(denId)
      const heroId = fight?.hero?.citizenId ?? null
      let nearest: { index: number; distance: number } | null = null
      for (let index = 0; index < den.guards.length; index++) {
        const guard = den.guards[index]
        if (!guard?.view.visible) continue
        const protector = fight?.protectors.find((one) => one.index === index)
        if (protector?.state === 'Dead') continue
        const distance = Math.hypot(guard.x - x, guard.y - guard.height * 0.45 - y)
        if (!nearest || distance < nearest.distance) nearest = { index, distance }
      }
      if (nearest && (!centerHit || toCenter < centerHit.distance))
        centerHit = { denId, protector: nearest.index, heroId, point: den.point, distance: toCenter }
    }
    return centerHit
      ? { denId: centerHit.denId, protector: centerHit.protector, heroId: centerHit.heroId, point: centerHit.point }
      : null
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
          // At rest a roaming foe's home is where its wander is now; a den's protector is where its mill is.
          // It faces the way it strolls.
          const spot = guard.wander()
          if (Math.abs(spot.x - guard.homeX) > 0.01) guard.homeFacing = spot.x < guard.homeX ? -1 : 1
          guard.homeX = spot.x
          guard.homeY = spot.y
          if (guard.orbit) {
            guard.view.visible = true
            guard.x = spot.x
            guard.y = spot.y
            guard.facing = guard.homeFacing
            guard.view.scale.x = Math.abs(guard.view.scale.x) * guard.facing
            guard.view.position.set(guard.x, guard.y)
            if (sortDepth) guard.view.zIndex = guard.y
            guard.bar.clear()
            drawMark(guard.bar, guard, this.selected?.denId === id && this.selected.protector === index)
          }
        }
        if (!protector && guard.flash === 0 && guard.lunge === 0 && guard.x === guard.homeX && guard.y === guard.homeY) {
          if (!(guard.orbit && guard.wander)) {
            guard.bar.clear()
            drawMark(guard.bar, guard, this.selected?.denId === id && this.selected.protector === index)
          }
          continue
        }
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
        const lunge =
          guard.lunge > 0 ? Math.sin((1 - guard.lunge / LUNGE_SECONDS) * Math.PI) * 3 * (guard.height / levelHeight(1)) : 0
        guard.view.scale.x = Math.abs(guard.view.scale.x) * guard.facing
        guard.view.position.set(guard.x + lunge * guard.facing, guard.y)
        this.tint(guard.view, guard.flash > 0 ? HIT_TINT : 0xffffff)
        if (sortDepth) guard.view.zIndex = guard.y
        const hurt = protector && protector.health < protector.maximumHealth
        drawBar(
          guard.bar,
          hurt ? protector.health / protector.maximumHealth : null,
          guard.x,
          guard.y - guard.height - 3,
          Math.max(16, Math.round(guard.height * 0.5)),
        )
        drawMark(guard.bar, guard, this.selected?.denId === id && this.selected.protector === index)
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
      const mine = hero.citizenId === this.viewerId
      drawBar(fighter.bar, mine ? null : hero.health / hero.maximumHealth, fighter.x, fighter.y - 30)
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
      const height = levelHeight(point.level)
      const figure = denFoeFigure(point.level, 0, Math.abs(cellX + cellY), facing)
      if (!figure) return den
      const walk = this.wander
      const wander = walk
        ? () => {
            const spot = walk(point, this.now())
            return { x: spot.x * TILE, y: spot.y * TILE }
          }
        : undefined
      const start = wander?.() ?? { x: centerX, y: centerY }
      views.push(this.place(figure, start.x, start.y))
      const bar = new Graphics()
      bar.zIndex = Number.MAX_SAFE_INTEGER
      this.layer.addChild(bar)
      views.push(bar)
      den.guards[0] = {
        view: figure,
        bar,
        homeX: start.x,
        homeY: start.y,
        x: start.x,
        y: start.y,
        facing,
        homeFacing: facing,
        flash: 0,
        lunge: 0,
        wander,
        height,
      }
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
    const height = levelHeight(point.level)
    for (let index = 0; index < point.protectors; index++) {
      const figure = denFoeFigure(point.level, index, direction, facing)
      if (!figure) continue
      // The same walk as the server's `DenGuards.PatrolAt`: a few steps by each post, not a circle around the den.
      const round = () => {
        const spot = patrolAt(point.x + 0.5, point.y + 0.5, index, point.protectors, point.level, this.now())
        return { x: spot.x * TILE, y: spot.y * TILE }
      }
      const start = round()
      views.push(this.place(figure, start.x, start.y))
      const bar = new Graphics()
      bar.zIndex = Number.MAX_SAFE_INTEGER
      this.layer.addChild(bar)
      views.push(bar)
      den.guards[index] = {
        view: figure,
        bar,
        homeX: start.x,
        homeY: start.y,
        x: start.x,
        y: start.y,
        facing,
        homeFacing: facing,
        flash: 0,
        lunge: 0,
        wander: round,
        orbit: true,
        height,
      }
    }
    return den
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
