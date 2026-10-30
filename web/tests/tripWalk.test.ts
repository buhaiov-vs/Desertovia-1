import assert from 'node:assert/strict'
import { test } from 'node:test'
import { walkActive, walkPositionAt } from '../src/game/tripWalk.ts'

const setOut = Date.parse('2026-01-01T12:00:00Z')
const walk = {
  fromX: 0,
  fromY: 0,
  toX: 10,
  toY: 10,
  setOutAt: new Date(setOut).toISOString(),
  arrivesAt: new Date(setOut + 20_000).toISOString(),
  speedMultiplier: 1,
  way: [{ x: 10, y: 0 }, { x: 10, y: 10 }],
}

test('a walker on a trip stands where the wall clock puts it, turns included', () => {
  assert.deepEqual(walkPositionAt(walk, setOut), { x: 0, y: 0 })
  assert.deepEqual(walkPositionAt(walk, setOut + 5_000), { x: 5, y: 0 })
  assert.deepEqual(walkPositionAt(walk, setOut + 15_000), { x: 10, y: 5 })
})

test('after a long background tab the walker is at the end of the trip, not where frames stopped', () => {
  assert.deepEqual(walkPositionAt(walk, setOut + 10 * 60_000), { x: 10, y: 10 })
  assert.equal(walkActive(walk, setOut + 19_999), true)
  assert.equal(walkActive(walk, setOut + 20_000), false)
  assert.equal(walkActive(null, setOut), false)
})
