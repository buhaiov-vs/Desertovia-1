import assert from 'node:assert/strict'
import { test } from 'node:test'
import { chunkOf, chunksCovering, chunksNear, LAND_CHUNK } from '../src/game/landChunk.ts'

const land = { x: -44, y: -32, width: 120, height: 90 }

test('chunk coordinates match the fog blocks, including tiles left of zero', () => {
  assert.equal(LAND_CHUNK, 32)
  assert.equal(chunkOf(0), 0)
  assert.equal(chunkOf(31), 0)
  assert.equal(chunkOf(32), 1)
  assert.equal(chunkOf(-1), -1)
  assert.equal(chunkOf(-32), -1)
  assert.equal(chunkOf(-33), -2)
  assert.equal(chunkOf(-44), -2)
})

test('the current land is covered once by clipped chunks', () => {
  const chunks = chunksCovering(land)
  const seen = new Set<string>()
  for (const chunk of chunks) {
    for (let y = 0; y < chunk.height; y++) {
      for (let x = 0; x < chunk.width; x++) {
        const key = `${chunk.originX + x},${chunk.originY + y}`
        assert.equal(seen.has(key), false, key)
        seen.add(key)
      }
    }
  }
  assert.equal(seen.size, land.width * land.height)
  assert.equal(seen.has('-44,-32'), true)
  assert.equal(seen.has('75,57'), true)
  assert.equal(chunks.some((chunk) => chunk.x === -2 && chunk.originX === -44 && chunk.width === 12), true)
  assert.equal(chunks.some((chunk) => chunk.x === 0 && chunk.y === 0 && chunk.width === 32 && chunk.height === 32), true)
})

test('a view around the camp does not ask for the far west block', () => {
  const near = chunksNear({ x: 0, y: 0, width: 34, height: 26 }, land)
  assert.equal(near.some((chunk) => chunk.x === -2), false)
  assert.ok(near.length < chunksCovering(land).length)
  const all = chunksNear({ x: land.x, y: land.y, width: land.width, height: land.height }, land, 0)
  assert.equal(all.length, chunksCovering(land).length)
})
