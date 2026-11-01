import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from 'react'
import type { Catalog, Citizen, CitizenSummary, TileArea, TilePoint, VillageState } from '../api'
import { useI18n } from '../i18n'
import type { UiKey } from '../i18n/en'
import type { SpawnPoint } from './spawnRings'
import { VillageWorld } from './VillageWorld'
import type { WalkBlock, WorldTarget } from './worldMap'

/** English uses one or many. Russian also uses the few form (2–4, except 12–14). */
function tileUnitKey(count: number, language: string): UiKey {
  const mod10 = count % 10
  const mod100 = count % 100
  if (language === 'ru') {
    if (mod10 === 1 && mod100 !== 11) return 'hud.tileOne'
    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return 'hud.tileFew'
    return 'hud.tileMany'
  }
  return count === 1 ? 'hud.tileOne' : 'hud.tileMany'
}

export interface GameCanvasHandle {
  say(ownerId: string, text: string, seconds?: number): void
  /** Above 1 zooms in, below 1 zooms out, around the screen centre. */
  zoom(factor: number): void
  /** Centres the view on a walker. */
  locate(walkerId: string): void
  /** What stops a walker from walking to a spot, in tiles; null when the way is open. */
  wayBlock(walkerId: string, spot: TilePoint): WalkBlock | null
}

export interface Placement {
  private?: boolean
  /** Building kind being placed. */
  kind: string
  /** Top-left tile of the building. */
  onPick: (x: number, y: number) => void
}

interface Props {
  village: VillageState
  citizens: CitizenSummary[]
  highlightId: string | null
  /** The signed-in player is the Tsar, so a hurt Tsar shows their own health bar. */
  viewerIsTsar?: boolean
  /** Signed-in citizen: map HP bar stays until combat health is full after dens. */
  ownCitizen?: Citizen | null
  /** Development only: forces daylight (false) or night (true); null follows the village sky. */
  nightOverride?: boolean | null
  /** Development only: no fog anywhere on this screen. */
  mapRevealed?: boolean
  /** Development only: rings and level numbers on every den, and all of them on the minimap. */
  densHighlighted?: boolean
  /** Server time minus this browser's clock, in milliseconds. */
  serverOffsetMs?: number
  obscured?: boolean
  catalog: Catalog | null
  placement?: Placement | null
  minimap?: boolean
  /** Called with the id of the citizen clicked on the map; null turns clicking off. */
  selectedEnemyId?:string|null
  /** Den protector marked with a gold ring after a left click or inspect. */
  selectedDen?: { denId: string; protector: number } | null
  onEnemyPick?:((battleId:string,unitId:string)=>void)|null
  /** Right click or long press on a raid foe: open its card without ordering an attack. */
  onEnemyInspect?: ((battleId: string, unitId: string) => void) | null
  /** Tap on a standing protector, a roaming foe, or one in the player's fight. Returns true when the tap is used. */
  onDenPick?: ((denId: string, protector: number, heroId: string | null) => boolean) | null
  /** Right click or long press on a den foe: open its card. */
  onDenInspect?: ((denId: string, protector: number, point: SpawnPoint) => void) | null
  onCitizenPick?: ((citizenId: string) => void) | null
  /** Called with what a citizen could work at where the map was clicked (null for nothing) and the clicked tile point; `walk` skips opening buildings. Null turns clicking off. */
  onWorldPick?: ((target: WorldTarget | null, screen: { x: number; y: number }, point: TilePoint, walk?: boolean) => void) | null
  /** Which clickable targets show the pointer cursor; null means every target. */
  worldPickable?: ((target: WorldTarget) => boolean) | null
  /** Tiles outlined on the map. */
  selection?: TileArea | null
}

