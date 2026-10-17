import {createRoot} from 'react-dom/client'
import {useState} from 'react'
import {api,type Catalog,type Citizen,type VillageState} from '../src/api'
import {MealsPanel} from '../src/ui/MealsPanel'
import {TechnologyPanel} from '../src/ui/TechnologyPanel'
import {I18nProvider} from '../src/i18n'
import '../src/index.css'
const catalog=await fetch('/api/catalog').then(r=>r.json()) as Catalog
const data=await fetch('/api/villages/demo-bridge').then(r=>r.json())
const tech=catalog.technologies.find(t=>t.id==='writing')!
let village={...data.state,currentResearch:'writing',resources:[{kind:'ResearchPoints',amount:30}],technologies:[{id:'writing',discovered:false,available:true,done:3,required:8,hands:1,crewNeeded:3,knowledgeWork:0,knowledgeCost:10}]} as VillageState
const initial=Date.now();let now=initial
let citizen={id:'meal-person',name:'Повар',load:[],personalGoods:[],stats:[],skills:[],status:'Active',state:'Idle',mealKind:null,mealUntil:null,mealReadyAt:null} as unknown as Citizen
const calls:string[]=[];let refresh=()=>{}
api.investKnowledge=async()=>{calls.push('knowledge');village={...village,resources:[{kind:'ResearchPoints',amount:20,capacity:null}],technologies:[{...village.technologies[0],done:5,knowledgeWork:2,knowledgeCost:0}]};return village}
api.eatMeal=async(_slug,resource)=>{calls.push(resource);citizen={...citizen,personalGoods:[],mealKind:resource,mealUntil:new Date(now+20*60000).toISOString(),mealReadyAt:new Date(now+30*60000).toISOString()};return citizen}
Object.assign(window,{progressionStand:{calls,stock:()=>{citizen={...citizen,personalGoods:[{kind:'Porridge',amount:1,costBasis:0},{kind:'Stew',amount:1,costBasis:0}]};refresh()},later:()=>{now=initial+21*60000;refresh()}}})
function Harness(){const[revision,setRevision]=useState(0);refresh=()=>setRevision(v=>v+1);return <I18nProvider><div className="modal-backdrop"><div className="modal menu"><header className="modal-header"><h3>Исследования и блюда</h3></header><div className="modal-body" data-revision={revision}>
 <MealsPanel slug="demo-bridge" citizen={citizen} now={now} pending={false} onCitizen={c=>{citizen=c;refresh()}}/>
 <TechnologyPanel catalog={{...catalog,technologies:[tech]}} village={village} citizen={null} ruler disabled={false} onPoll={()=>{}} onResearch={()=>{}} onVillage={v=>{village=v;refresh()}}/>
 </div></div></div></I18nProvider>}
createRoot(document.getElementById('app')!).render(<Harness/> )
