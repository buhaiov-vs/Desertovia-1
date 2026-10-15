import { test, expect } from '@playwright/test'
type Raid = { id: string; enemy: string; count: number; outcome: string; announcedAt: string; strikesAt: string; resolvedAt: string | null; killed?: number }

test('undead packs wait until their own countdown expires, including reconnect and later waves', async ({ page }) => {
  await page.goto('/e2e/raid-timing-harness.html')
  await page.waitForFunction(() => 'raidTiming' in window)
  const now = Date.parse('2026-10-15T12:00:00Z')
  const raid = { id: 'first', enemy: 'Zombie', count: 3, outcome: 'Pending', announcedAt: new Date(now).toISOString(), strikesAt: new Date(now + 180000).toISOString(), resolvedAt: null } as Raid
  const next = { ...raid, id: 'next', enemy: 'Skeleton', strikesAt: new Date(now + 300000).toISOString() } as Raid
  const update = (raids: Raid[], time: number) => page.evaluate(({ raids, time }) => {
    const stand = window as unknown as { raidTiming: { update: (raids: Raid[], now: number) => { count: number; positions: number[][]; front: unknown } } }
    return stand.raidTiming.update(raids, time)
  }, { raids, time })
  for (const time of [now, now + 90000, now + 179999]) {
    const state = await update([raid, next], time)
    expect(state.count).toBe(0)
    expect(state.front).toBeNull()
  }
  const arrived = await update([raid, next], now + 180000)
  expect(arrived.count).toBe(3)
  expect(arrived.front).not.toBeNull()
  expect((await update([raid, next], now + 181000)).positions).not.toEqual(arrived.positions)
  const resolved = { ...raid, outcome: 'Repelled', killed: 3, resolvedAt: new Date(now + 181000).toISOString() } as Raid
  expect((await update([resolved, next], now + 182000)).count).toBe(3)
  expect((await update([resolved, next], now + 192000)).count).toBe(0)
  await page.reload()
  await page.waitForFunction(() => 'raidTiming' in window)
  expect((await update([resolved, next], now + 299999)).count).toBe(0)
  expect((await update([resolved, next], now + 300000)).count).toBe(3)
  expect((await update([], now + 301000)).count).toBe(0)
})

test('recovery uses labelled real days, hours and minutes rather than an ambiguous minute count', async ({ page }) => {
  await page.goto('/e2e/raid-timing-harness.html')
  await page.waitForFunction(() => 'raidTiming' in window)
  const values = await page.evaluate(() => {
    const stand = window as unknown as { raidTiming: { formatRecoveryTime: (until: string | number, now: number, language: 'ru' | 'en') => string } }
    const format = stand.raidTiming.formatRecoveryTime
    const now = 1000000
    return [format(now + 1100 * 60000, now, 'ru'), format(now + 1500 * 60000, now, 'ru'),
      format(now + 86400000, now, 'en'), format(new Date(now + 1).toISOString(), now, 'ru'),
      format(now - 1, now, 'ru'), format('invalid', now, 'ru')]
  })
  expect(values).toEqual(['18 ч 20 мин (реальное время)', '1 д 1 ч (реальное время)',
    '1d (real time)', '1 мин (реальное время)', '0 мин (реальное время)', 'Срок уточняется'])
})

test('hospital and patient list label recovery time on a narrow screen', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('desertod.language', 'ru'))
  await page.setViewportSize({ width: 320, height: 740 })
  await page.goto('/e2e/hospital-harness.html')
  const dialog = page.getByRole('dialog', { name: 'Больница' })
  await expect(dialog.locator('.hospital-countdown')).toContainText('реальное время')
  await expect(dialog.locator('.patient-card-info small')).toContainText('реальное время')
  expect(await dialog.evaluate(el => el.scrollWidth <= el.clientWidth)).toBe(true)
})
