import { useMemo } from 'react'
import type { Citizen } from '../api'
import { currentEnergy } from '../energy'
import { useServerNow } from '../hooks'

export function useEnergy(citizen: Citizen | null | undefined) {
  const serverNow=citizen?.serverNow
  const offset = useMemo(() => serverNow ? Date.parse(serverNow) - Date.now() : 0, [serverNow])
  const now = useServerNow(Number.isFinite(offset) ? offset : 0)
  return citizen ? currentEnergy(citizen, now) : 0
}
