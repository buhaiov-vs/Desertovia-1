import { test, expect } from './fixtures.ts'
type RaidBattle={revision:number;units:{id:string;x:number;y:number;health:number;state:string;diedAt:string|null}[];[key:string]:unknown}
type Citizen={energy:number;maximumEnergy:number;energyPerHour:number;serverNow:string}

type Stand={initial:RaidBattle;orders:{target:string|null;mode:string}[];update:(b:RaidBattle,time?:number)=>{children:number;positions:number[][];rotations:number[]};pick:(x:number,y:number)=>unknown;mergeBattles:(a:RaidBattle[],b:RaidBattle[])=>RaidBattle[];currentEnergy:(c:Citizen,now:number)=>number}

test('live renderer shows every actual enemy, individual deaths and authoritative positions',async({page})=>{
  await page.goto('/e2e/live-combat-harness.html');await page.waitForFunction(()=>!!(window as unknown as {combatStand:unknown}).combatStand)
  const result=await page.evaluate(()=>{
    const s=(window as unknown as {combatStand:Stand}).combatStand
    const b=structuredClone(s.initial)
    const start=s.update(b)
    b.units[0].health=0;b.units[0].state='Dead';b.units[0].diedAt='2026-10-15T12:00:02Z'
    b.units[1].x+=1;b.revision++
    const changed=s.update(b)
    return {count:start.children,changed:changed.children,dead:changed.rotations.filter(n=>Math.abs(n-Math.PI/2)<.01).length,
      picked:s.pick(b.units[1].x*16,b.units[1].y*16),positionsChanged:JSON.stringify(start.positions)!==JSON.stringify(changed.positions)}
  })
  expect(result.count).toBe(26) // 12 enemy actor/bar pairs and the guard health bar pair.
  expect(result.changed).toBe(26);expect(result.dead).toBe(1);expect(result.positionsChanged).toBe(true)
  expect(result.picked).toEqual({battleId:'battle',unitId:'enemy-1'})
})

for(const width of [320,390,1280])test(`enemy controls fit ${width}px and send orders without local damage`,async({page})=>{
  await page.setViewportSize({width,height:844})
  await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
  await page.goto('/e2e/live-combat-harness.html')
  const card=page.getByRole('complementary',{name:'Выбранный противник'})
  await expect(card).toContainText('Моё здоровье: 26/40')
  await expect(card).toContainText('8')
  await card.getByRole('button',{name:'Атаковать',exact:true}).click()
  await card.getByRole('button',{name:'Держать позицию'}).click()
  await card.getByRole('button',{name:'Ближайший враг'}).click()
  expect(await page.evaluate(()=> (window as unknown as {combatStand:Stand}).combatStand.orders)).toEqual([
    {target:'enemy-0',mode:'intercept'},{target:null,mode:'hold'},{target:null,mode:'intercept'}])
  await expect(card.locator('progress')).toHaveAttribute('value','20')
  expect(await card.evaluate(el=>el.scrollWidth<=el.clientWidth)).toBe(true)
  const box=await card.boundingBox();expect(box!.x).toBeGreaterThanOrEqual(0);expect(box!.x+box!.width).toBeLessThanOrEqual(width)
  await page.screenshot({path:test.info().outputPath(`combat-${width}.png`)})
})

test('a stale village snapshot cannot rewind a newer battle revision and energy projects fractional recovery',async({page})=>{
  await page.goto('/e2e/live-combat-harness.html');await page.waitForFunction(()=>!!(window as unknown as {combatStand:unknown}).combatStand)
  const result=await page.evaluate(()=>{
    const s=(window as unknown as {combatStand:Stand}).combatStand
    const fresh=structuredClone(s.initial);fresh.revision=4;fresh.units[0].health=7
    const result=s.mergeBattles([fresh],[s.initial])[0]
    const c={energy:.5,maximumEnergy:40,energyPerHour:4,serverNow:'2026-10-15T12:00:00Z'} as Citizen
    return {revision:result.revision,hp:result.units[0].health,energy:s.currentEnergy(c,Date.parse(c.serverNow)+900000)}
  })
  expect(result).toEqual({revision:4,hp:7,energy:1.5})
})
