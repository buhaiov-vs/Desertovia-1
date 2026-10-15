import { useEffect, useId, useRef } from 'react'
import { useI18n } from '../i18n'
import { PATCH_NOTES } from '../patchNotes'
import { GAME_VERSION } from '../version'

/** Uses the existing menu scroll area: no extra overlay above the map or chat. */
export function PatchNotes({ onBack }: { onBack: () => void }) {
  const { language, t } = useI18n()
  const heading = useRef<HTMLHeadingElement>(null)
  const headingId = useId()
  useEffect(() => { heading.current?.focus() }, [])
  return (
    <section className="card patch-notes" aria-labelledby={headingId}>
      <div className="patch-notes-header">
        <h3 id={headingId} ref={heading} tabIndex={-1}>{t('settings.patchNotes')}</h3>
        <button type="button" onClick={onBack}>{t('settings.backToSettings')}</button>
      </div>
      {PATCH_NOTES.map(note => (
        <details key={note.version} open={note.version === GAME_VERSION}>
          <summary><strong>{note.version} · {note.title[language]}</strong><time dateTime={note.date}>{new Intl.DateTimeFormat(language, { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(`${note.date}T12:00:00Z`))}</time></summary>
          <ul>{note.changes[language].map(change => <li key={change}>{change}</li>)}</ul>
        </details>
      ))}
      <p className="small muted">{t('settings.patchNotesArchive')}</p>
    </section>
  )
}
