import {test,expect,uniqueName} from './fixtures.ts'
declare global{interface Window{progressionStand:{calls:string[];stock:()=>void;later:()=>void}}}
test.beforeEach(async({page})=>{await page.request.post('/auth/dev-login',{data:{name:uniqueName('progression-ui')}});await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'));await page.goto('/e2e/progression-harness.html')})
test('knowledge shows a bounded price on the elected technology and cannot be spent twice',async({page})=>{
 const card=page.locator('.card').filter({has:page.getByRole('heading',{name:'Применить знания'})})
 await expect(card).toContainText('максимум на 25%');await expect(card).toContainText('30')
 await card.getByRole('button',{name:'Вложить 10 · +2 работы'}).click();await expect(card.getByRole('status')).toContainText('уже вложены');await expect(card).toContainText('20')
 expect(await page.evaluate(()=>window.progressionStand.calls)).toEqual(['knowledge'])
 const bounds=await card.boundingBox();expect(bounds!.x).toBeGreaterThanOrEqual(0);expect(bounds!.x+bounds!.width).toBeLessThanOrEqual(page.viewportSize()!.width+1)
})
test('meal buttons are disabled without portions and one effect blocks stacking and remains on cooldown after expiry',async({page})=>{
 const card=page.locator('.card').filter({has:page.getByRole('heading',{name:'Блюда',exact:true})})
 await expect(card.getByRole('button',{name:/каша/})).toBeDisabled();await page.evaluate(()=>window.progressionStand.stock())
 await card.getByRole('button',{name:/каша/}).click();await expect(card.getByRole('status')).toContainText('20 мин');await expect(card.getByRole('button',{name:/похлёбка/})).toBeDisabled()
 await page.evaluate(()=>window.progressionStand.later());await expect(card.getByRole('status')).toHaveCount(0);await expect(card).toContainText('Следующее блюдо через 9 мин');await expect(card.getByRole('button',{name:/каша/})).toBeDisabled()
 expect(await page.evaluate(()=>window.progressionStand.calls)).toEqual(['Porridge'])
})
