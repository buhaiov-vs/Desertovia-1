import { DungeonWorld } from '../src/game/DungeonWorld'
import type { DungeonSnapshot, EnemyKind } from '../src/api'

const host = document.getElementById('app')!
host.style.width = '640px'
host.style.height = '480px'
const world = await DungeonWorld.create(host)

type FoeView = { dead: boolean; view: { visible: boolean; alpha: number } }
type Internals = {
  app: { ticker: { stop: () => void } }
  foes: Map<number, FoeView>
  update: (ticker: { deltaMS: number }) => void
}

const stand = world as unknown as Internals
stand.app.ticker.stop()

function snapshot(health: number, floor = 0): DungeonSnapshot {
  return {
    runId: 'run',
    elapsed: 0,
    outcome: 'Pending',
    heroes: [],
    foes: [{ id: 1, kind: 'Skeleton' as EnemyKind, x: 3, y: 3, health, maximumHealth: 20, target: null }],
    events: [],
    floor,
    floorCount: 2,
    width: 8,
    height: 8,
    tiles: '0'.repeat(64),
    entrance: { x: 1, y: 1 },
  }
}

function read() {
  const foe = stand.foes.get(1)
  if (!foe) return null
  return { dead: foe.dead, visible: foe.view.visible, alpha: foe.view.alpha, view: foe.view }
}

Object.assign(window, {
  dungeonStand: {
    show: (health: number, floor = 0) => world.setSnapshot(snapshot(health, floor)),
    tick: (frames: number) => {
      for (let index = 0; index < frames; index++) stand.update({ deltaMS: 100 })
    },
    read,
    count: () => stand.foes.size,
  },
})
