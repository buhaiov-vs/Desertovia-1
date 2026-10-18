import {test,expect,uniqueName} from './fixtures.ts'
import type {Page} from '@playwright/test'

type Stand={finishCancel:()=>void;retryCancel:()=>void;material:(level:number)=>void;damage:(n:number)=>void;open:(open:boolean)=>void;blocked:(code:string|null)=>void;night:(night:boolean)=>void;focus:(gate:boolean)=>void;bank:()=>void;zoom:(n:number)=>void;info:()=>{count:number;first:number;night:number;calls:string[]};alpha:()=>{common:number;holes:number;dimensions:number[][]}[]}
declare global {interface Window{fenceStand:Stand}}
test.beforeEach(async({page})=>{await page.request.post('/auth/dev-login',{data:{name:uniqueName('fences-ui')}});await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))})
async function openStand(page:Page,query=''){
 await page.goto('/e2e/fences-harness.html'+query)
 await expect.poll(()=>page.evaluate(()=>!!window.fenceStand)).toBe(true)
}

for(const level of [2,3])test(`level ${level} keeps solid silhouettes through dusk and matches the actual map`,async({page})=>{
 await openStand(page)
 const checks=await page.evaluate(()=>window.fenceStand.alpha())
 for(const c of checks){expect(c.common).toBeGreaterThan(100);expect(c.holes).toBe(0);expect(c.dimensions[0]).toEqual(c.dimensions[2])}
  await page.evaluate(l=>{window.fenceStand.material(l);window.fenceStand.night(false)},level)
  await expect.poll(()=>page.evaluate(()=>window.fenceStand.info().night)).toBeLessThan(.05)
  if(process.env.DESERTOD_ART_CAPTURE==='1')await page.locator('#fence-map').screenshot({scale:'css',path:test.info().outputPath(`fence-level${level}-day.png`)})
  await page.evaluate(()=>window.fenceStand.night(true));await expect.poll(()=>page.evaluate(()=>window.fenceStand.info().night)).toBeGreaterThan(.95)
  if(process.env.DESERTOD_ART_CAPTURE==='1')await page.locator('#fence-map').screenshot({scale:'css',path:test.info().outputPath(`fence-level${level}-night.png`)})
  await page.evaluate(()=>window.fenceStand.bank());if(process.env.DESERTOD_ART_CAPTURE==='1')await page.locator('#fence-map').screenshot({scale:'css',path:test.info().outputPath(`fence-level${level}-river-night.png`)});await page.evaluate(()=>window.fenceStand.focus(false))
})

test('damage packets keep the scene until a visible stage and gate opening removes only actual gate pieces',async({page})=>{
 await openStand(page)
 const initial=await page.evaluate(()=>window.fenceStand.info())
 expect(initial.count).toBeGreaterThanOrEqual(84)
 await page.evaluate(()=>window.fenceStand.damage(20));expect((await page.evaluate(()=>window.fenceStand.info())).first).toBe(initial.first)
 await page.evaluate(()=>window.fenceStand.damage(51));const changed=await page.evaluate(()=>window.fenceStand.info());expect(changed.first).not.toBe(initial.first)
 await page.evaluate(()=>window.fenceStand.damage(55));expect((await page.evaluate(()=>window.fenceStand.info())).first).toBe(changed.first)
 await page.evaluate(()=>{window.fenceStand.open(true);window.fenceStand.focus(true)})
 expect((await page.evaluate(()=>window.fenceStand.info())).count).toBe(initial.count-3)
 if(process.env.DESERTOD_ART_CAPTURE==='1')await page.locator('#fence-map').screenshot({scale:'css',path:test.info().outputPath('fence-gate-open.png')})
 await page.evaluate(()=>window.fenceStand.open(false));expect((await page.evaluate(()=>window.fenceStand.info())).count).toBe(initial.count)
})

test('governor sees a costed request and an announced raid explains why upgrade cannot start',async({page})=>{
 await openStand(page,'?governor=1')
 const button=page.getByRole('button',{name:'Запросить улучшение',exact:true})
 await expect(page.getByText('Прочность ×2',{exact:false})).toBeVisible()
 await page.evaluate(()=>window.fenceStand.blocked('defense.raidPending'));await expect(button).toBeDisabled()
 await expect(page.locator('.card')).toContainText('Дождись окончания всех волн')
 await page.evaluate(()=>window.fenceStand.blocked(null));await button.click()
 expect((await page.evaluate(()=>window.fenceStand.info())).calls).toEqual(['upgrade'])
 await expect(page.getByRole('status')).toContainText('Заявка отправлена царю')
})

test('dynamic fortification jobs remain visible and cancellation reports errors inside the menu then permits retry',async({page})=>{
 await openStand(page,'?work=1&cancelError=1')
 const defense=page.getByRole('region',{name:'Забор и ворота'})
 await expect(defense).toContainText('Город → Помочь стройке → Улучшение укреплений')
 await expect(defense).toContainText('Горное дело')
 await defense.screenshot({path:test.info().outputPath('fortification-work.png')})
 const jobs=page.locator('.sites')
 await expect(jobs.getByRole('button',{name:/Носить камни/})).toBeEnabled()
 await jobs.getByRole('button',{name:/Носить камни/}).click()
 expect((await page.evaluate(()=>window.fenceStand.info())).calls).toEqual(['job:0:bring-stones'])
 await expect(jobs.getByRole('button',{name:/Ставить столбы/})).toBeDisabled()
 const cancel=page.getByRole('button',{name:'Отменить чертёж',exact:true})
 await cancel.click();await expect(cancel).toBeDisabled()
 await page.evaluate(()=>window.fenceStand.finishCancel())
 await expect(page.getByRole('alert')).toContainText('Горожане уже поработали')
 await expect(cancel).toBeEnabled()
 await page.evaluate(()=>window.fenceStand.retryCancel());await cancel.click()
 await expect(cancel).toBeDisabled();await page.evaluate(()=>window.fenceStand.finishCancel())
 await expect(cancel).toHaveCount(0)
 await expect(defense.getByRole('button',{name:'Начать улучшение',exact:true})).toBeEnabled()
 await expect(page.locator('.site')).toHaveCount(0)
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true)
})

test('paid fortification work offers explicit dismantling with salvage instead of free cancellation',async({page})=>{
 await openStand(page,'?work=1&paid=1')
 await expect(page.getByRole('button',{name:'Отменить чертёж',exact:true})).toHaveCount(0)
 const defense=page.getByRole('region',{name:'Забор и ворота'})
 await expect(defense).toContainText('Носить камни: 12 / 420')
 await defense.getByRole('button',{name:'Разобрать площадку',exact:true}).click()
 await expect(defense).toContainText('25%')
 await defense.getByRole('button',{name:'Подтвердить разборку',exact:true}).click()
 await expect(defense).toContainText('Площадка разбирается')
 await expect(page.getByRole('button',{name:'Отменить чертёж',exact:true})).toHaveCount(0)
 expect((await page.evaluate(()=>window.fenceStand.info())).calls).toEqual(['site-demolish'])
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true)
})
