import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { api, ApiError, type Me, type PublicVillage, type VillageListItem } from '../api'
import { LanguageSwitcher, useI18n } from '../i18n'
import { LoginPanel } from '../ui/LoginPanel'
import { CreateCharacterForm } from '../ui/CreateCharacterForm'

export function HomePage() {
  const navigate = useNavigate()
  const { t, error } = useI18n()
  const [me, setMe] = useState<Me | null | undefined>(undefined)
  const [villages, setVillages] = useState<VillageListItem[]>([])
  const [openVillages, setOpenVillages] = useState<PublicVillage[]>([])
  const [joinSlug, setJoinSlug] = useState('')
  const [joinError, setJoinError] = useState<string | null>(null)
  const [joinPending, setJoinPending] = useState(false)

  const applyAccount = useCallback(async (account: Me | null) => {
    setMe(account)
    if (!account) return
    const providers = await api.providers()
    if (providers.primaryVillage) { navigate(`/v/${providers.primaryVillage}`, { replace: true }); return }
    const [mine, open] = await Promise.all([api.villages(), api.publicVillages()])
    setVillages(mine)
    setOpenVillages(open)
  }, [navigate])
  const load = useCallback(async () => applyAccount(await api.me()), [applyAccount])

  useEffect(() => {
    api.me().then(applyAccount)
  }, [applyAccount])

  if (me === undefined) return <div className="splash">{t('loading')}</div>

  return (
    <div className="menu-screen home">
      <header className="home-header">
        <div className="row home-title">
          <h1>{t('brand')}</h1>
          <LanguageSwitcher />
        </div>
        <p>{t('home.tagline')}</p>
        {me && (
          <div className="row">
            <span>
              {t('home.signedInAs')} <strong>{me.displayName}</strong>
            </span>
            <button
              onClick={async () => {
                await api.logout()
                setMe(null)
              }}
            >
              {t('home.signOut')}
            </button>
          </div>
        )}
      </header>

      {!me ? (
        <LoginPanel onLoggedIn={load} title="login.titleHome" />
      ) : !me.character ? (
        <CreateCharacterForm suggestedName={me.displayName} onCreated={applyAccount} />
      ) : (
        <>
          <section className="card">
            <h3>{t('home.yourVillages')}</h3>
            {villages.length === 0 ? (
              <p className="muted">{t('home.noVillages')}</p>
            ) : (
              <ul className="village-list">
                {villages.map((village) => (
                  <li key={village.slug}>
                    <Link to={`/v/${village.slug}`}>{village.name}</Link>
                    {village.isOwner && <span className="badge">{t('host')}</span>}
                  </li>
                ))}
              </ul>
            )}
          </section>

          <form
            className="card inline-form"
            onSubmit={async (event) => {
              event.preventDefault()
              const slug = joinSlug.trim().toLowerCase()
              if (!slug) return
              setJoinPending(true)
              setJoinError(null)
              try {
                await api.village(slug)
                navigate(`/v/${slug}`)
              } catch (exception) {
                if (exception instanceof ApiError && exception.status === 404) setJoinError(error(exception))
                else navigate(`/v/${slug}`)
              } finally {
                setJoinPending(false)
              }
            }}
          >
            <h3>{t('home.visit')}</h3>
            <input
              value={joinSlug}
              onChange={(event) => {
                setJoinSlug(event.target.value)
                setJoinError(null)
              }}
              placeholder={t('home.slugPlaceholder')}
              aria-invalid={joinError !== null}
            />
            <button type="submit" disabled={joinPending}>
              {t('home.go')}
            </button>
            {joinError && <p className="error join-error">{joinError}</p>}
          </form>

          <section className="card">
            <h3>{t('home.found')}</h3>
            <p className="small">{t('home.foundHint')}</p>
            <Link to="/new" className="button primary">
              {t('home.create')}
            </Link>
          </section>

          <section className="card">
            <h3>{t('home.open')}</h3>
            {openVillages.length === 0 ? (
              <p className="muted">{t('home.noOpen')}</p>
            ) : (
              <ul className="open-villages">
                {openVillages.map((village) => (
                  <li key={village.slug}>
                    <div className="row">
                      <Link to={`/v/${village.slug}`}>
                        <strong>{village.name}</strong>
                      </Link>
                      <span className="muted small">{t('home.citizens', { count: village.citizens })}</span>
                    </div>
                    {village.description && <p className="small">{village.description}</p>}
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}
    </div>
  )
}
