import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { test } from 'node:test'
import { SPAWN_LEVELS, spawnPoints, spotsAround } from '../src/game/spawnRings.ts'

const side = 12_000
const ring = { camp: { x: 15.5, y: 13.5 }, land: { x: 15 - side / 2, y: 13 - side / 2, width: side, height: side } }
const cases = JSON.parse(readFileSync(new URL('../e2e/fixtures/spawn-rings.json', import.meta.url), 'utf8')) as Array<{ seed: number; points: string[] }>

test('dens match the ones the server places', () => {
  for (const sample of cases) {
    assert.deepEqual(spawnPoints(sample.seed, ring).map((point) => `${point.id} ${point.x},${point.y}`), sample.points)
  }
})

test('eight dens on each ring to the straight edge, four corner dens past it', () => {
  const points = spawnPoints(7, ring)
  assert.equal(SPAWN_LEVELS.length, 30)
  assert.equal(points.filter((point) => point.level === 1).length, 8)
  assert.equal(points.filter((point) => point.level === 22).length, 8)
  assert.equal(points.filter((point) => point.level === 23).length, 4)
  assert.equal(points.filter((point) => point.level === 30).length, 4)
  for (const point of points) {
    const floor = SPAWN_LEVELS[point.level - 1].floor
    assert.ok(Math.hypot(point.x + 0.5 - ring.camp.x, point.y + 0.5 - ring.camp.y) + 1 >= floor, point.id)
  }
})

test('a den has eight spots two tiles out', () => {
  const den = spawnPoints(1, ring)[0]
  const spots = spotsAround(den)
  assert.equal(spots.length, 8)
  for (const spot of spots) assert.ok(Math.abs(Math.hypot(spot.x - den.x - 0.5, spot.y - den.y - 0.5) - 2) < 1e-9)
})
