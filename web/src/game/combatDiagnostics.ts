import { GAME_VERSION } from '../version'
/** Local bounded timing measurements only. No chat, names, account ids, coordinates or tokens. */
type WindowSample={at:number;battleCount:number;frames:number;frameP95Ms:number;frameMaxMs:number;slowFrames:number;updateP95Ms:number;worldP95Ms:number;packetGapMaxMs:number;packets:number;reconnects:number;hidden:boolean;obscured:boolean;targetFps:number;staticUpdates:number}
const limit=2048, history:WindowSample[]=[]
let active=0,started=0,lastPacket=0,packetGap=0,packets=0,reconnects=0,staticUpdates=0,obscured=false,targetFps=60
let frames:number[]=[],updates:number[]=[],world:number[]=[]
const p95=(values:number[])=>values.length?[...values].sort((a,b)=>a-b)[Math.min(values.length-1,Math.floor(values.length*.95))]:0
const keep=(list:number[],n:number)=>{if(Number.isFinite(n)&&n>=0&&list.length<limit)list.push(n)}
function flush(){
 if(!started||(!frames.length&&!packets&&!world.length))return
 history.push({at:Date.now(),battleCount:active,frames:frames.length,frameP95Ms:p95(frames),frameMaxMs:Math.max(0,...frames),slowFrames:frames.filter(n=>n>50).length,updateP95Ms:p95(updates),worldP95Ms:p95(world),packetGapMaxMs:packetGap,packets,reconnects,hidden:document.hidden,obscured,targetFps,staticUpdates})
 if(history.length>120)history.shift()
 frames=[];updates=[];world=[];packetGap=0;packets=0;reconnects=0;staticUpdates=0;started=performance.now()
}
export const combatDiagnostics={
 battles(battles:{finishedAt:string|null;phase:string}[]){
  const next=battles.filter(b=>!b.finishedAt&&!['Warning','Cancelled'].includes(b.phase)).length
  if(active&&!next)flush()
  if(!active&&next){started=performance.now();lastPacket=0}
  active=next
 },
 frame(deltaMs:number,updateMs:number){if(!active||document.hidden)return;keep(frames,deltaMs);keep(updates,updateMs);if(performance.now()-started>=15000)flush()},
 world(ms:number,staticUpdate:boolean){if(!active)return;keep(world,ms);if(staticUpdate)staticUpdates++},
 packet(){if(!active)return;const now=performance.now();if(lastPacket)packetGap=Math.max(packetGap,now-lastPacket);lastPacket=now;packets++},
 reconnect(){if(active)reconnects++},
 stop(){if(active)flush();active=0;started=0;lastPacket=0},
 context(nextObscured:boolean,fps:number){obscured=nextObscured;targetFps=fps},
 export(){flush();return {format:1,version:GAME_VERSION,capturedAt:new Date().toISOString(),device:{pixelRatio:devicePixelRatio,coarsePointer:matchMedia('(pointer:coarse)').matches},samples:[...history]}},
 download(server:unknown=null){const content=JSON.stringify({...this.export(),server},null,2),url=URL.createObjectURL(new Blob([content],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='desertovia-combat-diagnostics.json';a.click();window.setTimeout(()=>URL.revokeObjectURL(url),1000)},
}
