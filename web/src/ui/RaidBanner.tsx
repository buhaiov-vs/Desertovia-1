import { RaidEditor } from './RaidEditor'
import {
  comingRaids,
  type Catalog,
  type CitizenSummary,
  type VillageState,
} from '../api'
import { useI18n } from '../i18n'

function countdown(milliseconds: number) {
  const seconds = Math.max(0, Math.ceil(milliseconds / 1000))
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`
}

/** The next attack the tribe can see coming: who, how many, when, and their arrival time. */
export function RaidBanner({
  village,
  now,
}: {
  village: VillageState
  citizens: Map<string, CitizenSummary>
  catalog: Catalog
  now: number
  pending: boolean
}) {
  const i18n = useI18n()
  const { t } = i18n
  const fighting=village.battles?.find(b=>!b.finishedAt&&b.phase!=='Warning'&&b.phase!=='Cancelled')
  const raid = (fighting&&village.raids.find(r=>r.id===fighting.id)) || comingRaids(village.raids, now)[0]
  if (!raid) return null
  const battle=village.battles?.find(b=>b.id===raid.id)
  const active=!!battle&&battle.phase!=='Warning'&&!battle.finishedAt
  const alive=battle?.units.filter(u=>u.health>0&&!u.exitedAt).length??raid.count
  return (
    <div className="raid-banner" role="alert">
      <div className="raid-banner-title">
        {active?`${i18n.enemy(raid.enemy)} · ${i18n.language==='ru'?(battle.phase==='Retreating'?'отступают':'в деревне'):(battle.phase==='Retreating'?'retreating':'in the village')} · ${alive}/${raid.count}`:battle?.deferred?(i18n.language==='ru'?'Следующая волна ждёт завершения боя':'Next wave waits for this battle'):t('raid.coming', {
          count: raid.count,
          enemy: i18n.enemy(raid.enemy),
          time: countdown(Date.parse(raid.strikesAt) - now),
        })}
        {battle?.composition==='boss'&&<span> · {i18n.language==='ru'?'Боссы':'Boss fight'}</span>}
        {raid.entrySector && <span> · {i18n.language === 'ru' ? 'Подход' : 'Approach'}: {(i18n.language==='ru'?({north:'Север',west:'Запад',south:'Юг'} as Record<string,string>):({north:'North',west:'West',south:'South'} as Record<string,string>))[raid.entrySector] ?? raid.entrySector}</span>}
        {raid.waves > 1 && <span className="muted"> · {t('raid.wave', { wave: raid.wave, waves: raid.waves })}</span>}
      </div>

    </div>
  )
}

/** The Tsar's control of the undead event, with the record of the last raids. */
export function UndeadPanel({
  catalog,
  village,
  pending,
  error,
  onVillage,
  onCancel,
}: {
  village: VillageState
  pending: boolean
  error: string | null
  catalog:Catalog
  onVillage: (v:VillageState) => void
  onCancel: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const coming = village.raids.some((raid) => raid.outcome === 'Pending' && raid.source === 'Undead')
  const history = village.raids
    .filter((raid) => raid.outcome !== 'Pending' && raid.resolvedAt !== null)
    .sort((a, b) => Date.parse(b.resolvedAt!) - Date.parse(a.resolvedAt!))
  return (
    <section className="card">
      <h3>{t('undead.title')}</h3>
      {coming ? (
        <>
          <p className="small">{t('undead.coming')}</p>
          <button disabled={pending} onClick={onCancel}>
            {t('undead.cancel')}
          </button>
        </>
      ) : (
        <>
          <RaidEditor enemies={catalog.enemies} village={village} pending={pending} onVillage={onVillage}/>
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
              {i18n.enemy(raid.enemy)} × {raid.count}: {t(`raid.result.${raid.outcome as 'Repelled' | 'Lost' | 'Cancelled' | 'PropertyDamaged'}`)}
              {raid.stolen.length > 0 && ` · ${t('raid.stolen', { goods: i18n.goods(raid.stolen) })}`}
              {raid.loot.length > 0 && ` · ${t('raid.loot', { goods: i18n.goods(raid.loot) })}`}
              {raid.injured > 0 && ` · ✚ ${raid.injured}`}
              {raid.summary&&<details><summary>{i18n.language==='ru'?'Итоги боя':'Battle summary'}</summary>
                <p className="small">{Math.round(raid.summary.contactSeconds)} {i18n.language==='ru'?'сек контакта · участников':'seconds in contact · participants'}: {raid.summary.participants} · HP −{Math.round(raid.summary.playerHealthLost)} · ✚ {raid.summary.remediesUsed} · 🏹 {raid.summary.arrowsSpent}</p>
                <p className="small">{i18n.language==='ru'?'Урон зданиям / стенам':'Building / wall damage'}: {raid.summary.propertyDamage} / {raid.summary.wallDamage} · {i18n.language==='ru'?'сломано предметов':'broken items'}: {raid.summary.brokenItems}</p>
                <p className="small">{raid.summary.reason} · {Math.round(raid.summary.nextCoefficient*100)}%</p>
              </details>}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
