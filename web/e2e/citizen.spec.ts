import {
  closeModal,
  DEMO_SLUG,
  developmentLogin,
  expect,
  expectVillageView,
  openInventory,
  openMenu,
  test,
  uniqueName,
  visitEveryTab,
} from './fixtures.ts'

test.describe('citizen', () => {
  test('a visitor must sign in before seeing a village', async ({ page }) => {
    await page.goto(`/v/${DEMO_SLUG}`)
    await expect(page.getByRole('heading', { name: 'Sign in to play' })).toBeVisible()
    await expect(page.locator('canvas')).toHaveCount(0)

    await page.goto('/')
    await expect(page.getByRole('heading', { name: 'Sign in to start' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Open villages' })).toHaveCount(0)
  })

  test('signs in, creates a character, opens every panel and starts a task', async ({ page }) => {
    test.slow()
    await page.goto(`/v/${DEMO_SLUG}`)
    await developmentLogin(page, uniqueName('citizen'), 'Smoky')
    await expectVillageView(page)

    const inventoryBefore = await openInventory(page)
    await expect(inventoryBefore.getByRole('heading', { name: /Village storage/ })).toBeVisible()
    await expect(inventoryBefore.getByRole('heading', { name: /Smoky/ })).toBeVisible()
    await closeModal(page, 'Inventory')

    const menu = await openMenu(page)
    await visitEveryTab(page, ['Village', 'Character', 'Tasks', 'Tech', 'Settings'])

    await menu.locator('nav.tabs button', { hasText: 'Character' }).click()
    await expect(menu.getByText('Energy').first()).toBeVisible()

    await menu.locator('nav.tabs button', { hasText: 'Tasks' }).click()
    await menu.locator('button.task-card', { hasText: 'Gather wood' }).click()
    await expect(menu).toBeHidden()
    await expect(page.locator('.hud-status')).toContainText('next action in')

    const inventory = await openInventory(page)
    await expect(inventory.getByRole('heading', { name: /Smoky/ })).toBeVisible()
    await closeModal(page, 'Inventory')
  })

  test('an unknown village shows an error and a way back', async ({ page }) => {
    await page.goto('/v/no-such-village')
    await developmentLogin(page, uniqueName('lost'))

    await expect(page.getByText('Village "no-such-village" was not found.')).toBeVisible()
    await page.getByRole('link', { name: 'Back to home' }).click()
    await expect(page).toHaveURL('/')
  })
})