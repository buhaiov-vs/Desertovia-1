import {receiveFighterSample,fighterPositionAt,fighterIsMoving,type FighterSample} from './fighterSamples'
import { Container, Graphics, GraphicsContext } from 'pixi.js'
import type { EnemyKind, Raid, RaidBattle } from '../api'
import { TILE } from './sheets'
import { CAMPFIRE, MAP_HEIGHT, MAP_LEFT, MAP_TOP, MAP_WIDTH, Terrain } from './worldMap'

interface EnemyLook {
  /** Integer size multiplier baked into geometry; keeps pixel art sharp when facing flips. */
  size?: number
  /** World pixels per second while closing in. */
  speed: number
  /** Height of the steps while moving, in world pixels. */
  hop: number
  /** Drawn facing right, one character per world pixel; `.` is empty. */
  pixels: string[]
  colors: Record<string, number>
}

const LOOKS: Record<EnemyKind, EnemyLook> = {
  Wolf: {
    speed: 30,
    hop: 1.5,
    pixels: [
      '.........e.e.',
      '.........eee.',
      '........hhkh.',
      't.......hhhhn',
      'tt.bbbbbbhh..',
      '.tbbbbbbbb...',
      '..bbwwwwbb...',
      '..l.l..l.l...',
      '..l.l..l.l...',
    ],
    colors: { e: 0x55555e, h: 0x7a7a84, k: 0xffd23a, n: 0x1b1206, t: 0x66666e, b: 0x6e6e78, w: 0xa8a8b0, l: 0x45454c },
  },
  Boar: {
    speed: 26,
    hop: 1,
    pixels: [
      '..........ee..',
      '.....bbbbbbhh.',
      '..bbbbbbbbbhkh',
      '.bbbbbbbbbbhhhn',
      't.bbbbbbbbbhhs',
      '..bbbbbbbbb...',
      '..l.l...l.l...',
      '..d.d...d.d...',
    ],
    colors: { e: 0x3a2a1a, b: 0x5a3e2a, h: 0x6a4a32, k: 0xff4030, n: 0xc08070, s: 0xf2ede4, t: 0x3a2a1a, l: 0x3a2a1a, d: 0x1b1206 },
  },
  Bear: {
    speed: 20,
    hop: 0.8,
    pixels: [
      '...........ee...',
      '..........hhhh..',
      '..........hhkhh.',
      '....bbbbbbhhhhhn',
      '..bbbbbbbbbbhhh.',
      '.bbbbbbbbbbbbb..',
      '.bbbbbbbbbbbbb..',
      '.bbbbbbbbbbbbb..',
      '..bb.bb...bb.bb.',
      '..bb.bb...bb.bb.',
      '..cc.cc...cc.cc.',
    ],
    colors: { e: 0x3a2414, h: 0x5a3a22, k: 0xff7a30, n: 0x1b1206, b: 0x4a2e1a, c: 0xe0d8c0 },
  },
  Zombie: {
    size: 2,
    speed: 10,
    hop: 0.6,
    pixels: [
      '..hhh....',
      '.hgggg...',
      '.ggkgk...',
      '.gggggg..',
      '..ggg....',
      '.ssssgggg',
      '.ssss....',
      '.sssr....',
      '.ssss....',
      '.pppp....',
      '.pp.pp...',
      '.pp.pp...',
      '.ff.ff...',
    ],
    colors: { h: 0x3a2a1a, g: 0x7aa05a, k: 0xff3030, s: 0x5a4a6a, r: 0x8a2a2a, p: 0x3a3a4a, f: 0x2a2a2a },
  },
  Skeleton: {
    size: 2,
    speed: 16,
    hop: 1,
    pixels: [
      '..www...',
      '.wwwww..',
      '.wkwkw..',
      '.wwwww..',
      '..wtw...',
      '...w....',
      '.wwwwwww',
      '..w.w...',
      '.wwwww..',
      '..w.w...',
      '...w....',
      '..www...',
      '..w.w...',
      '..w.w...',
      '.ww.ww..',
    ],
    colors: { w: 0xe8e4d4, k: 0x60e0ff, t: 0x8a8474 },
  },
  Rat: {
    speed: 34,
    hop: 0.8,
    pixels: [
      '.......ee..',
      '..bbbbbbhh.',
      'tbbbbbbbhkhn',
      't.bbbbbbbhh.',
      '...l.l.l.l..',
    ],
    colors: { e: 0x8a6a6a, b: 0x5a5048, h: 0x6a6058, k: 0xff4030, n: 0xd09090, t: 0xb09080, l: 0x3a3028 },
  },
  Imp: {
    speed: 26,
    hop: 1.8,
    pixels: [
      '.h...h..',
      '.hrrrh..',
      '.rkrkr..',
      '.rrrrr..',
      'w.rrr.w.',
      'wwrrrrww',
      '..rrrr..',
      '..r..r.t',
      '.rr..rrt',
    ],
    colors: { h: 0x2a1a14, r: 0xa83a2a, k: 0xffe040, w: 0x5a2a3a, t: 0x7a2a1a },
  },
  Ghoul: {
    speed: 18,
    hop: 0.8,
    pixels: [
      '...ggg....',
      '..gkgkg...',
      '..ggggg...',
      '..gtttg...',
      '.ssssss.cc',
      'sssssssgc.',
      '.ssssss...',
      '.ssrsss...',
      '..pp.pp...',
      '..pp.pp...',
      '.cc..cc...',
    ],
    colors: { g: 0x8a9a7a, k: 0xffe040, t: 0x2a2a1a, s: 0x4a3a3a, r: 0x7a1a1a, p: 0x3a3030, c: 0xd8d0b8 },
  },
  Wraith: {
    speed: 22,
    hop: 2.2,
    pixels: [
      '..hhhh...',
      '.hhhhhh..',
      '.hkhhkh..',
      '.hhhhhh..',
      'hhhhhhhhh',
      '.hhhhhhh.',
      '.hhhhhh..',
      '..hhhhh..',
      '..h.hh.h.',
      '.h...h...',
    ],
    colors: { h: 0x9ab8c8, k: 0x1a2a3a },
  },
  BarrowKing: {
    speed: 12,
    hop: 0.5,
    pixels: [
      '..y.y.y...',
      '..yyyyy...',
      '..wwwww...',
      '..wkwkw...',
      '..wwwww...',
      '...www....',
      '.aaaaaaa..',
      'aaaaaaaaa.',
      'a.aaaaa.ss',
      'a.aaraa.s.',
      '..aaaaa.s.',
      '..pp.pp.s.',
      '..pp.pp...',
      '.ww..ww...',
    ],
    colors: { y: 0xe8c040, w: 0xe0dcc8, k: 0x60ff80, a: 0x3a4a3a, r: 0xa83030, p: 0x2a2a2a, s: 0x9a9aa8 },
  },
}

