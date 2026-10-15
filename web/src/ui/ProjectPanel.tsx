import type { Project, VillageState } from '../api'
import { useI18n } from '../i18n'
import { ResourceIcon } from './TileIcon'
function Bar({ value, maximum, tone }: { value: number; maximum: number; tone?: string }) {
  const percent = maximum > 0 ? Math.min(100, (value / maximum) * 100) : 0
  return (
    <div className={`bar ${tone ?? ''}`}>
      <div style={{ width: `${percent}%` }} />
    </div>
  )
}

/** The village project if there is one, otherwise the most advanced construction site. */
export function ProjectBanner({ village }: { village: VillageState }) {
  const i18n = useI18n()
  const project = village.project
  const site = [...village.sites].sort((a, b) => b.progress - a.progress)[0]
  const name = project ? i18n.project(project.definitionId).name : site ? i18n.building(site.kind).name : null
  const progress = project?.progress ?? site?.progress ?? 0
  if (!name) return null
  return (
    <div className="project-banner">
      <div className="project-banner-title">
        {i18n.t('banner.building', { project: name })} <span className="dot">•</span> {Math.floor(progress * 100)}%
      </div>
      <Bar value={progress} maximum={1} tone="gold" />
    </div>
  )
}

export function ProjectPanel({ project, completed }: { project: Project | null; completed: string[] }) {
  const i18n = useI18n()
  const { t } = i18n

  if (!project) {
    return (
      <section className="card">
        <h3>{t('project.title')}</h3>
        <p className="muted">{completed.length > 0 ? t('project.allDone') : t('project.none')}</p>
      </section>
    )
  }

  const names = i18n.project(project.definitionId)
  return (
    <section className="card project">
      <h3>
        {names.name} <span className="muted">— {i18n.phase(project.phase)}</span>
      </h3>
      <p className="muted small">{names.description}</p>
      <Bar value={project.progress} maximum={1} tone="gold" />
      {project.stageName&&<strong>{project.stageName}</strong>}
      <ProjectDetails project={project} />
    </section>
  )
}

function ProjectDetails({ project }: { project: Project }) {
  const i18n = useI18n()
  const { t } = i18n
  return (
    <>
      <div className="project-rows">
        {project.surveyRequired > 0 && (
          <div className="project-row">
            <span>{t('project.survey')}</span>
            <Bar value={project.surveyDone} maximum={project.surveyRequired} />
            <span className="numbers">
              {i18n.amount(project.surveyDone)} / {project.surveyRequired}
            </span>
          </div>
        )}
        {project.materials.map((material) => (
          <div className="project-row" key={material.kind}>
            <span>
              <ResourceIcon kind={material.kind} scale={1} /> {i18n.resource(material.kind)}
            </span>
            <Bar value={material.delivered} maximum={material.required} tone="wood" />
            <span className="numbers">
              {i18n.amount(material.delivered)} / {material.required}
            </span>
          </div>
        ))}
        <div className="project-row">
          <span>{t('project.works')}</span>
          <Bar value={project.buildDone} maximum={project.buildRequired} tone="stone" />
          <span className="numbers">
            {i18n.amount(project.buildDone)} / {project.buildRequired}
          </span>
        </div>
      </div>
      <p className="small">
        {t(project.needsEngineer ? 'project.limit' : 'project.limitFree', {
          limit: i18n.amount(project.buildLimit),
          working: project.buildersWorking,
          capacity: project.supervisionCapacity,
        })}
      </p>
    </>
  )
}
