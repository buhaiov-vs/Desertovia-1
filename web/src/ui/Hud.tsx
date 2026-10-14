import type { CSSProperties, ReactNode } from 'react'
import { isInjured, type Catalog, type Citizen, type CitizenSummary, type Structure, type VillageState } from '../api'
import { useI18n } from '../i18n'
import { TABS, tabsFor, type MenuTab } from './menuTabs'
import { NextAction } from './NextAction'
import { ProjectBanner } from './ProjectPanel'
import { ResourceIcon } from './TileIcon'
import { Hint } from './Hint'
import { restHints } from './needHints'
import { resourceHint } from './resourceHint'
import { HUD_RESOURCES } from './texts'
import { WeatherChip } from './WeatherChip'

export function TopBar({
  village,
  catalog,
  citizen,
  citizens,
  onForecast,
}: {
  village: VillageState
  catalog: Catalog
  citizen: Citizen | null | undefined
  citizens: Map<string, CitizenSummary>
  onForecast: (rain: boolean) => Promise<void>
}) {
  const i18n = useI18n()
  const { t } = i18n
  return (
    <div className="hud-top">
      <div className="hud-top-spacer" />
      <ProjectBanner village={village} />
      <div className="hud-resources" style={{ '--columns': Math.ceil((HUD_RESOURCES.length + 2) / 2) } as CSSProperties}>
        {HUD_RESOURCES.map((kind) => {
          const resource = village.resources.find((r) => r.kind === kind)
          return (
            <span className="resource-chip" key={kind}>
              <Hint text={resourceHint(kind, village, catalog, i18n)} className="resource-hint">
                <ResourceIcon kind={kind} scale={1.5} />
                {i18n.amount(resource?.amount ?? 0)}
              </Hint>
            </span>
          )
        })}
        <span className="resource-chip">
          <span className="morale-icon" aria-hidden>
            ☺
          </span>
          <Hint
            text={t('hint.morale', {
              value: Math.round(village.morale * 100),
              minimum: i18n.amount(catalog.rules.moraleMinimum * 100),
              maximum: i18n.amount(catalog.rules.moraleMaximum * 100),
            })}
          >
            {Math.round(village.morale * 100)}
          </Hint>
        </span>
        <WeatherChip village={village} catalog={catalog} citizen={citizen} citizens={citizens} onForecast={onForecast} />
      </div>
    </div>
  )
}

/** Header of a HUD card that folds it down to this one line. */
export function FoldHead({ collapsed, onToggle, children }: { collapsed: boolean; onToggle: () => void; children: ReactNode }) {
  const { t } = useI18n()
  return (
    <button
      className="hud-card-head"
      onClick={onToggle}
      aria-expanded={!collapsed}
      title={t(collapsed ? 'hud.expand' : 'hud.collapse')}
    >
      {children}
      <span className="hud-card-fold" aria-hidden>
        {collapsed ? '▸' : '▾'}
      </span>
    </button>
  )
}

export function BottomBar({
  citizen,
  catalog,
  structures,
  now,
  phone,
  tsar,
  ruler,
  hasResult,
  poll,
  onMenu,
  onInventory,
  onShowResult,
  pending,
  onCancelTask,
  drinks,
  onDrink,
  remedies,
  onHeal,
}: {
  citizen: Citizen | null | undefined
  catalog: Catalog
  /** The village's buildings: hints name only the beds already built. */
  structures: Structure[]
  now: number
  phone: boolean
  tsar: boolean
  /** The Tsar, or a citizen the Tsar appointed Governor: plans buildings and calls votes. */
  ruler: boolean
  hasResult: boolean
  /** "Vote open" while a hidden vote runs, "Call a vote" for the rulers; null hides the button. */
  poll: { label: string; onClick: () => void } | null
  onMenu: (tab: MenuTab) => void
  onInventory: () => void
  onShowResult: () => void
  pending: boolean
  onCancelTask: () => void
  /** Vigor drinks in the citizen's hands and in village storage. */
  drinks: number
  onDrink: () => void
  /** Remedies in the citizen's hands and in village storage. */
  remedies: number
  onHeal: () => void
}) {
  const { t } = useI18n()
  return (
    <div className="hud-bottom">
      <div className="hud-left">
        {!tsar && citizen && (
          <CitizenNeeds
            citizen={citizen}
            catalog={catalog}
            structures={structures}
            now={now}
            drinks={drinks}
            pending={pending}
            onDrink={onDrink}
            remedies={remedies}
            onHeal={onHeal}
          />
        )}
        <div className="hud-status">
          {tsar ? (
            <button className="primary" onClick={() => onMenu('build')}>
              {t('hud.build')}
            </button>
          ) : citizen ? (
            <>
              <NextAction citizen={citizen} catalog={catalog} now={now} compact pending={pending} onCancel={onCancelTask} />
              {hasResult && citizen.state === 'AwaitingRenewal' && (
                <button className="primary" onClick={onShowResult}>
                  {t('hud.results')}
                </button>
              )}
              {ruler && <button onClick={() => onMenu('build')}>{t('hud.build')}</button>}
            </>
          ) : (
            <button className="primary" onClick={() => onMenu('character')}>
              {t('hud.join')}
            </button>
          )}
          {poll && (
            <button className="primary" onClick={poll.onClick}>
              {poll.label}
            </button>
          )}
        </div>
      </div>
      <div className="hud-buttons">
        {phone ? (
          // Settings stay reachable from the tab row inside the menu, keeping the phone bar short.
          tabsFor(tsar, ruler).filter((tab) => tab !== 'settings').map((tab) => (
            <button key={tab} onClick={() => onMenu(tab)}>
              {t(TABS[tab])}
            </button>
          ))
        ) : (
          <button onClick={() => onMenu('village')}>
            {t('hud.menu')} <kbd>Esc</kbd>
          </button>
        )}
        <button onClick={onInventory}>
          {phone ? t('hud.storage') : t('hud.inventory')} {!phone && <kbd>I</kbd>}
        </button>
      </div>
    </div>
  )
}