const SHARED_CONTEXTS = new Map<EnemyKind, GraphicsContext>()

/** The code-drawn look of an enemy, shared by every layer that draws one; the feet are at (0, 0). */
export function enemyContext(kind: EnemyKind): GraphicsContext {
  let context = SHARED_CONTEXTS.get(kind)
  if (!context) {
    context = drawLook(LOOKS[kind])
    SHARED_CONTEXTS.set(kind, context)
  }
  return context
}

/** Height of an enemy's look, in world pixels. */
export function enemyHeight(kind: EnemyKind) {
  return LOOKS[kind].pixels.length * (LOOKS[kind].size ?? 1)
}

/** Raiders drawn for one attack at most; a bigger pack is shown by this many. */
const MOST_SHOWN = 8
/** Where a pack shows up and where it waits for the strike, in tiles from the campfire. */
const SPAWN_DISTANCE = 16
const EDGE_DISTANCE = 6
/** Where raiders who broke in run to, in tiles from the campfire. */
const BREAK_IN_DISTANCE = 1.5
/** After the strike the server takes a few seconds to fight the raid out; the pack charges meanwhile. */
const CHARGE_SECONDS = 3
/** How long a fought raid stays on the map: the slain fade away, the rest run off. */
const LINGER_SECONDS = 10
const FLEE_SPEED = 2.2
/** Guards meet the pack this far in front of it, in tiles. */
const FRONT_GAP = 1.5

const BLOCKING: number[] = [Terrain.Water, Terrain.Reeds, Terrain.Mountain, Terrain.Ravine, Terrain.Thicket, Terrain.Rockfall]

interface Raider {
  view: Graphics
  /** Place in the pack around its centre, in world pixels. */
  offsetX: number
  offsetY: number
  x: number
  y: number
  phase: number
  slain: boolean
}

