import { createRoot } from 'react-dom/client'
import { useState } from 'react'
import { I18nProvider } from '../src/i18n'
import { CivicPanel } from '../src/ui/CivicPanel'
import { BattleControls } from '../src/ui/BattleControls'
import { WalletDialog, WalletHud } from '../src/ui/WalletHud'
import { TopBar } from '../src/ui/Hud'
import type { Catalog, RaidBattle, RaidUnit, VillageState } from '../src/api'
import '../src/index.css'
const unit={id:'target',variant:'weak',state:'Retreating',health:13,maximumHealth:13,exitedAt:null} as RaidUnit
const village={slug:'stand',resources:[{kind:'Wood',amount:10,capacity:30}],tsar:{name:'Царь',injuredUntil:null},structures:[{kind:'Hut',plotId:'15,16'}],storages:[],housing:[],civicOrders:[{id:'order',plotId:'15,16',operation:'upgrade',status:'pending'}],raids:[{id:'raid',enemy:'Zombie'}]} as unknown as VillageState
const battle={id:'raid',fighters:[],finishedAt:null} as unknown as RaidBattle
const hudMode=new URLSearchParams(location.search).has('hud')
const hudData=hudMode?await Promise.all([fetch('/api/catalog').then(r=>r.json()),fetch('/api/villages/demo-bridge').then(r=>r.json())]):null
if(hudData)hudData[1].state.resources.forEach((r:{amount:number},i:number)=>{r.amount=100+i})
function Stand(){const [closed,close]=useState(false),[wallet,setWallet]=useState(false)
 if(hudData)return <div className="game-root"><div className="hud"><TopBar village={hudData[1].state} catalog={hudData[0]} citizen={null} citizens={new Map()} onForecast={async()=>{}} wallet={<WalletHud slug="stand" onOpen={()=>setWallet(true)}/>}/></div></div>
 return <><div style={{padding:8,background:'var(--parchment)',color:'var(--ink)',minHeight:'100dvh'}}><WalletHud slug="stand" onOpen={()=>setWallet(true)}/>{wallet&&<WalletDialog village={village} catalog={{tasks:[],buildings:[]} as unknown as Catalog} citizen={null} accountId="owner" onClose={()=>setWallet(false)} onPrivatePlace={()=>{}}/>}<CivicPanel village={village} tsar ruler actorId="owner" onVillage={()=>{}}/></div>{!closed&&<BattleControls village={village} battle={battle} unit={unit} citizen={null} catalog={{tasks:[]} as unknown as Catalog} tsar accountId="owner" onClose={()=>close(true)}/>}</>
}
createRoot(document.getElementById('root')!).render(<I18nProvider><Stand/></I18nProvider>)
