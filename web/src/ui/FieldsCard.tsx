import { isRipe, type FieldBed } from '../api'
import { cropTile } from '../game/worldMap'
import { useI18n } from '../i18n'
import { TileIcon } from './TileIcon'

/** The beds of one farm: what grows in each, how far it grew and whether it is wet. */
export function FieldsCard({ beds, now, raining }: { beds: FieldBed[]; now: number; raining: boolean }) {
  const i18n = useI18n()
  const { t } = i18n

  const state = (bed: FieldBed) => {
    if (bed.crop === null) return t('field.empty')
    if (isRipe(bed)) return t('field.ripe')
    const wetMinutes = bed.wateredUntil ? Math.ceil((Date.parse(bed.wateredUntil) - now) / 60000) : 0
    const growth = t('field.growth', { percent: Math.floor(bed.growth * 100) })
    const water = raining ? t('field.rain') : wetMinutes > 0 ? t('field.wet', { minutes: wetMinutes }) : t('field.dry')
    return `${growth} · ${water}`
  }

  return (
    <ul className="field-beds">
      {beds.map((bed) => (
        <li key={bed.index} className={`field-bed ${bed.thirsty ? 'thirsty' : ''}`}>
          <TileIcon tile={bed.crop ? cropTile(bed.crop, bed.growth, isRipe(bed)) : undefined} scale={2} />
          <span className="field-name">{bed.crop ? i18n.crop(bed.crop) : t('field.bed', { number: bed.index + 1 })}</span>
          <span className="field-state muted small">{state(bed)}</span>
        </li>
      ))}
    </ul>
  )
}
