import { useGamePopups } from '../gamePreferences'
import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties, type FormEvent } from 'react'
import { CHAT_MESSAGE_MAXIMUM, type ChatMessage } from '../api'
import { type FeedEntry } from '../hooks'
import { useI18n } from '../i18n'


export type ChatMode = 'hidden' | 'quarter' | 'full'
const MODE_KEY = 'desertod.chat.mode'
const EMPTY_INCOMING: ChatMessage[] = []
const ACTIVITIES_KEY = 'desertod.chat.activities'

/** `at` is in server time, so activities and messages sort together. */
type Line =
  | { kind: 'activity'; key: string; at: number; entry: FeedEntry }
  | { kind: 'message'; key: string; at: number; message: ChatMessage }

/** Village chat with the tribe's activities woven in; the activities can be hidden to read only the players. */
export function ChatPanel({
  mentionCandidates = [],
  feed,
  messages,
  member,
  tsar,
  ownCitizenId,
  serverOffsetMs,
  phone,
  onSend,
  incoming = EMPTY_INCOMING,
  onModeChange,
  interrupted = false,
}: {
  mentionCandidates?: {id:string;name:string}[]
  incoming?: ChatMessage[]
  onModeChange?: (mode: ChatMode) => void
  interrupted?: boolean
  feed: FeedEntry[]
  messages: ChatMessage[]
  /** The Tsar or a citizen of this village; only they read and write the chat. */
  member: boolean
  tsar: boolean
  ownCitizenId: string | null
  /** Server time minus local time; activities are stamped with the local time they arrived. */
  serverOffsetMs: number
  phone: boolean
  onSend: (text: string, mentions?: string[]) => Promise<void>
}) {
  const i18n = useI18n()
  const { t } = i18n
  const popups = useGamePopups()
  const [mentionPicker,setMentionPicker] = useState(false), [selectedMentions,setSelectedMentions] = useState<string[]>([])
  const [mode,setMode] = useState<ChatMode>(()=>{
    try {
      const saved=localStorage.getItem(MODE_KEY)
      if(saved==='hidden'||saved==='quarter'||saved==='full')return saved
      const old=localStorage.getItem('desertod.collapsed.chat')
      return old===null ? (phone?'hidden':'quarter') : old==='1'?'hidden':'quarter'
    } catch { return phone?'hidden':'quarter' }
  })
  const collapsed=mode==='hidden'
  const panel=useRef<HTMLElement>(null), launch=useRef<HTMLButtonElement>(null)
  const nearBottom=useRef(true), scrollTop=useRef(0)
  const scrollAnchor=useRef<{key:string;offset:number}|null>(null)
  const [unread,setUnread]=useState(0)
  const [toasts,setToasts]=useState<Array<{message:ChatMessage;expires:number}>>([])
  const processed=useRef(new Set(incoming.map(message=>message.id)))
  const [viewport,setViewport]=useState<CSSProperties>({})
  const onModeChangeRef=useRef(onModeChange)
  useEffect(()=>{onModeChangeRef.current=onModeChange})
  const [showActivities, setShowActivities] = useState(() => localStorage.getItem(ACTIVITIES_KEY) !== '0')
  const [draft, setDraft] = useState('')
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const list = useRef<HTMLOListElement>(null)

  const own = useCallback((message: ChatMessage) => (!message.isChaplain && (message.isTsar ? tsar : message.citizenId !== null && message.citizenId === ownCitizenId)),[tsar,ownCitizenId])

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

  const toBottom=useCallback(()=>{
    if(list.current)list.current.scrollTop=list.current.scrollHeight
    nearBottom.current=true
    scrollTop.current=list.current?.scrollTop ?? 0
    setUnread(0)
  },[])
  const changeMode=useCallback((next:ChatMode)=>{
    if(list.current&&!collapsed) {
      scrollTop.current=list.current.scrollTop
      const top=list.current.getBoundingClientRect().top
      const line=[...list.current.querySelectorAll<HTMLElement>('[data-line-key]')].find(el=>el.getBoundingClientRect().bottom>top)
      scrollAnchor.current=line ? {key:line.dataset.lineKey!,offset:line.getBoundingClientRect().top-top} : null
    }
    if(next==='hidden'||(next==='quarter'&&phone))list.current?.closest('section')?.querySelector('input')?.blur()
    if(next==='hidden')requestAnimationFrame(()=>launch.current?.focus())
    setMode(next)
    setToasts([])
  },[collapsed,phone])
  useEffect(()=>{
    try { localStorage.setItem(MODE_KEY,mode) } catch { /* Private storage can be unavailable. */ }
    onModeChangeRef.current?.(mode)
    if(mode==='hidden'||interrupted)return
    if(nearBottom.current)toBottom()
    else if(list.current) {
      list.current.scrollTop=scrollTop.current
      const saved=scrollAnchor.current
      const line=saved ? [...list.current.querySelectorAll<HTMLElement>('[data-line-key]')].find(el=>el.dataset.lineKey===saved.key) : undefined
      if(line&&saved)list.current.scrollTop+=line.getBoundingClientRect().top-list.current.getBoundingClientRect().top-saved.offset
    }
  },[mode,toBottom,interrupted])
  useEffect(()=>{if(interrupted&&mode==='full')changeMode('quarter')},[interrupted,mode,changeMode])
  useEffect(()=>{
    const fresh=incoming.filter(message=>!processed.current.has(message.id))
    for(const message of incoming)processed.current.add(message.id)
    // Bound the de-duplication ledger to the incoming window; the hook also de-duplicates history/hub messages.
    if(processed.current.size>200)processed.current=new Set(incoming.map(message=>message.id))
    const other=fresh.filter(message=>!own(message))
    if(other.length===0)return
    if(interrupted||collapsed||!nearBottom.current)setUnread(count=>count+other.length)
    if(collapsed&&!interrupted&&popups)setToasts(previous=>[...previous,...other.map(message=>({message,expires:Date.now()+4200}))].slice(-2))
  },[incoming,collapsed,own,interrupted,popups])
  useEffect(()=>{if(!popups)setToasts([])},[popups])
  useEffect(()=>{
    if(toasts.length===0)return
    const timer=window.setTimeout(()=>setToasts(previous=>previous.filter(toast=>toast.expires>Date.now())),Math.max(0,Math.min(...toasts.map(toast=>toast.expires))-Date.now()))
    return ()=>window.clearTimeout(timer)
  },[toasts])
  useEffect(()=>{
    if(!interrupted&&!collapsed&&nearBottom.current)toBottom()
  },[lines,collapsed,interrupted,toBottom])
  useEffect(()=>{
    if(mode!=='full'||interrupted)return
    const vv=window.visualViewport
    const update=()=>setViewport(vv ? {left:vv.offsetLeft,top:vv.offsetTop,width:vv.width,height:vv.height} : {})
    update();vv?.addEventListener('resize',update);vv?.addEventListener('scroll',update)
    const siblings=[...(panel.current?.parentElement?.children ?? [])].filter(el=>el!==panel.current&&el instanceof HTMLElement) as HTMLElement[]
    const parent=panel.current?.parentElement
    parent?.classList.add('chat-full')
    const previous=siblings.map(el=>el.inert);siblings.forEach(el=>el.inert=true)
    return ()=>{parent?.classList.remove('chat-full');vv?.removeEventListener('resize',update);vv?.removeEventListener('scroll',update);siblings.forEach((el,i)=>el.inert=previous[i])}
  },[mode,interrupted])
  useEffect(()=>{
    if(collapsed||interrupted)return
    const key=(event:KeyboardEvent)=>{
      if(event.key==='Escape') { event.preventDefault();changeMode(mode==='full'?'quarter':'hidden');requestAnimationFrame(()=>launch.current?.focus()) }
      if(event.key==='Tab'&&mode==='full'){
        const focusable=[...(panel.current?.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled)') ?? [])].filter(el=>el.getClientRects().length>0)
        const first=focusable[0],last=focusable[focusable.length-1]
        if(event.shiftKey&&document.activeElement===first){event.preventDefault();last?.focus()}
        else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first?.focus()}
      }
    }
    document.addEventListener('keydown',key);return ()=>document.removeEventListener('keydown',key)
  },[mode,collapsed,changeMode,interrupted])

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
      await onSend(text, selectedMentions.filter(id=>mentionCandidates.some(c=>c.id===id&&text.includes("@"+c.name))))
      setSelectedMentions([])
      setDraft('')
      toBottom()
    } catch (exception) {
      setError(i18n.error(exception))
    } finally {
      setSending(false)
    }
  }

  return (
    <section ref={panel} className={`chat-panel${collapsed ? ' collapsed' : ''}`} data-mode={mode} data-interrupted={interrupted} inert={interrupted}
      style={mode==='full'?viewport:undefined} role={mode==='full'?'dialog':'region'} aria-modal={mode==='full'?true:undefined} aria-label={t('chat.title')}>
      {collapsed&&toasts.length>0&&<div className="chat-toasts" aria-live="polite" aria-atomic="false">
        {toasts.map(({message})=><button key={message.id} className="chat-toast" onClick={()=>{nearBottom.current=true;changeMode('quarter')}}>
          <strong>{message.authorName}</strong><span>{message.text}</span>
        </button>)}
      </div>}
      {collapsed ? <button ref={launch} className="chat-launch" aria-label={t('chat.open')} aria-expanded={false} onClick={()=>changeMode('quarter')}>
        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 4h16v12H9l-5 4V4Z"/></svg> <span className="chat-heading">{t('chat.title')}</span>{unread>0&&<span className="hud-card-count">{unread>99?'99+':unread}</span>}
      </button> : <div className="chat-head">
        <div className="chat-heading"><strong>{t('chat.title')}</strong></div>{unread>0&&<span className="hud-card-count">{unread>99?'99+':unread}</span>}
        <button className={`chip${showActivities?' active':''}`} aria-pressed={showActivities} title={t('chat.activitiesTitle')} onClick={toggleActivities}>{t('chat.activities')}</button>
        <button className="chat-control" aria-label={t(mode==='full'?'chat.quarter':'chat.full')} title={t(mode==='full'?'chat.quarter':'chat.full')} onClick={()=>changeMode(mode==='full'?'quarter':'full')}>
          {mode==='full'?'¼':<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 3H3v6M15 3h6v6M3 15v6h6M21 15v6h-6"/></svg>}
        </button>
        <button className="chat-control" aria-label={t('chat.hide')} title={t('chat.hide')} onClick={()=>changeMode('hidden')}><svg viewBox="0 0 24 24" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg></button>
      </div>}
      <div className="chat-content" hidden={collapsed}>
          <ol className="chat-lines" ref={list} tabIndex={0} onScroll={()=>{
            if(collapsed||!list.current)return
            const el=list.current;scrollTop.current=el.scrollTop
            nearBottom.current=el.scrollHeight-el.clientHeight-el.scrollTop<24
            if(nearBottom.current)setUnread(0)
          }}>
            {lines.length === 0 && (
              <li className="chat-line muted">{showActivities ? t('menu.quiet') : t('chat.empty')}</li>
            )}
            {lines.map((line) =>
              line.kind === 'activity' ? (
                <li key={line.key} data-line-key={line.key} className="chat-line activity">
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
                  data-line-key={line.key}
                  className={`chat-line message${line.message.mentionCitizenIds?.includes(ownCitizenId??'')?' mentioned':''}${own(line.message) ? ' own' : ''}`}
                  title={new Date(line.message.sentAt).toLocaleString()}
                >
                  <span className={`chat-author${line.message.isTsar ? ' tsar' : ''}${line.message.isChaplain ? ' chaplain' : ''}`}>
                    {line.message.authorName}
                    {line.message.isTsar && <span className="badge">{t('hud.tsar')}</span>}
                  </span>{' '}
                  <span className="chat-text">{line.message.text}</span>
                </li>
              ),
            )}
          </ol>
          {unread>0&&<button className="chat-jump" onClick={toBottom}>{t('chat.new')} ↓ {unread}</button>}
          {member ? (
            <>{mentionPicker&&<div className="mention-picker">{mentionCandidates.filter(c=>c.id!==ownCitizenId).map(c=><button type="button" key={c.id} onClick={()=>{const next=draft+(draft&&!draft.endsWith(' ')?' ':'')+'@'+c.name+' ';if(next.length<=CHAT_MESSAGE_MAXIMUM){setDraft(next);setSelectedMentions(previous=>[...new Set([...previous,c.id])].slice(-5))}setMentionPicker(false)}}>@{c.name}</button>)}</div>}
            <form className="chat-form" onSubmit={send}>
              <button type="button" className="chat-mention" aria-label={i18n.language==='ru'?'Упомянуть жителя':'Mention a resident'} aria-expanded={mentionPicker} onClick={()=>setMentionPicker(!mentionPicker)}>＠</button>
              <input
                onFocus={()=>{if(phone&&mode==='quarter')changeMode('full')}}
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
                maxLength={CHAT_MESSAGE_MAXIMUM}
                placeholder={t('chat.placeholder')}
                aria-label={t('chat.placeholder')}
              />
              <button type="submit" className="primary" disabled={sending || draft.trim() === ''}>
                {t('chat.send')}
              </button>
            </form></>
          ) : (
            <p className="chat-line muted">{t('chat.membersOnly')}</p>
          )}
          {error && <p className="chat-error">{error}</p>}
      </div>
    </section>
  )
}
