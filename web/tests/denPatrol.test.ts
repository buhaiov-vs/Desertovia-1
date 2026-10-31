import assert from 'node:assert/strict'
import { test } from 'node:test'
import {
  PATROL_LEG_SECONDS,
  PATROL_RADIUS,
  PATROL_STRIDE,
  patrolAt,
  postRadius,
  standsStill,
} from '../src/game/denPatrol.ts'

const count = 8

test('walking protectors mill near their posts and stay inside the den disk', () => {
  const center = { x: 400.5, y: 250.5 }
  const start = 1_728_000_000_000
  let last = patrolAt(center.x, center.y, 0, count, 1, start)
  let travelled = 0
  for (let step = 1; step <= 120; step++) {
    const here = patrolAt(center.x, center.y, 0, count, 1, start + step * 1000)
    assert.ok(Math.hypot(here.x - center.x, here.y - center.y) <= PATROL_RADIUS + PATROL_STRIDE + 1e-9)
    const moved = Math.hypot(here.x - last.x, here.y - last.y)
    assert.ok(moved <= (2 * PATROL_STRIDE) / (PATROL_LEG_SECONDS * 0.75) + 1e-9)
    travelled += moved
    last = here
  }
  assert.ok(travelled > 4, `travelled ${travelled}`)
})

test('eight posts stay spread around the den, and a lich stays on its post', () => {
  const center = { x: 10.5, y: 20.5 }
  const homes = Array.from({ length: count }, (_, index) => patrolAt(center.x, center.y, index, count, 1, 0))
  let farthest = 0
  for (let a = 0; a < count; a++) {
    for (let b = a + 1; b < count; b++) {
      farthest = Math.max(farthest, Math.hypot(homes[a].x - homes[b].x, homes[a].y - homes[b].y))
    }
  }
  assert.ok(farthest > PATROL_RADIUS, `farthest ${farthest}`)

  const post = patrolAt(center.x, center.y, 3, count, 30, 0)
  const still = patrolAt(center.x, center.y, 3, count, 30, 20_000)
  assert.deepEqual(still, post)
  assert.equal(standsStill(30), true)
  assert.equal(standsStill(1), false)
  assert.equal(postRadius(14), 3)
  assert.equal(postRadius(30), 5)
})

test('rooted protectors leave no gap on their ring wider than their sight', () => {
  const center = { x: 400.5, y: 250.5 }
  for (const level of [14, 30]) {
    const radius = postRadius(level)
    const gap = radius * Math.sin(Math.PI / count)
    assert.ok(gap <= 4)
    for (let index = 0; index < count; index++) {
      const guard = patrolAt(center.x, center.y, index, count, level, 0)
      assert.ok(Math.abs(Math.hypot(guard.x - center.x, guard.y - center.y) - radius) < 1e-6)
    }
  }
})
