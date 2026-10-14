import {
  closeModal,
  developmentLogin,
  expect,
  expectVillageView,
  openInventory,
  openMenu,
  test,
  uniqueName,
  visitEveryTab,
} from './fixtures.ts'

test.describe('tsar', () => {
  test('founds a village and opens every panel', async ({ page }) => {
    test.slow()
    await page.goto('/new')
    await developmentLogin(page, uniqueName('tsar'))

    await expect(page.getByRole('heading', { name: 'Found a village' })).toBeVisible()
    const submit = page.getByRole('button', { name: 'Found the village' })
    await expect(submit).toBeDisabled()
    await page.getByLabel('Village name').fill(`Smoke ${uniqueName('v')}`)
    await page.getByRole('radio', { name: /By invite/ }).check()
    await submit.click()

    await expect(page).toHaveURL(/\/v\/[\w-]+$/)
    await expectVillageView(page)
    await expect(page.locator('.hud-top .badge', { hasText: 'Tsar' })).toBeVisible()

    await openMenu(page)
    await visitEveryTab(page, ['Village', 'Build', 'Tech', 'Settings'])

    const menu = page.getByRole('dialog', { name: 'Menu' })
    await menu.locator('nav.tabs button', { hasText: 'Build' }).click()
    await expect(menu.getByRole('heading', { name: 'Plan a building' })).toBeVisible()

    await closeModal(page, 'Menu')

    const inventory = await openInventory(page)
    await expect(inventory.getByRole('heading', { name: 'Storage buildings' })).toBeVisible()
    await closeModal(page, 'Inventory')
  })

  test('keyboard shortcuts open and close the menu and the inventory', async ({ page, isMobile }) => {
    test.skip(isMobile, 'Phones have no keyboard shortcuts.')
    await page.goto('/new')
    await developmentLogin(page, uniqueName('keys'))
    await page.getByLabel('Village name').fill(`Keys ${uniqueName('v')}`)
    await page.getByRole('button', { name: 'Found the village' }).click()
    await expectVillageView(page)

    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog', { name: 'Menu' })).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog', { name: 'Menu' })).toBeHidden()

    await page.keyboard.press('i')
    await expect(page.getByRole('dialog', { name: 'Inventory' })).toBeVisible()
    await page.keyboard.press('i')
    await expect(page.getByRole('dialog', { name: 'Inventory' })).toBeHidden()

    await page.getByRole('button', { name: 'Zoom in' }).click()
    await page.getByRole('button', { name: 'Zoom out' }).click()
  })
})
