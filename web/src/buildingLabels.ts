import type {VillageState} from './api'
/** New display numbers are separate from persistent global identifiers; zero hides a unique type. */
export function buildingOrdinal(entity?:{displayNumber?:number;number?:number}|null):number {
  return entity?.displayNumber??entity?.number??0
}
export function buildingSuffix(entity?:{displayNumber?:number;number?:number}|null):string {
  const number=buildingOrdinal(entity)
  return number>0?` №${number}`:''
}

/** Display affordance only; every request is validated again by the server. */
export function canUpgradeBuilding(village:VillageState,plotId:string):boolean {
  const structure=village.structures?.find(s=>s.plotId===plotId)
  const storage=village.storages?.find(s=>s.plotId===plotId)
  if(structure?.ruinedAt||storage?.ruinedAt||(structure?.damage??storage?.damage??0)>=100)return false
  return !!(structure&&['Hut','Longhouse','Castle'].includes(structure.kind)&&(structure.level??1)<3
    ||storage&&['WoodPile','StonePile','DryingRack'].includes(storage.kind))
}
