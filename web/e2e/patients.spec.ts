import {test,expect,uniqueName} from './fixtures.ts'
test('hospital contains its patients; a field infirmary keeps visible beds until discharge',async({page})=>{
 await page.setViewportSize({width:320,height:640})
 await page.request.post('/auth/dev-login',{data:{name:uniqueName('ward')}})
 await page.goto('/e2e/patients-harness.html')
 const inspect=()=>page.evaluate(()=>(window as unknown as {patientTest:{inspect:()=>{id:string;rotation:number;bed:boolean;reason:string;name:string;nameBounds:{x:number;y:number;width:number;height:number};statusBounds:{x:number;y:number;width:number;height:number};bodyBounds:{x:number;y:number;width:number;height:number}}[]}}).patientTest.inspect())
 const visibility=()=>page.evaluate(()=>(window as unknown as {patientTest:{visibility:()=>{body:boolean;bed:boolean;name:boolean;status:boolean}[]}}).patientTest.visibility())
 await expect.poll(async()=>{try{return (await visibility()).every(p=>!p.body&&!p.bed&&!p.name&&!p.status)}catch{return false}}).toBe(true)
 const damage=await page.evaluate(()=>(window as unknown as {patientTest:{damage:()=>{retained:boolean;lightsBefore:number;lightsAfter:number;overlays:number;elapsed:number}}}).patientTest.damage())
 expect(damage.retained).toBe(true);expect(damage.lightsAfter).toBe(damage.lightsBefore);expect(damage.overlays).toBe(1)
 test.info().annotations.push({type:'damage-update-cpu-ms-20-hits',description:String(damage.elapsed)})
 await page.locator('#ward').screenshot({path:test.info().outputPath('patients-inside.png')})
 await page.evaluate(()=>(window as unknown as {patientTest:{withoutHospital:()=>void}}).patientTest.withoutHospital())
 await expect.poll(async()=>(await inspect()).every(p=>p.bed)).toBe(true)
 await page.evaluate(()=>new Promise<void>(resolve=>requestAnimationFrame(()=>requestAnimationFrame(()=>resolve()))))
 const patients=await inspect();expect(patients).toHaveLength(4)
 expect(patients[0].reason).toContain('после дуэли');expect(patients[1].reason).toContain('после боя')
 for(const patient of patients){
  expect(patient.rotation).toBeCloseTo(-Math.PI/2,5)
  expect(patient.reason).toContain('На лечении')
   expect(patient.reason).not.toMatch(/реальн|час|минут|\d{2}:\d{2}/)
  for(const bounds of [patient.nameBounds,patient.statusBounds]){
   expect(bounds.x).toBeGreaterThanOrEqual(0);expect(bounds.x+bounds.width).toBeLessThanOrEqual(320)
  }
 }
 const overlaps=(a:{x:number;y:number;width:number;height:number},b:{x:number;y:number;width:number;height:number})=>a.x<b.x+b.width&&a.x+a.width>b.x&&a.y<b.y+b.height&&a.y+a.height>b.y
 for(let i=0;i<patients.length;i++)for(let j=i+1;j<patients.length;j++){
  for(const left of [patients[i].nameBounds,patients[i].statusBounds])for(const right of [patients[j].nameBounds,patients[j].statusBounds,patients[j].bodyBounds])expect(overlaps(left,right)).toBe(false)
 }
 await page.locator('#ward').screenshot({path:test.info().outputPath('patients-lying.png')})
 await page.evaluate(()=>(window as unknown as {patientTest:{discharge:()=>void}}).patientTest.discharge())
 await expect.poll(async()=>(await inspect()).every(p=>!p.bed&&p.rotation===0)).toBe(true)
})
