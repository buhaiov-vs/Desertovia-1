import {VillageWorld} from '../src/game/VillageWorld'
import type {Catalog,CitizenSummary,VillageState} from '../src/api'
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
 inspect:()=>patients.map(c=>{const w=walkers.get(c.id)!;return {id:c.id,rotation:w.sprite.rotation,bed:w.bed.visible,reason:w.subtitle.text,name:w.label.text,nameBounds:w.label.getBounds().rectangle,statusBounds:w.subtitle.getBounds().rectangle,bodyBounds:w.sprite.getBounds().rectangle}}),
 discharge:()=>world.setCitizens(patients.map(c=>({...c,recovery:null,injuredUntil:null})),null),
}})
