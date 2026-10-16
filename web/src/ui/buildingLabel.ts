import type { VillageState } from '../api'

/** Coordinates remain internal keys. The server allocates persistent village-wide numbers. */
export function buildingNumber(village: VillageState, plotId: string): string {
  const entity = [...(village.structures??[]), ...(village.storages??[]), ...(village.sites??[])].find(b => b.plotId === plotId && b.number)
  return entity?.number ? `№${entity.number}` : '…'
}
