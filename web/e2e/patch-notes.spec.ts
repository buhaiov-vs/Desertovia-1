import { test, expect } from '@playwright/test'

for (const language of ['ru', 'en'] as const) {
  test(`version opens documented history and returns to settings (${language})`, async ({ page }) => {
    await page.addInitScript(lang => localStorage.setItem('desertod.language', lang), language)
    await page.goto('/e2e/cooperative-harness.html')
    const button = page.getByRole('button', { name: /(?:Дезертовия|Desertovia) 0\.6/ })
    await button.click()
    const notes = page.locator('.patch-notes')
    await expect(notes).toBeVisible()
    await expect(button).toHaveAttribute('aria-expanded', 'true')
    await expect(notes.locator('details[open]')).toHaveCount(1)
    await expect(notes.locator('details[open] summary')).toContainText('0.6')
    await notes.locator('summary').filter({ hasText: '0.5.2' }).click()
    await expect(notes.locator('details[open]')).toHaveCount(2)
    await expect(notes.locator('details[open] summary').first()).toContainText('0.6')
    for (const theme of ['light', 'dark']) {
      await page.evaluate(value => { document.documentElement.dataset.uiTheme = value }, theme)
      for (const width of [320, 1440]) {
        await page.setViewportSize({ width, height: 900 })
        expect(await notes.evaluate(el => el.scrollWidth <= el.clientWidth)).toBe(true)
        const box = await notes.boundingBox()
        expect(box!.x).toBeGreaterThanOrEqual(0)
        expect(box!.x + box!.width).toBeLessThanOrEqual(width)
      }
    }
    await notes.getByRole('button', { name: language === 'ru' ? 'К настройкам' : 'Back to Settings' }).click()
    await expect(notes).toHaveCount(0)
    await expect(button).toBeFocused()
    await expect(button).toHaveAttribute('aria-expanded', 'false')
    await expect(page.getByRole('combobox', { name: language === 'ru' ? 'Язык' : 'Language' })).toBeVisible()
  })
}
