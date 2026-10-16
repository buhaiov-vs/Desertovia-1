import {TILE} from '../src/game/sheets'
import {Terrain,MAP_LEFT,MAP_TOP,MAP_WIDTH,BRIDGE_DECK} from '../src/game/worldMap'
import {combatDiagnostics} from '../src/game/combatDiagnostics'
import {receiveFighterSample,fighterPositionAt} from '../src/game/fighterSamples'
import {VillageWorld} from '../src/game/VillageWorld'
import type {Catalog,CitizenSummary,VillageState,RaidBattle} from '../src/api'
import type {Sprite,Graphics,Text,Ticker} from 'pixi.js'
const catalog=await fetch('/api/catalog').then(r=>r.json()) as Catalog
const response=await fetch('/api/villages/demo-bridge').then(r=>r.json())
const state=response.state as VillageState
state.structures=[{kind:'Hospital',plotId:'8,8',damage:0,level:1}]
state.storages=[];state.sites=[];state.fields=[];state.project=null;state.trees=[];state.wildlife=[];state.raids=[];state.wilds=null
const now=new Date().toISOString(),until=new Date(Date.now()+3600000).toISOString()
const base:CitizenSummary={id:'duel-patient',name:'Пациент дуэли',subtitle:null,isGovernor:false,
 appearance:{gender:'Male',style:0,skinTone:0,hairColor:0,outfitColor:0},titleRoleId:'laborer',taskId:null,workplace:null,siteId:null,spot:null,idleSpot:null,
 state:'Paused',cycleStartedAt:null,cycleEndsAt:null,carrying:[],injuredUntil:null,weapon:null,
 recovery:{cause:'duel',episodeId:'test',admittedAt:now,until,checkedAt:now,available:true,hospitalPercent:0,skillPercent:0}}
