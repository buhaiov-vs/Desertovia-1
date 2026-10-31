import { useState } from 'react'
import { api, type VillageState } from '../api'
import { useI18n } from '../i18n'

interface Props {
  slug: string
  night: boolean
  onToggleNight: () => void
  /** Darkens the map; the server's own night is left alone. */
  onNight: () => void
  mapRevealed: boolean
  onToggleMap: () => void
  densHighlighted: boolean
  onToggleDens: () => void
  onVillage: (state: VillageState) => void
}

/** Development and local deployments only: shortcuts for checking the map and raids without waiting for them. */
export function DevPanel({ slug, night, onToggleNight, onNight, mapRevealed, onToggleMap, densHighlighted, onToggleDens, onVillage }: Props) {
  const i18n = useI18n()
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const run = async (action: () => Promise<VillageState>) => {
    setBusy(true)
    setError(null)
    try {
      onVillage(await action())
    } catch (exception) {
      setError(i18n.error(exception))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="dev-panel">
      <button className="dev-panel-toggle" onClick={() => setOpen(!open)} aria-expanded={open} title={i18n.t('dev.title')}>
        {i18n.t('dev.open')}
      </button>
      {open && (
        <div className="dev-panel-body">
          <button onClick={onToggleNight} aria-pressed={night} title={i18n.t('hud.dayNightTitle')}>
            {night ? i18n.t('hud.day') : i18n.t('hud.night')}
          </button>
          <button
            disabled={busy}
            title={i18n.t('dev.nightRaidTitle')}
            onClick={() => {
              onNight()
              void run(() => api.devNightRaid(slug))
            }}
          >
            {i18n.t('dev.nightRaid')}
          </button>
          <button onClick={onToggleMap} aria-pressed={mapRevealed}>
            {mapRevealed ? i18n.t('dev.hideMap') : i18n.t('dev.revealMap')}
          </button>
          <button onClick={onToggleDens} aria-pressed={densHighlighted} title={i18n.t('dev.densTitle')}>
            {densHighlighted ? i18n.t('dev.hideDens') : i18n.t('dev.showDens')}
          </button>
          <button disabled={busy} onClick={() => run(() => api.devRaid(slug, false))}>
            {i18n.t('dev.wave')}
          </button>
          <button disabled={busy} onClick={() => run(() => api.devRaid(slug, true))}>
            {i18n.t('dev.everyMonster')}
          </button>
          <button disabled={busy} onClick={() => run(() => api.devCancelRaids(slug))}>
            {i18n.t('dev.cancelRaids')}
          </button>
          <button disabled={busy} title={i18n.t('dev.resurrectTitle')} onClick={() => run(() => api.devResurrect(slug))}>
            {i18n.t('dev.resurrect')}
          </button>
          {error && <p className="error">{error}</p>}
        </div>
      )}
    </div>
  )
}
