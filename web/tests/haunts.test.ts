import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { test } from 'node:test'
import { CELL, DEN_CLEARANCE, LEG_SECONDS, VALE_CLEARANCE, WANDER_RADIUS, fromVale, hauntAt, hauntsIn, wanderAt } from '../src/game/haunts.ts'
import { SPAWN_LEVELS, spawnPoints } from '../src/game/spawnRings.ts'

const side = 24_000
const ring = { camp: { x: 15.5, y: 13.5 }, land: { x: 15 - side / 2, y: 13 - side / 2, width: side, height: side } }
const cases = JSON.parse(readFileSync(new URL('../e2e/fixtures/haunts.json', import.meta.url), 'utf8')) as Array<{ seed: number; foes: string[] }>

test('roaming foes match the ones the server places', () => {
  for (const sample of cases) {
    const dens = spawnPoints(sample.seed, ring)
    const foes = sample.foes.map((line) => {
      const [, x, y] = line.split(' ')[0].split(':').map(Number)
      const foe = hauntAt(sample.seed, x, y, ring, dens)
      return foe ? `${foe.id} ${foe.level} ${foe.x},${foe.y}` : `h:${x}:${y} none`
    })
    assert.deepEqual(foes, sample.foes)
  }
})

test('nine in ten foes are of their band, the rest one level off', () => {
  const dens = spawnPoints(7, ring)
  let own = 0
  let off = 0
  for (let cellY = -150; cellY <= 150; cellY++) {
    for (let cellX = -150; cellX <= 150; cellX += 3) {
      const foe = hauntAt(7, cellX, cellY, ring, dens)
      if (!foe) continue
      const distance = Math.hypot(foe.x + 0.5 - ring.camp.x, foe.y + 0.5 - ring.camp.y)
      const band = Math.max(1, SPAWN_LEVELS.filter((level) => level.floor <= distance).length)
      assert.ok(Math.abs(foe.level - band) <= 1, foe.id)
      if (foe.level === band) own++
      else off++
      assert.ok(dens.every((den) => Math.hypot(den.x - foe.x, den.y - foe.y) >= DEN_CLEARANCE), foe.id)
    }
  }
  const share = off / (own + off)
  assert.ok(share > 0.07 && share < 0.13, `off-level share ${share}`)
})

test('a foe wanders near its tile and keeps moving, without jumps', () => {
  const dens = spawnPoints(1, ring)
  const foe = hauntsIn(1, { x: -700, y: -700, width: 1400, height: 1400 }, ring, dens)[0]
  const start = 1_728_000_000_000
  let last = wanderAt(1, foe, start)
  let travelled = 0
  for (let step = 1; step <= 600; step++) {
    const here = wanderAt(1, foe, start + step * 1000)
    assert.ok(Math.hypot(here.x - foe.x - 0.5, here.y - foe.y - 0.5) <= WANDER_RADIUS + 1e-9)
    const moved = Math.hypot(here.x - last.x, here.y - last.y)
    assert.ok(moved <= (2 * WANDER_RADIUS) / (LEG_SECONDS * 0.75) + 1e-9)
    travelled += moved
    last = here
  }
  assert.ok(travelled > 20, `travelled ${travelled}`)
})

test('level 1 foes roam around the vale inside the first ring, clear of it, one per square', () => {
  const dens = spawnPoints(1, ring)
  const near = hauntsIn(1, { x: -700, y: -700, width: 1400, height: 1400 }, ring, dens)
  const inner = near.filter((foe) => Math.hypot(foe.x + 0.5 - ring.camp.x, foe.y + 0.5 - ring.camp.y) < SPAWN_LEVELS[0].floor)
  assert.ok(inner.length > 100, `${inner.length} foes inside the first ring`)
  assert.ok(inner.every((foe) => foe.level <= 2))
  for (const foe of near) {
    assert.ok(fromVale(foe.x + 0.5, foe.y + 0.5) >= VALE_CLEARANCE, foe.id)
    const [, x, y] = foe.id.split(':').map(Number)
    assert.equal(Math.floor(foe.x / CELL), x)
    assert.equal(Math.floor(foe.y / CELL), y)
  }
})
