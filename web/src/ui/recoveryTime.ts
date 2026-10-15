import type { Language } from '../i18n'

/** Recovery deadlines are real timestamps, independent of the configurable in-game day. */
export function formatRecoveryTime(until: string | number | null | undefined, now: number, language: Language): string {
  const deadline = typeof until === 'number' ? until : until ? Date.parse(until) : NaN
  if (!Number.isFinite(deadline) || !Number.isFinite(now)) return language === 'ru' ? 'Срок уточняется' : 'Recovery time unavailable'
  const minutes = Math.max(0, Math.ceil((deadline - now) / 60000))
  const days = Math.floor(minutes / 1440)
  const hours = Math.floor(minutes % 1440 / 60)
  const rest = minutes % 60
  const parts = language === 'ru'
    ? [days ? `${days} д` : '', hours ? `${hours} ч` : '', rest || !days && !hours ? `${rest} мин` : '']
    : [days ? `${days}d` : '', hours ? `${hours}h` : '', rest || !days && !hours ? `${rest}m` : '']
  return `${parts.filter(Boolean).join(' ')} (${language === 'ru' ? 'реальное время' : 'real time'})`
}
