import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useCallback, useEffect, useRef, useState, type RefObject } from 'react'
import {
  api,
  loadVillage,
  type ChatMessage,
  type CitizenSummary,
  type CycleReport,
  type DungeonRun,
  type DungeonRunView,
  type DungeonSnapshot,
  type ProjectCompletion,
  type QuestCompletion,
  type RaidReport,
  type VillageEvents,
  type VillageState,
} from './api'
import type { GameCanvasHandle } from './game/GameCanvas'
import { PRIEST_ID } from './game/VillageWorld'
import { useI18n } from './i18n'

/** `at` is the local time the event arrived. */
export type FeedEntry =
  | { key: number; at: number; kind: 'cycle'; report: CycleReport }
  | { key: number; at: number; kind: 'project'; completion: ProjectCompletion }
  | { key: number; at: number; kind: 'raid'; report: RaidReport }
  | { key: number; at: number; kind: 'dungeon'; run: DungeonRun }

type DistributiveOmit<T, K extends PropertyKey> = T extends unknown ? Omit<T, K> : never

const FEED_LENGTH = 8
const CHAT_LENGTH = 100

/** Adds messages once each, oldest first, keeping the latest `CHAT_LENGTH`. */
function mergeChat(previous: ChatMessage[], added: ChatMessage[]) {
  const byId = new Map(previous.map((message) => [message.id, message]))
  for (const message of added) byId.set(message.id, message)
  return [...byId.values()]
    .sort((a, b) => Date.parse(a.sentAt) - Date.parse(b.sentAt) || a.id.localeCompare(b.id))
    .slice(-CHAT_LENGTH)
}

/** Subscribes to the chat lines and loads the recent ones; null when the player is not a member of the village. */
async function joinChat(connection: HubConnection, slug: string) {
  if (!(await connection.invoke<boolean>('JoinChat', slug))) return null
  return api.chat(slug)
}

/** Wait after the daytime or night ends before asking for the new sky, and between retries; covers clock drift. */
const WEATHER_RETRY_MS = 5_000

export interface LiveVillageHandlers {
  onCitizenUpdated?: (citizen: CitizenSummary) => void
  onCycleCompleted?: (report: CycleReport) => void
  onProjectCompleted?: (completion: ProjectCompletion) => void
  onQuestCompleted?: (completion: QuestCompletion) => void
  onRaidFought?: (report: RaidReport) => void
  onDungeonEnded?: (run: DungeonRun) => void
  /** A line written while the page is open, once each; not for the history. */
  onChatMessage?: (message: ChatMessage) => void
  /** The Tsar deleted the village. */
  onVillageDeleted?: () => void
}

/**
 * Loads a village and keeps it live through the SignalR hub.
 * `resident`: the player has a citizen here; with the Tsar, the only ones who get the chat.
 */
