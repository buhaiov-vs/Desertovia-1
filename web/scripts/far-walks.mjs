// Writes e2e/fixtures/far-walks.json: sample walks across the far land with the turns the browser takes.
// The server's FarWalkTests and the browser's farRoute test both check against it.
// Run from web/: node scripts/far-walks.mjs
import { writeFileSync } from 'node:fs'
import { farBlocksKind, farKind } from '../src/game/farWilds.ts'
import { farRoute } from '../src/game/farRoute.ts'
import { farOpen } from '../tests/farWalk.ts'

const SEEDS = [1, 7]
const FROM = { x: 415.5, y: 13.5 }
const DISTANCES = [600, 2000, 4500]

function nearestOpen(seed, x, y) {
  for (let radius = 0; ; radius++) {
    for (let dy = -radius; dy <= radius; dy++) {
      for (let dx = -radius; dx <= radius; dx++) {
        const kind = farKind(x + dx, y + dy, seed)
        if (!farBlocksKind(kind) && kind !== 'water') return { x: x + dx + 0.5, y: y + dy + 0.5 }
      }
    }
  }
}

const walks = []
for (const seed of SEEDS) {
  for (let direction = 0; direction < 8; direction++) {
    const dx = [1, 1, 0, -1, -1, -1, 0, 1][direction]
    const dy = [0, -1, -1, -1, 0, 1, 1, 1][direction]
    for (const distance of DISTANCES) {
      const x = Math.max(-5970, Math.min(5990, Math.floor(FROM.x) + dx * distance))
      const y = Math.max(-5970, Math.min(5990, Math.floor(FROM.y) + dy * distance))
      const to = nearestOpen(seed, x, y)
      const way = farRoute(FROM, to, farOpen(seed, FROM, to))
      walks.push({ seed, from: `${FROM.x},${FROM.y}`, to: `${to.x},${to.y}`, way: way && way.map((point) => `${point.x},${point.y}`) })
    }
  }
}

writeFileSync(new URL('../e2e/fixtures/far-walks.json', import.meta.url), JSON.stringify(walks, null, 2) + '\n')
console.log(`${walks.length} walks, ${walks.filter((walk) => !walk.way).length} without a way`)
