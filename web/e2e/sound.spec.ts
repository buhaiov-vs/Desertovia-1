import { expect, test } from '@playwright/test'

test('village music loads one calm track and switches to the battle theme during a raid', async ({ page }) => {
  const files: string[] = []
  page.on('request', (request) => {
    const name = request.url().split('?')[0]?.split('/').pop() ?? ''
    if (name.endsWith('.mp3')) files.push(name)
  })
  await page.goto('/e2e/sound-harness.html')
  await page.waitForFunction(() => 'villageMusic' in window)

  const rules = await page.evaluate(() => {
    const stand = window as unknown as {
      villageMusic: { cityRaidPlaying: (battles: { finishedAt: string | null; phase: string }[]) => boolean }
    }
    const playing = stand.villageMusic.cityRaidPlaying
    return {
      warning: playing([{ finishedAt: null, phase: 'Warning' }]),
      approaching: playing([{ finishedAt: null, phase: 'Approaching' }]),
      fighting: playing([{ finishedAt: null, phase: 'Fighting' }]),
      retreating: playing([{ finishedAt: null, phase: 'Retreating' }]),
      finished: playing([{ finishedAt: '2026-10-18T00:00:00Z', phase: 'Finished' }]),
      cancelled: playing([{ finishedAt: null, phase: 'Cancelled' }]),
    }
  })
  expect(rules).toEqual({
    warning: false,
    approaching: true,
    fighting: true,
    retreating: true,
    finished: false,
    cancelled: false,
  })

  const toggle = page.getByRole('button', { name: /Village music|Turn village music on|Музыка деревни|Включить музыку деревни/ })
  const box = await toggle.boundingBox()
  const viewport = page.viewportSize()
  expect(box).not.toBeNull()
  expect(viewport).not.toBeNull()
  expect(box!.x).toBeGreaterThanOrEqual(0)
  expect(box!.y).toBeGreaterThanOrEqual(0)
  expect(box!.x + box!.width).toBeLessThanOrEqual(viewport!.width)
  expect(box!.y + box!.height).toBeLessThanOrEqual(viewport!.height)

  await toggle.click()
  await expect.poll(() => page.evaluate(() => {
    const stand = window as unknown as { villageMusic: { snapshot: () => { src: string; paused: boolean; loop: boolean } } }
    return stand.villageMusic.snapshot()
  })).toMatchObject({ paused: false, loop: false })
  const calm = await page.evaluate(() => {
    const stand = window as unknown as { villageMusic: { snapshot: () => { src: string } } }
    return stand.villageMusic.snapshot().src
  })
  expect(calm).toMatch(/part_00\d\.mp3$/)

  await page.getByRole('button', { name: 'raid' }).click()
  await expect.poll(() => page.evaluate(() => {
    const stand = window as unknown as { villageMusic: { snapshot: () => { src: string; loop: boolean } } }
    return stand.villageMusic.snapshot()
  })).toMatchObject({ loop: true })
  await expect.poll(() => page.evaluate(() => {
    const stand = window as unknown as { villageMusic: { snapshot: () => { src: string } } }
    return stand.villageMusic.snapshot().src
  })).toMatch(/battle\.mp3$/)

  const during = [...files]
  expect(new Set(during.filter((name) => name.startsWith('part_'))).size).toBe(1)
  expect(during).toContain('battle.mp3')

  await page.getByRole('button', { name: 'raid' }).click()
  await expect.poll(() => page.evaluate(() => {
    const stand = window as unknown as { villageMusic: { snapshot: () => { src: string } } }
    return stand.villageMusic.snapshot().src
  })).toBe(calm)
  expect(new Set(files.filter((name) => name.startsWith('part_'))).size).toBe(1)

  await page.getByRole('button', { name: /Village music|Музыка деревни/ }).click()
  await expect.poll(() => page.evaluate(() => {
    const stand = window as unknown as { villageMusic: { snapshot: () => { paused: boolean } } }
    return stand.villageMusic.snapshot().paused
  })).toBe(true)
})
