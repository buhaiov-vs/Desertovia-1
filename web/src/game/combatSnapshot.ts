import {wallVisualStage} from './fortificationState'
import type { VillageState } from '../api'

/** Drawn world only. A finished task changes resources and quests without redrawing every tree and building. */
function visualWorld(state:VillageState){
 const project=state.project
 return JSON.stringify({
  id:state.id,
  explored:state.explored,
  trees:state.trees.map(tree=>`${tree.x},${tree.y}`).sort(),
  structures:state.structures.map(s=>[s.kind,s.plotId,s.number,s.displayNumber,!!s.ruinedAt,s.damage,s.level??1]),
  storages:state.storages.map(s=>[s.kind,s.plotId,s.number,s.displayNumber,!!s.ruinedAt,s.damage,s.slots.map(slot=>[slot.kind,Math.ceil(slot.fill*4)])]),
  sites:state.sites.map(s=>[s.kind,s.plotId,s.stage,Math.floor(s.progress*24),s.number,s.displayNumber,!!s.ruinedAt,s.damage??0,s.stages?.[s.stage]?.jobs.map(job=>[job.jobId,job.done,job.required])]),
  fields:state.fields.map(bed=>[bed.plotId,bed.index,bed.crop,bed.growth,bed.wateredUntil]),
  obstacles:state.wilds?.obstacles.map(o=>[o.x,o.y,o.kind,o.cleared]),
  kingdom:state.kingdomArea??null,
  castle:state.castleLevel??0,
  completed:state.completedProjects,
  project:project?[project.definitionId,project.phase,project.surveyDone,Math.floor((project.buildDone/Math.max(project.buildRequired,1))*16),project.materials.map(m=>Math.floor((m.delivered/Math.max(m.required,1))*4))]:null,
  vein:state.goldVeinRemaining,
  bridge:[!!state.bridgeOpened,state.bridgeRuinedAt??null,state.bridgeDamage??0],
  forts:(state.fortifications??[]).map(wall=>[wall.id,wall.x,wall.y,wallVisualStage(wall),wall.level??1,wall.open,wall.gate]),
 })
}

/** SignalR sends a fresh village object after every task. Rebuild scenery only when the picture changed. */
export function sameStaticVillage(before:VillageState|null,next:VillageState){
 if(!before||before===next)return false
 return visualWorld(before)===visualWorld(next)
}
