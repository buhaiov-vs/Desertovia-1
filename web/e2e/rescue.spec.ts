import {test,expect} from './fixtures.js'

test('wounded can request help without spam and sees remaining real time',async({page})=>{
  await page.goto('/e2e/rescue-harness.html')
  const dialog=page.getByRole('dialog')
  await expect(dialog).toContainText('40 мин 0 с')
  await expect(dialog).toContainText('в любой момент')
  await dialog.getByRole('button',{name:'Попросить помощи'}).click()
  await expect(dialog.getByRole('button',{name:'Помощь запрошена'})).toBeDisabled()
  await expect(page.locator('#calls')).toHaveText('1')
  const bounds=await dialog.boundingBox()
  expect(bounds!.x).toBeGreaterThanOrEqual(0)
  expect(bounds!.x+bounds!.width).toBeLessThanOrEqual(page.viewportSize()!.width)
})

test('another resident can carry a patient and a missing hospital explains refusal',async({page})=>{
  await page.goto('/e2e/rescue-harness.html')
  await page.getByRole('button',{name:'other',exact:true}).click()
  await page.getByRole('button',{name:'Отнести в больницу'}).click()
  await expect(page.locator('#calls')).toHaveText('1')
  await page.getByRole('button',{name:'missing',exact:true}).click()
  await expect(page.getByRole('button',{name:'Отнести в больницу'})).toBeDisabled()
  await expect(page.getByRole('dialog')).toContainText('Самолечение продолжается')
})

test('admitted and already claimed patients never offer a second delivery',async({page})=>{
  await page.goto('/e2e/rescue-harness.html')
  await page.getByRole('button',{name:'ward',exact:true}).click()
  await expect(page.getByRole('dialog')).toContainText('В больнице')
  await expect(page.getByRole('button',{name:'Отнести в больницу'})).toHaveCount(0)
  await page.getByRole('button',{name:'carrying',exact:true}).click()
  await expect(page.getByRole('dialog')).toContainText('Помогает: Спасатель')
  await expect(page.getByRole('button',{name:'Отнести в больницу'})).toBeDisabled()
})
