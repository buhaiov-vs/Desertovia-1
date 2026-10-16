import { test, expect, developmentLogin, expectVillageView, openMenu, DEMO_SLUG, uniqueName } from './fixtures.ts'

// Real Pixi composition is substantially heavier than the isolated chat stand.
test.setTimeout(120_000)
test.use({ deviceScaleFactor: 1, trace: { mode: 'retain-on-failure', screenshots: false, snapshots: true } })

test('real map enemy selection sends an attack immediately without opening an enemy card',async({page})=>{
  await page.addInitScript(()=>localStorage.setItem('desertod.language','en'))
  await page.goto(`/v/${DEMO_SLUG}`);await developmentLogin(page,uniqueName('tap-attack'));await expectVillageView(page)
  const battleId='78cad270-8fc4-4890-b763-552395a33911',unitId='b6b920ff-b572-48fd-8f97-37df982b56a7'
  const orders:unknown[]=[]
  await page.route(`**/api/villages/${DEMO_SLUG}/battles/${battleId}/order`,async route=>{
    orders.push(route.request().postDataJSON());await route.fulfill({json:{}})
  })
  await page.evaluate(({battleId,unitId})=>window.dispatchEvent(new CustomEvent('desertovia-e2e-enemy',{detail:{battleId,unitId}})),{battleId,unitId})
  await expect.poll(()=>orders).toEqual([{targetUnitId:unitId,mode:'intercept'}])
  await expect(page.locator('.battle-controls')).toHaveCount(0)
  await expect(page.getByRole('button',{name:'Attack',exact:true})).toHaveCount(0)
})

test('personal HUD matches the resource cards on the real map and leaves navigation uncluttered',async({page})=>{
  await page.addInitScript(()=>{if(!localStorage.getItem('desertod.language'))localStorage.setItem('desertod.language','en');localStorage.setItem('desertod.chat.mode','hidden')})
  await page.goto(`/v/${DEMO_SLUG}`);await developmentLogin(page,uniqueName('hud-map'));await expectVillageView(page)
  await page.evaluate(()=>localStorage.setItem('desertod.language','ru'));await page.reload();await expectVillageView(page)
  const vitals=page.getByLabel('Мои здоровье и силы');await expect(vitals).toBeVisible()
  const appearance=await vitals.evaluate(el=>{
    const card=el.closest('.hud-resources')!.querySelector('.resource-chip')!,weather=el.parentElement!.querySelector('.weather')!
    const a=getComputedStyle(el),b=getComputedStyle(card)
    return {background:a.backgroundColor,cardBackground:b.backgroundColor,border:a.border,borderCard:b.border,radius:a.borderRadius,cardRadius:b.borderRadius,height:el.getBoundingClientRect().height,weatherHeight:weather.getBoundingClientRect().height}
  })
  expect(appearance.background).toBe(appearance.cardBackground);expect(appearance.border).toBe(appearance.borderCard);expect(appearance.radius).toBe(appearance.cardRadius)
  expect(appearance.height).toBe(appearance.weatherHeight)
  await expect(page.locator('.battle-controls')).toHaveCount(0)
  await expect(page.locator('.hud-poll-actions')).toHaveCount(0)
  await page.screenshot({path:test.info().outputPath('owner-real-map.png')})
  const me=await(await page.request.get(`/api/villages/${DEMO_SLUG}/me`)).json()
  const data=await(await page.request.get(`/api/villages/${DEMO_SLUG}`)).json()
  const at=new Date().toISOString()
  data.state.battles=[{id:'fd8ac776-f3f9-4981-812b-cfa1df02b26b',villageId:data.state.id,engineVersion:2,phase:'Fighting',sector:'north',tactic:'entry',composition:'swarm',threatBudget:20,readyDefenders:1,availableReserves:0,startsAt:at,lastTickAt:at,endsAt:new Date(Date.now()+600000).toISOString(),finishedAt:null,revision:999999,deferred:false,units:[],events:[],
    fighters:[{citizenId:me.id,x:15.5,y:13.5,health:26,maximumHealth:40,damageDealt:0,kills:0,renownEarned:0,wounded:false,guard:true,active:true,mode:'intercept',targetUnitId:null,nextAttackAt:at,weapon:null,armor:null,arrows:0,isTsar:false,accountId:null,healReadyAt:null}]}]
  await page.route(`**/api/villages/${DEMO_SLUG}`,route=>route.fulfill({json:data}))
  await page.reload();await expectVillageView(page)
  const actions=page.getByRole('group',{name:'Действия в бою'});await expect(actions).toBeVisible()
  await expect(page.locator('.hud-needs')).toHaveCount(0);await expect(page.locator('.hud-left .hud-status')).toBeHidden()
  const actionBox=await actions.boundingBox(),navBox=await page.locator('.hud-buttons').boundingBox()
  expect(actionBox!.y+actionBox!.height).toBeLessThanOrEqual(navBox!.y)
  await expect(page.getByRole('progressbar',{name:'Моё здоровье',exact:true})).toHaveAttribute('value','26')
  await page.screenshot({path:test.info().outputPath('owner-real-map-battle.png')})
})