interface Pack {
  raid: Raid
  /** Direction the pack comes from, as an angle around the campfire. */
  angle: number
  raiders: Raider[]
}

function drawLook(look: EnemyLook): GraphicsContext {
  const context = new GraphicsContext()
  const size = look.size ?? 1
  const width = Math.max(...look.pixels.map((row) => row.length))
  const height = look.pixels.length
  context.ellipse(0, 0, width * size * 0.45, 1.5 * size).fill({ color: 0x1b1206, alpha: 0.3 })
  look.pixels.forEach((row, y) =>
    [...row].forEach((cell, x) => {
      const color = look.colors[cell]
      if (color !== undefined) context.rect((x - width / 2) * size, (y - height) * size, size, size).fill({ color })
    }),
  )
  return context
}

function hash(text: string): number {
  let value = 2166136261
  for (let index = 0; index < text.length; index++) {
    value ^= text.charCodeAt(index)
    value = Math.imul(value, 16777619)
  }
  return value >>> 0
}

/** Point around the campfire, in world pixels. */
function around(angle: number, tiles: number) {
  return { x: (CAMPFIRE.x + Math.cos(angle) * tiles) * TILE, y: (CAMPFIRE.y + Math.sin(angle) * tiles) * TILE }
}

function lerp(from: { x: number; y: number }, to: { x: number; y: number }, share: number) {
  const t = Math.min(1, Math.max(0, share))
  return { x: from.x + (to.x - from.x) * t, y: from.y + (to.y - from.y) * t }
}

/**
 * Attacks on the camp drawn on the map. The warning is a countdown only: packs appear at strikesAt,
 * then charge; once the server has fought it out, the slain fall and the rest run off. Times are on the
 * local clock, like the rest of the map.
 */
