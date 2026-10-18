import { appBase } from './paths'
import { api } from './api'
import { lazy, Suspense, useEffect, useState } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router'
import { useI18n } from './i18n'
import { CreateVillagePage } from './pages/CreateVillagePage'
import { HomePage } from './pages/HomePage'
import { ReportButton } from './reports/ReportButton'
import { UpdatePrompt } from './ui/UpdatePrompt'

// The map pages carry the PixiJS renderer; the home page does not wait for it.
const GamePage = lazy(() => import('./pages/GamePage').then((module) => ({ default: module.GamePage })))
const DungeonPage = lazy(() => import('./pages/DungeonPage').then((module) => ({ default: module.DungeonPage })))

function Loading() {
  const i18n = useI18n()
  return <div className="splash">{i18n.t('loading')}</div>
}

function PrimaryVillage() {
  const [primary,setPrimary]=useState<string|null|undefined>(undefined)
  useEffect(()=>{void api.providers().then(p=>setPrimary(p.primaryVillage??null),()=>setPrimary(null))},[])
  if(primary===undefined)return <Loading/>
  return primary?<GamePage primarySlug={primary}/>:<HomePage/>
}

export function App() {
  return (
    <BrowserRouter basename={appBase || "/"}>
      <Suspense fallback={<Loading />}>
        <Routes>
          <Route path="/" element={<PrimaryVillage />} />
          <Route path="/new" element={<CreateVillagePage />} />
          <Route path="/v/:slug" element={<GamePage />} />
          <Route path="/v/:slug/dungeon/:runId" element={<DungeonPage />} />
        </Routes>
      </Suspense>
      <UpdatePrompt />
      <ReportButton />
    </BrowserRouter>
  )
}
