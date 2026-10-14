import type { Catalog, Citizen, StatRequirement, Structure } from '../api'
import { useI18n } from '../i18n'
import { CharacterPortrait } from './CharacterPortrait'
import { citizenTitle } from './citizenTitle'
import { Hint } from './Hint'
import { restHints } from './needHints'

export function CharacterSheet({ citizen, catalog, structures }: { citizen: Citizen; catalog: Catalog; structures: Structure[] }) {
  const i18n = useI18n()
  const { t } = i18n
  const describeNeeds = (needs: StatRequirement[]) => needs.map((r) => `${i18n.stat(r.stat)} ${r.minimum}`).join(', ')
  const { rules } = catalog
  const rest = restHints(citizen, catalog, structures, i18n, citizen.energy)
  const streakBonus = Math.min(citizen.streak * rules.streakPerRenewal, rules.streakMaximumBonus)
  return (
    <section className="card">
      <h3 className="character-title">
        {citizen.appearance && <CharacterPortrait appearance={citizen.appearance} scale={3} />}
        <span>
          {citizen.name} <span className="muted">— {citizenTitle(citizen, i18n)}</span>
        </span>
      </h3>
      <div className="character-summary">
        <div>
          <span className="muted small">
            <Hint text={rest.energy}>{t('character.energy')}</Hint>
          </span>
          <div className="bar energy">
            <div style={{ width: `${(citizen.energy / citizen.maximumEnergy) * 100}%` }} />
          </div>
          <span className="small">
            {Math.floor(citizen.energy)} / {citizen.maximumEnergy}
          </span>
        </div>
        <div>
          <span className="muted small">
            <Hint text={t('hint.renown', { statMaximum: rules.statMaximum })}>{t('character.renown')}</Hint>
          </span>
          <strong>{citizen.renown}</strong>
        </div>
        <div>
          <span className="muted small">
            <Hint
              text={t('hint.streak', {
                grace: rules.graceSeconds,
                per: i18n.amount(rules.streakPerRenewal * 100),
                maximum: i18n.amount(rules.streakMaximumBonus * 100),
                bonus: i18n.amount(streakBonus * 100),
              })}
            >
              {t('character.streak')}
            </Hint>
          </span>
          <strong>{citizen.streak}</strong>
        </div>
        <div>
          <span className="muted small">
            <Hint text={t('hint.talent', { total: citizen.statTotal, cap: rules.talentCap })}>{t('character.talent')}</Hint>
          </span>
          <strong>
            {citizen.statTotal} / {catalog.rules.talentCap}
          </strong>
        </div>
        <div>
          <span className="muted small">{t('character.sleeps')}</span>
          <strong>
            <Hint text={rest.place}>{rest.underTrees ? t('character.underTrees') : t('character.inShelter')}</Hint>
          </strong>
          <span className="small muted">
            <Hint text={rest.restSpeed}>{t('character.rest', { percent: rest.percent })}</Hint>
          </span>
        </div>
      </div>
      {citizen.isHungry ? (
        <p className="warning small">{t('character.hungry')}</p>
      ) : (
        <p className="small muted">{t('character.fed')}</p>
      )}
      <p className="small muted">{t('character.eating', { cycles: catalog.rules.cyclesPerMeal })}</p>
      {citizen.injuredUntil && Date.parse(citizen.injuredUntil) > Date.parse(citizen.serverNow) && (
        <p className="warning small">
          {t('injury.title', {
            time: new Date(citizen.injuredUntil).toLocaleTimeString(i18n.language, { hour: '2-digit', minute: '2-digit' }),
          })}
          . {t('injury.hint')}
        </p>
      )}
      <h4>{t('fight.title')}</h4>
      <p className="small" title={t('fight.powerHint')}>
        {t('fight.power', { power: i18n.amount(citizen.power) })} ·{' '}
        {citizen.weapon || citizen.armor
          ? t('fight.gear', {
              gear: [citizen.weapon, citizen.armor]
                .filter((kind): kind is string => kind !== null)
                .map((kind) => i18n.resource(kind))
                .concat(citizen.arrows > 0 ? [t('fight.arrows', { count: citizen.arrows })] : [])
                .join(', '),
            })
          : t('fight.unarmed')}
      </p>
      <p className="small muted">{t('fight.powerHint')}</p>
      <table className="stats">
        <tbody>
          {citizen.stats.map((stat) => (
            <tr key={stat.stat}>
              <td>
                <Hint text={i18n.statHint(stat.stat)}>{i18n.stat(stat.stat)}</Hint>
              </td>
              <td className="stat-value">{stat.level}</td>
              <td className="stat-progress">
                {stat.experienceToNext > 0 ? (
                  <>
                    <div className="bar">
                      <div style={{ width: `${(stat.experience / stat.experienceToNext) * 100}%` }} />
                    </div>
                    <span className="muted small">
                      {stat.experience} / {stat.experienceToNext}
                    </span>
                  </>
                ) : (
                  <span className="muted small">{t('character.maximum')}</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <h4>{t('character.skills')}</h4>
      {citizen.skills.length === 0 ? (
        <p className="small muted">{t('character.noSkills')}</p>
      ) : (
        <table className="stats">
          <tbody>
            {citizen.skills.map((skill) => {
              const needs = skill.nextLevel.filter((r) => r.minimum > 0)
              return (
                <tr key={skill.skill} title={needs.length > 0 ? t('character.nextLevelNeeds', { stats: describeNeeds(needs) }) : undefined}>
                  <td>{i18n.skill(skill.skill)}</td>
                  <td className="stat-value">{skill.level}</td>
                  <td className="stat-progress">
                    {skill.experienceToNext > 0 ? (
                      <>
                        <div className="bar">
                          <div style={{ width: `${(skill.experience / skill.experienceToNext) * 100}%` }} />
                        </div>
                        <span className="muted small">
                          {skill.experience} / {skill.experienceToNext}
                        </span>
                        {skill.waitsForStats && (
                          <span className="warning small"> {t('character.waitsForStats', { stats: describeNeeds(needs) })}</span>
                        )}
                      </>
                    ) : (
                      <span className="muted small">{t('character.maximum')}</span>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}
      {citizen.cycleTired && <p className="warning small">{t('character.tired')}</p>}
    </section>
  )
}