export class EnemyLayer {
  private readonly contexts = new Map<EnemyKind, GraphicsContext>()
  private readonly packs = new Map<string, Pack>()
  private readonly layer: Container
  private raids: Raid[] = []
  private battles:RaidBattle[] = []
  private liveViews=new Map<string,{actor:Graphics;bar:Graphics;health:number;hitAt:number;barKey:string;
    facing:number}>()
  private liveSamples=new Map<string,FighterSample>()
  private warnings=new Map<string,{view:Graphics;key:string}>()
  private selectedUnit:string|null=null
  select(id:string|null){this.selectedUnit=id}
  setBattles(battles:RaidBattle[]){
    this.battles=battles
    const at=performance.now(),seen=new Set<string>()
    for(const b of battles){
      for(const u of b.units){seen.add(u.id);this.liveSamples.set(u.id,receiveFighterSample(this.liveSamples.get(u.id),b.id,b.revision,u.x,u.y,at))}
      for(const f of b.fighters){const id='guard:'+(f.isTsar?f.accountId:f.citizenId);seen.add(id);this.liveSamples.set(id,receiveFighterSample(this.liveSamples.get(id),b.id,b.revision,f.x,f.y,at))}
    }
    for(const id of this.liveSamples.keys())if(!seen.has(id))this.liveSamples.delete(id)
  }
  pick(x:number,y:number):{battleId:string;unitId:string}|null{
    for(const battle of this.battles){
      if(['Warning','Cancelled','Finished'].includes(battle.phase))continue
      const kind=this.raids.find(r=>r.id===battle.id)?.enemy??'Skeleton'
      const height=(variant:string,unitKind:EnemyKind=kind)=>enemyHeight(unitKind)*(variant==='boss'?1.7:variant==='strong'?1.35:variant==='weak' ? .85 : 1)
      const at=(u:RaidBattle['units'][number])=>this.liveViews.get(u.id)?.actor.position??{x:u.x*TILE,y:u.y*TILE}
      const unit=battle.units.filter(u=>{
        const p=at(u),dy=y-p.y
        return u.health>0&&!u.exitedAt&&Math.abs(p.x-x)<=TILE*.8&&dy>=-height(u.variant,u.enemy??kind)-4&&dy<=6
      }).sort((a,b)=>Math.hypot(at(a).x-x,at(a).y-height(a.variant,a.enemy??kind)/2-y)-Math.hypot(at(b).x-x,at(b).y-height(b.variant,b.enemy??kind)/2-y))[0]
      if(unit){this.selectedUnit=unit.id;return{battleId:battle.id,unitId:unit.id}}
    }
    return null
  }
  private updateLive(now:number,sortDepth:boolean){
    const shown=new Set<string>(),warningIds=new Set<string>()
    const draw=(id:string,x:number,y:number,hp:number,max:number,kind:EnemyKind|null,variant:string,dead:boolean,attackAt=0,deathAt=0)=>{
      shown.add(id)
      let view=this.liveViews.get(id)
      if(!view){
        view={actor:kind?new Graphics(this.context(kind)):new Graphics(),bar:new Graphics(),health:hp,hitAt:0,barKey:'',
          facing:1}
        view.actor.position.set(x*TILE,y*TILE);this.layer.addChild(view.actor,view.bar);this.liveViews.set(id,view)
      }
      const at=performance.now(),sample=this.liveSamples.get(id)
      const position=sample?fighterPositionAt(sample,at):{x,y}
      const beforeX=view.actor.x,moving=!!sample&&fighterIsMoving(sample,at)
      view.actor.position.set(position.x*TILE,position.y*TILE)
      if(Math.abs(view.actor.x-beforeX)>.0001)view.facing=view.actor.x<beforeX?-1:1
      const size=variant==='boss'?1.7:variant==='strong'?1.35:variant==='weak' ? .85 : 1
      view.actor.scale.set(view.facing*size,size)
      const strike=Math.max(0,Math.min(1,(now-attackAt)/240))
      view.actor.rotation=dead?Math.PI/2:Math.sin(strike*Math.PI)*.15
      if(!dead&&moving)view.actor.y-=Math.abs(Math.sin(now/90))*LOOKS[kind??'Skeleton'].hop
      if(hp<view.health)view.hitAt=now
      view.health=hp;view.actor.tint=now-view.hitAt<180?0xff7777:0xffffff
      view.actor.alpha=dead?Math.max(0,1-(now-deathAt)/10000):1
      const barKey=`${hp}/${max}/${variant}/${dead}/${id===this.selectedUnit}`
      if(view.barKey!==barKey){
        view.barKey=barKey;view.bar.clear()
        if(!dead){
          const barY=-(kind?enemyHeight(kind)*size:26)-7
          view.bar.rect(-13,barY,26,4).fill(0x241910)
          view.bar.rect(-12,barY+1,24*Math.max(0,hp/Math.max(1,max)),2).fill(kind?0xda5546:0x68c86a)
          if(id===this.selectedUnit)view.bar.circle(0,0,14*size).stroke({color:0xffd75b,width:2})
        }
      }
      view.bar.position.set(view.actor.x,view.actor.y)
      if(sortDepth){view.actor.zIndex=view.actor.y;view.bar.zIndex=view.actor.y+1}
    }
    for(const battle of this.battles){
      if(['Warning','Cancelled'].includes(battle.phase)||now<Date.parse(battle.startsAt))continue
      if(battle.finishedAt&&now-Date.parse(battle.finishedAt)>10000)continue
      const kind=this.raids.find(r=>r.id===battle.id)?.enemy??'Skeleton'
      for(const u of battle.units){
        if(u.exitedAt||u.diedAt&&now-Date.parse(u.diedAt)>10000)continue
        if(u.health>0&&u.strikeAt&&u.strikeX!=null&&u.strikeY!=null&&battle.phase!=='Retreating'){
          warningIds.add(u.id)
          let warning=this.warnings.get(u.id)
          if(!warning){warning={view:new Graphics(),key:''};this.layer.addChild(warning.view);this.warnings.set(u.id,warning)}
          const key=`${u.strikeAt}/${u.strikeX}/${u.strikeY}`
          if(warning.key!==key){warning.key=key;warning.view.clear().circle(0,0,1.65*TILE).fill({color:0xff5d32,alpha:.18}).stroke({color:0xff9c40,width:2})}
          warning.view.position.set(u.strikeX*TILE,u.strikeY*TILE);warning.view.zIndex=warning.view.y-.5
        }
        draw(u.id,u.x,u.y,u.health,u.maximumHealth,u.enemy??kind,u.variant,u.health<=0,['Fighting','Breaching','Raiding'].includes(u.state)&&u.nextAttackAt?Date.parse(u.nextAttackAt)-3000:0,u.diedAt?Date.parse(u.diedAt):now)
      }
      for(const f of battle.fighters)if(!battle.finishedAt&&f.active&&f.health>0)draw('guard:'+(f.isTsar?f.accountId:f.citizenId),f.x,f.y,f.health,f.maximumHealth,null,'regular',false)
    }
    for(const [id,warning] of this.warnings)if(!warningIds.has(id)){warning.view.destroy();this.warnings.delete(id)}
    for(const [id,view] of this.liveViews)if(!shown.has(id)){view.actor.destroy();view.bar.destroy();this.liveViews.delete(id)}
  }
  private terrain: Uint8Array | null = null

