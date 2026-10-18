import { combatDiagnostics } from '../game/combatDiagnostics'
import { useGamePopups, setGamePopups, usePlayerNames, setPlayerNames } from '../gamePreferences'
import { GAME_VERSION } from '../version'
import { useRef, useState } from 'react'
import { PatchNotes } from './PatchNotes'
import { api, type Me, type VillageState } from '../api'
import { LanguageSwitcher, useI18n } from '../i18n'
import { MusicSettings } from './BackgroundMusic'
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
  const { t, language } = useI18n()
  const popups = useGamePopups()
  const playerNames = usePlayerNames()
  const [showPatchNotes, setShowPatchNotes] = useState(false)
  const versionButton = useRef<HTMLButtonElement>(null)
  const [copied, setCopied] = useState(false)
  const [exporting,setExporting]=useState(false)
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
      <section className="card"><label><input type="checkbox" checked={popups} onChange={e=>setGamePopups(e.target.checked)}/>{language==='ru'?'Всплывающие сообщения в игре':'In-game message popups'}</label><p className="small muted">{language==='ru'?'История и счётчик непрочитанных сохраняются.':'History and unread counts stay available.'}</p></section>
      <section className="card"><label><input type="checkbox" checked={playerNames} onChange={e=>setPlayerNames(e.target.checked)}/>{language==='ru'?'Показывать ники игроков на карте':'Show player names on the map'}</label></section>
      <MusicSettings />
      <section className="card"><h3>{language==='ru'?'Диагностика боя':'Combat diagnostics'}</h3>
        <p className="small">{language==='ru'?'Замеры кадров и обновлений собираются во время боя. Скачайте сводку при рывках и приложите к сообщению об ошибке.':'Frame and update timings are collected during combat. Download a report when movement stutters.'}</p>
        <button disabled={exporting} onClick={async()=>{setExporting(true);try{let server:unknown=null;if(isOwner){try{server=await api.combatDiagnostics(village.slug)}catch{server={unavailable:true}}}combatDiagnostics.download(server)}finally{setExporting(false)}}}>{language==='ru'?'Скачать диагностику боя':'Download combat diagnostics'}</button>
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
      </>}
    </>
  )
}
