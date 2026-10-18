import { expect, test } from './fixtures.ts'

type Corpse = { dead: boolean; visible: boolean; alpha: number; view: object }
type Stand = {
  show: (health: number, floor?: number) => void
  tick: (frames: number) => void
  read: () => Corpse | null
  count: () => number
}

test('a slain dungeon foe fades once and stays gone', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop', 'Same corpse renderer on every viewport')
  await page.goto('/e2e/dungeon-corpse-harness.html')
  await page.waitForFunction(() => !!(window as unknown as { dungeonStand?: Stand }).dungeonStand)
  const result = await page.evaluate(() => {
    const stand = (window as unknown as { dungeonStand: Stand }).dungeonStand
    stand.show(20)
    const alive = stand.read()
    stand.show(0)
    stand.tick(1)
    const fallen = stand.read()
    stand.tick(45)
    const faded = stand.read()
    const corpse = faded?.view
    stand.tick(50)
    stand.show(0)
    const again = stand.read()
    stand.show(20, 1)
    stand.tick(1)
    const nextFloor = stand.read()
    const slim = (foe: Corpse | null) => (foe ? { dead: foe.dead, visible: foe.visible, alpha: foe.alpha } : null)
    return {
      alive: slim(alive),
      fallen: slim(fallen),
      faded: slim(faded),
      again: slim(again),
      sameCorpse: corpse === again?.view,
      nextFloor: slim(nextFloor),
      count: stand.count(),
    }
  })

  expect(result.alive).toMatchObject({ dead: false, visible: true })
  expect(result.fallen).toMatchObject({ dead: true, visible: true })
  expect(result.fallen!.alpha).toBeGreaterThan(0.9)
  expect(result.faded).toMatchObject({ dead: true, visible: false })
  expect(result.faded!.alpha).toBe(0)
  expect(result.sameCorpse).toBe(true)
  expect(result.again).toMatchObject({ dead: true, visible: false, alpha: 0 })
  expect(result.nextFloor).toMatchObject({ dead: false, visible: true })
  expect(result.nextFloor!.alpha).toBe(1)
  expect(result.count).toBe(1)
})
