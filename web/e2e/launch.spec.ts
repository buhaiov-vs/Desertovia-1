import { test, expect, developmentLogin, DEMO_SLUG, uniqueName, expectVillageView } from './fixtures.ts'
test('our single world opens directly after login and hides city selection', async ({ page }) => {
  await page.route('**/auth/providers', async route => {
    const response = await route.fetch()
    await route.fulfill({ response, json: { ...await response.json(), primaryVillage: DEMO_SLUG } })
  })
  await page.goto('/')
  await developmentLogin(page, uniqueName('single'))
  await expect(page).toHaveURL(`/v/${DEMO_SLUG}`)
  await expectVillageView(page)
  await expect(page.getByRole('link', { name: 'Found a village' })).toHaveCount(0)
})
test('bot login hides the unsupported Telegram widget and folds email away', async ({ page }) => {
  await page.route('**/auth/providers', async route => {
    const response = await route.fetch()
    await route.fulfill({ response, json: { ...await response.json(), telegramBotUsername: 'test_bot', telegramBrowserLogin: true } })
  })
  await page.goto('/')
  await expect(page.getByRole('button', { name: 'Sign in with Telegram' })).toBeVisible()
  await expect(page.getByText('Telegram widget')).toHaveCount(0)
  await expect(page.locator('.password-login')).toBeHidden()
  await page.getByText('Sign in with email', { exact: true }).click()
  await expect(page.locator('.password-login')).toBeVisible()
})
