import {test,expect,developmentLogin,expectVillageView,DEMO_SLUG,uniqueName} from './fixtures.ts'

test('hut stages retain world lights, refresh deliveries and clean up temporary views',async({page})=>{
 await page.goto(`/v/${DEMO_SLUG}`)
 await developmentLogin(page,uniqueName('construction'))
 await expectVillageView(page)
 await page.goto('/e2e/harness.html')
 await expect(page.locator('#construction-preview')).toBeVisible()
 const result=await page.evaluate(()=>{
  const api=(window as unknown as {constructionTest:{render(n:number):void;phases():string[];coordinates():{position:number[];light:number[]}[];delivery():boolean;cleanup():{before:number;after:number};lightLevels():number[]}}).constructionTest
  api.render(0);const day=api.lightLevels();api.render(0.5);const dusk=api.lightLevels();api.render(1)
  return {phases:api.phases(),coordinates:api.coordinates(),delivery:api.delivery(),cleanup:api.cleanup(),day,dusk,night:api.lightLevels()}
 })
 expect(result.phases).toEqual(['ground','foundation','frame','walls','roof','finish'])
 for(const p of result.coordinates)expect(p.light).toEqual(p.position)
 expect(result.delivery).toBe(true);expect(result.cleanup.after).toBe(result.cleanup.before)
 expect(result.day).toEqual(Array(6).fill(0));expect(result.dusk).toEqual(Array(6).fill(0.5));expect(result.night).toEqual(Array(6).fill(1))
 await page.locator('#construction-preview').screenshot({path:test.info().outputPath('hut-stages-night.png')})
 await page.evaluate(()=>{(window as unknown as {constructionTest:{render(n:number):void}}).constructionTest.render(0)})
 await page.locator('#construction-preview').screenshot({path:test.info().outputPath('hut-stages-day.png')})
})
