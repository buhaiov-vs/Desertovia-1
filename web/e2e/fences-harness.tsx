import {createRoot} from 'react-dom/client'
import {useState} from 'react'
import type {TileSet} from '../src/game/sheets'
import {VillageWorld} from '../src/game/VillageWorld'
import {FortificationView} from '../src/game/FortificationView'
import {api,type Catalog,type VillageState} from '../src/api'
import {Container} from 'pixi.js'
import {DefensePanel} from '../src/ui/DefensePanel'
import {I18nProvider} from '../src/i18n'
import '../src/index.css'
const catalog=await fetch('/api/catalog').then(r=>r.json()) as Catalog
const data=await fetch('/api/villages/demo-bridge').then(r=>r.json())
let state={...data.state,structures:[{kind:'Hut',plotId:'2,1',damage:0},{kind:'StoneYard',plotId:'6,1',damage:0}],storages:[],sites:[],project:null,
  completedProjects:['palisade'],fields:[],trees:[],wildlife:[],wilds:null,raids:[],battles:[]} as VillageState
state.fortifications=data.state.fortifications?.length?data.state.fortifications.map((f:NonNullable<VillageState['fortifications']>[number])=>({...f,level:2,damage:0,open:false})):[]
if(!state.fortifications.length){
 for(let y=-1;y<=26;y++)state.fortifications.push({id:`wall:-1,${y}`,x:-1,y,level:2,damage:0,gate:y>=12&&y<=14,open:false})
 for(let x=0;x<=27;x++)for(const y of [-1,26])state.fortifications.push({id:`wall:${x},${y}`,x,y,level:2,damage:0,gate:false,open:false})
}
state.fortificationUpgrade={level:2,nextLevel:3,sections:84,strength:2,damaged:0,materials:[{resource:'Stone',amount:420},{resource:'Planks',amount:84}],work:63,blockedBy:null,readyAfterBattle:false}
const world=await VillageWorld.create(document.getElementById('fence-map')!,{minimap:false});world.setCatalog(catalog);world.setVillage(state);world.setNightOverride(false);world.zoomBy(.75);world.centerOn(1,2)
const internals=world as unknown as {projectLayer:Container;tiles:TileSet;app:{renderer:{extract:{pixels:(o:unknown)=>{pixels:Uint8Array;width:number;height:number}}}};nightAmount:number}
const ids=new WeakMap<Container,number>();let serial=0;const calls:string[]=[]
let redraw:()=>void=()=>{}
function update(next:VillageState){state=next;world.setVillage(state);redraw()}
api.upgradeFortifications=async()=>{calls.push('upgrade');return {...state,fortificationUpgrade:{...state.fortificationUpgrade!,blockedBy:'defense.upgradeWorking'}}}
api.gateOrder=async()=>{}
const stand={
 material:(level:number)=>update({...state,fortifications:state.fortifications!.map(f=>({...f,level})),fortificationUpgrade:{...state.fortificationUpgrade!,level,strength:level===3?3.5:2,nextLevel:level===3?null:level+1,blockedBy:level===3?'building.maxLevel':null}}),
 damage:(damage:number)=>update({...state,fortifications:state.fortifications!.map((f,i)=>i===0?{...f,damage}:f)}),
 open:(open:boolean)=>update({...state,fortifications:state.fortifications!.map(f=>f.gate?{...f,open}:f)}),
 blocked:(blockedBy:string|null)=>update({...state,fortificationUpgrade:{...state.fortificationUpgrade!,blockedBy}}),
 night:(night:boolean)=>world.setNightOverride(night),
 focus:(gate:boolean)=>world.centerOn(1,gate?13:2),
 bank:()=>world.centerOn(26,-1),
 zoom:(factor:number)=>world.zoomBy(factor),
 info:()=>{
  const views=internals.projectLayer.children.filter(v=>v instanceof FortificationView)
  for(const view of views)if(!ids.has(view))ids.set(view,++serial)
  return {count:views.length,first:views[0]?ids.get(views[0]):0,night:internals.nightAmount,calls}
 },
 alpha:()=>[2,3].map(level=>{
   const wall={id:'test',x:0,y:0,level,damage:0,gate:false,open:false}
   const view=new FortificationView(internals.tiles,wall,[wall])
   const samples=[0,.5,1].map(a=>{view.setNight(a);return internals.app.renderer.extract.pixels({target:view,resolution:2})})
   let common=0,holes=0;for(let i=3;i<samples[0].pixels.length;i+=4)if(samples[0].pixels[i]>250&&samples[2].pixels[i]>250){common++;if(samples[1].pixels[i]<250)holes++}
   view.destroy({children:true});return {common,holes,dimensions:samples.map(s=>[s.width,s.height])}
 })
}
Object.assign(window,{fenceStand:stand})
function Controls(){const[revision,setRevision]=useState(0);redraw=()=>setRevision(v=>v+1);return <I18nProvider><div data-revision={revision}><DefensePanel slug="demo-bridge" village={state} ruler tsar={!new URLSearchParams(location.search).has('governor')} onVillage={update}/></div></I18nProvider>}
createRoot(document.getElementById('app')!).render(<Controls/> )
