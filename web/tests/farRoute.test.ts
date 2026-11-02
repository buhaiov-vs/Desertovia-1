import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { test } from 'node:test'
import { farRoute } from '../src/game/farRoute.ts'
import { farOpen } from './farWalk.ts'

const cases = JSON.parse(readFileSync(new URL('../e2e/fixtures/far-walks.json', import.meta.url), 'utf8')) as Array<{
  seed: number
  from: string
  to: string
  way: string[] | null
}>

function point(text: string) {
  const [x, y] = text.split(',').map(Number)
  return { x, y }
}

test('far walks take the turns the server takes', () => {
  for (const sample of cases) {
    const from = point(sample.from)
    const to = point(sample.to)
    const way = farRoute(from, to, farOpen(sample.seed, from, to))
    assert.deepEqual(way && way.map((turn) => `${turn.x},${turn.y}`), sample.way, `${sample.seed} ${sample.to}`)
  }
})

test('a walk across open ground is one straight step', () => {
  const open = () => true
  assert.deepEqual(farRoute({ x: 0.5, y: 0.5 }, { x: 900.5, y: 300.5 }, open), [{ x: 900.5, y: 300.5 }])
})

test('a walk goes around a wall through its only gap', () => {
  const open = (x: number, y: number) => x !== 50 || y === 90
  const way = farRoute({ x: 10.5, y: 10.5 }, { x: 90.5, y: 10.5 }, open)
  assert.ok(way && way.length > 1)
  assert.ok(way!.some((turn) => Math.floor(turn.y) >= 89))
  assert.deepEqual(way!.at(-1), { x: 90.5, y: 10.5 })
})

test('a sealed spot has no way', () => {
  const open = (x: number, y: number) => x >= 0 && y >= 0 && x < 200 && y < 200 && Math.max(Math.abs(x - 100), Math.abs(y - 100)) !== 5
  assert.equal(farRoute({ x: 10.5, y: 10.5 }, { x: 100.5, y: 100.5 }, open), null)
})
