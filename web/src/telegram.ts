type TelegramWindow = Window & { Telegram?: { WebApp?: { initData?: string; ready?: () => void } } }
let pending: Promise<string> | undefined
export function miniAppData(): Promise<string> {
  return pending ??= new Promise(resolve => {
    const current = () => (window as TelegramWindow).Telegram?.WebApp
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
