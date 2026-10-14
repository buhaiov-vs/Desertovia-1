import { test as base, expect, type Page } from '@playwright/test'

export const DEMO_SLUG = 'demo-bridge'
export const DEMO_NAME = 'Демо: Царский мост'

/**
 * Every test fails on an uncaught exception or a console error. "Failed to load resource" is left out: the client
 * asks for the account and the citizen and treats 401 and 404 as "not signed in" and "not joined", and the village
 * prefetch fails with 401 before signing in.
 */
export const test = base.extend<{ page: Page }>({
  page: async ({ page }, run) => {
    const problems: string[] = []
    page.on('pageerror', (error) => problems.push(`Uncaught: ${error.message}`))
    page.on('console', (message) => {
      if (message.type() === 'error' && !message.text().startsWith('Failed to load resource')) {
        problems.push(`Console: ${message.text()}`)
      }
    })
    await run(page)
    expect(problems, 'Browser errors during the test').toEqual([])
  },
})

export { expect }

/** Development login names are accounts; a fresh one per test keeps the tests independent. */
export function uniqueName(prefix: string) {
  return `${prefix}-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`
}

/** Signs in through whichever login panel is on screen. */
export async function developmentLogin(page: Page, name: string, characterName = name) {
  const panel = page.locator('section.login')
  const form = panel.locator('form', { has: page.getByPlaceholder('Any name') })
  await form.getByPlaceholder('Any name').fill(name)
  await form.getByRole('button', { name: 'Sign in' }).click()
  await expect(panel).toBeHidden()
  const creation = page.locator('form.character-creation')
  await expect(creation).toBeVisible()
  await creation.getByLabel('Name', { exact: true }).fill(characterName)
  await creation.getByRole('button', { name: 'Create character', exact: true }).click()
  await expect(creation).toBeHidden()
}

export async function expectVillageView(page: Page) {
  await expect(page.locator('.splash')).toBeHidden()
  await expect(page.locator('canvas').first()).toBeVisible()
  await expect(page.locator('.hud-resources')).toBeVisible()
  await expect(page.locator('.hud-buttons')).toBeVisible()
  if ((page.viewportSize()?.width ?? 1000) <= 720) {
    const contained = await page.locator('.hud-buttons').evaluate(element =>
      [...element.querySelectorAll('button')].every(button => {
        const rect = button.getBoundingClientRect()
        return rect.left >= 0 && rect.right <= window.innerWidth && rect.bottom <= window.innerHeight
      }))
    expect(contained, 'Every phone navigation button stays on screen').toBe(true)
  }
}

/** Opens the game menu with the first bottom bar button: "Menu" on desktop, the Village tab on phones. */
export async function openMenu(page: Page) {
  await page.locator('.hud-buttons button').first().click()
  const menu = page.getByRole('dialog', { name: 'Menu' })
  await expect(menu).toBeVisible()
  return menu
}

/** Clicks through every tab of the open menu and checks each one shows content. */
export async function visitEveryTab(page: Page, expected: string[]) {
  const menu = page.getByRole('dialog', { name: 'Menu' })
  const tabs = menu.locator('nav.tabs button')
  await expect(tabs).toHaveText(expected)
  for (const name of expected) {
    await tabs.filter({ hasText: name }).click()
    await expect(tabs.filter({ hasText: name })).toHaveClass(/active/)
    await expect(menu.locator('.modal-body .card').first()).toBeVisible()
  }
}

export async function closeModal(page: Page, name: string) {
  const dialog = page.getByRole('dialog', { name })
  await dialog.locator('button.close').click()
  await expect(dialog).toBeHidden()
}

export async function openInventory(page: Page) {
  await page.locator('.hud-buttons button').last().click()
  const inventory = page.getByRole('dialog', { name: 'Inventory' })
  await expect(inventory).toBeVisible()
  return inventory
}
