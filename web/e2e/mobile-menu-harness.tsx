import {useState} from 'react'
import {createRoot} from 'react-dom/client'
import {GameMenu, type MenuProps} from '../src/ui/GameMenu'
import {BottomBar} from '../src/ui/Hud'
import {I18nProvider} from '../src/i18n'
import type {MenuTab} from '../src/ui/menuTabs'
import type {Catalog,VillageState} from '../src/api'
import '../src/index.css'
const village={slug:'test',name:'Дезертовия',resources:[],structures:[],storages:[],sites:[],completedProjects:[],housing:[],civicOrders:[],quests:[],morale:1,technologies:[]} as unknown as VillageState
const isTsar=new URLSearchParams(location.search).get('role')!=='governor'
const catalog={tasks:[],roles:[],buildings:[],rules:{},technologies:[]} as unknown as Catalog
function Stand(){
 const [tab,setTab]=useState<MenuTab>('settings'),[opened,setOpened]=useState(true)
 const props={village,catalog,slug:'test',citizen:null,me:{id:'owner',displayName:'Царь'},tab,onTab:setTab,onClose:()=>setOpened(false),tsar:isTsar,ruler:true,now:Date.now(),pending:false,error:null,feed:[],population:{total:110,online:12},inviteCode:null,onAccountChanged:()=>{},onNewInviteCode:async()=>{}} as unknown as MenuProps
 return <I18nProvider><div className="game-root"><div className="hud"><div className="hud-bottom"><BottomBar {...props} phone structures={[]} hasResult={false} poll={null} onMenu={key=>{setTab(key);setOpened(true)}} onInventory={()=>{}} onShowResult={()=>{}} onCancelTask={()=>{}} drinks={0} onDrink={()=>{}} remedies={0} onHeal={()=>{}}/></div></div></div>{opened&&<GameMenu {...props}/>}</I18nProvider>
}
createRoot(document.getElementById('app')!).render(<Stand/>);
