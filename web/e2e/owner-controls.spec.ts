import { test, expect } from './fixtures.ts'
test.beforeEach(async({page})=>{await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))})
test('combat medicine is natively disabled without stock, becomes available and issues one receipted action',async({page})=>{
 await page.goto('/e2e/owner-controls-harness.html')
 const medicine=page.getByRole('button',{name:'Лечение',exact:false})
 await expect(medicine).toBeDisabled();await expect(medicine).toContainText('Нет лекарства')
 await medicine.evaluate((el:HTMLButtonElement)=>el.click())
 expect(await page.evaluate(()=>(window as unknown as {ownerStand:{orders:unknown[]}}).ownerStand.orders)).toHaveLength(0)
 await page.getByRole('button',{name:'Дать лекарство на стенде'}).click();await expect(medicine).toBeEnabled();await medicine.click()
 await expect(medicine).toBeDisabled()
 const orders=await page.evaluate(()=>(window as unknown as {ownerStand:{orders:{mode:string;requestId:string}[]}}).ownerStand.orders)
 expect(orders).toHaveLength(1);expect(orders[0].mode).toBe('heal');expect(orders[0].requestId).toBeTruthy()
 await page.getByRole('button',{name:'Завершить бой на стенде'}).click();await expect(page.getByRole('group',{name:'Действия в бою'})).toHaveCount(0)
})
for(const width of [360,390,430,1440])test(`own HP and stamina fit the shared top row at ${width}px without permanent construction buttons`,async({page})=>{
 await page.setViewportSize({width,height:900});await page.goto('/e2e/owner-controls-harness.html')
 const vitals=page.getByLabel('Мои здоровье и силы');await expect(vitals).toBeVisible()
 await expect(vitals.getByRole('progressbar',{name:'Моё здоровье'})).toHaveAttribute('value','26');await expect(vitals.getByRole('progressbar',{name:'Мои силы'})).toHaveAttribute('value','24')
 await expect(vitals).not.toContainText(/Здоров|На лечении|26\/40|24\/40/)
 await vitals.getByRole('button',{name:'Показать HP'}).click();await expect(page.getByRole('tooltip')).toContainText('26/40');await page.keyboard.press('Escape')
 await expect(page.getByRole('button',{name:'Начать голосование',exact:true})).toHaveCount(0);await expect(page.getByRole('button',{name:'Строить',exact:true})).toHaveCount(0)
 const launch=await page.locator('.chat-launch').boundingBox()
 for(const button of await page.locator('.personal-battle-panel button').all()){
   const box=await button.boundingBox();expect(box!.height).toBe(44);expect(box!.height).toBe(launch!.height);expect(box!.y).toBe(launch!.y)
   expect(box!.x+box!.width).toBeLessThanOrEqual(width)
 }
 const boxes=await page.locator('.hud-state-row > *').evaluateAll(els=>els.map(el=>{const b=el.getBoundingClientRect();return {left:b.left,right:b.right,top:b.top,bottom:b.bottom}}))
 for(const b of boxes){expect(b.left).toBeGreaterThanOrEqual(0);expect(b.right).toBeLessThanOrEqual(width+1)}
 for(let i=0;i<boxes.length;i++)for(let j=i+1;j<boxes.length;j++){const a=boxes[i],b=boxes[j];expect(Math.min(a.right,b.right)<=Math.max(a.left,b.left)+1||Math.min(a.bottom,b.bottom)<=Math.max(a.top,b.top)+1).toBe(true)}
 await page.screenshot({path:test.info().outputPath(`owner-hud-${width}.png`)})

})
