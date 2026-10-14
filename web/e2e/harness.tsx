// Test-only entry point; excluded from normal production builds.
import { Application, Container } from 'pixi.js'
import { createRoot } from 'react-dom/client'
import { useState } from 'react'
import { BuildingLighting } from '../src/game/BuildingLighting'
import { TileSet } from '../src/game/sheets'
import registrations from '../src/assets/night/registration.json'
import { StorageCard } from '../src/ui/StorageCard'
import { BottomBar } from '../src/ui/Hud'
import { ChatPanel } from '../src/ui/ChatPanel'
import { I18nProvider } from '../src/i18n'
import type { Catalog, Citizen } from '../src/api'
import '../src/index.css'

const app=new Application(); await app.init({width:650,height:700,backgroundAlpha:0,resolution:1,preference:'webgl'})
document.getElementById('lighting')!.appendChild(app.canvas)
const tiles=await TileSet.load(); const root=new Container();app.stage.addChild(root)
const views=Object.keys(registrations).map((kind,i)=>{
 const view=new BuildingLighting(tiles,kind,(i%4)*7,Math.floor(i/4)*6);root.addChild(view,view.light);return view
})
Object.assign(window,{lightingTest:{
 measure:()=>views.map(v=>{const b=v.getLocalBounds();return {x:v.x,y:v.y,bounds:[b.minX,b.minY,b.maxX,b.maxY]}}),
 render:(amount:number)=>{views.forEach(v=>v.setNight(amount));app.renderer.render(app.stage)},
 emissionCheck:()=>views.map(view=>{
  view.setNight(0);const day=app.renderer.extract.pixels({target:view.light,resolution:2})
  view.setNight(1);const night=app.renderer.extract.pixels({target:view.light,resolution:2})
  return {day:day.pixels.reduce((sum,value,i)=>sum+(i%4===3?value:0),0),night:night.pixels.reduce((sum,value,i)=>sum+(i%4===3?value:0),0)}
 }),
 alphaCheck:()=>views.map(view=>{
  const samples=[0,0.5,1].map(level=>{view.setNight(level);return app.renderer.extract.pixels({target:view,resolution:3})})
  const [day,middle,night]=samples;let common=0,transparent=0
  for(let i=3;i<day.pixels.length;i+=4) if(day.pixels[i]>250&&night.pixels[i]>250){common++;if(middle.pixels[i]<250)transparent++}
  return {common,transparent,dimensions:samples.map(s=>[s.width,s.height])}
 }),
}})
const catalog=await fetch('/api/catalog').then(r=>r.json()) as Catalog
const result=await fetch('/api/villages/demo-bridge').then(r=>r.json())
// Get a realistic DTO for UI checks without changing a player or world.
const originalCitizen=await fetch('/api/villages/demo-bridge/me').then(r=>r.json())
const citizen={...originalCitizen, load:[], state:'AwaitingRenewal', taskId:'hunt-hare',
 cycleStartedAt:new Date(Date.now()-60000).toISOString(),cycleEndsAt:new Date(Date.now()-1000).toISOString(),
 graceEndsAt:new Date(Date.now()+55000).toISOString()} as Citizen
const uiNow=Date.now()
function Controls(){
 const [stock,setStock]=useState(7),[taken,setTaken]=useState(0)
 return <I18nProvider>
  <div className="hud" style={{position:'relative',height:'auto'}}>
   <BottomBar citizen={citizen} catalog={catalog} structures={result.state.structures} now={uiNow}
    phone tsar={false} ruler hasResult pending={false} onMenu={()=>{}} onInventory={()=>{}} onShowResult={()=>{}}
    poll={{label:'Начать голосование',onClick:()=>{}}} onCancelTask={()=>{}} drinks={0} onDrink={()=>{}} remedies={0} onHeal={()=>{}} />
  </div>
  <StorageCard storage={{kind:'Heap',plotId:'0,0',damage:0,slots:[],goods:[{resource:'Food',amount:stock}]}} catalog={catalog}
   take={{citizen,disabled:false,onTake:(_,amount)=>setTaken(amount)}}/>
  <button id="stock-update" onClick={()=>setStock(stock+1)}>Refresh stock</button><output id="taken">{taken}</output>
  <ChatPanel feed={[]} messages={[{id:'npc',authorName:'Отец Срамослав',citizenId:null,isTsar:false,isChaplain:true,text:'Держимся вместе, чада!',sentAt:new Date(uiNow+1000).toISOString()}]}
   member tsar ownCitizenId={null} serverOffsetMs={0} phone={false} onSend={async()=>{}} />
 </I18nProvider>
}
createRoot(document.getElementById('app')!).render(<Controls/>);
