import {test,expect} from './fixtures.ts'

test.beforeEach(async({page})=>{
 await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
 await page.goto('/e2e/hospital-harness.html')
 await expect(page.getByRole('dialog',{name:'Больница'})).toBeVisible()
})

test('hospital fits narrow viewport and confirms account candle price before payment',async({page})=>{
 await page.setViewportSize({width:320,height:640})
 const modal=page.getByRole('dialog',{name:'Больница'})
 await expect(modal).toContainText('Свечи у батюшки: 200')
 await expect(modal).toContainText('Работа на паузе')
 expect(await modal.evaluate(el=>el.scrollWidth<=el.clientWidth)).toBe(true)
 for(const button of await modal.locator('button').all()) {
  const bounds=await button.boundingBox();if(!bounds)continue
  expect(bounds.x).toBeGreaterThanOrEqual(0);expect(bounds.x+bounds.width).toBeLessThanOrEqual(320)
 }
 await page.getByRole('button',{name:/Свечка за здравие/}).click()
 await expect(page.getByText('Заплатить 50 свечей?',{exact:true})).toBeVisible()
 await expect(page.locator('#calls')).toHaveText('0')
 await page.getByRole('button',{name:'Отмена',exact:true}).click()
 await expect(page.locator('#calls')).toHaveText('0')
})

test('pending payment survives closing and reuses operation id without a new charge',async({page})=>{
 await page.getByRole('button',{name:/Свечка за здравие/}).click()
 await page.getByRole('button',{name:'Оплатить · 50 свечей'}).click()
 await expect(page.locator('#calls')).toHaveText('1')
 const operation=await page.locator('#operation').textContent()
 await page.getByRole('button',{name:'Закрыть',exact:true}).click()
 await page.locator('#reopen').click()
 await page.getByRole('button',{name:'Проверить эту оплату'}).click()
 await expect(page.locator('#calls')).toHaveText('2')
 await expect(page.locator('#operation')).toHaveText(operation!)
 await expect(page.getByText('Лечение проведено. Срок выписки обновлён.')).toBeVisible()
 await expect(page.getByRole('button',{name:'Проверить эту оплату'})).toHaveCount(0)
 await expect(page.getByText('Свечи у батюшки: 150')).toBeVisible()
})
