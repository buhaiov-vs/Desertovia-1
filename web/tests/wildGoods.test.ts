import assert from 'node:assert/strict'
import { test } from 'node:test'
import { FAR_LAND_LEFT, FAR_LAND_SIDE, FAR_LAND_TOP, FAR_VALE, farKind, farTaken } from '../src/game/farWilds.ts'
import { SPREAD, SPAWN_LEVELS, spawnPoints } from '../src/game/spawnRings.ts'
import { WILD_PLACES, wildGoodAt, wildPatches } from '../src/game/wildGoods.ts'

const camp = { x: 15.5, y: 13.5 }

test('patches sit between foe rings, off mountains, and the same seed keeps them', () => {
  const patches = wildPatches(1)
  assert.equal(patches.length, 160)
  const sample = patches
    .slice()
    .sort((a, b) => a.level - b.level || a.x - b.x || a.y - b.y)
    .slice(0, 6)
    .map((patch) => `${patch.x},${patch.y},${patch.kind},${patch.level}`)
    .join('|')
  assert.equal(sample, '-355,166,herbs,1|-341,-134,herbs,1|-137,381,herbs,1|-134,-347,herbs,1|167,-354,herbs,1|167,379,herbs,1')
  assert.deepEqual(
    wildPatches(1).map((patch) => [patch.x, patch.y, patch.kind]),
    patches.map((patch) => [patch.x, patch.y, patch.kind]),
  )
  const dens = spawnPoints(1, {
    camp,
    land: { x: FAR_LAND_LEFT, y: FAR_LAND_TOP, width: FAR_LAND_SIDE, height: FAR_LAND_SIDE },
    blocked: farTaken,
  })
  const kinds = new Set<string>()
  for (const patch of patches) {
    kinds.add(patch.kind)
    const level = SPAWN_LEVELS[patch.level - 1]
    const distance = Math.hypot(patch.x + 0.5 - camp.x, patch.y + 0.5 - camp.y)
    assert.ok(distance >= level.floor + 20 && distance <= level.floor + SPREAD + 20, `${patch.level} ${distance}`)
    assert.ok(
      patch.x + 1 < FAR_VALE.x || patch.y + 1 < FAR_VALE.y || patch.x >= FAR_VALE.x + FAR_VALE.width || patch.y >= FAR_VALE.y + FAR_VALE.height,
    )
    for (let dy = 0; dy < 2; dy++) {
      for (let dx = 0; dx < 2; dx++) {
        const kind = farKind(patch.x + dx, patch.y + dy, 1)
        assert.notEqual(kind, 'mountain')
        assert.notEqual(kind, 'thicket')
        assert.equal(wildGoodAt(patch.x + dx, patch.y + dy, 1), patch.kind)
      }
    }
    assert.equal(wildGoodAt(patch.x + 2, patch.y, 1), null)
    for (const den of dens.filter((den) => den.level === patch.level)) {
      const reach = Math.floor(den.claim / 2) + 10
      const left = den.x - patch.x
      const top = den.y - patch.y
      assert.ok(left * left + top * top >= reach * reach, patch.kind)
    }
  }
  for (const kind of WILD_PLACES) assert.ok(kinds.has(kind), kind)
})
