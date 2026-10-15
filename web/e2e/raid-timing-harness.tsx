import { Container } from 'pixi.js'
import { EnemyLayer } from '../src/game/enemies'
import { formatRecoveryTime } from '../src/ui/recoveryTime'
import type { Raid } from '../src/api'

const layer = new Container()
const enemies = new EnemyLayer(layer)
Object.assign(window, { raidTiming: {
  formatRecoveryTime,
  update: (raids: Raid[], now: number, seconds = 0.1) => {
    enemies.setRaids(raids)
    enemies.update(seconds, now, true)
    return { count: layer.children.length, positions: layer.children.map(child => [child.x, child.y]), front: enemies.front(now) }
  },
} })
