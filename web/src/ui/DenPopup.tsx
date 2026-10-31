import { useState } from 'react'
import { api, type DenSnapshot, type EnemyKind } from '../api'
import type { SpawnPoint } from '../game/spawnRings'
import { useI18n } from '../i18n'

/** Card for a den protector or a roaming foe: who they are, and an Attack button. */
export function DenPopup({
  slug,
  point,
  protector,
  fight,
  canAttack,
  busyReason,
  onAttacked,
  onClose,
}: {
  slug: string
  point: SpawnPoint
  protector: number
  fight: DenSnapshot | null
  canAttack: boolean
  busyReason: string | null
  onAttacked: () => void
  onClose: () => void
}) {
  const i18n = useI18n()
  const ru = i18n.language === 'ru'
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const unit = fight?.protectors.find((one) => one.index === protector) ?? null
  const alive = !unit || unit.state !== 'Dead'
  const title = point.kind ? i18n.enemy(point.kind as EnemyKind) : point.name
  const pack = point.roams
    ? ru
      ? 'Один бродяга'
      : 'One roaming foe'
    : ru
      ? `${point.protectors} стражей`
      : `${point.protectors} protectors`
  const attack = async () => {
    setBusy(true)
    setError('')
    try {
      await api.denAttack(slug, point.id, protector)
      onAttacked()
    } catch (exception) {
      setError(i18n.error(exception))
    } finally {
      setBusy(false)
    }
  }
  return (
    <aside className="battle-controls" aria-label={ru ? 'Враг' : 'Enemy'}>
      <header>
        <strong>
          {title} · {ru ? 'ур.' : 'lv.'} {point.level}
        </strong>
        <button type="button" className="close" aria-label={i18n.t('close')} onClick={(event) => { event.stopPropagation(); onClose() }}>
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="m6 6 12 12M18 6 6 18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
          </svg>
        </button>
      </header>
      <p className="small">
        {pack}
        {point.roams ? '' : ru ? ` · страж ${protector + 1}` : ` · guard ${protector + 1}`}
      </p>
      {unit && (
        <>
          <p className="small">
            {unit.state === 'Dead'
              ? ru
                ? 'Повержен'
                : 'Down'
              : unit.state === 'Chasing'
                ? ru
                  ? 'В бою'
                  : 'Fighting'
                : unit.state === 'Returning'
                  ? ru
                    ? 'Возвращается'
                    : 'Returning'
                  : ru
                    ? 'На посту'
                    : 'At post'}{' '}
            · ♥ {Math.ceil(unit.health)}/{Math.ceil(unit.maximumHealth)}
          </p>
          <progress value={unit.health} max={unit.maximumHealth} aria-label={ru ? 'Здоровье врага' : 'Enemy health'} />
        </>
      )}
      {!unit && <p className="small">{ru ? 'Покой. Кликните, чтобы напасть.' : 'At rest. Click to attack.'}</p>}
      {busyReason && <p role="status">{busyReason}</p>}
      {error && (
        <p className="error small" role="status">
          {error}
        </p>
      )}
      {canAttack && alive && (
        <button disabled={busy} onClick={() => void attack()}>
          {busy ? (ru ? 'Иду…' : 'Going…') : ru ? 'Атаковать' : 'Attack'}
        </button>
      )}
    </aside>
  )
}
