import {combatDiagnostics} from '../src/game/combatDiagnostics'
import {receiveFighterSample,fighterPositionAt} from '../src/game/fighterSamples'
import {VillageWorld} from '../src/game/VillageWorld'
import type {Catalog,CitizenSummary,VillageState,RaidBattle} from '../src/api'
import type {Sprite,Graphics,Text} from 'pixi.js'
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
  const continuity={before:fighterPositionAt(moving,1500),after:fighterPositionAt(overlap,1500),end:fighterPositionAt(overlap,2500)}
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
