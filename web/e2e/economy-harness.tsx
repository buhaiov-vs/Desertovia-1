import { canBuildAt } from '../src/game/worldMap'
import { createRoot } from 'react-dom/client'
import { api, type Catalog, type Citizen, type EconomyRequest, type EconomyView, type VillageState } from '../src/api'
import { I18nProvider } from '../src/i18n'
import { EconomyPanel } from '../src/ui/EconomyPanel'
import { ActionEnergy } from '../src/ui/ActionEnergy'
import '../src/index.css'

declare global { interface Window { economyCalls: EconomyRequest[]; failEconomyOnce: boolean; kingdomCanBuild: (personal:boolean,x:number,y:number)=>boolean } }
window.economyCalls=[];window.failEconomyOnce=false
const view: EconomyView={energyDrinkReadyAt:null,coins:1000,testCandles:0,earnedToday:100,profitToday:0,taxPreview:0,taxEnabled:false,exchangeRate:100,dailyExchangeLimit:5,exchangedToday:0,kingdomCandleRoom:50,dayResetsAt:new Date(Date.now()+3600000).toISOString(),treasury:0,inventory:[],lots:[],journal:[],nextJournalBefore:null,startedAt:new Date().toISOString(),workPay:10,constructionPay:20,residents:[{accountId:'me',name:'Игрок'},{accountId:'other',name:'Очень длинное имя другого игрока'}],privateStorages:[],stockPrices:{Wood:2,Stone:3,Planks:8},privateProperty:[]}
api.economy=async()=>structuredClone(view)
api.economyAction=async(_slug,request)=>{
  window.economyCalls.push(structuredClone(request))
  if(window.failEconomyOnce){window.failEconomyOnce=false;throw new Error('Обрыв связи. Повтори запрос.')}
  if(request.action==='buy-stock'){const amount=request.amount??1;view.coins-=amount*2;view.treasury+=amount*2;view.inventory=[{kind:'Wood',amount,costBasis:amount*2}]}
  return structuredClone(view)
}
const village={id:'00000000-0000-0000-0000-000000000001',slug:'test',structures:[],storages:[],sites:[],bridgeOpened:false,completedProjects:[],kingdomArea:{x:-4,y:-3,width:32,height:32},resources:[{kind:'Wood',amount:10,capacity:30},{kind:'Stone',amount:8,capacity:30},{kind:'Planks',amount:2,capacity:30}]} as unknown as VillageState
const catalog={buildings:[],buildArea:{x:0,y:0,width:28,height:26},reservedAreas:[]} as unknown as Catalog
window.kingdomCanBuild=(personal,x,y)=>canBuildAt(catalog,village,x,y,personal)
const citizen={energy:0,exhaustedCycles:2,actionEnergyCosts:{'test-work':2},accountId:'me',name:'Игрок',weapon:null,armor:null} as unknown as Citizen
createRoot(document.getElementById('app')!).render(<I18nProvider><div className="modal-backdrop"><section className="modal menu"><div className="modal-body"><div id="faint-warning"><ActionEnergy citizen={citizen} action="test-work"/></div><EconomyPanel village={village} catalog={catalog} citizen={citizen} accountId="me"/></div></section></div></I18nProvider>)
