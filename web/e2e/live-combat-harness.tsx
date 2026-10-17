import { receiveFighterSample, fighterPositionAt } from '../src/game/fighterSamples'
import { buildLayout, Terrain } from '../src/game/worldMap'
// Production render and controls, isolated from real players, payments and director calls.
import { useRef, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { Application, Container, Graphics } from 'pixi.js'
import { api, mergeBattles, type Catalog, type Citizen, type Raid, type RaidBattle, type VillageState } from '../src/api'
import { currentEnergy } from '../src/energy'
import { EnemyLayer } from '../src/game/enemies'
import { RaidBanner } from '../src/ui/RaidBanner'
import { BattleControls } from '../src/ui/BattleControls'
import { ActionEnergy } from '../src/ui/ActionEnergy'
import { I18nProvider } from '../src/i18n'
import '../src/index.css'
const now=Date.parse('2026-10-15T12:00:00Z')
const iso=(ms:number)=>new Date(ms).toISOString()
const raid={wave:2,waves:3,id:'battle',enemy:'Skeleton',engineVersion:2,count:12,outcome:'Pending',announcedAt:iso(now-180000),strikesAt:iso(now),resolvedAt:null} as Raid
const battle={id:'battle',villageId:'test',phase:'Fighting',sector:'west',tactic:'entry',composition:'swarm',threatBudget:40,readyDefenders:1,availableReserves:2,startsAt:iso(now),lastTickAt:iso(now+1000),endsAt:iso(now+240000),finishedAt:null,revision:1,deferred:false,
 units:Array.from({length:12},(_,i)=>({id:'enemy-'+i,variant:i===0?'strong':'weak',x:2+i%4*3,y:2+Math.floor(i/4)*3,health:20,maximumHealth:20,attack:3,speed:.75,range:1.15,state:'Approaching',targetKind:'centre',targetId:null,diedAt:null,exitedAt:null})),
 fighters:[{citizenId:'me',x:2,y:12,health:26,maximumHealth:40,damageDealt:7,kills:0,wounded:false,guard:true,active:true,mode:'intercept',targetUnitId:null,weapon:'Bow',armor:'LeatherArmor',arrows:8}],events:[]} as RaidBattle
const catalog={tasks:[{id:'guard',effect:'Guard'}]} as unknown as Catalog
const citizen={id:'me',name:'Охранник',taskId:'guard',weapon:'Bow',armor:'LeatherArmor',arrows:8,energy:.5,maximumEnergy:40,energyPerHour:4,serverNow:iso(Date.now()),exhaustedCycles:2,actionEnergyCosts:{work:1.25}} as unknown as Citizen
const village={id:'test',slug:'test',raids:[raid],battles:[battle]} as VillageState
const app=new Application();await app.init({width:Math.min(600,innerWidth),height:260,background:0x68854b,resolution:1,preference:'webgl'})
const layer=new Container();app.stage.addChild(layer);const enemies=new EnemyLayer(layer)
enemies.setRaids([raid]);enemies.setBattles([battle]);enemies.update(.1,now+1000,true)
const orders:unknown[]=[]
api.battleOrder=async(_slug,_id,target,mode)=>{orders.push({target,mode})}
Object.assign(window,{combatStand:{
 update:(next:RaidBattle,time=now+2000)=>{enemies.setBattles([next]);enemies.update(.2,time,true);return {children:layer.children.length,positions:layer.children.map(c=>[c.x,c.y]),rotations:layer.children.map(c=>c.rotation)}},
 terrain:(seed:number,wilds:Parameters<typeof buildLayout>[1])=>Array.from(buildLayout(seed,wilds).terrain,k=>[Terrain.Water,Terrain.Mountain,Terrain.Ravine].includes(k as 2|7|8)?1:k===Terrain.Marsh?2:0),
 geometry:(id:string)=>{
  const internals=enemies as unknown as {liveViews:Map<string,{actor:Container}>;warnings:Map<string,{view:Graphics}>}
  const actor=internals.liveViews.get(id)!.actor,warning=internals.warnings.get(id)?.view
  return {scale:Math.abs(actor.scale.x),warning:warning?{x:warning.x,y:warning.y,width:warning.width}:null}
 },
 initial:structuredClone(battle),orders,mergeBattles,currentEnergy,receiveFighterSample,fighterPositionAt,
 frames:()=>{
  const bars=layer.children.filter(child=>child instanceof Graphics)
  let updates=0
  const clear=Graphics.prototype.clear
  Graphics.prototype.clear=function(){if(bars.includes(this))updates++;return clear.call(this)}
  const before=performance.now()
  for(let i=0;i<300;i++)enemies.update(1/30,now+2000+i*1000/30,false)
  const stableUpdates=updates,elapsed=performance.now()-before
  battle.units[0].health--;enemies.update(1/30,now+12001,false)
  Graphics.prototype.clear=clear
  return {stableUpdates,changedUpdates:updates-stableUpdates,elapsed}
 },
 rendered:(id:string)=>{const actor=(enemies as unknown as {liveViews:Map<string,{actor:Container}>}).liveViews.get(id)!.actor;return {x:actor.x,y:actor.y}},
 pick:(x:number,y:number)=>enemies.pick(x,y),
}})
function Stand(){
 const [unit,setUnit]=useState(battle.units[0]),[selectionToken,setSelectionToken]=useState(0),[shown,setShown]=useState(true)
 const issuedOrder=useRef('')
 return <I18nProvider><div className="game-root" style={{background:'#68854b'}}><div ref={node=>{if(node&&!node.contains(app.canvas))node.append(app.canvas)}}/>
 <div style={{position:"absolute",bottom:0,zIndex:80}}><button onClick={()=>setUnit({...unit,health:0,state:'Dead'})}>Добить на стенде</button>
 <button onClick={()=>setUnit({...unit,health:19})}>Обновить здоровье</button>
 <button onClick={()=>setUnit(battle.units[1])}>Выбрать другого врага</button>
 <button onClick={()=>setSelectionToken(n=>n+1)}>Повторно выбрать врага</button>
 <button onClick={()=>setShown(v=>!v)}>Открыть или закрыть меню</button></div>
 <div id="queue-banner"><RaidBanner village={{...village,raids:[{...raid,id:'queued-third',wave:3,strikesAt:iso(now-60000)},raid]}} citizens={new Map()} catalog={catalog} now={now+1000} pending={false}/></div>
 <div id="energy"><ActionEnergy citizen={citizen} action="work"/></div>
 {shown&&<BattleControls selectionToken={selectionToken} issuedOrder={issuedOrder} village={village} battle={battle} unit={unit} citizen={citizen} catalog={catalog} onClose={()=>{}}/>}
 </div></I18nProvider>
}
createRoot(document.getElementById('app')!).render(<Stand/>);
