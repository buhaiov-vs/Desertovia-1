import { Container, Graphics, Sprite } from 'pixi.js'
import type { BuildingDefinition, Site } from '../api'
import { BuildingLighting } from './BuildingLighting'
import { TILE, type TileSet } from './sheets'
import frames from '../assets/construction/hut.json'

type Phase = 'ground' | keyof typeof frames | 'finish'
const stages: Phase[] = ['ground', 'foundation', 'frame', 'walls', 'roof', 'finish']
const anchors = {
  foundation: { center:328, base:568, width:412 },
  frame: { center:300, base:568, width:424 },
  walls: { center:329, base:518, width:430 },
  roof: { center:303, base:518, width:424 },
}

/** Use catalog stage IDs, not arbitrary percentages of total building cost. */
export function hutPhase(site: Site, definition?: BuildingDefinition): Phase {
  const id = definition?.stages[site.stage]?.id ?? site.stages[site.stage]?.id
  return stages.includes(id as Phase) ? id as Phase : stages[Math.min(5, Math.max(0,site.stage))]
}

/** Rendering cache must change when goods arrive, even if the work progress does not. */
export function siteVisualKey(site: Site) {
  return [site.kind,site.plotId,site.stage,Math.floor(site.progress*24),
    site.stages[site.stage]?.jobs.map(job=>[job.jobId,job.done,job.required])]
}

