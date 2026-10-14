import { useState } from 'react'
import {
  comingRaids,
  isInjured,
  RAID_SIZES,
  UNDEAD,
  type Catalog,
  type CitizenSummary,
  type EnemyKind,
  type Raid,
  type RaidSize,
  type Task,
  type VillageState,
} from '../api'
import { useI18n } from '../i18n'

const UNDEAD_WARNING_MINUTES = 3

/** Citizens on a guard task running at the moment the raid strikes, mirroring the server's choice of defenders. */
function defendersOf(raid: Raid, citizens: Iterable<CitizenSummary>, catalog: Catalog) {
  const strikesAt = Date.parse(raid.strikesAt)
  const guardTasks = new Set(catalog.tasks.filter((task) => task.effect === 'Guard').map((task) => task.id))
  return [...citizens].filter(
    (citizen) =>
      citizen.state === 'Working' &&
      citizen.taskId !== null &&
      guardTasks.has(citizen.taskId) &&
      citizen.siteId === null &&
      citizen.cycleStartedAt !== null &&
      citizen.cycleEndsAt !== null &&
      Date.parse(citizen.cycleStartedAt) <= strikesAt &&
      strikesAt < Date.parse(citizen.cycleEndsAt) &&
      !isInjured(citizen, strikesAt),
  )
}

function countdown(milliseconds: number) {
  const seconds = Math.max(0, Math.ceil(milliseconds / 1000))
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`
}

/** The next attack the tribe can see coming: who, how many, when, and who stands on watch. */
export function RaidBanner({
  village,
  citizens,
  catalog,
  now,
  watch,
  pending,
  onWatch,
}: {
  village: VillageState
  citizens: Map<string, CitizenSummary>
  catalog: Catalog
  now: number
  /** The guard task the player's citizen can take; null for the Tsar and visitors. */
  watch: Task | null
  pending: boolean
  onWatch: (task: Task) => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const raid = comingRaids(village.raids, now)[0]
  if (!raid) return null
  const defenders = defendersOf(raid, citizens.values(), catalog)
  return (
    <div className="raid-banner" role="alert">
      <div className="raid-banner-title">
        {t('raid.coming', {
          count: raid.count,
          enemy: i18n.enemy(raid.enemy),
          time: countdown(Date.parse(raid.strikesAt) - now),
        })}
        {raid.waves > 1 && <span className="muted"> · {t('raid.wave', { wave: raid.wave, waves: raid.waves })}</span>}
      </div>
      <div className="raid-banner-row small">
        <span>{t('raid.defenders', { count: defenders.length })}</span>
        {watch && (
          <button className="primary" disabled={pending} onClick={() => onWatch(watch)} title={t('raid.hint')}>
            {t('raid.guard')}
          </button>
        )}
      </div>
    </div>
  )
}

/** The Tsar's control of the undead event, with the record of the last raids. */
export function UndeadPanel({
  village,
  pending,
  error,
  onCall,
  onCancel,
}: {
  village: VillageState
  pending: boolean
  error: string | null
  onCall: (enemy: EnemyKind, size: RaidSize) => void
  onCancel: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const [enemy, setEnemy] = useState<EnemyKind>('Zombie')
  const [size, setSize] = useState<RaidSize>('Small')
  const coming = village.raids.some((raid) => raid.outcome === 'Pending' && raid.source === 'Undead')
  const history = village.raids
    .filter((raid) => raid.outcome !== 'Pending' && raid.resolvedAt !== null)
    .sort((a, b) => Date.parse(b.resolvedAt!) - Date.parse(a.resolvedAt!))
  return (
    <section className="card">
      <h3>{t('undead.title')}</h3>
      <p className="small">{t('undead.hint', { minutes: UNDEAD_WARNING_MINUTES })}</p>
      {coming ? (
        <>
          <p className="small">{t('undead.coming')}</p>
          <button disabled={pending} onClick={onCancel}>
            {t('undead.cancel')}
          </button>
        </>
      ) : (
        <>
          <div className="chips" role="radiogroup" aria-label={t('undead.enemy')}>
            {UNDEAD.map((kind) => (
              <button
                key={kind}
                role="radio"
                aria-checked={enemy === kind}
                className={`chip ${enemy === kind ? 'active' : ''}`}
                onClick={() => setEnemy(kind)}
              >
                {i18n.enemy(kind)}
              </button>
            ))}
          </div>
          <div className="chips" role="radiogroup" aria-label={t('undead.size')}>
            {RAID_SIZES.map((option) => (
              <button
                key={option}
                role="radio"
                aria-checked={size === option}
                className={`chip ${size === option ? 'active' : ''}`}
                onClick={() => setSize(option)}
              >
                {t(`undead.size.${option}`)}
              </button>
            ))}
          </div>
          <button className="primary" disabled={pending} onClick={() => onCall(enemy, size)}>
            {t('undead.call')}
          </button>
        </>
      )}
      {error && <p className="error">{error}</p>}
      <h3>{t('raid.history')}</h3>
      {history.length === 0 ? (
        <p className="muted small">{t('raid.none')}</p>
      ) : (
        <ul className="feed">
          {history.map((raid) => (
            <li key={raid.id}>
              {new Date(raid.resolvedAt!).toLocaleTimeString(i18n.language, { hour: '2-digit', minute: '2-digit' })} —{' '}
              {i18n.enemy(raid.enemy)} × {raid.count}: {t(`raid.result.${raid.outcome as 'Repelled' | 'Lost'}`)}
              {raid.stolen.length > 0 && ` · ${t('raid.stolen', { goods: i18n.goods(raid.stolen) })}`}
              {raid.loot.length > 0 && ` · ${t('raid.loot', { goods: i18n.goods(raid.loot) })}`}
              {raid.injured > 0 && ` · ✚ ${raid.injured}`}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