export function useLiveVillage(slug: string, resident: boolean, handlers: LiveVillageHandlers = {}) {
  const [state, setState] = useState<VillageState | null>(null)
  const [citizens, setCitizens] = useState<Map<string, CitizenSummary>>(new Map())
  /** Citizens who are away; see `Village.awayCitizenIds`. */
  const [away, setAway] = useState<ReadonlySet<string>>(new Set())
  const [isOwner, setIsOwner] = useState(false)
  const [inviteCode, setInviteCode] = useState<string | null>(null)
  const [feed, setFeed] = useState<FeedEntry[]>([])
  const [chat, setChat] = useState<ChatMessage[]>([])
  /** The hub connection once it has joined the village. */
  const [hub, setHub] = useState<HubConnection | null>(null)
  const [error, setError] = useState<unknown>(null)
  const member = isOwner || resident
  const memberRef = useRef(member)
  const callbacks = useRef(handlers)
  useEffect(() => {
    callbacks.current = handlers
    memberRef.current = member
  })
  /** Own lines come back from the hub as well as from the request. */
  const announced = useRef(new Set<string>())
  const receive = useCallback((message: ChatMessage) => {
    setChat((previous) => mergeChat(previous, [message]))
    if (announced.current.has(message.id)) return
    announced.current.add(message.id)
    callbacks.current.onChatMessage?.(message)
  }, [])

  useEffect(() => {
    let cancelled = false
    let feedKey = 0
    const push = (entry: DistributiveOmit<FeedEntry, 'key' | 'at'>) =>
      setFeed((previous) => [{ ...entry, key: feedKey++, at: Date.now() } as FeedEntry, ...previous].slice(0, FEED_LENGTH))

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/village')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('villageUpdated', (next: VillageState) => setState(next))
    connection.on('villageEvents', (events: VillageEvents) => {
      for (const report of events.cycles) {
        push({ kind: 'cycle', report })
        callbacks.current.onCycleCompleted?.(report)
      }
      for (const completion of events.projects) {
        push({ kind: 'project', completion })
        callbacks.current.onProjectCompleted?.(completion)
      }
      for (const completion of events.quests) callbacks.current.onQuestCompleted?.(completion)
      for (const report of events.raids ?? []) {
        push({ kind: 'raid', report })
        callbacks.current.onRaidFought?.(report)
      }
      for (const run of events.dungeons ?? []) {
        if (run.outcome !== 'Cancelled') push({ kind: 'dungeon', run })
        callbacks.current.onDungeonEnded?.(run)
      }
      if (events.citizens.length > 0) {
        setCitizens((previous) => {
          const next = new Map(previous)
          for (const citizen of events.citizens) next.set(citizen.id, citizen)
          return next
        })
        for (const citizen of events.citizens) callbacks.current.onCitizenUpdated?.(citizen)
      }
      const left = new Set(events.left ?? [])
      if (left.size > 0) {
        setCitizens((previous) => new Map([...previous].filter(([id]) => !left.has(id))))
        setChat((previous) => previous.filter((message) => !message.citizenId || !left.has(message.citizenId)))
      }
      const wentAway = events.away ?? []
      const cameBack = events.back ?? []
      if (wentAway.length > 0 || cameBack.length > 0 || left.size > 0) {
        setAway((previous) => {
          const next = new Set(previous)
          for (const id of wentAway) next.add(id)
          for (const id of cameBack) next.delete(id)
          for (const id of left) next.delete(id)
          return next
        })
      }
    })
    connection.on('chatMessage', receive)
    connection.on('villageDeleted', () => callbacks.current.onVillageDeleted?.())
    // Groups are per connection, and lines written while offline come back with the history.
    connection.onreconnected(async () => {
      await connection.invoke('JoinVillage', slug)
      if (!memberRef.current) return
      const history = await joinChat(connection, slug)
      if (history && !cancelled) setChat((previous) => mergeChat(previous, history))
    })

    const load = async () => {
      try {
        const village = await loadVillage(slug)
        if (cancelled) return
        setState(village.state)
        setIsOwner(village.isOwner)
        setInviteCode(village.inviteCode)
        setCitizens(new Map(village.citizens.map((c) => [c.id, c])))
        setAway(new Set(village.awayCitizenIds ?? []))
        await connection.start()
        if (cancelled) return
        await connection.invoke('JoinVillage', slug)
        if (!cancelled) setHub(connection)
      } catch (exception) {
        if (!cancelled) setError(exception)
      }
    }

    void load()
    return () => {
      cancelled = true
      void connection.stop()
    }
  }, [slug, receive])

  // Night falls, day breaks and the sky changes when the period ends, and the server sends nothing at that moment.
  const periodEndsAt = state?.weather.periodEndsAt
  useEffect(() => {
    if (!periodEndsAt) return
    let cancelled = false
    let id = 0
    const refresh = async () => {
      try {
        const next = (await api.village(slug)).state
        if (cancelled) return
        if (next.weather.periodEndsAt === periodEndsAt) id = window.setTimeout(refresh, WEATHER_RETRY_MS)
        else setState(next)
      } catch {
        if (!cancelled) id = window.setTimeout(refresh, WEATHER_RETRY_MS)
      }
    }
    id = window.setTimeout(refresh, Math.max(0, Date.parse(periodEndsAt) - Date.now()) + WEATHER_RETRY_MS)
    return () => {
      cancelled = true
      window.clearTimeout(id)
    }
  }, [slug, periodEndsAt])

  // A newcomer gets the chat once they settle in.
  useEffect(() => {
    if (!hub || !member) return
    let cancelled = false
    joinChat(hub, slug).then(
      (history) => {
        if (history && !cancelled) setChat((previous) => mergeChat(previous, history))
      },
      () => {
        // The next reconnect tries again.
      },
    )
    return () => {
      cancelled = true
    }
  }, [hub, member, slug])

  const sendChat = useCallback(async (text: string) => receive(await api.sendChat(slug, text)), [slug, receive])

  const updateCitizen = useCallback(
    (citizen: CitizenSummary) => setCitizens((previous) => new Map(previous).set(citizen.id, citizen)),
    [],
  )

  return { state, setState, citizens, away, updateCitizen, isOwner, inviteCode, setInviteCode, feed, chat, sendChat, member, error }
}

/**
 * Watches a dungeon run through its hub: the map and heroes once, then a snapshot ten times a second while the fight
 * lasts. The player's own hero, if any, takes orders through `moveTo`, `attack` and `heal`.
 */
