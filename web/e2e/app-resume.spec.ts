import { test, expect } from './fixtures.ts'

type StandWindow = Window & { resumeStand: { listen: (fn: () => void) => () => void }; resumes: number; stopResume: () => void }

for (const silent of [false, true]) test(`Mini App restore refreshes once and disposes listeners (silent=${silent})`, async ({ page }) => {
  await page.goto('/e2e/live-combat-harness.html')
  await page.waitForFunction(() => !!(window as unknown as StandWindow).resumeStand)
  await page.clock.install()
  await page.evaluate(() => {
    const w = window as unknown as StandWindow
    w.resumes = 0
    w.stopResume = w.resumeStand.listen(() => { w.resumes++ })
  })
  if (silent) {
    await page.clock.fastForward(5_000)
  } else {
    await page.evaluate(() => {
      Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'hidden' })
      document.dispatchEvent(new Event('visibilitychange'))
      Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' })
      document.dispatchEvent(new Event('visibilitychange'))
      document.dispatchEvent(new Event('visibilitychange'))
    })
  }
  await page.clock.runFor(400)
  expect(await page.evaluate(() => (window as unknown as StandWindow).resumes)).toBe(1)
  await page.evaluate(() => (window as unknown as StandWindow).stopResume())
  await page.clock.fastForward(5_000)
  await page.clock.runFor(400)
  expect(await page.evaluate(() => (window as unknown as StandWindow).resumes)).toBe(1)
})
