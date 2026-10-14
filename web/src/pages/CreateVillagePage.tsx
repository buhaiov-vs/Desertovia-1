import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { api, VILLAGE_DESCRIPTION_MAXIMUM, VILLAGE_NAME_MAXIMUM, VILLAGE_NAME_MINIMUM, type Me } from '../api'
import { LanguageSwitcher, useI18n } from '../i18n'
import type { UiKey } from '../i18n/en'
import { LoginPanel } from '../ui/LoginPanel'
import { CreateCharacterForm } from '../ui/CreateCharacterForm'

const ACCESS: { isPrivate: boolean; label: UiKey; hint: UiKey }[] = [
  { isPrivate: false, label: 'newVillage.public', hint: 'newVillage.publicHint' },
  { isPrivate: true, label: 'newVillage.private', hint: 'newVillage.privateHint' },
]

/** Where a streamer founds a village and becomes its Tsar. */
export function CreateVillagePage() {
  const navigate = useNavigate()
  const i18n = useI18n()
  const { t } = i18n
  const [me, setMe] = useState<Me | null | undefined>(undefined)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [isPrivate, setIsPrivate] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.me().then(setMe)
  }, [])

  if (me === undefined) return <div className="splash">{t('loading')}</div>

  const nameLength = name.trim().length
  const valid = nameLength >= VILLAGE_NAME_MINIMUM && nameLength <= VILLAGE_NAME_MAXIMUM

  return (
    <div className="menu-screen pond home">
      <header className="home-header">
        <div className="row home-title">
          <h1>{t('brand')}</h1>
          <LanguageSwitcher />
        </div>
        <Link to="/" className="home-back">
          ← {t('toHome')}
        </Link>
      </header>

      {!me ? (
        <LoginPanel onLoggedIn={async () => setMe(await api.me())} title="login.titleHome" />
      ) : !me.character ? (
        <CreateCharacterForm suggestedName={me.displayName} onCreated={setMe} />
      ) : (
        <form
          className="card new-village"
          onSubmit={async (event) => {
            event.preventDefault()
            setPending(true)
            setError(null)
            try {
              const village = await api.createVillage({ name, description: description.trim() || null, isPrivate })
              navigate(`/v/${village.slug}`)
            } catch (exception) {
              setError(i18n.error(exception))
              setPending(false)
            }
          }}
        >
          <h2>{t('newVillage.title')}</h2>
          <p className="small">{t('newVillage.intro')}</p>

          <label className="field">
            {t('newVillage.name')}
            <input
              value={name}
              onChange={(event) => setName(event.target.value)}
              maxLength={VILLAGE_NAME_MAXIMUM}
              autoFocus
              required
            />
            <span className="muted small">
              {t('newVillage.nameHint', { minimum: VILLAGE_NAME_MINIMUM, maximum: VILLAGE_NAME_MAXIMUM })}
            </span>
          </label>

          <label className="field">
            {t('newVillage.description')}
            <textarea
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              maxLength={VILLAGE_DESCRIPTION_MAXIMUM}
              placeholder={t('newVillage.descriptionPlaceholder')}
              rows={3}
            />
            <span className="muted small counter">
              {t('newVillage.counter', { count: description.length, maximum: VILLAGE_DESCRIPTION_MAXIMUM })}
            </span>
          </label>

          <fieldset className="field access-options">
            <legend>{t('newVillage.access')}</legend>
            {ACCESS.map((option) => (
              <label key={option.label} className={`access-option ${isPrivate === option.isPrivate ? 'active' : ''}`}>
                <input
                  type="radio"
                  name="access"
                  checked={isPrivate === option.isPrivate}
                  onChange={() => setIsPrivate(option.isPrivate)}
                />
                <span>
                  <strong>{t(option.label)}</strong>
                  <span className="small muted">{t(option.hint)}</span>
                </span>
              </label>
            ))}
          </fieldset>

          <button type="submit" className="primary" disabled={!valid || pending}>
            {pending ? t('newVillage.creating') : t('newVillage.submit')}
          </button>
          {error && <p className="error">{error}</p>}
        </form>
      )}
    </div>
  )
}
