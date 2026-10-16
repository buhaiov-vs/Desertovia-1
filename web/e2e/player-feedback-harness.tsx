import {useState} from 'react'
import {createRoot} from 'react-dom/client'
import {I18nProvider} from '../src/i18n'
import {TargetPopup} from '../src/ui/TargetPopup'
import {TaskList} from '../src/ui/TaskList'
import {InventoryPanel} from '../src/ui/InventoryPanel'
import type {Catalog,Citizen,VillageState} from '../src/api'
import type {WorldTarget} from '../src/game/worldMap'
import '../src/index.css'
const catalog=await fetch('/api/catalog').then(r=>r.json()) as Catalog
const initial=(await fetch('/api/villages/demo-bridge').then(r=>r.json())).state as VillageState
const citizen={id:'resident',accountId:'owner',name:'Хозяин',titleRoleId:'laborer',state:'Idle',stats:[],skills:[],load:[{resource:'Wood',amount:5}],personalGoods:[{kind:'Food',amount:1,costBasis:3}],isHungry:true,renown:0,streak:0,energy:20,maximumEnergy:40,energyPerHour:4,homePlotId:null,taskId:null,spot:null,cycleEndsAt:null,cycleStartedAt:null,injuredUntil:null} as unknown as Citizen
const village={...initial,structures:[{kind:'Hut',plotId:'-4,10',number:12,damage:20,level:1,ownerAccountId:'owner'},{kind:'Library',plotId:'8,8',number:15,damage:0,level:1}],storages:[{kind:'Heap',plotId:'1,1',number:13,damage:20,goods:[],slots:[],resourceLimit:10,totalLimit:null}],sites:[],housing:[{plotId:'-4,10',number:12,ownerAccountId:'owner',ownerName:'Хозяин',builtAt:'2026-10-16T12:00:00Z',beds:4,occupied:0,level:1}],civicOrders:[],kingdomArea:{x:0,y:0,width:28,height:26},fields:[],project:null} as VillageState
const targets:Record<string,WorldTarget>={home:{area:null,kind:'building',building:'Hut',plotId:'-4,10',storage:false},storage:{area:null,kind:'building',building:'Heap',plotId:'1,1',storage:true},public:{area:null,kind:'stone',spot:{x:5,y:5}},personal:{area:null,kind:'stone',spot:{x:-5,y:5}}}
function Stand(){
 const [selected,setSelected]=useState('home'),[call,setCall]=useState('')
 return <I18nProvider><nav style={{display:'flex',gap:4,flexWrap:'wrap'}}>{['home','storage','public','personal','stats','inventory','nofood'].map(key=><button key={key} onClick={()=>setSelected(key)}>{key}</button>)}</nav><output id="call">{call}</output>
 {selected==='stats'?<TaskList tasks={catalog.tasks.filter(t=>t.id==='read-books')} catalog={catalog} citizen={citizen} built={new Set(['Library'])} discovered={new Set(catalog.technologies.map(t=>t.id))} wildlife={[]} goldVein={100} fields={[]} resources={village.resources} disabled={false} onStart={()=>{}}/>:
 selected==='inventory'||selected==='nofood'?<InventoryPanel village={village} catalog={catalog} citizen={selected==='nofood'?{...citizen,personalGoods:[]}:citizen} now={Date.now()} pending={false} error={null} onCarry={()=>{}} onDrop={()=>{}} onEat={()=>setCall('eat')} onTake={()=>{}} onClose={()=>setSelected('home')}/>:
 <TargetPopup target={targets[selected]} screen={{x:30,y:100}} phone={innerWidth<720} village={village} catalog={catalog} citizen={citizen} now={Date.now()} pending={false} error={null} ruler={false} onStart={(task,spot,plot)=>setCall(JSON.stringify({task:task.id,spot,plot}))} onStartJob={()=>{}} onCivic={async(plot,operation)=>setCall(JSON.stringify({plot,operation}))} onTake={()=>{}} onClose={()=>setSelected('stats')}/>}
 </I18nProvider>
}
createRoot(document.getElementById('app')!).render(<Stand/>);
