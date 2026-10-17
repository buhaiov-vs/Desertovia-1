import {test,expect,uniqueName} from './fixtures.ts'

type Stand={material:(level:number)=>void;damage:(n:number)=>void;open:(open:boolean)=>void;blocked:(code:string|null)=>void;night:(night:boolean)=>void;focus:(gate:boolean)=>void;bank:()=>void;zoom:(n:number)=>void;info:()=>{count:number;first:number;night:number;calls:string[]};alpha:()=>{common:number;holes:number;dimensions:number[][]}[]}
declare global {interface Window{fenceStand:Stand}}
test.beforeEach(async({page})=>{await page.request.post('/auth/dev-login',{data:{name:uniqueName('fences-ui')}});await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'));await page.goto('/e2e/fences-harness.html');await expect.poll(()=>page.evaluate(()=>!!window.fenceStand)).toBe(true)})

for(const level of [2,3])test(`level ${level} keeps solid silhouettes through dusk and matches the actual map`,async({page})=>{
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
 await page.goto('/e2e/fences-harness.html?governor=1');await expect.poll(()=>page.evaluate(()=>!!window.fenceStand)).toBe(true)
 const button=page.getByRole('button',{name:'Запросить улучшение',exact:true})
 await expect(page.getByText('Прочность ×2',{exact:false})).toBeVisible()
 await page.evaluate(()=>window.fenceStand.blocked('defense.raidPending'));await expect(button).toBeDisabled()
 await expect(page.locator('.card')).toContainText('Дождись окончания всех волн')
 await page.evaluate(()=>window.fenceStand.blocked(null));await button.click()
 expect((await page.evaluate(()=>window.fenceStand.info())).calls).toEqual(['upgrade'])
 await expect(page.getByRole('status')).toContainText('Заявка отправлена царю')
})
