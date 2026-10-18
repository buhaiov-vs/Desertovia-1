import { useSyncExternalStore } from 'react'

export const PREFERENCES_EVENT = 'desertovia-preferences'
export const playerNamesVisible = () => { try { return localStorage.getItem('desertovia.playerNames') !== '0' } catch { return true } }
export function setPlayerNames(enabled: boolean) {
  try { localStorage.setItem('desertovia.playerNames', enabled ? '1' : '0') } catch { /* Unavailable storage. */ }
  window.dispatchEvent(new Event(PREFERENCES_EVENT))
}
export const usePlayerNames = () => useSyncExternalStore(subscribe, playerNamesVisible, () => true)

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

const VOLUME_KEY = 'desertovia.musicVolume'
const MUTE_KEY = 'desertovia.musicMuted'

export function musicVolume() {
  try {
    const saved = localStorage.getItem(VOLUME_KEY)
    if (saved === null) return 0.4
    const value = Number(saved)
    return Number.isFinite(value) ? Math.min(1, Math.max(0, value)) : 0.4
  } catch { return 0.4 }
}
export function musicMuted() {
  try { return localStorage.getItem(MUTE_KEY) === '1' } catch { return false }
}
export function setMusicVolume(volume: number) {
  try { localStorage.setItem(VOLUME_KEY, String(Math.min(1, Math.max(0, volume)))) } catch { /* Browser privacy mode. */ }
  window.dispatchEvent(new Event(EVENT))
}
export function setMusicMuted(muted: boolean) {
  try { localStorage.setItem(MUTE_KEY, muted ? '1' : '0') } catch { /* Browser privacy mode. */ }
  window.dispatchEvent(new Event(EVENT))
}
export const useMusicVolume = () => useSyncExternalStore(subscribe, musicVolume, () => 0.4)
export const useMusicMuted = () => useSyncExternalStore(subscribe, musicMuted, () => false)
