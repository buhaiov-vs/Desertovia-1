import type { VillageState } from '../api'
import {buildingOrdinal} from '../buildingLabels'

/** Coordinates stay internal keys. The visible sequence belongs to its building family. */
export function buildingNumber(village: VillageState, plotId: string): string {
  const entity = [...(village.structures??[]), ...(village.storages??[]), ...(village.sites??[])].find(b => b.plotId === plotId)
  if(!entity)return '…'
  const number=buildingOrdinal(entity)
  return number>0?`№${number}`:''
}
