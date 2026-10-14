import { useState } from 'react'
import { api, INVITE_PARAMETER, type Appearance, type Gender, type Me } from '../api'
import { GENDERS, HAIR_COLORS, OUTFIT_COLORS, SKIN_TONES, randomAppearance, stylesFor } from '../game/appearance'
import { useI18n } from '../i18n'
import { CharacterPortrait } from './CharacterPortrait'

function Swatches({
  colors,
  value,
  onChange,
  label,
  noneLabel,
}: {
  colors: (string | null)[]
  value: number
  onChange: (index: number) => void
  label: string
  noneLabel?: string
}) {
  return (
    <div className="field">
      {label}
      <div className="swatches" role="radiogroup" aria-label={label}>
        {colors.map((color, index) => (
          <button
            key={index}
            type="button"
            role="radio"
            aria-checked={value === index}
            className={`swatch ${value === index ? 'active' : ''} ${color ? '' : 'original'}`}
            style={color ? { background: `#${color}` } : undefined}
            title={color ? undefined : noneLabel}
            onClick={() => onChange(index)}
          />
        ))}
      </div>
    </div>
  )
}

/** Created once after signing up; every village the player opens gets a citizen with this name and look. */
export function CreateCharacterForm({ suggestedName, onCreated }: { suggestedName: string; onCreated: (me: Me) => void }) {
  const i18n = useI18n()
  const { t } = i18n
  const [name, setName] = useState(suggestedName)
  const [look, setLook] = useState<Appearance>(randomAppearance)
  const [error, setError] = useState<string | null>(null)
  const styles = stylesFor(look.gender)
  const change = (patch: Partial<Appearance>) => setLook((current) => ({ ...current, ...patch }))

  return (
    <form
      className="card character-creation"
      onSubmit={async (event) => {
        event.preventDefault()
        try {
          onCreated(await api.createCharacter(name, look))
        } catch (exception) {
          setError(i18n.error(exception))
        }
      }}
    >
      <h3>{t('create.title')}</h3>
      <p className="small">{t('create.intro')}</p>
      <div className="creation-layout">
        <div className="creation-preview">
          <CharacterPortrait appearance={look} scale={8} />
          <button type="button" onClick={() => setLook(randomAppearance())}>
            {t('create.random')}
          </button>
        </div>
        <div className="creation-options">
          <label className="field">
            {t('create.name')}
            <input value={name} onChange={(event) => setName(event.target.value)} maxLength={32} required />
          </label>
          <div className="field">
            {t('create.gender')}
            <div className="chips">
              {GENDERS.map((gender: Gender) => (
                <button
                  key={gender}
                  type="button"
                  className={`chip ${look.gender === gender ? 'active' : ''}`}
                  onClick={() => change({ gender, style: Math.min(look.style, stylesFor(gender) - 1) })}
                >
                  {t(gender === 'Male' ? 'create.male' : 'create.female')}
                </button>
              ))}
            </div>
          </div>
          <div className="field">
            {t('create.style')}
            <div className="stepper">
              <button type="button" onClick={() => change({ style: (look.style + styles - 1) % styles })} aria-label={t('create.previous')}>
                ◀
              </button>
              <span>
                {look.style + 1} / {styles}
              </span>
              <button type="button" onClick={() => change({ style: (look.style + 1) % styles })} aria-label={t('create.next')}>
                ▶
              </button>
            </div>
          </div>
          <Swatches
            label={t('create.skin')}
            colors={SKIN_TONES.map(([light]) => light)}
            value={look.skinTone}
            onChange={(skinTone) => change({ skinTone })}
          />
          <Swatches
            label={t('create.hair')}
            colors={HAIR_COLORS.map(([light]) => light)}
            value={look.hairColor}
            onChange={(hairColor) => change({ hairColor })}
          />
          <Swatches
            label={t('create.outfit')}
            colors={OUTFIT_COLORS.map((tones) => tones?.[1] ?? null)}
            value={look.outfitColor}
            onChange={(outfitColor) => change({ outfitColor })}
            noneLabel={t('create.outfitOriginal')}
          />
        </div>
      </div>
      <button type="submit" className="primary" disabled={!name.trim()}>
        {t('create.submit')}
      </button>
      {error && <p className="error">{error}</p>}
    </form>
  )
}

/** Public villages are joined as soon as they open; a private one first asks for the Tsar's invite code. */
export function JoinVillageForm({
  joining,
  error,
  onJoin,
}: {
  joining: boolean
  error: string | null
  onJoin: (inviteCode: string) => void
}) {
  const { t } = useI18n()
  const [inviteCode, setInviteCode] = useState(() => new URLSearchParams(window.location.search).get(INVITE_PARAMETER) ?? '')
  return (
    <form
      className="card"
      onSubmit={(event) => {
        event.preventDefault()
        onJoin(inviteCode.trim())
      }}
    >
      <h3>{t('join.title')}</h3>
      <label className="field invite-field">
        {t('create.inviteCode')}
        <input
          value={inviteCode}
          onChange={(event) => setInviteCode(event.target.value.toUpperCase())}
          maxLength={16}
          autoComplete="off"
          spellCheck={false}
          required
        />
        <span className="muted small">{t('create.privateHint')}</span>
      </label>
      <button type="submit" className="primary" disabled={joining || !inviteCode.trim()}>
        {t('join.submit')}
      </button>
      {error && <p className="error">{error}</p>}
    </form>
  )
}
