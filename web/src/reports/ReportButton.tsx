import { useCallback, useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { api, ApiError } from '../api'
import { captureReportScreen, setReporting } from './capture'
import { readReportContext } from './context'

type Draft = ReturnType<typeof readReportContext> & { id: string; capturedAt: string; screenshot: string | null }

/** Drafts are memory-only: no API, localStorage, IndexedDB or file writes before explicit submission. */
export function ReportButton() {
  const [draft, setDraft] = useState<Draft | null>(null)
  const [capturing, setCapturing] = useState(false)
  const [sending, setSending] = useState(false)
  const [kind, setKind] = useState('bug')
  const [text, setText] = useState('')
  const [error, setError] = useState('')
  const [imageOmitted, setImageOmitted] = useState(false)
  const [attempted, setAttempted] = useState(false)
  const submitted = useRef<Parameters<typeof api.submitReport>[0] | null>(null)
  const [receipt, setReceipt] = useState<string | null>(null)
  const gate = useRef(false)
  const button = useRef<HTMLButtonElement>(null)
  const dialog = useRef<HTMLDivElement>(null)
  const generation = useRef(0)

  const close = useCallback(() => {
    if (sending) return
    generation.current++
    setReporting(false)
    setDraft(null); setText(''); setError(''); setReceipt(null); setKind('bug'); setImageOmitted(false); setAttempted(false); submitted.current = null
    requestAnimationFrame(() => button.current?.focus())
  }, [sending])
  useEffect(() => () => { generation.current++; setReporting(false) }, [])
  useEffect(() => {
    if (!draft && !receipt) return
    const originalOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    const appRoot = document.getElementById('root')
    const wasInert = appRoot?.inert ?? false
    if (appRoot) appRoot.inert = true
    dialog.current?.querySelector<HTMLElement>('select,button:not(:disabled)')?.focus()
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { event.stopPropagation(); event.preventDefault(); close() }
      if (event.key === 'Tab') {
        const elements = [...(dialog.current?.querySelectorAll<HTMLElement>('button:not(:disabled),select:not(:disabled),textarea:not(:disabled),input:not(:disabled)') ?? [])]
        const first = elements[0], last = elements.at(-1)
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus() }
        if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus() }
      }
    }
    window.addEventListener('keydown', onKey, true)
    return () => { if (appRoot) appRoot.inert = wasInert; document.body.style.overflow = originalOverflow; window.removeEventListener('keydown', onKey, true) }
  }, [draft, receipt, close])

  async function open() {
    if (gate.current || draft) return
    gate.current = true; setReporting(true); setCapturing(true); setError('')
    const token = ++generation.current
    const context = readReportContext()
    const capturedAt = new Date().toISOString()
    let screenshot: string | null = null
    try { screenshot = await captureReportScreen() } catch { /* A missing image must not prevent a report. */ }
    if (generation.current === token) {
      setDraft({ ...context, capturedAt, screenshot, id: crypto.randomUUID() })
      setCapturing(false)
    }
    gate.current = false
  }
  async function send() {
    if (!draft || sending || !text.trim()) return
    setSending(true); setAttempted(true); setError('')
    submitted.current ??= { ...draft, kind, text: text.trim() }
    try {
      const result = await api.submitReport(submitted.current)
      // Release the image/context as soon as the server acknowledges saving it.
      setImageOmitted(!!draft.screenshot && result.screenshotIncluded === false)
      setReceipt(result.id)
      setDraft(null); submitted.current = null
      setText('')
    } catch (e) {
      // A definitive validation rejection saved nothing: let the player correct the text/image.
      // Network/5xx uncertainty keeps the exact original payload for idempotent retry.
      if (e instanceof ApiError && (e.status === 413 || e.status === 400 && e.code !== 'report.changed')) {
        submitted.current = null; setAttempted(false)
      }
      setError(e instanceof ApiError && e.status === 401 ? 'Войди в игру, чтобы отправить отчёт.' :
        e instanceof ApiError && e.status === 413 ? 'Снимок слишком большой. Убери снимок и отправь текст.' :
        e instanceof ApiError && e.code === 'report.invalid' ? 'Проверь описание (до 2000 знаков). Если ошибка повторится, убери снимок и отправь текст.' :
        e instanceof ApiError && e.code === 'report.limit' ? 'Можно отправить до пяти обращений в час. Попробуй позже.' :
        'Не удалось подтвердить отправку. Повтори отправку того же отчёта: дубликат не появится.')
    } finally { setSending(false) }
  }
  return createPortal(<>
    {!draft && !receipt && <button ref={button} type="button" className="report-trigger" data-report-exclude
      aria-label="Сообщить об ошибке или предложить идею" title="Сообщить об ошибке или предложить идею"
      disabled={capturing || !!draft || !!receipt} aria-busy={capturing} onClick={() => void open()}>!</button>}
    {(draft || receipt) && <div className="report-overlay" data-report-exclude onPointerDown={event => event.stopPropagation()}>
      <div className="report-dialog" role="dialog" aria-modal="true" aria-labelledby="report-title" ref={dialog}>
        <header><h2 id="report-title">{receipt ? 'Отчёт сохранён' : 'Обратная связь'}</h2>
          <button type="button" className="report-close" aria-label="Закрыть отчёт" disabled={sending} onClick={close}>×</button></header>
        {receipt ? <><p>Обращение №{receipt.slice(0, 8)} принято. Батюшка включит его в общий ежедневный отчёт.</p>
          <p>{imageOmitted ? 'Снимок не сохранён: место для вложений закончилось. Текст и данные игры сохранены.' : ''}</p>
          <button type="button" onClick={close}>Готово</button></> : draft && <>
          <label>Тип обращения<select value={kind} disabled={sending || attempted} onChange={e => setKind(e.target.value)}>
            <option value="bug">Ошибка</option><option value="complaint">Жалоба</option><option value="suggestion">Предложение</option>
          </select></label>
          <label>Описание<textarea maxLength={2000} rows={4} value={text} disabled={sending || attempted}
            placeholder="Что произошло или что предлагаешь изменить?" onChange={e => setText(e.target.value)} /><span className="small muted">{text.length}/2000</span></label>
          {draft.screenshot ? <div className="report-preview"><img src={`data:image/jpeg;base64,${draft.screenshot}`} alt="Экран игры до открытия отчёта" />
            <button type="button" disabled={sending || attempted} onClick={() => setDraft({ ...draft, screenshot: null })}>Убрать снимок</button></div> :
            <p className="report-hint">Без снимка экрана. Описание и данные игры можно отправить.</p>}
          <p className="report-hint">Приложим код персонажа, версию и состояние игры. До отправки черновик нигде не сохраняется.</p>
          {error && <p className="error" role="alert">{error}</p>}
          <footer><button type="button" disabled={sending} onClick={close}>Отмена</button>
            <button type="button" disabled={sending || !text.trim()} onClick={() => void send()}>{sending ? 'Отправка…' : 'Отправить'}</button></footer>
        </>}
      </div>
    </div>}
  </>, document.body)
}