/** Food and sleep of the player's citizen, with the numbers behind them. */
function CitizenNeeds({
  citizen,
  catalog,
  structures,
  now,
  drinks,
  pending,
  onDrink,
  remedies,
  onHeal,
}: {
  citizen: Citizen
  catalog: Catalog
  structures: Structure[]
  now: number
  drinks: number
  pending: boolean
  onDrink: () => void
  remedies: number
  onHeal: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const meals = catalog.rules.cyclesPerMeal
  const hoursSinceFetch = Math.max(0, now - Date.parse(citizen.serverNow)) / 3_600_000
  const energy = Math.min(citizen.maximumEnergy, citizen.energy + citizen.energyPerHour * hoursSinceFetch)
  const drinkReadyAt = citizen.drinkReadyAt ? Date.parse(citizen.drinkReadyAt) : 0
  const cooling = now < drinkReadyAt
  const full = energy >= citizen.maximumEnergy
  const drinkTitle = cooling
    ? t('needs.drinkCooldown', { minutes: Math.ceil((drinkReadyAt - now) / 60_000) })
    : full
      ? t('needs.drinkFull')
      : t('needs.drinkTitle', { energy: catalog.rules.vigorDrinkEnergy })
  const { rules } = catalog
  const percentOf = (multiplier: number) => i18n.amount(multiplier * 100)
  const mealsDone = Math.min(citizen.cyclesSinceMeal, meals)
  const rest = restHints(citizen, catalog, structures, i18n, energy)

  return (
    <div className="hud-needs">
      <span className={`resource-chip${citizen.isHungry ? ' needs-warning' : ''}`}>
        <ResourceIcon kind="Food" scale={1.5} />
        <span>
          {citizen.isHungry ? (
            <>
              <Hint
                text={t('hint.hungry', {
                  experience: percentOf(rules.hungryExperienceMultiplier),
                  rest: percentOf(rules.hungryRegenerationMultiplier),
                })}
              >
                {t('needs.hungry')}
              </Hint>
              {' · '}
              <Hint text={t('hint.hungryExperience', { percent: percentOf(rules.hungryExperienceMultiplier) })}>
                {t('needs.hungryExperience', { multiplier: i18n.amount(rules.hungryExperienceMultiplier) })}
              </Hint>
              {' · '}
              <Hint text={t('hint.hungryRest', { percent: percentOf(rules.hungryRegenerationMultiplier) })}>
                {t('needs.hungryRest', { multiplier: i18n.amount(rules.hungryRegenerationMultiplier) })}
              </Hint>
            </>
          ) : (
            <>
              <Hint text={t('hint.fed', { cycles: meals })}>{t('needs.fed')}</Hint>
              {' · '}
              <Hint text={t('hint.meal', { done: mealsDone, cycles: meals })}>
                {t('needs.meal', { done: mealsDone, total: meals })}
              </Hint>
            </>
          )}
        </span>
      </span>
      <span className={`resource-chip${citizen.cycleTired ? ' needs-warning' : ''}`}>
        <span className="sleep-icon" aria-hidden>
          z<small>z</small>
        </span>
        <span>
          <Hint text={rest.place}>{rest.underTrees ? t('needs.underTrees') : t('needs.inShelter')}</Hint>
          {' · '}
          <Hint text={rest.energy}>{t('needs.energy', { energy: Math.floor(energy), maximum: citizen.maximumEnergy })}</Hint>
          {' · '}
          <Hint text={rest.perHour}>{t('needs.perHour', { perHour: i18n.amount(citizen.energyPerHour) })}</Hint>{' '}
          <Hint text={rest.restSpeed}>{t('needs.restSpeed', { percent: rest.percent })}</Hint>
        </span>
      </span>
      {drinks > 0 && (
        <button className="drink-button" disabled={pending || cooling || full} onClick={onDrink} title={drinkTitle}>
          <ResourceIcon kind="VigorDrink" scale={1.5} />
          {t('needs.drink', { energy: catalog.rules.vigorDrinkEnergy, count: drinks })}
        </button>
      )}
      {isInjured(citizen, now) && <InjuryChip citizen={citizen} remedies={remedies} pending={pending} onHeal={onHeal} />}
    </div>
  )
}

/** Shown while the citizen is wounded: when they can work again, and the remedy that heals them now. */
export function InjuryChip({
  citizen,
  remedies,
  pending,
  onHeal,
}: {
  citizen: Citizen
  remedies: number
  pending: boolean
  onHeal: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const time = new Date(citizen.injuredUntil!).toLocaleTimeString(i18n.language, { hour: '2-digit', minute: '2-digit' })
  return (
    <>
      <span className="resource-chip needs-warning injury-chip" title={t('injury.hint')}>
        <span className="injury-icon" aria-hidden>
          ✚
        </span>
        {t('injury.title', { time })}
      </span>
      <button
        className="drink-button"
        disabled={pending || remedies === 0}
        onClick={onHeal}
        title={remedies === 0 ? t('injury.noRemedy') : t('injury.hint')}
      >
        <ResourceIcon kind="Remedy" scale={1.5} />
        {t('injury.heal', { count: remedies })}
      </button>
    </>
  )
}