  constructor(layer: Container) {
    this.layer = layer
  }

  setTerrain(terrain: Uint8Array) {
    this.terrain = terrain
  }

  setRaids(raids: Raid[]) {
    this.raids = raids
    for (const pack of this.packs.values()) {
      const next = raids.find((raid) => raid.id === pack.raid.id)
      if (next) pack.raid = next
    }
  }

  /** Where guards on watch go to meet the nearest pack on its way, in world pixels; null when none is coming. */
  front(now: number): { x: number; y: number } | null {
    let nearest: { x: number; y: number; strikesAt: number } | null = null
    for (const pack of this.packs.values()) {
      if (pack.raid.outcome !== 'Pending') continue
      const strikesAt = Date.parse(pack.raid.strikesAt)
      if (nearest && nearest.strikesAt <= strikesAt) continue
      const center = this.center(pack, now)
      const toCamp = Math.atan2(CAMPFIRE.y * TILE - center.y, CAMPFIRE.x * TILE - center.x)
      nearest = {
        x: center.x + Math.cos(toCamp) * FRONT_GAP * TILE,
        y: center.y + Math.sin(toCamp) * FRONT_GAP * TILE,
        strikesAt,
      }
    }
    return nearest && { x: nearest.x, y: nearest.y }
  }

  update(seconds: number, now: number, sortDepth: boolean) {
    this.updateLive(now,sortDepth)
    const shown = new Set<string>()
    for (const raid of this.raids) {
      if ((raid.engineVersion??0)>=2)continue
      if (!this.visible(raid, now)) continue
      shown.add(raid.id)
      if (!this.packs.has(raid.id)) this.packs.set(raid.id, this.addPack(raid, now))
    }
    for (const [id, pack] of this.packs) {
      if (shown.has(id)) continue
      for (const raider of pack.raiders) raider.view.destroy()
      this.packs.delete(id)
    }

    for (const pack of this.packs.values()) this.move(pack, seconds, now, sortDepth)
  }

  private visible(raid: Raid, now: number) {
    // announcedAt starts the warning, not the movement. Later waves wait for their own strikesAt.
    if (now < Date.parse(raid.strikesAt)) return false
    if (raid.outcome === 'Pending') return Date.parse(raid.strikesAt) <= now
    return raid.resolvedAt !== null && now - Date.parse(raid.resolvedAt) < LINGER_SECONDS * 1000
  }

  /** Centre of the pack while it is on its way or charging. */
  private center(pack: Pack, now: number) {
    const raid = pack.raid
    const strikesAt = Date.parse(raid.strikesAt)
    const path = raid.approachPath
    if (path && path.length > 1) {
      // Show the final approach at the actual attack time; the full server route remains authoritative.
      const visible = path.slice(-7)
      const share = Math.min(1, Math.max(0, (now - strikesAt) / (CHARGE_SECONDS * 1000))) * (visible.length - 1)
      const index = Math.min(visible.length - 2, Math.floor(share))
      return lerp({ x: (visible[index].x + .5) * TILE, y: (visible[index].y + .5) * TILE },
        { x: (visible[index+1].x + .5) * TILE, y: (visible[index+1].y + .5) * TILE }, share - index)
    }
    const edge = around(pack.angle, EDGE_DISTANCE)
    return lerp(edge, around(pack.angle, BREAK_IN_DISTANCE), (now - strikesAt) / (CHARGE_SECONDS * 1000))
  }

