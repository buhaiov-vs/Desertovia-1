import type { Citizen } from './api'

/** Display projection only. Engaging a den always uses the server's current balance. */
export function currentCombatHealth(
  citizen: Pick<Citizen, 'health' | 'maximumHealth' | 'healthUpdatedAt' | 'serverNow'>,
  now: number,
  fullRegenerationMinutes = 30,
) {
  const maximum = citizen.maximumHealth
  const stored = citizen.health
  if (!Number.isFinite(maximum) || maximum <= 0 || !Number.isFinite(stored)) return stored ?? 0
  if (stored >= maximum) return maximum
  const at = Date.parse(citizen.healthUpdatedAt ?? citizen.serverNow)
  if (!Number.isFinite(at) || !(fullRegenerationMinutes > 0)) return stored
  const minutes = Math.max(0, now - at) / 60_000
  return Math.min(maximum, stored + maximum * (minutes / fullRegenerationMinutes))
}
