import { useCallback, useEffect, useState } from 'react'
import { api, type HospitalPayment, type HospitalStatus, type HospitalTier, type VillageState } from '../api'
import { useI18n } from '../i18n'
import type { UiKey } from '../i18n/en'
import dayImage from '../assets/night/hospital.png'

const paymentKey = (slug: string, actor: string) => `desertovia.hospital.payment.${slug}.${actor}`
const remaining = (until: string | null | undefined, now: number) => {
  const minutes = Math.max(0, Math.ceil(((until ? Date.parse(until) : now) - now) / 60000))
  return `${Math.floor(minutes / 60)}:${String(minutes % 60).padStart(2, '0')}`
}

/** A pending receipt stays bound to the same player, patient and offer across refresh/reconnect. */
export function HospitalPanel({ slug, actorId, citizenId, tsar, village, onVillage, onClose, onUpdated, serverOffsetMs = 0, initialPatientId }:
  { slug: string; actorId: string; citizenId?: string; tsar: boolean; village: VillageState
    onVillage: (state: VillageState) => void; onClose: () => void; onUpdated: () => void; serverOffsetMs?: number; initialPatientId?:string|null }) {
  const i18n = useI18n(), { t } = i18n
  const [status, setStatus] = useState<HospitalStatus | null>(null)
  const [patientId, setPatientId] = useState<string | null>(initialPatientId ?? citizenId ?? null)
  const [confirmation, setConfirmation] = useState<HospitalTier | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [now, setNow] = useState(() => Date.now() + serverOffsetMs)
  const [payment, setPayment] = useState<HospitalPayment | null>(() => {
    try { return JSON.parse(localStorage.getItem(paymentKey(slug, actorId)) ?? 'null') } catch { return null }
  })
  const refresh = useCallback(async () => {
    try { setStatus(await api.hospital(slug)) }
    catch { setMessage(t('hospital.unavailable')) }
  }, [slug, t])
  useEffect(() => {
    void refresh()
    const poll = window.setInterval(() => { void refresh() }, 15000)
    const clock = window.setInterval(() => setNow(Date.now() + serverOffsetMs), 1000)
    return () => { clearInterval(poll); clearInterval(clock) }
  }, [refresh, serverOffsetMs])
  const selected = status?.patients.find(p => p.id === patientId)
  const recovery = selected?.recovery ?? (patientId === citizenId ? status?.recovery : null)
  const admitted = !!recovery?.until && Date.parse(recovery.until) > now
  const submit = async (request: HospitalPayment) => {
    if (busy) return
    setBusy(true); setMessage(null)
    try {
      // Persist BEFORE network; storage failure must not create an untrackable purchase.
      localStorage.setItem(paymentKey(slug, actorId), JSON.stringify(request)); setPayment(request)
      const receipt = await api.hospitalHeal(slug, request)
      if (receipt.state !== 'pending') {
        localStorage.removeItem(paymentKey(slug, actorId)); setPayment(null)
      }
      const errorKey = `hospital.${receipt.error}` as UiKey
      setMessage(receipt.state === 'completed' ? t('hospital.completed') : receipt.state === 'refunded' ? t('hospital.refunded')
        : receipt.state === 'pending' ? t('hospital.pending') : t(errorKey) === errorKey ? t('hospital.error') : t(errorKey))
      setConfirmation(null); await refresh(); onUpdated()
    } catch { setMessage(t('hospital.pending')) }
    finally { setBusy(false) }
  }
  const hospital = village.structures.find(s => s.kind === 'Hospital')
  return <div className="modal-backdrop" onPointerDown={e => e.target === e.currentTarget && onClose()}>
    <section className="modal hospital-panel" role="dialog" aria-modal="true" aria-label={t('hospital.title')}>
      <header className="modal-header"><h2>{t('hospital.title')}</h2><button className="close" aria-label={t('close')} onClick={onClose}>✕</button></header>
      <div className="modal-body">
        <div className="hospital-heading"><div className="hospital-portrait" style={{ backgroundImage: `url(${dayImage})` }} />
          <div><strong>{t('hospital.level', { level: status?.level ?? 0 })}</strong><p className="small muted">{t('hospital.balance', { count: status?.balance ?? '—' })}</p></div></div>
        {!hospital && <p className="small muted">{t('hospital.notBuilt')}</p>}
        {(!status || !status.available) && <p role="status" className="warning">{t('hospital.unavailable')}</p>}
        <label className="field">{t('hospital.help')}<select value={patientId ?? ''} onChange={e => { setPatientId(e.target.value); setConfirmation(null) }} disabled={busy || !!payment}>
          {citizenId && <option value={citizenId}>{t('hospital.self')}</option>}
          {!citizenId && <option value="">{t('hospital.help')}</option>}
          {status?.patients.filter(p => p.id !== citizenId).map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
        </select></label>
        <section className="card hospital-recovery">{admitted&&<div className="hospital-bed-patient" aria-label="Пациент лежит на больничной койке">🛏️</div>}<strong>{admitted ? recovery?.cause==='battle'?'На лечении после боя':t('hospital.duel') : t('hospital.available')}</strong>
          {admitted && <><p className="hospital-countdown">{t('hospital.until', { time: remaining(recovery?.until, now) })}</p>
          <p className="small">{t('hospital.pause')}</p><p className="small muted">{t('hospital.bonus', { hospital: recovery?.hospitalPercent ?? 0, skill: recovery?.skillPercent ?? 0 })}</p></>}
        </section>
        {message && <p role="status" className="warning">{message}</p>}
        {payment ? <button className="primary" disabled={busy} onClick={() => void submit(payment)}>{t(busy ? 'hospital.wait' : 'hospital.retry')}</button>
          : confirmation ? <section className="card" aria-label={t('hospital.confirm', { price: confirmation.price })}>
            <strong>{t('hospital.confirm', { price: confirmation.price })}</strong><p className="small">{t('hospital.tiers')}</p>
            <div className="hospital-actions"><button className="primary" disabled={busy || !patientId || !recovery?.episodeId} onClick={() => {
              if (patientId && recovery?.episodeId) void submit({ patientId, code: confirmation.code, requestId: crypto.randomUUID(), expectedPrice: confirmation.price, episodeId: recovery.episodeId })
            }}>{t('hospital.buy', { price: confirmation.price })}</button><button onClick={() => setConfirmation(null)}>{t('hospital.cancel')}</button></div>
          </section> : <div className="hospital-tiers">{status?.tiers.map(tier => <button className="hospital-tier" key={tier.code}
            disabled={!status.available || !admitted || busy || !!tier.blocked || (status.balance ?? 0) < tier.price} onClick={() => setConfirmation(tier)}>
            <strong>{tier.title}</strong><span>{t('hospital.effect', { min: tier.minHours, max: tier.maxHours })}</span><span>{tier.price} 🕯</span>
          </button>)}</div>}
        <details><summary>{t('hospital.patients', { count: status?.patients.length ?? 0 })}</summary>
          {status?.patients.map(p => <p key={p.id}>{p.name} · на лечении {p.recovery.cause==='battle'?'после боя':'после дуэли'} · {remaining(p.recovery.until, now)}</p>)}
        </details>
        {tsar && hospital && (status?.level ?? 3) < 3 && !village.sites.some(s => s.plotId === hospital.plotId) &&
          <button disabled={busy} onClick={async () => { setBusy(true); try { onVillage(await api.hospitalUpgrade(slug, hospital.plotId)); await refresh() } catch(e) { setMessage(i18n.error(e)) } finally { setBusy(false) } }}>
            {t('hospital.upgrade', { level: (status?.level ?? 0) + 1 })}</button>}
      </div>
    </section>
  </div>
}
