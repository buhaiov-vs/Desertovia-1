import { lazy, Suspense } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router'
import { useI18n } from './i18n'
import { CreateVillagePage } from './pages/CreateVillagePage'
import { HomePage } from './pages/HomePage'
import { UpdatePrompt } from './ui/UpdatePrompt'

// The map pages carry the PixiJS renderer; the home page does not wait for it.
const GamePage = lazy(() => import('./pages/GamePage').then((module) => ({ default: module.GamePage })))
const DungeonPage = lazy(() => import('./pages/DungeonPage').then((module) => ({ default: module.DungeonPage })))

function Loading() {
  const i18n = useI18n()
  return <div className="splash">{i18n.t('loading')}</div>
}

export function App() {
  return (
    <BrowserRouter>
      <Suspense fallback={<Loading />}>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/new" element={<CreateVillagePage />} />
          <Route path="/v/:slug" element={<GamePage />} />
          <Route path="/v/:slug/dungeon/:runId" element={<DungeonPage />} />
        </Routes>
      </Suspense>
      <UpdatePrompt />
    </BrowserRouter>
  )
}
