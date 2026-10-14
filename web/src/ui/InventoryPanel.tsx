import type { Catalog, Citizen, VillageState } from '../api'
import { useI18n } from '../i18n'
import { citizenTitle } from './citizenTitle'
import { HandsCard } from './HandsCard'
import { StorageCard } from './StorageCard'
import { ResourceIcon } from './TileIcon'
import { HUD_RESOURCES } from './texts'

export function InventoryPanel({
  village,
  citizen,
  catalog,
  now,
  pending,
  error,
  onCarry,
  onDrop,
  onTake,
  onClose,
}: {
  village: VillageState
  citizen: Citizen | null | undefined
  catalog: Catalog
  now: number
  pending: boolean
  error: string | null
  onCarry: () => void
  onDrop: () => void
  onTake: (plotId: string, resource: string, amount: number) => void
  onClose: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const resources = village.resources.filter((r) => HUD_RESOURCES.includes(r.kind) || r.amount > 0)
  const working = citizen?.cycleEndsAt ? now < Date.parse(citizen.cycleEndsAt) : false
  return (
    <div className="modal-backdrop" onPointerDown={(event) => event.target === event.currentTarget && onClose()}>
      <div className="modal inventory" role="dialog" aria-label={t('inventory.title')}>
        <header className="modal-header">
          <h2>{t('inventory.title')}</h2>
          <button className="close" onClick={onClose} title={`${t('close')} (I)`}>
            ✕
          </button>
        </header>
        <div className="modal-body">
          {error && <p className="error">{error}</p>}
          {citizen && (
            <section className="card">
              <h3>
                {citizen.name} <span className="muted">— {citizenTitle(citizen, i18n)}</span>
              </h3>
              <div className="character-summary">
                <div>
                  <span className="muted small">{t('character.energy')}</span>
                  <strong>
                    {Math.floor(citizen.energy)} / {citizen.maximumEnergy}
                  </strong>
                </div>
                <div>
                  <span className="muted small">{t('character.renown')}</span>
                  <strong>{citizen.renown}</strong>
                </div>
                <div>
                  <span className="muted small">{t('character.streak')}</span>
                  <strong>{citizen.streak}</strong>
                </div>
              </div>
            </section>
          )}
          {citizen && (
            <HandsCard citizen={citizen} village={village} catalog={catalog} pending={pending} onCarry={onCarry} onDrop={onDrop} />
          )}

          <section className="card">
            <h3>
              {t('inventory.storage')}{' '}
              <span className="muted">{t('inventory.morale', { value: Math.round(village.morale * 100) })}</span>
            </h3>
            <div className="inventory-grid">
              {resources.map((resource) => (
                <div className="slot" key={resource.kind} title={i18n.resource(resource.kind)}>
                  <ResourceIcon kind={resource.kind} scale={3} />
                  <span className="slot-name">{i18n.resource(resource.kind)}</span>
                  <span className="slot-amount">
                    {i18n.amount(resource.amount)}
                    {resource.capacity !== null && <span className="muted"> / {i18n.amount(resource.capacity)}</span>}
                  </span>
                  {resource.capacity !== null && (
                    <div className="bar thin">
                      <div style={{ width: `${Math.min(100, (resource.amount / Math.max(resource.capacity, 1)) * 100)}%` }} />
                    </div>
                  )}
                </div>
              ))}
            </div>
            <p className="small muted">{t('inventory.shared')}</p>
          </section>
          <section className="card">
            <h3>{t('inventory.buildings')}</h3>
            <ul className="storage-list">
              {village.storages.map((storage) => (
                <li key={storage.plotId}>
                  <strong>{i18n.storage(storage.kind)}</strong>
                  <StorageCard
                    storage={storage}
                    catalog={catalog}
                    take={
                      citizen
                        ? {
                            citizen,
                            disabled: pending || working,
                            onTake: (resource, amount) => onTake(storage.plotId, resource, amount),
                          }
                        : undefined
                    }
                  />
                </li>
              ))}
            </ul>
          </section>
        </div>
      </div>
    </div>
  )
}