for (const mode of ['hidden', 'quarter'] as const) test(`real game selected object owns its header with ${mode} chat`, async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('desertod.language', 'en')
    localStorage.setItem('desertod.chat.mode', 'hidden')
    localStorage.setItem('desertovia.uiTheme', 'dark')
  })
  await page.goto(`/v/${DEMO_SLUG}`)
  await developmentLogin(page, uniqueName('popup-regression'))
  await expectVillageView(page)
  const chat = page.locator('.chat-panel')
  const background = await chat.evaluate(el => ({ background: getComputedStyle(el).backgroundColor, hit: getComputedStyle(el).pointerEvents, width: el.getBoundingClientRect().width }))
  expect(background.background).toBe('rgba(0, 0, 0, 0)')
  expect(background.hit).toBe('none')
  expect(background.width).toBeLessThan(180)
  const viewport = page.viewportSize()!
  const popup = page.locator('.target-popup')
    if (mode === 'quarter') await page.getByRole('button', { name: 'Open chat' }).click()
    const state = await (await page.request.get(`/api/villages/${DEMO_SLUG}`)).json()
    const storage = state.state.storages[0] as { plotId: string; kind: string }
    const [x, y] = storage.plotId.split(',').map(Number)
    await page.evaluate(({ storage, x, y, screen }) => {
      window.dispatchEvent(new CustomEvent('desertovia-e2e-select', { detail: {
        target: { kind: 'building', plotId: storage.plotId, building: storage.kind, storage: true, area: { x, y, width: 3, height: 3 } },
        screen, point: { x, y },
      } }))
    }, { storage, x, y, screen: { x: viewport.width / 2, y: viewport.height / 2 } })
    await expect(popup).toBeVisible()
    await expect(chat).toBeHidden()
    const header = popup.locator('.target-popup-header')
    await expect(header.locator('strong')).not.toBeEmpty()
    await expect(header.locator('button.close')).toBeVisible()
    const unblocked = await header.locator('button.close').evaluate(el => {
      const r = el.getBoundingClientRect()
      return el.contains(document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2))
    })
    expect(unblocked, 'The card close button receives the tap').toBe(true)
    // The map keeps animating behind the sheet. Hit-test first, then send a real pointer click
    // without waiting for the renderer to provide two identical animation frames.
    const closeBox = await header.locator('button.close').boundingBox()
    expect(closeBox).not.toBeNull()
    await page.mouse.click(closeBox!.x + closeBox!.width / 2, closeBox!.y + closeBox!.height / 2)
    await expect(popup).toBeHidden()
    await expect(chat).toBeVisible()
    await expect(chat).toHaveAttribute('data-mode', mode)
})

test('real character sheet shows spouse once and preserves own stat rows on a phone', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('desertod.language', 'en'))
  await page.goto(`/v/${DEMO_SLUG}`)
  const name = uniqueName('my-stats')
  await developmentLogin(page, name)
  await expectVillageView(page)
  await page.route('**/api/villages/*/me', async route => {
    const response = await route.fetch()
    const data = await response.json()
    data.spouseName = 'SpouseWithAVeryLongNameForPhone'
    data.homePlotId = '9,11'
    await route.fulfill({ response, json: data })
  })
  await page.reload()
  await expectVillageView(page)
  const menu = await openMenu(page)
  await menu.locator('nav.tabs button', { hasText: 'Character' }).click()
  await expect(menu.locator('.character-title')).toContainText(`My character — ${name}`)
  await expect(menu.getByText('Супруг: SpouseWithAVeryLongNameForPhone', { exact: true })).toHaveCount(1)
  const stats = menu.locator('table.stats').first()
  await expect(stats.locator('tbody > tr')).toHaveCount(5)
  await expect(stats.locator('p')).toHaveCount(0)
  expect(await stats.evaluate(el => el.scrollWidth <= el.clientWidth + 1)).toBe(true)
})