const patients=[base,{...base,id:'battle-patient',name:'Раненый после боя',state:'Idle',injuredUntil:until,hospitalizedAt:now,recovery:null},{...base,id:'third-patient',name:'Очень_длинный_ник_пациента'},{...base,id:'fourth-patient',name:'Горожанин_на_лечении'}] as CitizenSummary[]
const world=await VillageWorld.create(document.getElementById('ward')!,{minimap:false})
world.setCatalog(catalog);world.setVillage(state);world.setNightOverride(false);world.setCitizens(patients,null);world.centerOn(9.5,13)
const walkers=(world as unknown as {walkers:Map<string,{sprite:Sprite;bed:Graphics;subtitle:Text;label:Text}>}).walkers
Object.assign(window,{patientTest:{
 workPathCheck:()=>{
  const internal=world as unknown as {app:{ticker:{stop:()=>void}};terrain:Uint8Array;update:(tick:Ticker)=>void;walkers:Map<string,{x:number;y:number;way:{x:number;y:number}[];targetX:number;targetY:number;hunt:unknown}>}
  internal.app.ticker.stop();world.setVillage({...state,bridgeOpened:true,battles:[]})
  const c={...base,id:'river-worker',name:'Worker',state:'Working',recovery:null,injuredUntil:null,taskId:'quarry-stone',workplace:'stones',spot:{x:38.5,y:6.5},idleSpot:null,cycleStartedAt:now,cycleEndsAt:until} as CitizenSummary
  world.setCitizens([c],c.id)
  const walker=internal.walkers.get(c.id)!;walker.x=25.5*TILE;walker.y=6.5*TILE;walker.targetX=walker.x;walker.targetY=walker.y;walker.way=[]
  world.setCitizens([c],c.id)
  const turns=walker.way.map(p=>({x:p.x/TILE,y:p.y/TILE})),route:{x:number;y:number}[]=[]
  let previous={x:25.5,y:6.5}
  for(const end of turns){const steps=Math.ceil(Math.hypot(end.x-previous.x,end.y-previous.y)/.1);for(let i=1;i<=steps;i++)route.push({x:previous.x+(end.x-previous.x)*i/steps,y:previous.y+(end.y-previous.y)*i/steps});previous=end}
  let unsafe=0
  for(const point of route){const x=Math.floor(point.x),y=Math.floor(point.y);const kind=internal.terrain[(y-MAP_TOP)*MAP_WIDTH+x-MAP_LEFT];const deck=x>=BRIDGE_DECK.x&&x<BRIDGE_DECK.x+BRIDGE_DECK.width&&y>=BRIDGE_DECK.y&&y<BRIDGE_DECK.y+BRIDGE_DECK.height;if(kind===Terrain.Water&&!deck)unsafe++}
  return {points:turns.length,samples:route.length,unsafe,usesBridge:route.some(p=>p.x>=BRIDGE_DECK.x&&p.x<BRIDGE_DECK.x+BRIDGE_DECK.width&&p.y>=BRIDGE_DECK.y&&p.y<BRIDGE_DECK.y+BRIDGE_DECK.height)}
 },
 motionCheck:()=>{
  const internal=world as unknown as {app:{ticker:{stop:()=>void}};update:(tick:Ticker)=>void;walkers:Map<string,{x:number;sprite:Sprite}>;enemies:{liveViews:Map<string,{bar:Graphics}>}}
  internal.app.ticker.stop()
  const c={...base,id:'moving-fighter',name:'Moving',state:'Idle',recovery:null,idleSpot:{x:12.5,y:13.5}} as CitizenSummary
  world.setCitizens([c],c.id)
  const nativeNow=performance.now.bind(performance);let clock=0
  Object.defineProperty(performance,'now',{configurable:true,value:()=>clock})
  let snapshot={...state,battles:[{id:'motion',phase:'Fighting',finishedAt:null,revision:1,units:[],fighters:[{citizenId:c.id,x:12.5,y:13.5,health:40,maximumHealth:40,active:true,mode:'intercept',isTsar:false}]} as unknown as RaidBattle]}
  const arrivals=[0,1000,2250,3200,4700,5700,6700],positions:{at:number;x:number;rawX:number}[]=[]
  let packet=0,maxHealthBarDrift=0
  try{
   for(let frame=0;frame<=420;frame++){
    clock=frame*1000/60
    if(packet<arrivals.length&&clock>=arrivals[packet]){
     snapshot={...snapshot,battles:[{...snapshot.battles[0],revision:packet+1,fighters:snapshot.battles[0].fighters.map(f=>({...f,x:12.5+packet*.65}))}]}
     world.setVillage(snapshot);packet++
    }
    internal.update({deltaMS:1000/60,elapsedMS:1000/60} as Ticker)
    const w=internal.walkers.get(c.id)!,bar=internal.enemies.liveViews.get('guard:'+c.id)?.bar;if(bar)maxHealthBarDrift=Math.max(maxHealthBarDrift,Math.abs(bar.x-w.sprite.x));positions.push({at:clock,x:w.sprite.x,rawX:w.x})
   }
  }finally{Object.defineProperty(performance,'now',{configurable:true,value:nativeNow})}
  const steady=positions.filter(p=>p.at>=1100&&p.at<1900)
  const late=positions.filter(p=>p.at>=4200&&p.at<4700)
  return {steadyFrames:steady.length,distinctSteady:new Set(steady.map(p=>p.x)).size,distinctRaw:new Set(steady.map(p=>p.rawX)).size,lateFrames:late.length,distinctLate:new Set(late.map(p=>p.x)).size,maxHealthBarDrift}
 },
 wallCheck:()=>{
  const wall={id:'west-wall',x:-1,y:13,damage:0,gate:false,open:false}
  let snapshot={...state,fortifications:[wall]}
  world.setVillage(snapshot)
  let updates=0
  const original=world.setCitizens.bind(world);world.setCitizens=(...args)=>{updates++;original(...args)}
  for(let damage=1;damage<100;damage++){snapshot={...snapshot,fortifications:[{...wall,damage}]};world.setVillage(snapshot)}
  const damageUpdates=updates
  world.setVillage({...snapshot,fortifications:[{...wall,damage:100}]})
  const breachUpdates=updates-damageUpdates
  world.setVillage({...snapshot,fortifications:[{...wall,gate:true,open:true}]})
  return {damageUpdates,breachUpdates,gateUpdates:updates-damageUpdates-breachUpdates}
 },
 combatCheck:()=>{
  const c={...base,id:'fighter',name:'Fighter',state:'Idle',recovery:null,idleSpot:{x:12.5,y:13.5}} as CitizenSummary
  world.setCitizens([c],c.id)
  let battle={id:'measured-battle',phase:'Fighting',finishedAt:null,revision:1,units:[],fighters:[{citizenId:c.id,x:12.5,y:13.5,health:40,maximumHealth:40,active:true,mode:'hold',isTsar:false}]} as unknown as RaidBattle
  let snapshot={...state,battles:[battle]}
  world.setVillage(snapshot)
  let citizenUpdates=0
  const original=world.setCitizens.bind(world)
  world.setCitizens=(...args)=>{citizenUpdates++;original(...args)}
  for(let i=0;i<100;i++){
   battle={...battle,revision:battle.revision+1,fighters:battle.fighters.map(f=>({...f,x:f.x+.001}))}
   snapshot={...snapshot,battles:[battle],fortifications:(snapshot.fortifications??[]).map(w=>({...w}))}
   combatDiagnostics.packet();world.setVillage(snapshot)
  }
  const steady=citizenUpdates
  world.setVillage({...snapshot,structures:[]})
  const topology=citizenUpdates
  world.setVillage({...snapshot,battles:[{...battle,revision:battle.revision+1,fighters:[]}]})
  const membership=citizenUpdates
  const first=receiveFighterSample(undefined,'b',1,10,10,0)
  const moving=receiveFighterSample(first,'b',2,11,10,1000)
  const overlap=receiveFighterSample(moving,'b',3,12,10,1500)
  const continuity={before:fighterPositionAt(moving,1500),after:fighterPositionAt(overlap,1500),end:fighterPositionAt(overlap,3500)}
  return {steady,topology,membership,continuity,diagnostics:combatDiagnostics.export()}
 },
 inspect:()=>patients.map(c=>{const w=walkers.get(c.id)!;return {id:c.id,rotation:w.sprite.rotation,bed:w.bed.visible,reason:w.subtitle.text,name:w.label.text,nameBounds:w.label.getBounds().rectangle,statusBounds:w.subtitle.getBounds().rectangle,bodyBounds:w.sprite.getBounds().rectangle}}),
 visibility:()=>patients.map(c=>{const w=walkers.get(c.id)!;return {body:w.sprite.visible,bed:w.bed.visible,name:w.label.visible,status:w.subtitle.visible}}),
 withoutHospital:()=>{state.structures=[];world.setVillage(state);world.centerOn(17,23)},
 damage:()=>{
  const internal=world as unknown as {buildingViews:import('pixi.js').Container[];litBuildings:Set<unknown>;damageViews:Map<string,unknown>}
  const originals=[...internal.buildingViews],lights=internal.litBuildings.size
  const start=performance.now()
  for(let i=1;i<=20;i++){state.structures[0].damage=i;world.setVillage(state)}
  return {retained:originals.every(view=>internal.buildingViews.includes(view)&&!view.destroyed),lightsBefore:lights,lightsAfter:internal.litBuildings.size,overlays:internal.damageViews.size,elapsed:performance.now()-start}
 },
 discharge:()=>world.setCitizens(patients.map(c=>({...c,recovery:null,injuredUntil:null})),null),
}})
