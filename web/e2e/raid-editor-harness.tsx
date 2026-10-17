import {createRoot} from 'react-dom/client'
import {useState} from 'react'
import {api,type RaidSettings,type RaidEditorView,type VillageState} from '../src/api'
import {RaidEditor} from '../src/ui/RaidEditor'
import {I18nProvider} from '../src/i18n'
import '../src/index.css'
const launches:{requestId:string;settings:RaidSettings}[]=[],previews:RaidSettings[]=[]
let saved:RaidEditorView={presets:[],coefficient:1.1,reason:'Два лёгких отражения подряд'}
api.raidEditor=async()=>structuredClone(saved)
api.saveRaidPreset=async(_slug,id,name,settings)=>{
 const p={id:id??`saved-${saved.presets.length+1}`,name,settings:structuredClone(settings),updatedAt:new Date().toISOString()}
 saved={...saved,presets:[...saved.presets.filter(s=>s.id!==p.id),p]};return structuredClone(saved)
}
api.previewRaid=async(_slug,settings)=>{
 previews.push(structuredClone(settings))
 const count=settings.mode==='manual'?settings.count:Math.min(settings.count,3)
 return {recommendedBudget:30,actualCost:count*20,count,totalHealth:count*200,totalAttack:count*10,risk:'dangerous',
  units:Array.from({length:count},()=>({enemy:settings.enemy,variant:settings.preset==='boss'?'boss':'weak',health:200,attack:10,speed:.6,cost:20}))}
}
let current={slug:'test',raids:[]} as unknown as VillageState
api.launchRaid=async(_slug,requestId,settings)=>{
 launches.push({requestId,settings:structuredClone(settings)})
 current={...current,raids:[{outcome:'Pending'} as VillageState['raids'][number]]};return current
}
Object.assign(window,{raidEditorStand:{launches,previews}})
function Harness(){const[v,setV]=useState(current);return <I18nProvider><div className="modal-backdrop"><div className="modal menu"><header className="modal-header"><h3>Набеги</h3></header><div className="modal-body"><RaidEditor village={v} pending={false} onVillage={setV}/></div></div></div></I18nProvider>}
createRoot(document.getElementById('app')!).render(<Harness/> )
