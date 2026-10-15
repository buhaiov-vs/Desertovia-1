import { test, expect, developmentLogin, expectVillageView, openMenu, DEMO_SLUG, uniqueName } from './fixtures.ts'

test('real game selected object owns its header while dark hidden chat does not block the map', async ({ page }) => {
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
  for (const mode of ['hidden', 'quarter']) {
    if (mode === 'quarter') await page.getByRole('button', { name: 'Open chat' }).click()
    let selected = false
    for (const y of [.42, .50, .58, .65]) {
      if (selected) break
      for (const x of [.45, .30, .60, .20, .70]) {
        await page.mouse.click(viewport.width * x, viewport.height * y)
        if (await popup.isVisible()) { selected = true; break }
      }
    }
    expect(selected, 'A real map object opens its existing card').toBe(true)
    await expect(chat).toBeHidden()
    const header = popup.locator('.target-popup-header')
    await expect(header.locator('strong')).not.toBeEmpty()
    await expect(header.locator('button.close')).toBeVisible()
    const unblocked = await header.locator('button.close').evaluate(el => {
      const r = el.getBoundingClientRect()
      return el.contains(document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2))
    })
    expect(unblocked, 'The card close button receives the tap').toBe(true)
    await page.screenshot({ path: test.info().outputPath(`dark-selected-${mode}.png`) })
    await header.locator('button.close').click()
    await expect(chat).toBeVisible()
    await expect(chat).toHaveAttribute('data-mode', mode)
  }
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
  await page.screenshot({ path: test.info().outputPath('own-stats-with-spouse.png') })
})
