import type { Citizen } from './api'

/** Display projection only. Starting an action always uses the server's current balance. */
export function currentEnergy(citizen: Citizen, now: number) {
  const at = Date.parse(citizen.serverNow)
  if(!Number.isFinite(at)||!Number.isFinite(citizen.maximumEnergy)||!Number.isFinite(citizen.energyPerHour))return citizen.energy
  const hours = Number.isFinite(at) ? Math.max(0, now - at) / 3_600_000 : 0
  return Math.min(citizen.maximumEnergy, citizen.energy + citizen.energyPerHour * hours)
}
