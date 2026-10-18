import { useEffect } from 'react'
import { GAME_VERSION } from '../version'
import { combatDiagnostics } from '../game/combatDiagnostics'

export interface ReportContext {
  slug?: string
  page: 'village' | 'dungeon' | 'other'
  menu?: string
  selection?: string
  dungeonId?: string
  x?: number
  y?: number
}
let current: (() => ReportContext) | null = null
export function useReportContext(read: () => ReportContext) {
  useEffect(() => {
    current = read
    return () => { if (current === read) current = null }
  }, [read])
}
export function readReportContext() {
  const { slug, ...screen } = current?.() ?? { page: 'other' }
  const last = combatDiagnostics.reportSummary()
  return { slug: slug ?? null, screen: {
    ...screen, clientVersion: GAME_VERSION, width: innerWidth, height: innerHeight,
    platform: /Android/i.test(navigator.userAgent) ? 'Android' : /iPhone|iPad/i.test(navigator.userAgent) ? 'iOS' : 'desktop',
    frameP95Ms: last?.frameP95Ms, packetGapMaxMs: last?.packetGapMaxMs, reconnects: last?.reconnects,
  } }
}
