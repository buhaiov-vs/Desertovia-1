import { appPath } from '../paths'
import { miniAppData } from '../telegram'
import { useEffect, useRef, useState } from 'react'
import {
  api,
  DISPLAY_NAME_MAXIMUM,
  EMAIL_MAXIMUM,
  PASSWORD_MAXIMUM,
  PASSWORD_MINIMUM,
  type LoginProviders,
} from '../api'
import { useI18n } from '../i18n'
import type { UiKey } from '../i18n/en'

function TelegramLoginButton({ botUsername, returnUrl, language }: { botUsername: string; returnUrl: string; language: string }) {
  const container = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const element = container.current
    if (!element) return
    const script = document.createElement('script')
    script.src = 'https://telegram.org/js/telegram-widget.js?22'
    script.async = true
    script.setAttribute('data-telegram-login', botUsername)
    script.setAttribute('data-size', 'large')
    script.setAttribute('data-lang', language)
    script.setAttribute(
      'data-auth-url',
      `${window.location.origin}${appPath("/auth/telegram/callback")}?returnUrl=${encodeURIComponent(returnUrl)}`,
    )
    element.appendChild(script)
    return () => {
      element.innerHTML = ''
    }
  }, [botUsername, returnUrl, language])

  return <div ref={container} />
}

function PasswordForm({ onLoggedIn }: { onLoggedIn: () => void }) {
  const i18n = useI18n()
  const { t } = i18n
  const [registering, setRegistering] = useState(false)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [name, setName] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const valid =
    email.trim().length > 0 &&
    password.length >= (registering ? PASSWORD_MINIMUM : 1) &&
    (!registering || name.trim().length > 0)

  return (
    <form
      className="password-login"
      onSubmit={async (event) => {
        event.preventDefault()
        setPending(true)
        setError(null)
        try {
          if (registering) await api.passwordRegister(email, password, name)
          else await api.passwordLogin(email, password)
          onLoggedIn()
        } catch (exception) {
          setError(i18n.error(exception))
        } finally {
          setPending(false)
        }
      }}
    >
      <label className="field">
        <span className="small">{t('login.email')}</span>
        <input
          type="email"
          autoComplete="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          maxLength={EMAIL_MAXIMUM}
          required
        />
      </label>
      <label className="field">
        <span className="small">{t('login.password')}</span>
        <input
          type="password"
          autoComplete={registering ? 'new-password' : 'current-password'}
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          minLength={registering ? PASSWORD_MINIMUM : undefined}
          maxLength={PASSWORD_MAXIMUM}
          required
        />
        {registering && (
          <span className="muted small">{t('login.passwordHint', { minimum: PASSWORD_MINIMUM })}</span>
        )}
      </label>
      {registering && (
        <label className="field">
          <span className="small">{t('login.displayName')}</span>
          <input
            autoComplete="nickname"
            value={name}
            onChange={(event) => setName(event.target.value)}
            maxLength={DISPLAY_NAME_MAXIMUM}
            required
          />
        </label>
      )}
      <div className="row">
        <button type="submit" className="primary" disabled={!valid || pending}>
          {t(registering ? 'login.register' : 'login.submit')}
        </button>
        <button
          type="button"
          className="link-button"
          onClick={() => {
            setRegistering(!registering)
            setError(null)
          }}
        >
          {t(registering ? 'login.haveAccount' : 'login.noAccount')}
        </button>
      </div>
      {error && <p className="error">{error}</p>}
    </form>
  )
}

