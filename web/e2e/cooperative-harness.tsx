import {useState} from 'react'
import {createRoot} from 'react-dom/client'
import {I18nProvider} from '../src/i18n'
import {CivicPanel} from '../src/ui/CivicPanel'
import {SettingsPanel} from '../src/ui/SettingsPanel'
import {api,type VillageState,type Citizen,type Me} from '../src/api'
import '../src/index.css'
const initial={id:'test',slug:'test',name:'Тестовая деревня',structures:[],sites:[],housing:[{plotId:'1,1',beds:12,occupied:11,level:1}],civicOrders:[{id:'order',plotId:'1,1',operation:'upgrade',requestedBy:'governor',partnerAccountId:null,partnerAccepted:false,status:'pending'}]} as unknown as VillageState
const citizen={homePlotId:null,spouseName:'Житель с очень длинным именем для проверки переноса на телефоне'} as Citizen
api.civic=async(_slug,plotId,operation)=>({...initial,civicOrders:[{id:'new',plotId,operation,requestedBy:'governor',partnerAccountId:null,partnerAccepted:false,status:'pending'}]})
api.civicDecision=async()=>({...initial,civicOrders:[]})
function Stand(){
 const [village,setVillage]=useState(initial),[tsar,setTsar]=useState(false)
 return <I18nProvider><div className="modal-backdrop"><section className="modal" role="dialog" aria-label="Управление городом"><header className="modal-header"><strong>Дезертовия</strong><button onClick={()=>setTsar(!tsar)}>{tsar?'Стать посадником':'Стать царём'}</button></header><div className="modal-body">
 <CivicPanel village={village} tsar={tsar} ruler actorId={tsar?'owner':'governor'} citizen={citizen} onVillage={setVillage}/>
 <CivicPanel village={village} tsar={tsar} ruler actorId={tsar?'owner':'governor'} onVillage={setVillage} plotId="1,1"/>
 <SettingsPanel village={village} me={{id:'owner',displayName:'Царь'} as Me} isOwner={false} inviteCode={null} onNewInviteCode={async()=>{}} onAccountChanged={()=>{}}/>
 </div></section></div></I18nProvider>
}
createRoot(document.getElementById('app')!).render(<Stand/>);
