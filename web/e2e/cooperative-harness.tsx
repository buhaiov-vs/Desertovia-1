import {useState} from 'react'
import {createRoot} from 'react-dom/client'
import {I18nProvider} from '../src/i18n'
import {BuildPanel} from '../src/ui/BuildPanel'
import {CivicPanel} from '../src/ui/CivicPanel'
import {SettingsPanel} from '../src/ui/SettingsPanel'
import {ApiError,api,type VillageState,type Citizen,type Me,type Catalog} from '../src/api'
import '../src/index.css'
const initial={id:'test',slug:'test',name:'Тестовая деревня',structures:[],sites:[],housing:[{plotId:'1,1',beds:12,occupied:11,level:1}],civicOrders:[{id:'order',plotId:'1,1',operation:'upgrade',requestedBy:'governor',partnerAccountId:null,partnerAccepted:false,status:'pending'}]} as unknown as VillageState
const upgradeError=new URLSearchParams(location.search).get('upgradeError')
if(upgradeError)initial.structures=[{kind:upgradeError==='maximum'?'Longhouse':upgradeError==='hospital'?'Hospital':'Hut',level:upgradeError==='maximum'?3:1,plotId:'1,1'}] as VillageState['structures']
const buildMode=new URLSearchParams(location.search).has('build')
const applications={...initial,civicOrders:[{id:'build-order',plotId:'9,11',operation:'build',buildingKind:'Hospital',requestedBy:'governor',partnerAccountId:null,partnerAccepted:false,status:'pending'}]} as VillageState
const fullVillage={...applications,resources:[],completedProjects:[],technologies:[],storages:[],sites:[{kind:'LeanTo',stages:[],progress:0,stage:0,plotId:'1,1'},{kind:'LeanTo',stages:[],progress:0,stage:0,plotId:'5,1'},{kind:'LeanTo',stages:[],progress:0,stage:0,plotId:'9,1'}]} as unknown as VillageState
if(new URLSearchParams(location.search).has('pending'))fullVillage.sites=fullVillage.sites.slice(0,2)
const buildCatalog={rules:{maximumOpenSites:3,repeatCostGrowth:1.2},storages:[],jobs:[],buildings:['Hospital','Castle'].map(kind=>({kind,requires:[],technology:null,shelter:0,stages:[{id:'ground',jobs:[]}]}))} as unknown as Catalog
const citizen={homePlotId:null,spouseName:'Житель с очень длинным именем для проверки переноса на телефоне'} as Citizen
api.civic=async(_slug,plotId,operation)=>({...initial,civicOrders:[{id:'new',plotId,operation,requestedBy:'governor',partnerAccountId:null,partnerAccepted:false,status:'pending'}]})
api.civicDecision=async()=>{if(upgradeError)throw new ApiError(400,'This building cannot be upgraded further.',upgradeError==='maximum'?'building.maxLevel':upgradeError==='hospital'?'building.hospitalUpgrade':'building.upgradeUnsupported',{});if(new URLSearchParams(location.search).has('busy'))throw new ApiError(400,'Finish the existing work first.','building.busy',{});return {...initial,civicOrders:[]}}
function Stand(){
 const [picked,setPicked]=useState('')
 const [village,setVillage]=useState(buildMode?applications:initial),[tsar,setTsar]=useState(false)
 return <I18nProvider><div className="modal-backdrop"><section className="modal" role="dialog" aria-label="Управление городом"><header className="modal-header"><strong>Дезертовия</strong><button onClick={()=>setTsar(!tsar)}>{tsar?'Стать посадником':'Стать царём'}</button></header><div className="modal-body">
 {buildMode&&<><BuildPanel tsar={tsar} catalog={buildCatalog} village={fullVillage} onPlace={b=>setPicked(b.kind)} onWithdraw={()=>{}}/><output>{picked}</output></>}
 <CivicPanel village={village} tsar={tsar} ruler actorId={tsar?'owner':'governor'} citizen={citizen} onVillage={setVillage}/>
 <CivicPanel village={village} tsar={tsar} ruler actorId={tsar?'owner':'governor'} onVillage={setVillage} plotId="1,1"/>
 <SettingsPanel village={village} me={{id:'owner',displayName:'Царь'} as Me} isOwner={false} inviteCode={null} onNewInviteCode={async()=>{}} onAccountChanged={()=>{}}/>
 </div></section></div></I18nProvider>
}
createRoot(document.getElementById('app')!).render(<Stand/>);
