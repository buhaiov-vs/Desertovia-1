import assert from 'node:assert/strict'
import { test } from 'node:test'
import { PATROL_RADIUS, SIGHT, patrolAt, postRadius, standsStill } from '../src/game/denPatrol.ts'

const count = 8

test('eight protectors leave no gap on the ring wider than their sight', () => {
  const center = { x: 400.5, y: 250.5 }
  for (const level of [1, 14, 30]) {
    const radius = postRadius(level)
    assert.ok(radius > 2, 'the ring stands outside the spots')
    const gap = radius * Math.sin(Math.PI / count)
    assert.ok(gap <= SIGHT)
    for (let lap = 0; lap < 8; lap++) {
      const unixMs = lap * (40_000 / 8)
      for (let step = 0; step < 64; step++) {
        const angle = (step * Math.PI * 2) / 64
        const at = { x: center.x + Math.cos(angle) * radius, y: center.y + Math.sin(angle) * radius }
        let nearest = Infinity
        for (let index = 0; index < count; index++) {
          const guard = patrolAt(center.x, center.y, index, count, level, unixMs)
          nearest = Math.min(nearest, Math.hypot(guard.x - at.x, guard.y - at.y))
        }
        assert.ok(nearest <= SIGHT)
      }
    }
  }
})

test('a walking pack moves around the den, and a lich stays on its post', () => {
  const start = patrolAt(10.5, 20.5, 0, count, 1, 0)
  const later = patrolAt(10.5, 20.5, 0, count, 1, 3_000)
  assert.ok(Math.hypot(later.x - start.x, later.y - start.y) > 1)
  assert.ok(Math.abs(Math.hypot(start.x - 10.5, start.y - 20.5) - PATROL_RADIUS) < 1e-6)

  const post = patrolAt(10.5, 20.5, 3, count, 30, 0)
  const still = patrolAt(10.5, 20.5, 3, count, 30, 20_000)
  assert.deepEqual(still, post)
  assert.equal(standsStill(30), true)
  assert.equal(standsStill(1), false)
  assert.equal(postRadius(14), 3)
  assert.equal(postRadius(30), 5)
})
