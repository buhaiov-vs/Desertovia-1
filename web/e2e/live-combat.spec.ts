import { readFileSync } from 'node:fs'
import { createHash } from 'node:crypto'
import { test, expect } from './fixtures.ts'
type RaidBattle={revision:number;units:{id:string;x:number;y:number;health:number;state:string;diedAt:string|null}[];[key:string]:unknown}
type Citizen={energy:number;maximumEnergy:number;energyPerHour:number;serverNow:string}

type Stand={terrain:(seed:number,wilds:unknown)=>number[];initial:RaidBattle;orders:{target:string|null;mode:string}[];update:(b:RaidBattle,time?:number)=>{children:number;positions:number[][];rotations:number[]};pick:(x:number,y:number)=>unknown;mergeBattles:(a:RaidBattle[],b:RaidBattle[])=>RaidBattle[];currentEnergy:(c:Citizen,now:number)=>number}

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
      picked:s.pick(b.units[1].x*16,b.units[1].y*16),headPick:s.pick(b.units[1].x*16,b.units[1].y*16-24),positionsChanged:JSON.stringify(start.positions)!==JSON.stringify(changed.positions)}
  })
  expect(result.count).toBe(26) // 12 enemy actor/bar pairs and the guard health bar pair.
  expect(result.changed).toBe(26);expect(result.dead).toBe(1);expect(result.positionsChanged).toBe(true)
  expect(result.picked).toEqual({battleId:'battle',unitId:'enemy-1'});expect(result.headPick).toEqual(result.picked)
})

for(const width of [320,390,1280])test(`enemy controls fit ${width}px and send orders without local damage`,async({page})=>{
  await page.setViewportSize({width,height:844})
  await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
  await page.goto('/e2e/live-combat-harness.html')
  const card=page.getByRole('complementary',{name:'Выбранный противник'})
  await expect(card).toContainText('Моё здоровье: 26/40')
  await expect(card).toContainText('8')
  await expect(card.getByRole('status')).toContainText('Цель выбрана')
  await expect(card.getByRole('button',{name:/Атаковать|Ближайший враг|Держать позицию|Перехватывать/i})).toHaveCount(0)
  await page.getByRole('button',{name:'Обновить здоровье'}).click()
  expect(await page.evaluate(()=> (window as unknown as {combatStand:Stand}).combatStand.orders)).toEqual([{target:'enemy-0',mode:'intercept'}])
  await page.getByRole('button',{name:'Выбрать другого врага'}).click()
  await expect.poll(()=>page.evaluate(()=> (window as unknown as {combatStand:Stand}).combatStand.orders)).toEqual([{target:'enemy-0',mode:'intercept'},{target:'enemy-1',mode:'intercept'}])
  await page.getByRole('button',{name:'Открыть или закрыть меню'}).click()
  await expect(card).toHaveCount(0)
  await page.getByRole('button',{name:'Открыть или закрыть меню'}).click()
  await expect(card).toBeVisible()
  expect(await page.evaluate(()=> (window as unknown as {combatStand:Stand}).combatStand.orders)).toHaveLength(2)
  await page.getByRole('button',{name:'Повторно выбрать врага'}).click()
  await expect.poll(()=>page.evaluate(()=> (window as unknown as {combatStand:Stand}).combatStand.orders.length)).toBe(3)
  await expect(card.locator('progress')).toHaveAttribute('value','20')
  expect(await card.evaluate(el=>el.scrollWidth<=el.clientWidth)).toBe(true)
  const box=await card.boundingBox();expect(box!.x).toBeGreaterThanOrEqual(0);expect(box!.x+box!.width).toBeLessThanOrEqual(width)
  await page.screenshot({path:test.info().outputPath(`combat-${width}.png`)})
})

test('steady combat animation reuses health geometry until health actually changes',async({page})=>{
  await page.goto('/e2e/live-combat-harness.html')
  await page.waitForFunction(()=>!!(window as unknown as {combatStand:unknown}).combatStand)
  const result=await page.evaluate(()=>(window as unknown as {combatStand:{frames:()=>{stableUpdates:number;changedUpdates:number;elapsed:number}}}).combatStand.frames())
  expect(result.stableUpdates).toBe(0)
  expect(result.changedUpdates).toBeGreaterThan(0)
  test.info().annotations.push({type:'animation-cpu-ms-300-frames',description:String(result.elapsed)})
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


test('server collision fixtures still match the authored ponds, mountains and marsh',async({page})=>{
  await page.goto('/e2e/live-combat-harness.html');await page.waitForFunction(()=>!!(window as unknown as {combatStand:unknown}).combatStand)
  const cases=JSON.parse(readFileSync(new URL('./fixtures/authored-terrain.json',import.meta.url),'utf8')) as {seed:number;wilds:unknown;hash:string}[]
  for(const sample of cases){
    const mask=await page.evaluate(({seed,wilds})=>(window as unknown as {combatStand:Stand}).combatStand.terrain(seed,wilds),sample)
    expect(createHash('sha256').update(Buffer.from(mask)).digest('hex')).toBe(sample.hash)
  }
})


test('the active second wave stays in the banner ahead of an overdue queued third wave',async({page})=>{
  await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
  await page.goto('/e2e/live-combat-harness.html')
  await expect(page.locator('#queue-banner')).toContainText('в деревне')
  await expect(page.locator('#queue-banner')).toContainText('волна 2 из 3')
})