  private move(pack: Pack, seconds: number, now: number, sortDepth: boolean) {
    const raid = pack.raid
    const look = LOOKS[raid.enemy]
    const resolved = raid.outcome !== 'Pending' && raid.resolvedAt !== null
    const sinceResolved = resolved ? (now - Date.parse(raid.resolvedAt!)) / 1000 : 0
    if (resolved) {
      const slain = Math.min(pack.raiders.length, Math.round((raid.killed / Math.max(1, raid.count)) * pack.raiders.length))
      pack.raiders.forEach((raider, index) => (raider.slain = index < slain))
    }
    // Raiders who broke in roam the camp for a moment before running off with the loot.
    const plundering = raid.outcome === 'Lost' && sinceResolved < CHARGE_SECONDS
    const center = resolved
      ? plundering
        ? around(pack.angle, BREAK_IN_DISTANCE)
        : around(pack.angle, SPAWN_DISTANCE)
      : this.center(pack, now)
    const fade = resolved ? Math.max(0, 1 - sinceResolved / LINGER_SECONDS) : 1

    for (const raider of pack.raiders) {
      if (raider.slain) {
        raider.view.rotation = Math.PI / 2
        raider.view.alpha = fade
        raider.view.position.set(Math.round(raider.x), Math.round(raider.y))
        continue
      }
      const targetX = center.x + raider.offsetX
      const targetY = center.y + raider.offsetY
      const dx = targetX - raider.x
      const dy = targetY - raider.y
      const distance = Math.hypot(dx, dy)
      const moving = distance > 0.5
      if (moving) {
        const charging = !resolved && now >= Date.parse(raid.strikesAt)
        const speed = look.speed * (resolved || charging ? FLEE_SPEED : 1)
        // Creeping packs keep up with the clock even after the tab slept.
        const step = resolved || charging ? Math.min(distance, speed * seconds) : Math.min(distance, Math.max(speed * seconds, distance - TILE))
        raider.x += (dx / distance) * step
        raider.y += (dy / distance) * step
        if (Math.abs(dx) > 0.5) raider.view.scale.x = dx < 0 ? -1 : 1
        raider.phase += seconds * 10
      }
      const hop = moving ? Math.abs(Math.sin(raider.phase)) * -look.hop : 0
      raider.view.position.set(Math.round(raider.x), Math.round(raider.y + hop))
      raider.view.alpha = resolved ? fade : Math.min(1, raider.view.alpha + seconds)
      if (sortDepth) raider.view.zIndex = raider.y
    }
  }

  private addPack(raid: Raid, now: number): Pack {
    const pack: Pack = { raid, angle: this.approach(raid.id), raiders: [] }
    const count = Math.min(MOST_SHOWN, Math.max(1, raid.count))
    const start = raid.outcome === 'Pending' ? this.center(pack, now) : around(pack.angle, EDGE_DISTANCE)
    const seed = hash(raid.id)
    for (let index = 0; index < count; index++) {
      const ring = Math.floor(index / 4)
      const angle = (index % 4) * (Math.PI / 2) + ring * 0.8 + ((seed >> index) & 3) * 0.2
      const radius = index === 0 ? 0 : (0.7 + ring * 0.6) * TILE
      const raider: Raider = {
        view: new Graphics(this.context(raid.enemy)),
        offsetX: Math.cos(angle) * radius,
        offsetY: Math.sin(angle) * radius * 0.6,
        x: 0,
        y: 0,
        phase: index,
        slain: false,
      }
      raider.x = start.x + raider.offsetX
      raider.y = start.y + raider.offsetY
      raider.view.alpha = 0
      raider.view.position.set(raider.x, raider.y)
      raider.view.zIndex = raider.y
      this.layer.addChild(raider.view)
      pack.raiders.push(raider)
    }
    return pack
  }

  /** A direction from the raid id, preferring one where the pack appears on dry land. */
  private approach(raidId: string) {
    const base = (hash(raidId) % 360) * (Math.PI / 180)
    for (let attempt = 0; attempt < 12; attempt++) {
      const angle = base + attempt * (Math.PI / 6)
      const spawn = around(angle, SPAWN_DISTANCE)
      const edge = around(angle, EDGE_DISTANCE)
      if (this.dryLand(spawn.x, spawn.y) && this.dryLand(edge.x, edge.y)) return angle
    }
    return base
  }

  private dryLand(worldX: number, worldY: number) {
    if (!this.terrain) return true
    const column = Math.floor(worldX / TILE) - MAP_LEFT
    const row = Math.floor(worldY / TILE) - MAP_TOP
    if (column < 0 || row < 0 || column >= MAP_WIDTH || row >= MAP_HEIGHT) return false
    return !BLOCKING.includes(this.terrain[row * MAP_WIDTH + column])
  }

  private context(kind: EnemyKind) {
    let context = this.contexts.get(kind)
    if (!context) {
      context = drawLook(LOOKS[kind])
      this.contexts.set(kind, context)
    }
    return context
  }
}