export function useDungeonRun(runId: string) {
  const [view, setView] = useState<DungeonRunView | null | undefined>(undefined)
  const [snapshot, setSnapshot] = useState<DungeonSnapshot | null>(null)
  const [error, setError] = useState<unknown>(null)
  const hub = useRef<HubConnection | null>(null)
  const heroId = useRef<string | null>(null)

  useEffect(() => {
    let cancelled = false
    let refetching = false
    let phase: string | null = null
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/dungeon')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    const join = async () => {
      const next = await connection.invoke<DungeonRunView | null>('JoinRun', runId)
      if (cancelled) return
      phase = next?.run.phase ?? null
      setView(next)
      heroId.current = next?.myHeroId ?? null
      if (next?.snapshot) setSnapshot(next.snapshot)
    }
    // The first snapshot of a run that was still on its way brings the fighters' live state: fetch it once.
    const refetch = async () => {
      if (refetching) return
      refetching = true
      try {
        await join()
      } finally {
        refetching = false
      }
    }

    connection.on('dungeonSnapshot', (next: DungeonSnapshot) => {
      setSnapshot(next)
      if (phase !== 'Running') void refetch()
    })
    connection.on('dungeonEnded', () => void refetch())
    connection.onreconnected(() => void join())

    const start = async () => {
      try {
        await connection.start()
        if (cancelled) return
        hub.current = connection
        await join()
      } catch (exception) {
        if (!cancelled) setError(exception)
      }
    }
    void start()
    return () => {
      cancelled = true
      hub.current = null
      void connection.stop()
    }
  }, [runId])

  const send = useCallback(
    (method: string, ...args: unknown[]) => {
      const connection = hub.current
      if (!connection || !heroId.current) return
      void connection.invoke<boolean>(method, runId, heroId.current, ...args).catch(() => {
        // An order lost to a reconnect is simply given again.
      })
    },
    [runId],
  )
  const moveTo = useCallback((x: number, y: number) => send('MoveTo', x, y), [send])
  const attack = useCallback((foeId: number) => send('Attack', foeId), [send])
  const heal = useCallback((allyId: string) => send('Heal', allyId), [send])

  return { view, snapshot, error, moveTo, attack, heal }
}

/** Current time in milliseconds, adjusted to the server clock, refreshed every `intervalMs`. */
export function useServerNow(serverOffsetMs: number, intervalMs = 250) {
  const [localNow, setLocalNow] = useState(() => Date.now())

  useEffect(() => {
    const id = window.setInterval(() => setLocalNow(Date.now()), intervalMs)
    return () => window.clearInterval(id)
  }, [intervalMs])

  return localNow + serverOffsetMs
}

/** The elder comments on village life every `intervalMs`, now and then on the work in progress. */
export function usePriest(canvas: RefObject<GameCanvasHandle | null>, village: VillageState | null, intervalMs = 22_000) {
  const i18n = useI18n()
  const latest = useRef({ village, i18n })
  useEffect(() => {
    latest.current = { village, i18n }
  })

  useEffect(() => {
    let index = 0
    const speak = () => {
      const { village: current, i18n: translator } = latest.current
      const lines = translator.priestLines
      const work = current ? currentWork(current, translator) : null
      const line =
        index % 3 === 1 && work ? translator.t('priest.progress', work) : lines[index % lines.length]
      canvas.current?.say(PRIEST_ID, line)
      index++
    }
    const first = window.setTimeout(speak, 3000)
    const id = window.setInterval(speak, intervalMs)
    return () => {
      window.clearTimeout(first)
      window.clearInterval(id)
    }
  }, [canvas, intervalMs])
}

function currentWork(village: VillageState, i18n: ReturnType<typeof useI18n>) {
  if (village.project) {
    return { project: i18n.project(village.project.definitionId).name, percent: Math.floor(village.project.progress * 100) }
  }
  const site = village.sites[0]
  return site ? { project: i18n.building(site.kind).name, percent: Math.floor(site.progress * 100) } : null
}

/** Mirrors a CSS media query. */
export function useMediaQuery(query: string) {
  const [matches, setMatches] = useState(() => window.matchMedia(query).matches)
  useEffect(() => {
    const media = window.matchMedia(query)
    const listener = () => setMatches(media.matches)
    media.addEventListener('change', listener)
    return () => media.removeEventListener('change', listener)
  }, [query])
  return matches
}

/** A folded HUD card stays folded in this browser; until the player chooses, `initial` decides. */
export function useCollapsed(id: string, initial: boolean) {
  const key = `desertod.collapsed.${id}`
  const [saved, setSaved] = useState(() => window.localStorage.getItem(key))
  const collapsed = saved === null ? initial : saved === '1'
  const toggle = () => {
    const next = collapsed ? '0' : '1'
    window.localStorage.setItem(key, next)
    setSaved(next)
  }
  return [collapsed, toggle] as const
}
