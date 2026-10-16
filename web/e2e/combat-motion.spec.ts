import {test,expect,uniqueName} from './fixtures.ts'
test('slow fighting avatars retain subpixel motion and do not stop before a delayed packet',async({page})=>{
 await page.request.post('/auth/dev-login',{data:{name:uniqueName('motion')}})
 await page.goto('/e2e/patients-harness.html')
 await page.waitForFunction(()=>!!(window as unknown as {patientTest:unknown}).patientTest)
 const result=await page.evaluate(()=>(window as unknown as {patientTest:{motionCheck:()=>{steadyFrames:number;distinctSteady:number;distinctRaw:number;lateFrames:number;distinctLate:number;maxHealthBarDrift:number}}}).patientTest.motionCheck())
 test.info().annotations.push({type:'actual-map-motion',description:JSON.stringify(result)})
 console.log('Synthetic map motion samples',JSON.stringify(result))
 expect(result.distinctSteady,'visible actor should move on at least half of rendered frames').toBeGreaterThan(result.steadyFrames/2)
 expect(result.maxHealthBarDrift,'health bar and avatar must use the same motion sample').toBeLessThan(.01)
 expect(result.distinctLate,'a late movement snapshot must not cause a half-second stop').toBeGreaterThan(result.lateFrames/2)
})

test('wall health packets skip world rebuilds until a breach or gate geometry change',async({page})=>{
 await page.request.post('/auth/dev-login',{data:{name:uniqueName('wall-motion')}})
 await page.goto('/e2e/patients-harness.html')
 await page.waitForFunction(()=>!!(window as unknown as {patientTest:unknown}).patientTest)
 const result=await page.evaluate(()=>(window as unknown as {patientTest:{wallCheck:()=>{damageUpdates:number;breachUpdates:number;gateUpdates:number}}}).patientTest.wallCheck())
 expect(result).toEqual({damageUpdates:0,breachUpdates:1,gateUpdates:1})
})


test('working avatars cross the river by the bridge instead of walking directly across water',async({page})=>{
 await page.request.post('/auth/dev-login',{data:{name:uniqueName('work-path')}})
 await page.goto('/e2e/patients-harness.html');await page.waitForFunction(()=>!!(window as unknown as {patientTest:unknown}).patientTest)
 const result=await page.evaluate(()=>(window as unknown as {patientTest:{workPathCheck:()=>{points:number;unsafe:number;usesBridge:boolean}}}).patientTest.workPathCheck())
 expect(result.points).toBeGreaterThan(0);expect(result.unsafe).toBe(0);expect(result.usesBridge).toBe(true)
})
