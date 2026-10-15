import { useEnergy } from './useEnergy'
import type { Citizen } from '../api'
import { useI18n } from '../i18n'

/** Server quote includes weather and medical effects; a positive balance can still be insufficient. */
export function ActionEnergy({ citizen, action }: { citizen: Citizen; action: string }) {
  const { t } = useI18n()
  const energy = useEnergy(citizen)
  const cost = citizen.actionEnergyCosts?.[action]
  if (cost === undefined) return null
  return <span className={`task-meta${energy < cost ? ' warning' : ''}`}>
    {t('task.energyPrice', { cost: Number(cost.toFixed(2)) })}
    {energy < cost && ` · ${t((citizen.exhaustedCycles ?? 0) >= 2 ? 'task.energyFaint' : 'task.energyLow')}`}
  </span>
}
