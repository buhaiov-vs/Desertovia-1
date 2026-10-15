import { useState } from 'react'
import { EnergyDrinkShop } from './EnergyDrinkShop'
import { api, type Citizen } from '../api'
import { useI18n } from '../i18n'
export function SramoslavPanel({ slug, citizen, onClose, onReply }: { citizen?: Citizen|null; slug: string; onClose: () => void; onReply: (text: string) => void }) {
  const i18n = useI18n()
  const [text, setText] = useState('')
  const [reply, setReply] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  return <div className="modal-backdrop" onPointerDown={e => e.target === e.currentTarget && onClose()}>
    <section className="modal" role="dialog" aria-label={i18n.t('priest.name')}>
      <header className="modal-header"><h2>{i18n.t('priest.name')}</h2><button className="close" onClick={onClose}>✕</button></header>
      <div className="modal-body"><p>{reply ?? i18n.t('priest.intro')}</p>
      <form onSubmit={async e => { e.preventDefault(); setPending(true); try { const r = await api.sramoslav(slug, text); setReply(r.text); onReply(r.text); setText('') } catch (error) { setReply(i18n.error(error)) } finally { setPending(false) } }}>
        <label className="field">{i18n.t('priest.ask')}<input value={text} onChange={e => setText(e.target.value)} maxLength={300} placeholder={i18n.t('priest.placeholder')} /></label>
        <button className="primary" disabled={pending || !text.trim()}>{i18n.t(pending ? 'priest.waiting' : 'priest.send')}</button>
      </form>{citizen&&<EnergyDrinkShop slug={slug} citizen={citizen}/>}</div>
    </section></div>
}
