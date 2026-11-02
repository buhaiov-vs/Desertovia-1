import assert from 'node:assert/strict'
import { test } from 'node:test'
import { farBlocksKind, farKind, farOpening, farWildRoll, FAR_LAND_LEFT, FAR_LAND_SIDE, FAR_LAND_TOP, type FarKind } from '../src/game/farWilds.ts'

const DX = [1, 0, -1, 0]
const DY = [0, -1, 0, 1]

function fingerprint(seed: number) {
  let hash = 2166136261
  for (let y = FAR_LAND_TOP; y < FAR_LAND_TOP + FAR_LAND_SIDE; y += 80) {
    for (let x = FAR_LAND_LEFT; x < FAR_LAND_LEFT + FAR_LAND_SIDE; x += 80) {
      const kind = farKind(x, y, seed)
      const code = kind === 'open' ? 0 : kind === 'mountain' ? 1 : kind === 'thicket' ? 2 : kind === 'rockfall' ? 3 : 4
      hash = Math.imul(hash ^ code ^ ((x & 255) << 8) ^ (y & 255), 16777619) >>> 0
    }
  }
  return hash
}

function findBig(seed: number, accept: (kind: FarKind) => boolean, least: number) {
  const seen = new Set<string>()
  for (let y = FAR_LAND_TOP + 20; y < FAR_LAND_TOP + FAR_LAND_SIDE - 20; y += 16) {
    for (let x = FAR_LAND_LEFT + 20; x < FAR_LAND_LEFT + FAR_LAND_SIDE - 20; x += 16) {
      const key = `${x},${y}`
      if (seen.has(key) || !accept(farKind(x, y, seed))) continue
      const tiles = component(seed, { x, y }, accept)
      for (const tile of tiles) seen.add(`${tile.x},${tile.y}`)
      if (tiles.length >= least) return tiles
    }
  }
  return null
}

function find(seed: number, accept: (kind: FarKind) => boolean) {
  for (let y = FAR_LAND_TOP + 20; y < FAR_LAND_TOP + FAR_LAND_SIDE - 20; y += 48) {
    for (let x = FAR_LAND_LEFT + 20; x < FAR_LAND_LEFT + FAR_LAND_SIDE - 20; x += 48) {
      if (accept(farKind(x, y, seed))) return { x, y }
    }
  }
  return null
}

function component(seed: number, start: { x: number; y: number }, accept: (kind: FarKind) => boolean) {
  const tiles: Array<{ x: number; y: number }> = []
  const seen = new Set<string>([`${start.x},${start.y}`])
  const queue = [start]
  while (queue.length > 0 && tiles.length < 12_000) {
    const here = queue.shift()!
    tiles.push(here)
    for (let dir = 0; dir < 4; dir++) {
      const x = here.x + DX[dir]
      const y = here.y + DY[dir]
      const key = `${x},${y}`
      if (seen.has(key) || !accept(farKind(x, y, seed))) continue
      seen.add(key)
      queue.push({ x, y })
    }
  }
  return tiles
}

function spread(tiles: Array<{ x: number; y: number }>) {
  let minX = Infinity
  let maxX = -Infinity
  let minY = Infinity
  let maxY = -Infinity
  for (const tile of tiles) {
    if (tile.x < minX) minX = tile.x
    if (tile.x > maxX) maxX = tile.x
    if (tile.y < minY) minY = tile.y
    if (tile.y > maxY) maxY = tile.y
  }
  return { width: maxX - minX, height: maxY - minY }
}

test('far rolls match the server', () => {
  assert.ok(Math.abs(farWildRoll(1, 2, 3, 4) - 0.17061734478920698) < 1e-12)
  assert.equal(farKind(0, 0, 1), 'open')
})

test('the camp picture stays open', () => {
  for (let y = -32; y < 58; y += 9) {
    for (let x = -44; x < 76; x += 9) assert.equal(farKind(x, y, 1), 'open')
  }
})

test('open country around the camp stays free of far hills and rivers', () => {
  for (const seed of [1, 7]) {
    for (let y = 13 - 110; y <= 13 + 110; y += 7) {
      for (let x = 15 - 110; x <= 15 + 110; x += 7) {
        const dx = x - 15
        const dy = y - 13
        if (dx * dx + dy * dy >= 120 * 120) continue
        assert.equal(farKind(x, y, seed), 'open', `${seed} ${x},${y}`)
      }
    }
  }
})

test('hills and thickets stand in every quarter of the land', () => {
  const span = 750
  const corners = [
    [FAR_LAND_LEFT, FAR_LAND_TOP],
    [FAR_LAND_LEFT + FAR_LAND_SIDE - span, FAR_LAND_TOP],
    [FAR_LAND_LEFT, FAR_LAND_TOP + FAR_LAND_SIDE - span],
    [FAR_LAND_LEFT + FAR_LAND_SIDE - span, FAR_LAND_TOP + FAR_LAND_SIDE - span],
  ]
  for (const [x0, y0] of corners) {
    let mountain = false
    let thicket = false
    for (let y = y0; y < y0 + span && (!mountain || !thicket); y += 4) {
      for (let x = x0; x < x0 + span && (!mountain || !thicket); x += 4) {
        const kind = farKind(x, y, 1)
        if (kind === 'mountain' || kind === 'rockfall') mountain = true
        if (kind === 'thicket') thicket = true
      }
    }
    assert.ok(mountain, `mountain near ${x0},${y0}`)
    assert.ok(thicket, `thicket near ${x0},${y0}`)
  }
})

test('mountains, thickets and rivers are long and not straight', () => {
  const mountain = component(1, find(1, (kind) => kind === 'mountain' || kind === 'rockfall')!, (kind) => kind === 'mountain' || kind === 'rockfall')
  const mountainSpread = spread(mountain)
  assert.ok(mountain.length > 400, `mountain ${mountain.length}`)
  assert.ok(mountainSpread.width > 40 && mountainSpread.height > 40, `mountain ${mountainSpread.width}x${mountainSpread.height}`)
  assert.ok(mountain.some((tile) => farKind(tile.x, tile.y, 1) === 'rockfall'))

  const river = component(1, find(1, (kind) => kind === 'water')!, (kind) => kind === 'water')
  const riverSpread = spread(river)
  assert.ok(river.length > 150, `river ${river.length}`)
  assert.ok(riverSpread.width > 20 && riverSpread.height > 20, `river ${riverSpread.width}x${riverSpread.height}`)

  const thicket = findBig(1, (kind) => kind === 'thicket', 80)
  assert.ok(thicket, 'no long thicket')
  const thicketSpread = spread(thicket!)
  assert.ok(thicketSpread.width > 15 && thicketSpread.height > 15, `thicket ${thicketSpread.width}x${thicketSpread.height}`)
})

test('a river is water, and a gap in a wall is open ground', () => {
  assert.equal(farBlocksKind('water'), false)
  assert.equal(farBlocksKind('mountain'), true)
  const blocks = (x: number, y: number) => farBlocksKind(farKind(x, y, 1))
  const hit = find(1, (kind) => kind === 'mountain')!
  const opening = farOpening(hit.x, hit.y, blocks)
  if (opening) assert.equal(blocks(opening.x, opening.y), false)
})

test('seed 1 matches the server fingerprint', () => {
  assert.equal(fingerprint(1), 257289047)
})
