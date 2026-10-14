import { appPath } from './paths'
import { useCallback, useEffect, useState } from 'react'

const STORAGE_KEY = 'desertod.notifications'
const ICON = appPath('/favicon.svg')

export const notificationsSupported = () => typeof window !== 'undefined' && 'Notification' in window

function enabledByPlayer() {
  return window.localStorage.getItem(STORAGE_KEY) === 'on'
}

/**
 * Shows a system notification while the game tab is in the background; a click brings the tab back.
 * With the tab in front the game itself shows what happened, so nothing is sent.
 */
export function notify(title: string, body: string, tag: string) {
  if (!notificationsSupported() || Notification.permission !== 'granted' || !enabledByPlayer()) return
  if (document.visibilityState === 'visible' && document.hasFocus()) return
  const notification = new Notification(title, { body, tag, icon: ICON, badge: ICON })
  notification.onclick = () => {
    window.focus()
    notification.close()
  }
}

export type NotificationState = 'unsupported' | 'denied' | 'off' | 'on'

function currentState(): NotificationState {
  if (!notificationsSupported()) return 'unsupported'
  if (Notification.permission === 'denied') return 'denied'
  return Notification.permission === 'granted' && enabledByPlayer() ? 'on' : 'off'
}

/** The player's choice; turning on asks the browser for permission (it must come from a click). */
export function useNotificationSetting() {
  const [state, setState] = useState<NotificationState>(currentState)

  useEffect(() => {
    // The player may change the permission in the browser settings while the game is open.
    const refresh = () => setState(currentState())
    document.addEventListener('visibilitychange', refresh)
    return () => document.removeEventListener('visibilitychange', refresh)
  }, [])

  const enable = useCallback(async () => {
    if (!notificationsSupported()) return
    const permission = Notification.permission === 'default' ? await Notification.requestPermission() : Notification.permission
    window.localStorage.setItem(STORAGE_KEY, permission === 'granted' ? 'on' : 'off')
    setState(currentState())
  }, [])

  const disable = useCallback(() => {
    window.localStorage.setItem(STORAGE_KEY, 'off')
    setState(currentState())
  }, [])

  return { state, enable, disable }
}

/** Marks the tab title while the player is away, so a pinned or crowded tab still shows that work is done. */
export function flagTitle(mark: string, baseTitle: string) {
  if (document.visibilityState === 'visible' && document.hasFocus()) return
  document.title = `${mark} ${baseTitle}`
}

export function useTitleReset(baseTitle: string) {
  useEffect(() => {
    const reset = () => {
      if (document.visibilityState === 'visible') document.title = baseTitle
    }
    document.title = baseTitle
    window.addEventListener('focus', reset)
    document.addEventListener('visibilitychange', reset)
    return () => {
      window.removeEventListener('focus', reset)
      document.removeEventListener('visibilitychange', reset)
    }
  }, [baseTitle])
}
