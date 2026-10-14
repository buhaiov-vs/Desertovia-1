import { useState } from 'react'
import type { Catalog, Citizen, ResourceAmount, Storage } from '../api'
import { useI18n } from '../i18n'
import { ResourceIcon } from './TileIcon'

/** Lets the player's citizen take goods out of the building into their hands. */
export interface StorageTake {
  citizen: Citizen
  disabled: boolean
  onTake: (resource: string, amount: number) => void
}

/** One storage building: what it takes, how many slots are used and what lies inside. */
export function StorageCard({ storage, catalog, take }: { storage: Storage; catalog: Catalog; take?: StorageTake }) {
  const i18n = useI18n()
  const { t } = i18n
  const info = catalog.storages.find((s) => s.kind === storage.kind)
  const limit = catalog.rules.takeLimit

  return (
    <div className="storage-card">
      {info && (
        <p className="muted small">
          {t('inventory.slots', { used: storage.slots.length, total: info.slots, size: info.slotSize })}
          {' · '}
          {info.accepts.length === 0
            ? t('storage.acceptsAll')
            : t('storage.accepts', { goods: info.accepts.map((kind) => i18n.resource(kind)).join(', ') })}
        </p>
      )}
      {storage.goods.length === 0 && <span className="muted small">{t('inventory.emptyBuilding')}</span>}
      {take ? (
        <ul className="storage-take">
          {storage.goods.map((good) => (
            <TakeRow key={good.resource} good={good} limit={limit} take={take} />
          ))}
        </ul>
      ) : (
        <div className="storage-goods">
          {storage.goods.map((good) => (
            <span key={good.resource} className="cost" title={i18n.resource(good.resource)}>
              <ResourceIcon kind={good.resource} scale={1} /> {i18n.amount(good.amount)}
            </span>
          ))}
        </div>
      )}
      {take && storage.goods.length > 0 && <p className="small muted">{t('storage.takeHint', { amount: i18n.amount(limit) })}</p>}
    </div>
  )
}

/** One stored good with a number field that starts at the most the citizen can still carry. */
function TakeRow({ good, limit, take }: { good: ResourceAmount; limit: number; take: StorageTake }) {
  const i18n = useI18n()
  const { t } = i18n
  const held = take.citizen.load.find((carried) => carried.resource === good.resource)?.amount ?? 0
  const most = Math.max(0, Math.min(limit - held, good.amount))
  const [chosen, setChosen] = useState<string | null>(null)
  const draft = chosen ?? String(most)
  const amount = Number(draft)
  const valid = draft.trim() !== '' && Number.isInteger(amount) && amount >= 1 && amount <= most

  return (
    <li>
      <ResourceIcon kind={good.resource} scale={1} />
      <span>{i18n.resource(good.resource)}</span>
      <span className="muted">{i18n.amount(good.amount)}</span>
      <input
        type="number"
        className="take-amount"
        min={1}
        max={most}
        step={1}
        value={draft}
        disabled={take.disabled || most < 1}
        aria-label={t('storage.takeAmount')}
        onChange={(event) => {
          setChosen(event.target.value)
        }}
      />
      <button
        disabled={take.disabled || most < 1 || !valid}
        title={t('storage.takeTitle', { amount: i18n.amount(limit) })}
        onClick={() => {
          if (valid) take.onTake(good.resource, amount)
        }}
      >
        {t('storage.take')}
      </button>
    </li>
  )
}
