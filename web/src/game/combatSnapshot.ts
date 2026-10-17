import {wallVisualStage} from './fortificationState'
import type { VillageState } from '../api'
/** SignalR combat packets share all other state. A new wall DTO alone need not rebuild the world. */
export function sameStaticVillage(before:VillageState|null,next:VillageState){
 if(!before||before===next)return false
 for(const key of Object.keys(next) as (keyof VillageState)[]){
  if(key==='battles'||key==='fortifications')continue
  if(before[key]!==next[key])return false
 }
 // Only material/damage-stage/gate/layout changes rebuild, never every damage point.
 const a=before.fortifications??[],b=next.fortifications??[]
 return a.length===b.length&&a.every((wall,i)=>wall.id===b[i].id&&wall.x===b[i].x&&wall.y===b[i].y&&wallVisualStage(wall)===wallVisualStage(b[i])&&(wall.level??1)===(b[i].level??1)&&wall.open===b[i].open&&wall.gate===b[i].gate)
}