export function LoginPanel({ onLoggedIn, title = 'login.title' }: { onLoggedIn: () => void; title?: UiKey }) {
  const i18n = useI18n()
  const { t } = i18n
  const [providers, setProviders] = useState<LoginProviders | null>(null)
  const [developmentName, setDevelopmentName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const returnUrl = window.location.pathname + window.location.search
  const [browserLogin, setBrowserLogin] = useState<{url: string; pin: string; expires: number} | null>(null)
  const [telegramPending, setTelegramPending] = useState(false)
  const loggedIn = useRef(onLoggedIn)
  loggedIn.current = onLoggedIn
  useEffect(() => {
    let active = true
    void miniAppData().then(async data => {
      if (!active || !data) return
      setTelegramPending(true)
      try { await api.telegramMiniApp(data); if (active) loggedIn.current() }
      catch (exception: unknown) { if (active) setError(i18n.error(exception)) }
      finally { if (active) setTelegramPending(false) }
    })
    return () => { active = false }
  }, [i18n])
  useEffect(() => {
    if (!browserLogin) return
    let active = true
    let pending = false
    const poll = async () => {
      if (!active || pending) return
      if (Date.now() > browserLogin.expires) { setError(i18n.t('login.telegramExpired')); setBrowserLogin(null); return }
      pending = true
      try { const result = await api.telegramBrowserPoll(); if (active && !result.pending) { setBrowserLogin(null); loggedIn.current() } }
      catch (exception: unknown) { if (active) { setError(i18n.error(exception)); setBrowserLogin(null) } }
      finally { pending = false }
    }
    const timer = setInterval(() => { void poll() }, 2000)
    return () => { active = false; clearInterval(timer) }
  }, [browserLogin, i18n])


  useEffect(() => {
    api.providers().then(setProviders, (exception: unknown) => setError(i18n.error(exception)))
  }, [i18n])

  if (!providers) return <section className="card">{error ?? t('loading')}</section>

  const loginUrl = (provider: string) => `${appPath(`/auth/login/${provider}`)}?returnUrl=${encodeURIComponent(returnUrl)}`
  const nothingConfigured =
    !providers.twitch &&
    !providers.google &&
    !providers.telegramBotUsername &&
    !providers.password &&
    !providers.developmentLogin

  return (
    <section className="card login">
      <h3>{t(title)}</h3>
      <div className="login-buttons">
        {providers.telegramBrowserLogin && (
          <div>
            <button className="primary" disabled={telegramPending} onClick={async () => {
              setTelegramPending(true); setError(null)
              try { setBrowserLogin({ ...(await api.telegramBrowserStart()), expires: Date.now() + 300000 }) }
              catch (exception: unknown) { setError(i18n.error(exception)) }
              finally { setTelegramPending(false) }
            }}>{t(browserLogin ? 'login.telegramRestart' : 'login.telegram')}</button>
            {browserLogin && <div className="telegram-confirm"><p>{t('login.telegramInstructions')}</p><strong>{t('login.telegramCode')}: {browserLogin.pin}</strong><p><a className="button primary" href={browserLogin.url} target="_blank" rel="noopener noreferrer">{t('login.telegramConfirm')}</a></p><span className="muted small">{t('login.telegramReturn')}</span></div>}
          </div>
        )}
        {providers.twitch && (
          <a className="button twitch" href={loginUrl('twitch')}>
            {t('login.twitch')}
          </a>
        )}
        {providers.google && (
          <a className="button youtube" href={loginUrl('google')}>
            {t('login.google')}
          </a>
        )}
        {providers.telegramBotUsername && !providers.telegramBrowserLogin && (
          <details><summary>{t('login.telegramWidget')}</summary><TelegramLoginButton botUsername={providers.telegramBotUsername} returnUrl={returnUrl} language={i18n.language} /></details>
        )}
      </div>

      {providers.password && (providers.telegramBrowserLogin ? <details><summary>{t('login.other')}</summary><PasswordForm onLoggedIn={onLoggedIn} /></details> : <PasswordForm onLoggedIn={onLoggedIn} />)}

      {providers.developmentLogin && (
        <form
          className="inline-form"
          onSubmit={async (event) => {
            event.preventDefault()
            try {
              await api.developmentLogin(developmentName)
              onLoggedIn()
            } catch (exception) {
              setError(i18n.error(exception))
            }
          }}
        >
          <span className="muted small">{t('login.development')}</span>
          <input
            value={developmentName}
            onChange={(event) => setDevelopmentName(event.target.value)}
            placeholder={t('login.anyName')}
            maxLength={32}
          />
          <button type="submit" disabled={!developmentName.trim()}>
            {t('login.submit')}
          </button>
        </form>
      )}

      {nothingConfigured && <p className="error">{t('login.none')}</p>}
      {error && <p className="error">{error}</p>}
    </section>
  )
}
