import { GAME_VERSION } from '../version'
import { useRef, useState } from 'react'
import { PatchNotes } from './PatchNotes'
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
  const [showPatchNotes, setShowPatchNotes] = useState(false)
  const versionButton = useRef<HTMLButtonElement>(null)
  const [copied, setCopied] = useState(false)
  const [theme,setTheme]=useState(()=>localStorage.getItem('desertovia.uiTheme')??'auto')
  const [labels,setLabels]=useState(()=>localStorage.getItem('desertovia.buildingLabels')==='true')

  return (
    <>
      <section className="card settings-version">
        <h3>{t('settings.version')}</h3>
        <button
          type="button"
          ref={versionButton}
          className="version-patch-notes"
          aria-expanded={showPatchNotes}
          onClick={() => setShowPatchNotes(!showPatchNotes)}
        >
          <strong>{t('brand')} {GAME_VERSION}</strong>
          <span>{t('settings.patchNotes')} →</span>
        </button>
      </section>
      {showPatchNotes ? <PatchNotes onBack={() => {
        setShowPatchNotes(false)
        requestAnimationFrame(() => versionButton.current?.focus())
      }} /> : <>
      <section className="card">
        <h3>{t('settings.language')}</h3>
        <LanguageSwitcher />
      </section>

      <section className="card"><h3>Оформление</h3><div className="row">
        {['auto','light','dark'].map((mode,i)=><button className={theme===mode?'active':''} key={mode} onClick={()=>{setTheme(mode);localStorage.setItem('desertovia.uiTheme',mode);document.documentElement.dataset.uiTheme=mode==='auto'?(window.matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light'):mode}}>{['Как на устройстве','Светлый','Тёмный'][i]}</button>)}
        </div><label><input type="checkbox" checked={labels} onChange={e=>{setLabels(e.target.checked);localStorage.setItem('desertovia.buildingLabels',String(e.target.checked))}}/> Названия зданий на карте · применится при обновлении</label></section>
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
      </>}
    </>
  )
}