/** The hut pilot shares its existing plot, door, server stages and final art. */
export class HutConstruction extends Container {
  readonly light = new Container()
  readonly roofAnchor: { x: number; y: number }
  readonly phase: Phase
  private readonly finished?: BuildingLighting
  constructor(tiles: TileSet,site: Site,definition: BuildingDefinition|undefined,x:number,y:number) {
    super()
    this.phase=hutPhase(site,definition)
    this.position.set(x*TILE,y*TILE)
    this.zIndex=(y+3)*TILE
    const heightOf = (phase: Phase) => phase === 'ground' ? 16 : phase === 'finish' ? 41 :
      (frames[phase].bounds[3] - frames[phase].bounds[1]) * 40 / anchors[phase].width
    const stageIndex = stages.indexOf(this.phase)
    const work = (site.stages[site.stage]?.jobs ?? []).filter(job => !job.jobId.startsWith('bring-'))
    const required = work.reduce((sum, job) => sum + job.required, 0)
    const share = required > 0 ? Math.min(1, work.reduce((sum, job) => sum + job.done, 0) / required) : 0
    const visibleHeight = Math.max(heightOf(stages[Math.max(0, stageIndex - 1)]), heightOf(this.phase) * share)
    this.roofAnchor = { x: x * TILE + 24, y: y * TILE + 48 - visibleHeight }
    const ground=new Graphics()
    ground.ellipse(24,43,23,5).fill({color:0x715534,alpha:0.32})
    this.addChild(ground)
    if (this.phase==='ground') {
      const outline=new Graphics()
      outline.moveTo(4,44).lineTo(4,31).lineTo(44,31).lineTo(44,44)
        .stroke({color:0xc0aa78,width:0.8,alpha:0.8})
      for(const px of [4,44]) outline.rect(px-1,30,2,4).fill(0x77502c)
      this.addChild(outline)
    } else {
      const phaseIndex=stages.indexOf(this.phase)
      const work=(site.stages[site.stage]?.jobs ?? []).filter(job=>!job.jobId.startsWith('bring-'))
      const required=work.reduce((total,job)=>total+job.required,0)
      const fraction=required ? Math.min(1,work.reduce((total,job)=>total+job.done,0)/required) : 0
      const draw=(phase:Phase) => {
        if(phase==='ground')return undefined
        if(phase==='finish') {
          const finished=new BuildingLighting(tiles,'Hut',0,0,3,3,false)
          this.addChild(finished)
          return finished
        }
        const a=anchors[phase], sprite=new Sprite(tiles.constructionFrame(phase))
        const scale=40/a.width
        sprite.scale.set(scale)
        sprite.position.set(24-a.center*scale,48-a.base*scale)
        this.addChild(sprite)
        return sprite
      }
      draw(stages[phaseIndex-1])
      const current=draw(this.phase)
      if(current) {
        if(current instanceof BuildingLighting)this.finished=current
        const height=this.phase==='finish' ? 41 :
          (frames[this.phase].bounds[3]-frames[this.phase].bounds[1])*40/anchors[this.phase].width
        const mask=new Graphics().rect(-1,48-height*fraction,50,height*fraction).fill(0xffffff)
        current.mask=mask
        this.addChild(mask)
      }
    }

    // Small matching timber scaffold stays on the side, never across the door.
    if(['frame','walls','roof','finish'].includes(this.phase)) {
      const scaffold=new Graphics()
      const ladder=this.phase==='finish'
      const bottom=43,top=ladder?17:23,left=2,right=7
      scaffold.moveTo(left,bottom).lineTo(left,top).moveTo(right,bottom).lineTo(right,top)
        .stroke({color:0x684023,width:1.5})
      for(let yy=top+3;yy<bottom;yy+=5)
        scaffold.moveTo(left,yy).lineTo(right,yy).stroke({color:0xc59759,width:1})
      if(!ladder) scaffold.rect(0,top+1,11,2).fill(0x9b703e)
      this.addChild(scaffold)
    }

    // Delivered goods represent jobs of the current stage, not an invented stock remainder.
    const jobs=site.stages[site.stage]?.jobs ?? []
    let slot=0
    for(const job of jobs) {
      if(!job.jobId.startsWith('bring-') || job.done<=0 || job.required<=0)continue
      const count=Math.min(3,Math.ceil(job.done/job.required*3))
      const pile=new Graphics(),py=49-slot*4
      for(let n=0;n<count;n++) {
        const px=2+(n%2)*4,yy=py-Math.floor(n/2)*2
        if(job.jobId==='bring-stones') {
          pile.poly([px,yy,px+1,yy-3,px+4,yy-3,px+5,yy,px+3,yy+1]).fill(0x8d969c)
          pile.rect(px+1,yy-2,2,1).fill(0xb5bcc0)
        } else if(job.jobId==='bring-hides') {
          pile.poly([px,yy-3,px+4,yy-2,px+3,yy+1,px-1,yy]).fill(0x9f6640)
        } else {
          const reed=job.jobId==='bring-reeds'
          pile.rect(px,yy-2,6,2).fill(reed?0xbaad59:0x81532b)
          pile.rect(px,yy-2,5,0.7).fill(reed?0xd9cc80:0xba8c4f)
          if(reed)pile.rect(px+3,yy-2,0.8,2).fill(0x775733)
        }
      }
      this.addChild(pile);slot++
    }

    // One small work lantern. Emission is attached at the world plot, above darkness.
    const lantern=new Graphics().rect(42,35,1,9).fill(0x684023)
      .rect(40.5,33,4,5).fill(0x4b3426).rect(41.5,34,2,3).fill(0xb99958)
    this.addChild(lantern)
    const glow=new Graphics()
    glow.ellipse(42.5,43,11,6).fill({color:0xffb24d,alpha:0.10})
      .ellipse(42.5,39,6,6).fill({color:0xffb24d,alpha:0.09})
      .rect(41.5,34,2,3).fill({color:0xffc16b,alpha:0.85})
    glow.blendMode='add'
    this.light.addChild(glow)
    this.light.position.copyFrom(this.position)
    this.light.alpha=0
  }
  setNight(amount:number) {
    const level=Math.max(0,Math.min(1,amount))
    this.finished?.setNight(level)
    this.light.alpha=level
  }
  override destroy(options?:Parameters<Container['destroy']>[0]) {
    this.light.destroy({children:true})
    super.destroy(options)
  }
}
