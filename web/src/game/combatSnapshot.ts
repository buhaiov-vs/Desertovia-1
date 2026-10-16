import type { VillageState } from '../api'
/** SignalR combat packets share all other state. A new wall DTO alone need not rebuild the world. */
export function sameStaticVillage(before:VillageState|null,next:VillageState){
 if(!before||before===next)return false
 for(const key of Object.keys(next) as (keyof VillageState)[]){
  if(key==='battles'||key==='fortifications')continue
  if(before[key]!==next[key])return false
 }
 // Partial damage has no visual/collision change; a breach, gate or layout change still rebuilds.
 const a=before.fortifications??[],b=next.fortifications??[]
 return a.length===b.length&&a.every((wall,i)=>wall.id===b[i].id&&wall.x===b[i].x&&wall.y===b[i].y&&(wall.damage>=100)===(b[i].damage>=100)&&wall.open===b[i].open&&wall.gate===b[i].gate)
}
