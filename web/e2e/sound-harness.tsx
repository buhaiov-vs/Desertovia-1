import { useState } from 'react'
import { createRoot } from 'react-dom/client'
import { cityRaidPlaying, backgroundMusic } from '../src/game/backgroundMusic'
import { I18nProvider } from '../src/i18n'
import { BackgroundMusic, MusicSettings } from '../src/ui/BackgroundMusic'
import '../src/index.css'

const stand = window as unknown as {
  villageMusic: {
    snapshot: () => ReturnType<ReturnType<typeof backgroundMusic>['snapshot']>
    cityRaidPlaying: typeof cityRaidPlaying
  }
}
stand.villageMusic = {
  snapshot: () => backgroundMusic().snapshot(),
  cityRaidPlaying,
}

function Stand() {
  const [phase, setPhase] = useState<string | null>(null)
  const battles = phase === null ? [] : [{ finishedAt: phase === 'Finished' ? '2026-10-18T00:00:00Z' : null, phase }]
  return (
    <I18nProvider>
      <div className="game-root">
        <BackgroundMusic battles={battles} />
        <button type="button" style={{ position: 'absolute', zIndex: 50, left: 8, top: 8 }} onClick={() => setPhase((current) => current === 'Fighting' ? null : 'Fighting')}>
          raid
        </button>
        <div style={{ position: 'absolute', zIndex: 30, left: 12, top: 48, width: 'min(320px, calc(100% - 70px))' }}>
          <MusicSettings />
        </div>
      </div>
    </I18nProvider>
  )
}

createRoot(document.getElementById('app') ?? document.body).render(<Stand />)
