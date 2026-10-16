import {test,expect} from './fixtures.ts'
test.beforeEach(async({page})=>{await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'));await page.goto('/e2e/cooperative-harness.html')})
test('civic actions fit a phone and governors cannot approve',async({page})=>{
 await page.setViewportSize({width:320,height:740})
 const modal=page.getByRole('dialog',{name:'Управление городом'})
 await expect(modal).toContainText('11/12')
 await page.locator('.civic-applications summary').click()
 await expect(page.getByRole('button',{name:'Одобрить',exact:true})).toHaveCount(0)
 expect(await modal.evaluate(e=>e.scrollWidth<=e.clientWidth)).toBe(true)
 for(const button of await modal.locator('button').all()){const b=await button.boundingBox();if(b){expect(b.x).toBeGreaterThanOrEqual(0);expect(b.x+b.width).toBeLessThanOrEqual(320)}}
 await page.screenshot({path:'test-results/cooperative-phone.png',fullPage:true})
 await page.getByRole('button',{name:'Стать царём'}).click()
 await expect(page.getByRole('button',{name:'Одобрить',exact:true})).toBeVisible()
 await page.getByRole('button',{name:'Одобрить',exact:true}).click()
 await expect(page.getByRole('button',{name:'Одобрить',exact:true})).toHaveCount(0)
})
test('dark interface is independent and demolition asks for confirmation',async({page})=>{
 await page.getByRole('button',{name:'Тёмный',exact:true}).click()
 await expect(page.locator('html')).toHaveAttribute('data-ui-theme','dark')
 const color=await page.locator('.modal-body').evaluate(e=>getComputedStyle(e).backgroundColor)
 expect(color).toBe('rgb(23, 27, 27)')
 await page.getByRole('button',{name:'Снести · заявка',exact:true}).click()
 await expect(page.getByRole('button',{name:'Подтвердить снос'})).toBeVisible()
 await page.getByRole('button',{name:'Отмена',exact:true}).click()
 await expect(page.getByRole('button',{name:'Подтвердить снос'})).toHaveCount(0)
 await page.screenshot({path:'test-results/cooperative-dark.png',fullPage:true})
})

test('full construction queue blocks governor applications with a clear Russian warning',async({page})=>{
 await page.setViewportSize({width:390,height:844})
 for(const query of ['build=1','build=1&pending=1']){
 await page.goto('/e2e/cooperative-harness.html?'+query)
 await expect(page.getByText('Очередь строительства полна.',{exact:false})).toBeVisible()
 const hospital=page.locator('.task-card').filter({has:page.locator('.task-name',{hasText:'Больница'})})
 const castle=page.locator('.task-card').filter({has:page.locator('.task-name',{hasText:/замок/i})})
 await expect(hospital).toBeDisabled();await expect(castle).toBeDisabled()
 await expect(page.locator('output')).toBeEmpty()
 await page.locator('.civic-applications summary').click()
 await expect(page.locator('.civic-order')).toContainText('Строительство · Больница')
 await expect(page.getByRole('button',{name:'Одобрить',exact:true})).toHaveCount(0)
 }
})

test('civic errors are translated instead of showing the English server message',async({page})=>{
 await page.goto('/e2e/cooperative-harness.html?busy=1')
 await page.getByRole('button',{name:'Стать царём'}).click()
 await page.locator('.civic-applications summary').click()
 await page.getByRole('button',{name:'Одобрить',exact:true}).click()
 await expect(page.getByRole('alert')).toHaveText('Сначала завершите текущую стройку.')
 await expect(page.getByText('Finish the existing work first.',{exact:true})).toHaveCount(0)
})

test('unavailable upgrades explain maximum level or unsupported building in Russian',async({page})=>{
 for(const [reason,label,message] of [
  ['maximum','Максимальный уровень','Достигнут максимальный уровень улучшения.'],
  ['unsupported','Нет улучшений','Для этого типа здания улучшение не предусмотрено.'],
  ['hospital','В карточке больницы','Царь может улучшить больницу в её карточке.'],
 ]){
  await page.goto('/e2e/cooperative-harness.html?upgradeError='+reason)
  await expect(page.getByRole('button',{name:label,exact:true})).toBeDisabled()
  await expect(page.getByText(message,{exact:true})).toBeVisible()
  await page.getByRole('button',{name:'Стать царём'}).click()
  await page.locator('.civic-applications summary').click()
  await page.getByRole('button',{name:'Одобрить',exact:true}).click()
  await expect(page.getByRole('alert')).toHaveText(message)
  await expect(page.getByText('This building cannot be upgraded further.',{exact:true})).toHaveCount(0)
 }
})
