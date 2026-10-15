import { buildLayout, Terrain } from '../src/game/worldMap'
// Production render and controls, isolated from real players, payments and director calls.
import { useState } from 'react'
import { createRoot } from 'react-dom/client'
import { Application, Container } from 'pixi.js'
import { api, mergeBattles, type Catalog, type Citizen, type Raid, type RaidBattle, type VillageState } from '../src/api'
import { currentEnergy } from '../src/energy'
import { EnemyLayer } from '../src/game/enemies'
import { BattleControls } from '../src/ui/BattleControls'
import { ActionEnergy } from '../src/ui/ActionEnergy'
import { I18nProvider } from '../src/i18n'
import '../src/index.css'
const now=Date.parse('2026-10-15T12:00:00Z')
const iso=(ms:number)=>new Date(ms).toISOString()
const raid={id:'battle',enemy:'Skeleton',engineVersion:2,count:12,outcome:'Pending',announcedAt:iso(now-180000),strikesAt:iso(now),resolvedAt:null} as Raid
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
 initial:structuredClone(battle),orders,mergeBattles,currentEnergy,
 pick:(x:number,y:number)=>enemies.pick(x,y),
}})
function Stand(){
 const [unit,setUnit]=useState(battle.units[0])
 return <I18nProvider><div className="game-root" style={{background:'#68854b'}}><div ref={node=>{if(node&&!node.contains(app.canvas))node.append(app.canvas)}}/>
 <button onClick={()=>setUnit({...unit,health:0,state:'Dead'})}>Добить на стенде</button>
 <div id="energy"><ActionEnergy citizen={citizen} action="work"/></div>
 <BattleControls village={village} battle={battle} unit={unit} citizen={citizen} catalog={catalog} onClose={()=>{}}/>
 </div></I18nProvider>
}
createRoot(document.getElementById('app')!).render(<Stand/>);
