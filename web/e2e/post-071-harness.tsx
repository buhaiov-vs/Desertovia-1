import { createRoot } from 'react-dom/client'
import { useState } from 'react'
import { I18nProvider } from '../src/i18n'
import { CivicPanel } from '../src/ui/CivicPanel'
import { BattleControls } from '../src/ui/BattleControls'
import { WalletDialog, WalletHud } from '../src/ui/WalletHud'
import type { Catalog, RaidBattle, RaidUnit, VillageState } from '../src/api'
import '../src/index.css'
const unit={id:'target',variant:'weak',state:'Retreating',health:13,maximumHealth:13,exitedAt:null} as RaidUnit
const village={slug:'stand',resources:[{kind:'Wood',amount:10,capacity:30}],tsar:{name:'Царь',injuredUntil:null},structures:[{kind:'Hut',plotId:'15,16'}],storages:[],housing:[],civicOrders:[{id:'order',plotId:'15,16',operation:'upgrade',status:'pending'}],raids:[{id:'raid',enemy:'Zombie'}]} as unknown as VillageState
const battle={id:'raid',fighters:[],finishedAt:null} as unknown as RaidBattle
function Stand(){const [closed,close]=useState(false),[wallet,setWallet]=useState(false)
 return <><div style={{padding:8}}><WalletHud slug="stand" onOpen={()=>setWallet(true)}/>{wallet&&<WalletDialog village={village} catalog={{tasks:[],buildings:[]} as unknown as Catalog} citizen={null} accountId="owner" onClose={()=>setWallet(false)} onPrivatePlace={()=>{}}/>}<CivicPanel village={village} tsar ruler actorId="owner" onVillage={()=>{}}/></div>{!closed&&<BattleControls village={village} battle={battle} unit={unit} citizen={null} catalog={{tasks:[]} as unknown as Catalog} tsar accountId="owner" onClose={()=>close(true)}/>}</>
}
createRoot(document.getElementById('root')!).render(<I18nProvider><Stand/></I18nProvider>)
