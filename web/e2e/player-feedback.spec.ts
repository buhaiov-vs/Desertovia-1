import {test,expect,uniqueName} from './fixtures.ts'

test.beforeEach(async({page})=>{
 await page.request.post('/auth/dev-login',{data:{name:uniqueName('feedback-ui')}})
 await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
 await page.goto('/e2e/player-feedback-harness.html')
})

test('finished private house shows its stable number and owner and offers direct move-in',async({page})=>{
 const card=page.getByRole('dialog')
 await expect(card.locator('header strong')).toHaveText('Хижина №12')
 await expect(card).toContainText('Владелец: Хозяин')
 await expect(card).not.toContainText('-4,10')
 await card.getByRole('button',{name:'Заселиться',exact:true}).click()
 await expect(page.locator('#call')).toHaveText(JSON.stringify({plot:'-4,10',operation:'housing'}))
 const width=page.viewportSize()!.width
 const box=await card.boundingBox();expect(box!.x).toBeGreaterThanOrEqual(0);expect(box!.x+box!.width).toBeLessThanOrEqual(width+1)
})

test('repair and carry choices send the selected storage key instead of an automatic target',async({page})=>{
 await page.getByRole('button',{name:'storage',exact:true}).click()
 const card=page.getByRole('dialog')
 await expect(card.locator('header strong')).toContainText('№13')
 const repair=card.locator('.task-card').filter({has:page.locator('.task-name',{hasText:'Чинить постройки'})})
 await expect(repair).toContainText('10')
 await repair.click();await expect(page.locator('#call')).toHaveText(JSON.stringify({task:'repair',spot:null,plot:'1,1'}))
 const carry=card.locator('.task-card').filter({has:page.locator('.task-name',{hasText:/Отнести|Нести/})})
 await carry.click();await expect(page.locator('#call')).toHaveText(/"plot":"1,1"/)
})

test('gathering previews public versus personal output and work names both trained stats',async({page})=>{
 await page.getByRole('button',{name:'public',exact:true}).click();await expect(page.getByRole('dialog')).toContainText('Добыча → общий запас')
 await page.getByRole('button',{name:'personal',exact:true}).click();await expect(page.getByRole('dialog')).toContainText('Добыча → личный запас')
 await page.getByRole('button',{name:'stats',exact:true}).click()
 await expect(page.locator('.task-meta').first()).toContainText('Ум, Обаяние')
})

test('hungry inventory offers a meal and disables it when there is no owned food',async({page})=>{
 await page.getByRole('button',{name:'inventory',exact:true}).click()
 await page.getByRole('button',{name:'Поесть',exact:true}).click();await expect(page.locator('#call')).toHaveText('eat')
 await page.getByRole('dialog').locator('.close').click()
 await page.getByRole('button',{name:'nofood',exact:true}).click()
 await expect(page.getByRole('button',{name:'Поесть · Нет еды при себе',exact:true})).toBeDisabled()
})
