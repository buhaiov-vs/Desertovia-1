import { test, expect, developmentLogin, expectVillageView, openMenu, DEMO_SLUG, uniqueName } from './fixtures.ts'

// Real Pixi composition is substantially heavier than the isolated chat stand.
test.setTimeout(120_000)
test.use({ deviceScaleFactor: 1, trace: { mode: 'retain-on-failure', screenshots: false, snapshots: true } })

for (const mode of ['hidden', 'quarter'] as const) test(`real game selected object owns its header with ${mode} chat`, async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('desertod.language', 'en')
    localStorage.setItem('desertod.chat.mode', 'hidden')
    localStorage.setItem('desertovia.uiTheme', 'dark')
  })
  await page.goto(`/v/${DEMO_SLUG}`)
  await developmentLogin(page, uniqueName('popup-regression'))
  await expectVillageView(page)
  const chat = page.locator('.chat-panel')
  const background = await chat.evaluate(el => ({ background: getComputedStyle(el).backgroundColor, hit: getComputedStyle(el).pointerEvents, width: el.getBoundingClientRect().width }))
  expect(background.background).toBe('rgba(0, 0, 0, 0)')
  expect(background.hit).toBe('none')
  expect(background.width).toBeLessThan(180)
  const viewport = page.viewportSize()!
  const popup = page.locator('.target-popup')
    if (mode === 'quarter') await page.getByRole('button', { name: 'Open chat' }).click()
    const state = await (await page.request.get(`/api/villages/${DEMO_SLUG}`)).json()
    const storage = state.state.storages[0] as { plotId: string; kind: string }
    const [x, y] = storage.plotId.split(',').map(Number)
    await page.evaluate(({ storage, x, y, screen }) => {
      window.dispatchEvent(new CustomEvent('desertovia-e2e-select', { detail: {
        target: { kind: 'building', plotId: storage.plotId, building: storage.kind, storage: true, area: { x, y, width: 3, height: 3 } },
        screen, point: { x, y },
      } }))
    }, { storage, x, y, screen: { x: viewport.width / 2, y: viewport.height / 2 } })
    await expect(popup).toBeVisible()
    await expect(chat).toBeHidden()
    const header = popup.locator('.target-popup-header')
    await expect(header.locator('strong')).not.toBeEmpty()
    await expect(header.locator('button.close')).toBeVisible()
    const unblocked = await header.locator('button.close').evaluate(el => {
      const r = el.getBoundingClientRect()
      return el.contains(document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2))
    })
    expect(unblocked, 'The card close button receives the tap').toBe(true)
    // The map keeps animating behind the sheet. Hit-test first, then send a real pointer click
    // without waiting for the renderer to provide two identical animation frames.
    const closeBox = await header.locator('button.close').boundingBox()
    expect(closeBox).not.toBeNull()
    await page.mouse.click(closeBox!.x + closeBox!.width / 2, closeBox!.y + closeBox!.height / 2)
    await expect(popup).toBeHidden()
    await expect(chat).toBeVisible()
    await expect(chat).toHaveAttribute('data-mode', mode)
})

test('real character sheet shows spouse once and preserves own stat rows on a phone', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('desertod.language', 'en'))
  await page.goto(`/v/${DEMO_SLUG}`)
  const name = uniqueName('my-stats')
  await developmentLogin(page, name)
  await expectVillageView(page)
  await page.route('**/api/villages/*/me', async route => {
    const response = await route.fetch()
    const data = await response.json()
    data.spouseName = 'SpouseWithAVeryLongNameForPhone'
    data.homePlotId = '9,11'
    await route.fulfill({ response, json: data })
  })
  await page.reload()
  await expectVillageView(page)
  const menu = await openMenu(page)
  await menu.locator('nav.tabs button', { hasText: 'Character' }).click()
  await expect(menu.locator('.character-title')).toContainText(`My character — ${name}`)
  await expect(menu.getByText('Супруг: SpouseWithAVeryLongNameForPhone', { exact: true })).toHaveCount(1)
  const stats = menu.locator('table.stats').first()
  await expect(stats.locator('tbody > tr')).toHaveCount(5)
  await expect(stats.locator('p')).toHaveCount(0)
  expect(await stats.evaluate(el => el.scrollWidth <= el.clientWidth + 1)).toBe(true)
})
