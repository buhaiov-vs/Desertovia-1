import { useState } from 'react'
import { inviteLink, type VillageState } from '../api'
import { useI18n } from '../i18n'
import type { UiKey } from '../i18n/en'

/** The Tsar's invite: a link for open villages, a link and a replaceable code for private ones. */
export function InviteCard({
  village,
  inviteCode,
  onNewCode,
}: {
  village: VillageState
  inviteCode: string | null
  onNewCode: () => Promise<void>
}) {
  const i18n = useI18n()
  const { t } = i18n
  const [message, setMessage] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const link = inviteLink(village.slug, village.isPrivate ? inviteCode : null)

  const flash = (text: string) => {
    setMessage(text)
    window.setTimeout(() => setMessage(null), 4000)
  }

  const copy = async (text: string) => {
    await navigator.clipboard.writeText(text)
    flash(t('invite.copied'))
  }

  const renew = async () => {
    if (!window.confirm(t('invite.renewConfirm'))) return
    setPending(true)
    try {
      await onNewCode()
      flash(t('invite.renewed'))
    } catch (exception) {
      flash(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  const intro: UiKey = village.isPrivate ? 'invite.private' : 'invite.public'

  return (
    <section className="card">
      <h3>{t('invite.title')}</h3>
      <p className="small">{t(intro)}</p>
      <div className="row invite-row">
        <input className="invite-link" value={link} readOnly onFocus={(event) => event.target.select()} />
        <button onClick={() => copy(link)}>{t('invite.copyLink')}</button>
      </div>
      {village.isPrivate && inviteCode && (
        <div className="row invite-row">
          <code className="invite-code">{inviteCode}</code>
          <button onClick={() => copy(inviteCode)}>{t('invite.copyCode')}</button>
          <button onClick={renew} disabled={pending}>
            {t('invite.renew')}
          </button>
        </div>
      )}
      {message && <p className="small highlight">{message}</p>}
    </section>
  )
}