export const GameCanvas = forwardRef<GameCanvasHandle, Props>(function GameCanvas(
  {
    village,
    citizens,
    highlightId,
    viewerIsTsar = false,
    ownCitizen = null,
    nightOverride = null,
    mapRevealed = false,
    densHighlighted = false,
    serverOffsetMs = 0,
    obscured = false,
    catalog,
    placement = null,
    minimap = true,
    selectedEnemyId = null,
    selectedDen = null,
    onEnemyPick = null,
    onEnemyInspect = null,
    onDenPick = null,
    onDenInspect = null,
    onCitizenPick = null,
    onWorldPick = null,
    worldPickable = null,
    selection = null,
  },
  ref,
) {
  const hostRef = useRef<HTMLDivElement>(null)
  const [world, setWorld] = useState<VillageWorld | null>(null)
  const { t, language, amount, building, storage } = useI18n()
  const priestName = t('priest.name')
  const tsarTitle = t('hud.tsar')
  const governorTitle = t('hud.governor')

  useEffect(() => {
    const host = hostRef.current
    if (!host) return
    let disposed = false
    let created: VillageWorld | null = null
    VillageWorld.create(host, { minimap }).then((instance) => {
      if (disposed) {
        instance.destroy()
        return
      }
      created = instance
      setWorld(instance)
      // Development builds only: lets browser checks draw made-up village states.
      if (import.meta.env.DEV) Object.assign(window, { desertodWorld: instance })
    })
    return () => {
      disposed = true
      created?.destroy()
      setWorld(null)
    }
  }, [minimap])

  useEffect(() => world?.setCatalog(catalog), [world, catalog])
  useEffect(() => world?.setVillage(village), [world, village])
  useEffect(() => world?.setCitizens(citizens, highlightId), [world, citizens, highlightId])
  useEffect(() => world?.setViewer(viewerIsTsar), [world, viewerIsTsar])
  useEffect(() => {
    if (!world) return
    const maximum = ownCitizen?.maximumHealth
    const health = ownCitizen?.health
    if (!ownCitizen || maximum == null || health == null || !(maximum > 0)) {
      world.setOwnVitals(null)
      return
    }
    const updatedAt = Date.parse(ownCitizen.healthUpdatedAt ?? ownCitizen.serverNow)
    world.setOwnVitals({
      health,
      maximumHealth: maximum,
      updatedAt: Number.isFinite(updatedAt) ? updatedAt : Date.now(),
      fullRegenerationMinutes: catalog?.rules.combatHealthFullRegenerationMinutes ?? 30,
    })
  }, [world, ownCitizen, catalog])
  useEffect(() => world?.setNightOverride(nightOverride), [world, nightOverride])
  useEffect(() => world?.setMapRevealed(mapRevealed), [world, mapRevealed])
  useEffect(() => world?.setDensHighlighted(densHighlighted), [world, densHighlighted])
  useEffect(() => world?.setServerOffset(serverOffsetMs), [world, serverOffsetMs])
  useEffect(() => world?.setObscured(obscured), [world, obscured])
  useEffect(() => world?.setPriestName(priestName), [world, priestName])
  useEffect(() => world?.setTitles(tsarTitle, governorTitle), [world, tsarTitle, governorTitle])
  useEffect(() => {
    world?.setDistanceText((tiles) => {
      const count = Math.round(tiles)
      if (count < 1) return t('hud.inVillage')
      return t('hud.villageDistance', { distance: amount(count), unit: t(tileUnitKey(count, language)) })
    })
  }, [world, t, language, amount])
  useEffect(
    () => world?.setBuildingNames((kind, isStorage) => (isStorage ? storage(kind) : building(kind).name)),
    [world, building, storage],
  )
  useEffect(() => world?.setPlacement(placement?.onPick ?? null, placement?.kind, placement?.private), [world, placement])
  useEffect(()=>world?.setEnemySelection(selectedEnemyId),[world,selectedEnemyId])
  useEffect(() => world?.setDenSelection(selectedDen ?? null), [world, selectedDen])
  useEffect(()=>world?.setEnemyPick(onEnemyPick),[world,onEnemyPick])
  useEffect(() => world?.setEnemyInspect(onEnemyInspect), [world, onEnemyInspect])
  useEffect(() => world?.setDenPick(onDenPick), [world, onDenPick])
  useEffect(() => world?.setDenInspect(onDenInspect), [world, onDenInspect])
  useEffect(() => world?.setCitizenPick(onCitizenPick), [world, onCitizenPick])
  useEffect(() => world?.setWorldPick(onWorldPick), [world, onWorldPick])
  useEffect(() => world?.setWorldPickable(worldPickable), [world, worldPickable])
  useEffect(() => world?.setSelection(selection), [world, selection])

  useImperativeHandle(
    ref,
    () => ({
      say: (ownerId, text, seconds) => world?.say(ownerId, text, seconds),
      zoom: (factor) => world?.zoomBy(factor),
      locate: (walkerId) => void world?.locate(walkerId),
      wayBlock: (walkerId, spot) => world?.wayBlock(walkerId, spot) ?? null,
    }),
    [world],
  )

  return <div ref={hostRef} className="game-canvas" />
})
