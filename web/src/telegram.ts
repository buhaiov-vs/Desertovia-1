type TelegramWebApp = {
  initData?: string
  ready?: () => void
  /** Present when this Telegram client reports Mini App collapse and restore. */
  isActive?: boolean
  onEvent?: (event: string, handler: () => void) => void
  offEvent?: (event: string, handler: () => void) => void
}
type TelegramWindow = Window & { Telegram?: { WebApp?: TelegramWebApp } }
const webApp = () => (window as TelegramWindow).Telegram?.WebApp
let pending: Promise<string> | undefined
export function miniAppData(): Promise<string> {
  return pending ??= new Promise(resolve => {
    const current = webApp
    if (current()) { current()?.ready?.(); resolve(current()?.initData ?? ''); return }
    const script = document.createElement('script')
    script.src = 'https://telegram.org/js/telegram-web-app.js'
    script.async = true
    const finish = () => { current()?.ready?.(); resolve(current()?.initData ?? '') }
    script.onload = finish
    script.onerror = () => resolve('')
    document.head.appendChild(script)
    setTimeout(finish, 5000)
  })
}

/** Longer than a busy frame: the one-second clock only jumps this far when the page itself was stopped. */
const FROZEN_MS = 3_000

/**
 * Reloads after a reported restore, and after a collapse that reports nothing.
 * A stopped page cannot run a timer, so the next tick arrives late. That gap is the restore.
 * Telegram still saying the Mini App is inactive waits for its own `activated` event.
 */
export function onAppResume(listener: () => void): () => void {
  let away = document.visibilityState === 'hidden' || webApp()?.isActive === false
  let timer = 0
  let last = Date.now()
  const schedule = (reported = false) => {
    if (!away || document.visibilityState === 'hidden') return
    if (!reported && webApp()?.isActive === false) return
    away = false
    last = Date.now()
    window.clearTimeout(timer)
    timer = window.setTimeout(() => { timer = 0; listener() }, 300)
  }
  const markAway = () => { away = true }
  const onVisible = () => { if (document.visibilityState === 'hidden') markAway(); else schedule() }
  const onPageShow = (event: PageTransitionEvent) => { if (event.persisted) { markAway(); schedule() } }
  const onResume = () => { markAway(); schedule() }
  const onActivated = () => { markAway(); schedule(true) }
  document.addEventListener('visibilitychange', onVisible)
  document.addEventListener('freeze', markAway)
  document.addEventListener('resume', onResume)
  window.addEventListener('pagehide', markAway)
  window.addEventListener('pageshow', onPageShow)
  const clock = window.setInterval(() => {
    const now = Date.now()
    const frozen = now - last > FROZEN_MS
    last = now
    if (!frozen) return
    markAway()
    schedule()
  }, 1_000)
  let attached: TelegramWebApp | null = null
  const attach = () => {
    const app = webApp()
    if (!app?.onEvent || app === attached) return
    if (app.isActive === false) away = true
    app.onEvent('deactivated', markAway)
    app.onEvent('activated', onActivated)
    attached = app
  }
  attach()
  const watch = window.setInterval(() => { attach(); if (attached) window.clearInterval(watch) }, 500)
  const stopWatch = window.setTimeout(() => window.clearInterval(watch), 8_000)
  return () => {
    window.clearTimeout(timer)
    window.clearInterval(clock)
    window.clearInterval(watch)
    window.clearTimeout(stopWatch)
    document.removeEventListener('visibilitychange', onVisible)
    document.removeEventListener('freeze', markAway)
    document.removeEventListener('resume', onResume)
    window.removeEventListener('pagehide', markAway)
    window.removeEventListener('pageshow', onPageShow)
    attached?.offEvent?.('deactivated', markAway)
    attached?.offEvent?.('activated', onActivated)
  }
}
