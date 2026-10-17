import {buildingSuffix} from '../buildingLabels'
import { ActionEnergy } from './ActionEnergy'
import { LEND_A_HAND, type Catalog, type Citizen, type JobDefinition, type Site, type SiteJob, type VillageState } from '../api'
import { useI18n } from '../i18n'
import { TaskIcon } from './TileIcon'
export interface JobChoice {
  plotId: string
  stage: number
  jobId: string
}

/** Construction sites as a citizen sees them: every stage split into small jobs for different people. */
export function SitesPanel({
  catalog,
  village,
  citizen,
  disabled,
  onStartJob,
}: {
  catalog: Catalog
  village: VillageState
  citizen: Citizen
  disabled: boolean
  onStartJob: (choice: JobChoice) => void
}) {
  const { t } = useI18n()
  if (village.sites.length === 0) return <p className="muted small">{t('site.none')}</p>
  return (
    <div className="sites">
      {village.sites.filter(s=>!s.ownerAccountId||s.ownerAccountId===citizen.accountId||s.publicDemolition).map((site) => (
        <SiteCard
          key={site.plotId}
          site={site}
          catalog={catalog}
          village={village}
          citizen={citizen}
          disabled={disabled||!!site.ruinedAt}
          onStartJob={onStartJob}
        />
      ))}
    </div>
  )
}

export function SiteCard({
  site,
  catalog,
  village,
  citizen,
  disabled,
  onStartJob,
}: {
  site: Site
  catalog: Catalog
  village: VillageState
  citizen: Citizen
  disabled: boolean
  onStartJob: (choice: JobChoice) => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const definition = catalog.buildings.find((b) => b.kind === site.kind)
  const stage = site.stages[site.stage]
  const stageDefinition = definition?.stages[site.stage]
  if (!definition || !stage || !stageDefinition) return null

  if(site.ruinedAt)return <section className="card"><h3>{i18n.building(site.kind).name} · {i18n.language==='ru'?'Руины':'Ruins'}</h3><p>{i18n.language==='ru'?'Стройка остановлена. Восстановите участок работой «Ремонт зданий». Материалы и прежний вклад сохранены.':'Construction is suspended. Restore the site using Repair buildings. Materials and prior contributions are preserved.'}</p></section>
  const jobsDone = stage.jobs.every((job) => job.done >= job.required - 1e-6)
  const handsWanted = jobsDone && stage.hands < stage.crewNeeded
  const ahead = site.stages.flatMap((later, index) =>
    index <= site.stage
      ? []
      : later.jobs
          .filter((job) => job.done < job.required && jobDefinition(catalog, job.jobId)?.kind === 'Haul')
          .map((job) => ({ job, stage: index })),
  )

  const card = (job: SiteJob, stageIndex: number) => (
    <JobCard
      key={`${stageIndex}-${job.jobId}`}
      job={job}
      definition={jobDefinition(catalog, job.jobId)}
      current={citizen.siteId === site.plotId && citizen.taskId === job.jobId && citizen.state !== 'Idle'}
      citizen={citizen}
      village={village}
      disabled={disabled}
      privateSite={!!site.ownerAccountId&&!site.publicDemolition}
      onStart={() => onStartJob({ plotId: site.plotId, stage: stageIndex, jobId: job.jobId })}
    />
  )

  return (
    <section className="card site">
      <div className="site-head">
        <strong>{i18n.building(site.kind).name}{buildingSuffix(site)}</strong>
        <span className="muted small">
          {t('site.stage', { number: site.stage + 1, total: site.stages.length, stage: i18n.stage(stage.id) })}
        </span>
        <span className={`small ${stage.hands >= stage.crewNeeded ? 'highlight' : ''}`} title={t('site.crewHint', { needed: stage.crewNeeded })}>
          {t('site.hands', { hands: stage.hands, needed: stage.crewNeeded })}
        </span>
      </div>
      <div className="bar gold">
        <div style={{ width: `${Math.floor(site.progress * 100)}%` }} />
      </div>
      <p className="small muted">{t('site.crewHint', { needed: stage.crewNeeded })}</p>
      <div className="task-grid">
        {stage.jobs.map((job) => card(job, site.stage))}
        {handsWanted &&
          card({ jobId: LEND_A_HAND, done: stage.hands, required: stage.crewNeeded, room: stage.crewNeeded - stage.hands }, site.stage)}
      </div>
      {handsWanted && <p className="small highlight">{t('site.handsWanted')}</p>}
      {ahead.length > 0 && (
        <>
          <h4 className="muted small">{t('site.ahead')}</h4>
          <div className="task-grid">{ahead.map(({ job, stage: index }) => card(job, index))}</div>
        </>
      )}
    </section>
  )
}

function jobDefinition(catalog: Catalog, jobId: string) {
  return catalog.jobs.find((job) => job.id === jobId)
}

function JobCard({
  job,
  definition,
  current,
  citizen,
  village,
  disabled,
  onStart,
  privateSite = false,
}: {
  job: SiteJob
  definition: JobDefinition | undefined
  current: boolean
  citizen: Citizen
  village: VillageState
  disabled: boolean
  onStart: () => void
  privateSite?: boolean
}) {
  const i18n = useI18n()
  const { t } = i18n
  const lend = job.jobId === LEND_A_HAND
  const done = !lend && job.done >= job.required - 1e-6
  const remaining = Math.max(0, job.required - job.done)
  let lock: string | null = null
  if (!done && !lend && Math.max(0, job.room - (job.reserved ?? 0)) <= 0 && (job.reserved ?? 0) > 0) {
    lock = t('site.jobBusy')
  } else if (!done && definition?.kind === 'Haul' && definition.resource) {
    const resource = definition.resource
    const inHands = privateSite ? citizen.personalGoods?.find(g=>g.kind===resource)?.amount ?? 0 : citizen.load.find((good) => good.resource === resource)?.amount ?? 0
    const inStorage = privateSite ? 0 : village.resources.find((r) => r.kind === resource)?.amount ?? 0
    if (inHands + inStorage < Math.min(0.5, remaining)) {
      lock = t('site.noMaterial', { resource: i18n.resource(resource).toLowerCase() })
    }
  } else if (!done && !lend && job.room <= 1e-6) {
    lock = t('site.waiting')
  }

  const meta =
    definition?.kind === 'Haul' && definition.resource
      ? t('site.carry', { goods: i18n.held(definition.resource, definition.perCycle) })
      : definition
        ? `${i18n.stat(definition.primary)} · ${t('site.work', { amount: i18n.amount(definition.perCycle) })}`
        : ''

  return (
    <button className={`task-card ${current ? 'current' : ''} ${done ? 'done' : ''}`} disabled={disabled || done || lock !== null} onClick={onStart}>
      <TaskIcon taskId={job.jobId} scale={2} />
      <span className="task-text">
        <span className="task-name">{i18n.job(job.jobId)}</span>
        <span className="task-meta">
          {lend ? '' : `${i18n.amount(job.done)} / ${i18n.amount(job.required)} · `}
          {meta}
          {(job.reserved ?? 0) > 0 && ` · ${t('site.assigned', { amount: job.reserved ?? 0, free: Math.max(0, job.room - (job.reserved ?? 0)) })}`}
        </span>
        <ActionEnergy citizen={citizen} action={job.jobId} />
        <div className="bar thin">
          <div style={{ width: `${Math.min(100, (job.done / Math.max(job.required, 1e-6)) * 100)}%` }} />
        </div>
        {done && <span className="task-meta highlight">✓ {t('site.doneMark')}</span>}
        {lock && <span className="task-lock">{lock}</span>}
      </span>
    </button>
  )
}
