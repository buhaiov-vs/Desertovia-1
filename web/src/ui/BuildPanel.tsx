import {
  buildingCount,
  buildingMaterials,
  builtKinds,
  discoveredTechnologies,
  siteHasWork,
  type BuildingDefinition,
  type Catalog,
  type VillageState,
} from '../api'
import { useI18n } from '../i18n'
import { TileIcon } from './TileIcon'
import { BUILDING_SIGNS, taskIcon } from '../game/worldMap'

/** The rulers' plans. Blueprints are free; citizens bring what a building takes, repeats a little more each. */
export function BuildPanel({
  tsar,
  catalog,
  village,
  onPlace,
  onWithdraw,
}: {
  /** The Tsar; false for a citizen the Tsar appointed Governor. */
  tsar: boolean
  catalog: Catalog
  village: VillageState
  onPlace: (definition: BuildingDefinition) => void
  onWithdraw: (plotId: string) => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const built = builtKinds(village, catalog)
  const discovered = discoveredTechnologies(village)
  const maximum = catalog.rules.maximumOpenSites
  const full = village.sites.length >= maximum
  const buildable = catalog.buildings.filter((b) => b.stages.length > 0)
  const later = catalog.buildings.filter((b) => b.stages.length === 0)

  const describeStorage = (definition: BuildingDefinition) => {
    const storage = catalog.storages.find((s) => s.kind === definition.storage)
    if (!storage) return null
    const holds =
      storage.accepts.length === 0
        ? t('build.holdsAll')
        : t('build.holds', { goods: storage.accepts.map((r) => i18n.resource(r).toLowerCase()).join(', ') })
    return `${holds} · ${t('build.slots', { slots: storage.slots, size: storage.slotSize })}`
  }

  const describeMaterials = (definition: BuildingDefinition) => {
    const scale = Math.pow(catalog.rules.repeatCostGrowth, buildingCount(village, catalog, definition.kind))
    const materials = buildingMaterials(catalog, definition, scale)
    if (materials.length === 0) return null
    return t('build.materials', {
      goods: materials.map((m) => `${i18n.resource(m.resource).toLowerCase()} ${i18n.amount(m.amount)}`).join(', '),
    })
  }

  return (
    <>
      <section className="card">
        <h3>{t(tsar ? 'tsar.title' : 'governor.title')}</h3>
        <p className="small">{t(tsar ? 'tsar.role' : 'governor.role')}</p>
      </section>

      <section className="card">
        <h3>
          {t('build.sites')}{' '}
          <span className="muted">
            {village.sites.length} / {maximum}
          </span>
        </h3>
        {village.sites.length === 0 && <p className="muted small">{t('build.noSites')}</p>}
        <ul className="site-list">
          {village.sites.map((site) => {
            const definition = catalog.buildings.find((b) => b.kind === site.kind)
            const stage = definition?.stages[site.stage]
            return (
              <li key={site.plotId}>
                <div className="site-head">
                  <strong>{i18n.building(site.kind).name}</strong>
                  {stage && definition && (
                    <span className="muted small">
                      {t('site.stage', { number: site.stage + 1, total: definition.stages.length, stage: i18n.stage(stage.id) })}
                    </span>
                  )}
                  {!siteHasWork(site) && (
                    <button className="small-button" onClick={() => onWithdraw(site.plotId)}>
                      {t('build.withdraw')}
                    </button>
                  )}
                </div>
                <div className="bar gold">
                  <div style={{ width: `${Math.floor(site.progress * 100)}%` }} />
                </div>
              </li>
            )
          })}
        </ul>
      </section>

      <section className="card">
        <h3>{t('build.title')}</h3>
        <p className="small muted">{t('build.intro')}</p>
        {full && <p className="warning small">{t('build.tooMany', { maximum })}</p>}
        <div className="task-grid">
          {buildable.map((definition) => {
            const missing = definition.requires.filter((kind) => !built.has(kind))
            const undiscovered = definition.technology && !discovered.has(definition.technology) ? definition.technology : null
            const storage = describeStorage(definition)
            const materials = describeMaterials(definition)
            return (
              <button
                key={definition.kind}
                className="task-card"
                disabled={full || missing.length > 0 || undiscovered !== null}
                onClick={() => onPlace(definition)}
              >
                <TileIcon tile={BUILDING_SIGNS[definition.kind] ?? taskIcon('build')} scale={2} />
                <span className="task-text">
                  <span className="task-name">{i18n.building(definition.kind).name}</span>
                  <span className="task-meta">{i18n.building(definition.kind).description}</span>
                  {storage && <span className="task-meta">{storage}</span>}
                  {definition.shelter > 0 && <span className="task-meta">{t('build.shelter', { beds: definition.shelter })}</span>}
                  {materials && <span className="task-meta">{materials}</span>}
                  <span className="task-meta">
                    {t('build.stages', { stages: definition.stages.map((s) => i18n.stage(s.id).toLowerCase()).join(' → ') })}
                  </span>
                  {missing.length > 0 && (
                    <span className="task-lock">
                      {t('build.needs', { buildings: missing.map((kind) => i18n.building(kind).name).join(', ') })}
                    </span>
                  )}
                  {undiscovered && (
                    <span className="task-lock">
                      {t('build.needsTechnology', { technology: i18n.technology(undiscovered).name })}
                    </span>
                  )}
                </span>
              </button>
            )
          })}
        </div>
      </section>

      <section className="card">
        <h3 className="muted">{t('build.later')}</h3>
        <p className="small muted">{later.map((b) => i18n.building(b.kind).name).join(' · ')}</p>
      </section>
    </>
  )
}
