import { useState } from 'react'
import type { Catalog, Citizen, TechnologyPoll, VillageState } from '../api'
import { useI18n } from '../i18n'
import { BUILDING_SIGNS, taskIcon } from '../game/worldMap'
import { TileIcon } from './TileIcon'

/** The Tsar or a Governor ticks the available discoveries citizens will choose from. */
export function PollSetup({
  catalog,
  village,
  pending,
  error,
  onStart,
  onClose,
}: {
  catalog: Catalog
  village: VillageState
  pending: boolean
  error: string | null
  onStart: (technologyIds: string[]) => void
  onClose: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const available = new Set(village.technologies.filter((state) => state.available).map((state) => state.id))
  const technologies = catalog.technologies.filter((technology) => available.has(technology.id))
  const minimum = Math.min(2, technologies.length)
  const [picked, setPicked] = useState<Set<string>>(() => new Set())

  const toggle = (id: string) =>
    setPicked((current) => {
      const next = new Set(current)
      if (!next.delete(id)) next.add(id)
      return next
    })

  return (
    <div className="modal-backdrop" onPointerDown={(event) => event.target === event.currentTarget && onClose()}>
      <div className="modal poll" role="dialog" aria-label={t('poll.call')}>
        <header className="modal-header">
          <h2>{t('poll.call')}</h2>
          <button className="close" onClick={onClose} title={`${t('close')} (Esc)`}>
            ✕
          </button>
        </header>
        <div className="modal-body">
          <p className="small">{technologies.length === 0 ? t('poll.nothing') : t('poll.setupHint')}</p>
          <div className="task-grid">
            {technologies.map((technology) => {
              const { name, description } = i18n.technology(technology.id)
              return (
                <label key={technology.id} className={`task-card poll-option ${picked.has(technology.id) ? 'current' : ''}`}>
                  <input type="checkbox" checked={picked.has(technology.id)} onChange={() => toggle(technology.id)} />
                  <TileIcon tile={BUILDING_SIGNS[technology.researchedAt] ?? taskIcon('research-technology')} scale={2} />
                  <span className="task-text">
                    <span className="task-name">{name}</span>
                    <span className="task-meta">{description}</span>
                  </span>
                </label>
              )
            })}
          </div>
          {error && <p className="error">{error}</p>}
        </div>
        <div className="done-actions">
          <button
            className="primary"
            disabled={pending || technologies.length === 0 || picked.size < minimum}
            onClick={() => onStart(technologies.filter((technology) => picked.has(technology.id)).map((technology) => technology.id))}
          >
            {t('poll.start')}
          </button>
          {picked.size < minimum && <span className="muted small">{t('poll.pickMore', { minimum })}</span>}
        </div>
      </div>
    </div>
  )
}

/**
 * Pops up on every screen while a vote is open, and once more with the results when it closes. Citizens vote here;
 * the Tsar and viewers watch. The rulers may cancel it.
 */
export function PollPanel({
  poll,
  catalog,
  citizen,
  ruler,
  pending,
  error,
  onVote,
  onCancel,
  onClose,
}: {
  poll: TechnologyPoll
  catalog: Catalog
  /** Null for the Tsar and viewers: they cannot vote. */
  citizen: Citizen | null | undefined
  /** The Tsar, or a citizen the Tsar appointed Governor: may cancel the vote. */
  ruler: boolean
  pending: boolean
  error: string | null
  onVote: (technologyId: string) => void
  onCancel: () => void
  onClose: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const open = poll.closedAt === null
  const mine = citizen ? poll.ballots.find((ballot) => ballot.citizenId === citizen.id)?.technologyId : undefined
  const voted = poll.ballots.length
  const leading = Math.max(1, ...poll.options.map((option) => option.votes))

  return (
    <div className="modal-backdrop" onPointerDown={(event) => event.target === event.currentTarget && onClose()}>
      <div className="modal poll" role="dialog" aria-label={open ? t('poll.title') : t('poll.resultsTitle')}>
        <header className="modal-header">
          <h2>{open ? t('poll.title') : t('poll.resultsTitle')}</h2>
          <button className="close" onClick={onClose} title={`${t('close')} (Esc)`}>
            ✕
          </button>
        </header>
        <div className="modal-body">
          {open ? (
            <>
              <p className="small">
                {citizen ? (mine ? t('poll.change') : t('poll.choose')) : ruler ? t('poll.tsarWatch') : t('poll.watch')}
              </p>
              <p className="small muted">
                {t('poll.progress', { voted, electorate: poll.electorate, needed: poll.votesNeeded })}
              </p>
            </>
          ) : (
            <>
              {poll.winner && (
                <p>
                  <strong>{t('poll.chosen', { technology: i18n.technology(poll.winner).name })}</strong>
                </p>
              )}
              <p className="small muted">{t('poll.turnout', { voted, electorate: poll.electorate })}</p>
            </>
          )}
          <div className="task-grid">
            {poll.options.map((option) => {
              const technology = catalog.technologies.find((candidate) => candidate.id === option.technologyId)
              const { name, description } = i18n.technology(option.technologyId)
              const chosen = open ? mine === option.technologyId : poll.winner === option.technologyId
              return (
                <div key={option.technologyId} className={`task-card ${chosen ? 'current' : ''}`}>
                  <TileIcon
                    tile={(technology && BUILDING_SIGNS[technology.researchedAt]) ?? taskIcon('research-technology')}
                    scale={2}
                  />
                  <span className="task-text">
                    <span className="task-name">{name}</span>
                    <span className="task-meta">{description}</span>
                    <span className="task-meta">{t('poll.votes', { count: option.votes })}</span>
                    <div className="bar gold thin">
                      <div style={{ width: `${(option.votes / leading) * 100}%` }} />
                    </div>
                    {open && citizen && (
                      <span className="task-actions">
                        {mine === option.technologyId ? (
                          <span className="muted small">{t('poll.yourVote')}</span>
                        ) : (
                          <button className="small-button primary" disabled={pending} onClick={() => onVote(option.technologyId)}>
                            {t('poll.vote')}
                          </button>
                        )}
                      </span>
                    )}
                  </span>
                </div>
              )
            })}
          </div>
          {error && <p className="error">{error}</p>}
        </div>
        <div className="done-actions">
          {open ? (
            <>
              {ruler && (
                <button disabled={pending} onClick={onCancel}>
                  {t('poll.cancel')}
                </button>
              )}
              <button onClick={onClose}>{t('poll.hide')}</button>
            </>
          ) : (
            <button className="primary" onClick={onClose}>
              {t('poll.ok')}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}
