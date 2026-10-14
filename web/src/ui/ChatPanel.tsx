import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { CHAT_MESSAGE_MAXIMUM, type ChatMessage } from '../api'
import { useCollapsed, type FeedEntry } from '../hooks'
import { useI18n } from '../i18n'
import { FoldHead } from './Hud'

const ACTIVITIES_KEY = 'desertod.chat.activities'

/** `at` is in server time, so activities and messages sort together. */
type Line =
  | { kind: 'activity'; key: string; at: number; entry: FeedEntry }
  | { kind: 'message'; key: string; at: number; message: ChatMessage }

/** Village chat with the tribe's activities woven in; the activities can be hidden to read only the players. */
export function ChatPanel({
  feed,
  messages,
  member,
  tsar,
  ownCitizenId,
  serverOffsetMs,
  phone,
  onSend,
}: {
  feed: FeedEntry[]
  messages: ChatMessage[]
  /** The Tsar or a citizen of this village; only they read and write the chat. */
  member: boolean
  tsar: boolean
  ownCitizenId: string | null
  /** Server time minus local time; activities are stamped with the local time they arrived. */
  serverOffsetMs: number
  phone: boolean
  onSend: (text: string) => Promise<void>
}) {
  const i18n = useI18n()
  const { t } = i18n
  const [collapsed, toggle] = useCollapsed('chat', phone)
  const [showActivities, setShowActivities] = useState(() => localStorage.getItem(ACTIVITIES_KEY) !== '0')
  const [draft, setDraft] = useState('')
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  /** Messages after this server time count as unread while the panel is folded. */
  const [seenAt, setSeenAt] = useState(() => Date.now() + serverOffsetMs)
  const list = useRef<HTMLOListElement>(null)

  const own = (message: ChatMessage) => (message.isTsar ? tsar : message.citizenId === ownCitizenId)

  const lines = useMemo(() => {
    const all: Line[] = messages.map((message) => ({
      kind: 'message',
      key: `m-${message.id}`,
      at: Date.parse(message.sentAt),
      message,
    }))
    if (showActivities) {
      for (const entry of feed) all.push({ kind: 'activity', key: `a-${entry.key}`, at: entry.at + serverOffsetMs, entry })
    }
    return all.sort((a, b) => a.at - b.at)
  }, [messages, feed, showActivities, serverOffsetMs])

  const fold = () => {
    const latest = messages.length > 0 ? Date.parse(messages[messages.length - 1].sentAt) : 0
    // Everything was on screen until the panel folds, or was just read when it unfolds.
    setSeenAt((previous) => Math.max(previous, latest))
    toggle()
  }
  const unread = collapsed ? messages.filter((message) => !own(message) && Date.parse(message.sentAt) > seenAt).length : 0

  // The newest line stays in view, as in any chat.
  useEffect(() => {
    if (list.current) list.current.scrollTop = list.current.scrollHeight
  }, [lines, collapsed])

  const toggleActivities = () => {
    const next = !showActivities
    localStorage.setItem(ACTIVITIES_KEY, next ? '1' : '0')
    setShowActivities(next)
  }

  const send = async (event: FormEvent) => {
    event.preventDefault()
    const text = draft.trim()
    if (!text || sending) return
    setSending(true)
    setError(null)
    try {
      await onSend(text)
      setDraft('')
    } catch (exception) {
      setError(i18n.error(exception))
    } finally {
      setSending(false)
    }
  }

  return (
    <section className={`chat-panel${collapsed ? ' collapsed' : ''}`} aria-label={t('chat.title')}>
      <div className="chat-head">
        <FoldHead collapsed={collapsed} onToggle={fold}>
          <strong>{t('chat.title')}</strong>
          {unread > 0 && <span className="hud-card-count">{unread}</span>}
        </FoldHead>
        {!collapsed && (
          <button
            className={`chip${showActivities ? ' active' : ''}`}
            aria-pressed={showActivities}
            title={t('chat.activitiesTitle')}
            onClick={toggleActivities}
          >
            {t('chat.activities')}
          </button>
        )}
      </div>
      {!collapsed && (
        <>
          <ol className="chat-lines" ref={list}>
            {lines.length === 0 && (
              <li className="chat-line muted">{showActivities ? t('menu.quiet') : t('chat.empty')}</li>
            )}
            {lines.map((line) =>
              line.kind === 'activity' ? (
                <li key={line.key} className="chat-line activity">
                  {line.entry.kind === 'cycle'
                    ? i18n.describeReport(line.entry.report)
                    : line.entry.kind === 'raid'
                      ? i18n.describeRaid(line.entry.report)
                      : line.entry.kind === 'dungeon'
                        ? i18n.describeDungeon(line.entry.run)
                        : t('feed.built', { project: i18n.project(line.entry.completion.projectId).name })}
                </li>
              ) : (
                <li
                  key={line.key}
                  className={`chat-line message${own(line.message) ? ' own' : ''}`}
                  title={new Date(line.message.sentAt).toLocaleString()}
                >
                  <span className={`chat-author${line.message.isTsar ? ' tsar' : ''}`}>
                    {line.message.authorName}
                    {line.message.isTsar && <span className="badge">{t('hud.tsar')}</span>}
                  </span>{' '}
                  <span className="chat-text">{line.message.text}</span>
                </li>
              ),
            )}
          </ol>
          {member ? (
            <form className="chat-form" onSubmit={send}>
              <input
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
                maxLength={CHAT_MESSAGE_MAXIMUM}
                placeholder={t('chat.placeholder')}
                aria-label={t('chat.placeholder')}
              />
              <button type="submit" className="primary" disabled={sending || draft.trim() === ''}>
                {t('chat.send')}
              </button>
            </form>
          ) : (
            <p className="chat-line muted">{t('chat.membersOnly')}</p>
          )}
          {error && <p className="chat-error">{error}</p>}
        </>
      )}
    </section>
  )
}
