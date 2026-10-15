import { useSyncExternalStore } from 'react'

const KEY = 'desertovia.gamePopups', EVENT = 'desertovia-preferences'
let fallback = true
const read = () => { try { const saved=localStorage.getItem(KEY); return saved===null ? fallback : saved !== '0' } catch { return fallback } }
const subscribe = (callback: () => void) => {
  window.addEventListener(EVENT, callback); window.addEventListener('storage', callback)
  return () => { window.removeEventListener(EVENT, callback); window.removeEventListener('storage', callback) }
}
export const useGamePopups = () => useSyncExternalStore(subscribe, read, () => true)
export function setGamePopups(enabled: boolean) {
  fallback = enabled
  try { localStorage.setItem(KEY, enabled ? '1' : '0') } catch { /* Browser privacy mode. */ }
  window.dispatchEvent(new Event(EVENT))
}
