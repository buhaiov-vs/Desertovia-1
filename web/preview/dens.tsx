import { Application, Container, Text } from 'pixi.js'
import { DenLayer } from '../src/game/dens'
import { enemyFigure, setFigureMoving } from '../src/game/enemies'
import { TileSet, TILE } from '../src/game/sheets'
import { SPAWN_LEVELS, type SpawnPoint } from '../src/game/spawnRings'

const app = new Application()
await app.init({ width: 1400, height: 980, background: 0x5f8a3c, antialias: false })
document.body.appendChild(app.canvas)
await TileSet.load()
const world = new Container()
world.scale.set(1.15)
app.stage.addChild(world)
const objects = new Container()
objects.sortableChildren = true
const spots = new Container()
const marks = new Container()
world.addChild(spots, objects, marks)

const dens = new DenLayer(objects, marks)
const points: SpawnPoint[] = SPAWN_LEVELS.map((level, index) => ({
  id: `${level.level}:${index % 8}`,
  level: level.level,
  name: level.name,
  kind: level.kind,
  protectors: level.protectors,
  claim: 48,
  x: 3 + (index % 6) * 10,
  y: 6 + Math.floor(index / 6) * 11,
}))
dens.setPoints(points)
dens.markGround(spots, { originX: 0, originY: 0, width: 64, height: 64 } as never)
dens.sync({ x: 0, y: 0, width: 64, height: 64 }, () => true, { x: 100, y: 0 })
for (const point of points) {
  const label = new Text({ text: `${point.level} ${point.name}`, style: { fontSize: 18, fill: 0xffffff } })
  label.scale.set(0.2)
  label.anchor.set(0.5, 0)
  label.position.set((point.x + 0.5) * TILE, (point.y + 2.1) * TILE)
  world.addChild(label)
}
for (const [index, kind] of (['Rat', 'Boar', 'Skeleton', 'Wolf'] as const).entries()) {
  for (const moving of [false, true]) {
    const figure = enemyFigure(kind)
    figure.position.set((3 + index * 3 + (moving ? 1.3 : 0)) * TILE, 1.6 * TILE)
    setFigureMoving(figure, moving)
    objects.addChild(figure)
  }
}
