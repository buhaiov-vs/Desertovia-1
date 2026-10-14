import { useRegisterSW } from 'virtual:pwa-register/react'
import { useI18n } from '../i18n'

const CHECK_INTERVAL_MS = 60 * 60 * 1000

/** Offers the new version once the service worker has downloaded it; the player picks the moment to reload. */
export function UpdatePrompt() {
  const { t } = useI18n()
  const {
    needRefresh: [needRefresh, setNeedRefresh],
    updateServiceWorker,
  } = useRegisterSW({
    // An installed game can stay open for days, so it looks for a new version on its own.
    onRegisteredSW(_url, registration) {
      if (registration) setInterval(() => void registration.update(), CHECK_INTERVAL_MS)
    },
  })

  if (!needRefresh) return null
  return (
    <div className="update-prompt" role="status">
      <span>{t('update.ready')}</span>
      <button className="primary" onClick={() => void updateServiceWorker(true)}>
        {t('update.reload')}
      </button>
      <button onClick={() => setNeedRefresh(false)}>{t('update.later')}</button>
    </div>
  )
}
