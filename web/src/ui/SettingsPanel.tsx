import { GAME_VERSION } from '../version'
import { useState } from 'react'
import { api, type Me, type VillageState } from '../api'
import { LanguageSwitcher, useI18n } from '../i18n'
import { InviteCard } from './InviteCard'
import { NotificationToggle } from './NotificationToggle'

interface Props {
  village: VillageState
  me: Me
  isOwner: boolean
  inviteCode: string | null
  onNewInviteCode: () => Promise<void>
  /** Reloads the account after signing out. */
  onAccountChanged: () => void
}

/** Things that are not part of the game: language, notifications, account and invites. */
export function SettingsPanel({ village, me, isOwner, inviteCode, onNewInviteCode, onAccountChanged }: Props) {
  const { t } = useI18n()
  const [copied, setCopied] = useState(false)

  return (
    <>
      <section className="card settings-version">
        <h3>{t('settings.version')}</h3>
        <strong>{t('brand')} {GAME_VERSION}</strong>
      </section>
      <section className="card">
        <h3>{t('settings.language')}</h3>
        <LanguageSwitcher />
      </section>

      <NotificationToggle />

      <section className="card">
        <h3>{t('settings.account')}</h3>
        <div className="row">
          <span>
            {t('home.signedInAs')} <strong>{me.displayName}</strong>
          </span>
          <button
            onClick={async () => {
              await api.logout()
              onAccountChanged()
            }}
          >
            {t('home.signOut')}
          </button>
        </div>
        <p className="small">{t('settings.accountIdHint')}</p>
        <div className="row invite-row">
          <code className="invite-code">{me.id}</code>
          <button
            onClick={async () => {
              await navigator.clipboard.writeText(me.id)
              setCopied(true)
              window.setTimeout(() => setCopied(false), 4000)
            }}
          >
            {t('settings.copyAccountId')}
          </button>
        </div>
        {copied && <p className="small highlight">{t('settings.accountIdCopied')}</p>}
      </section>

      {isOwner && <InviteCard village={village} inviteCode={inviteCode} onNewCode={onNewInviteCode} />}
    </>
  )
}
