import {fenceShape} from './fortificationState'
import {Container,Graphics,Sprite} from 'pixi.js'
import type {Fortification} from '../api'
import {TILE,type TileSet} from './sheets'

/** Small shared textures, matched day/night coordinates; no glow or per-wall render mask. */
export class FortificationView extends Container {
  private readonly day:Sprite
  private readonly night:Sprite
  constructor(tiles:TileSet,wall:Fortification,walls:readonly Fortification[]){
    super()
    const shape=fenceShape(wall,walls),destroyed=wall.damage>=100
    const frame=destroyed?'rubble':shape==='west-end'?'east-end':shape
    this.day=new Sprite(tiles.fenceFrame(wall.level??2,frame,false))
    this.night=new Sprite(tiles.fenceFrame(wall.level??2,frame,true))
    for(const sprite of [this.day,this.night]){
      if(shape==='east-end'&&!destroyed){sprite.scale.x=-1;sprite.x=TILE}
      if(destroyed&&['v','north-end','south-end'].includes(shape)){sprite.rotation=Math.PI/2;sprite.x=TILE}
      this.addChild(sprite)
    }
    this.position.set(wall.x*TILE,wall.y*TILE)
    if(!destroyed&&wall.damage>=50){
      const horizontal=shape==='h'||shape==='east-end'
      const crack=new Graphics().rect(7,horizontal?8:4,1,3).rect(8,horizontal?10:7,1,2).rect(6,horizontal?11:8,1,2).fill({color:0x251b19,alpha:.8})
      this.addChild(crack)
    }
    this.setNight(0)
  }
  setNight(amount:number){this.day.alpha=1;this.night.alpha=amount}
}
