import { useState } from 'react'
import { createRoot } from 'react-dom/client'
import { api, type Catalog, type Citizen, type RaidBattle, type VillageState } from '../src/api'
import { TopBar, BottomBar } from '../src/ui/Hud'
import { WalletHud } from '../src/ui/WalletHud'
import { PersonalBattlePanel } from '../src/ui/PersonalBattlePanel'
import { I18nProvider } from '../src/i18n'
import '../src/index.css'
api.economy=async()=>({coins:0,testCandles:0} as Awaited<ReturnType<typeof api.economy>>)
const now=Date.now()
const citizen={id:'me',name:'My hero',energy:24,maximumEnergy:40,energyPerHour:0,serverNow:new Date(now).toISOString(),state:'Idle',load:[],personalGoods:[]} as unknown as Citizen
const catalog={tasks:[],roles:[],buildings:[],rules:{moraleMinimum:.5,moraleMaximum:1.5},technologies:[]} as unknown as Catalog
const fighter={citizenId:'me',x:1,y:1,health:26,maximumHealth:40,damageDealt:0,kills:0,wounded:false,guard:true,active:true,mode:'intercept',targetUnitId:null} as RaidBattle['fighters'][number]
const battle={id:'battle',phase:'Fighting',fighters:[fighter],units:[],finishedAt:null} as unknown as RaidBattle
const village={id:'test',slug:'test',morale:1,raids:[],battles:[battle],resources:[],structures:[],storages:[],sites:[],completedProjects:[],technologies:[],fields:[],wildlife:[],trees:[],quests:[],project:null,era:'Camp',tsar:{},weather:{kind:'Clear',periodStartedAt:new Date(now).toISOString(),periodEndsAt:new Date(now+600000).toISOString(),forecast:null,night:false}} as unknown as VillageState
const orders:{mode:string;requestId?:string}[]=[]
function Stand(){
 const [remedies,setRemedies]=useState(0),[active,setActive]=useState(true),[current,setCurrent]=useState(fighter),[error,setError]=useState('')
 api.battleOrder=async(_slug,_id,_target,mode,_destination,requestId)=>{orders.push({mode,requestId});if(mode==='heal'){setCurrent({...current,health:34,healReadyAt:new Date(now+60000).toISOString()});setRemedies(n=>n-1)}else setActive(false)}
 Object.assign(window,{ownerStand:{orders}})
 const state={...village,battles:[{...battle,fighters:[current]}]}
 return <I18nProvider><div className="game-root" style={{background:'#7a9b58'}}><div className="hud"><TopBar village={state} catalog={catalog} citizen={citizen} accountId="account" citizens={new Map()} onForecast={async()=>{}} wallet={<WalletHud slug="test" onOpen={()=>{}}/>}/><BottomBar catalog={catalog} citizen={null} structures={[]} now={now} phone={innerWidth<720} tsar ruler hasResult={false} pending={false} poll={{label:'Начать голосование',onClick:()=>{}}} onMenu={()=>{}} onInventory={()=>{}} onShowResult={()=>{}} onCancelTask={()=>{}} drinks={0} onDrink={()=>{}} remedies={0} onHeal={()=>{}}/></div>{active&&<PersonalBattlePanel slug="test" battle={battle} fighter={current} remedies={remedies} now={now} onError={setError}/>}
 <div style={{position:'absolute',left:0,bottom:160}}><button onClick={()=>setRemedies(1)}>Дать лекарство на стенде</button><button onClick={()=>setActive(false)}>Завершить бой на стенде</button></div><p>{error}</p></div></I18nProvider>
}
createRoot(document.getElementById('app')!).render(<Stand/>);
