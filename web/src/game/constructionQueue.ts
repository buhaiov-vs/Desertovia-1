import type { VillageState } from '../api'

/** Pending public operations share the queue; approved operations are already counted as sites. */
export function publicConstructionLoad(village: VillageState): number {
  const sites = (village.sites ?? []).filter(s => !s.ownerAccountId).length
  const pending = (village.civicOrders ?? []).filter(order => {
    if (order.status !== 'pending' || !['build', 'upgrade', 'demolish'].includes(order.operation)) return false
    if (order.operation === 'build') return true
    const owner = village.structures?.find(s => s.plotId === order.plotId)?.ownerAccountId
      ?? village.storages?.find(s => s.plotId === order.plotId)?.ownerAccountId
    return !owner
  }).length
  return sites + pending
}
