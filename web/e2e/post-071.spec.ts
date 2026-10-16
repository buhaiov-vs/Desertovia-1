import { test,expect } from './fixtures.ts'
const wallet={coins:123,testCandles:4,workPay:10,constructionPay:20,inventory:[],stockPrices:{Wood:2},residents:[],lots:[],exchangeRate:100,dailyExchangeLimit:5,exchangedToday:0,kingdomCandleRoom:50,dayResetsAt:'2026-10-17T00:00:00Z',earnedToday:0,profitToday:0,taxPreview:0,privateProperty:[],privateStorages:[],journal:[],nextJournalBefore:null,startedAt:'2026-10-16T00:00:00Z'}

test('Tsar can attack and close control is centered and touch sized',async({page})=>{
 await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
 await page.route('**/api/villages/stand/economy?*',r=>r.fulfill({json:wallet}))
 let commands=0
 await page.route('**/battles/raid/order',async r=>{commands++;expect(r.request().postDataJSON()).toMatchObject({targetUnitId:'target',mode:'intercept'});await r.fulfill({status:200})})
 await page.goto('/e2e/post-071-harness.html')
 await page.getByRole('button',{name:'Вступить в бой и атаковать'}).click();expect(commands).toBe(1)
 const close=page.locator('.battle-controls .close'),box=await close.boundingBox(),icon=await close.locator('svg').boundingBox()
 expect(box!.width).toBeGreaterThanOrEqual(44);expect(box!.height).toBeGreaterThanOrEqual(44)
 expect(Math.abs(icon!.x+icon!.width/2-box!.x-box!.width/2)).toBeLessThan(1)
 expect(Math.abs(icon!.y+icon!.height/2-box!.y-box!.height/2)).toBeLessThan(1)
 await close.click();await expect(page.locator('.battle-controls')).toHaveCount(0);expect(commands).toBe(1)
})
test('application actions follow the copy without a giant blank region; wallet shows personal test balances',async({page})=>{
 await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
 await page.route('**/api/villages/stand/economy?*',r=>r.fulfill({json:wallet}))
 await page.goto('/e2e/post-071-harness.html')
 await page.locator('.battle-controls .close').click()
 await page.locator('.civic-applications summary').click()
 const copy=await page.locator('.civic-order>span').boundingBox(),action=await page.locator('.civic-order .row').boundingBox()
 expect(action!.y-copy!.y-copy!.height).toBeLessThan(24)
 await expect(page.locator('.wallet-hud')).toContainText('123');await expect(page.locator('.wallet-hud')).toContainText('Dcoin');await expect(page.locator('.wallet-hud')).toContainText('4');await expect(page.locator('.wallet-hud')).toContainText('тест')
 await page.locator('.wallet-hud').click();await expect(page.getByRole('dialog',{name:'Кошелёк',exact:true})).toBeVisible();await expect(page.locator('.economy-balances')).toContainText('123');await page.getByRole('button',{name:'Вернуться к карте'}).click();await expect(page.getByRole('dialog',{name:'Кошелёк',exact:true})).toHaveCount(0)
 await page.screenshot({path:`test-results/post-071-${test.info().project.name}.png`,fullPage:true})
})
