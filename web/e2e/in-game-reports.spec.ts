import { test, expect, openMenu, uniqueName } from './fixtures.ts'
import { writeFile } from 'node:fs/promises'
import sharp from 'sharp'
import { createRequire } from 'node:module'
const { version: GAME_VERSION } = createRequire(import.meta.url)('../package.json')

test.use({serviceWorkers:'block'})

async function village(page: import('@playwright/test').Page) {
  await page.goto('/')
  // Settle initial account loading before API login/navigation, including WebKit.
  await expect(page.locator('section.login')).toBeVisible()
  await page.request.post('/auth/dev-login', {data:{name:uniqueName('report')}})
  await page.request.post('/api/me/character', {data:{name:'Report tester',appearance:{body:'Female',style:0,skinTone:0,hairColor:0,outfitColor:0}}})
  const created = await (await page.request.post('/api/villages', {data:{name:uniqueName('Report village'),isPrivate:false}})).json()
  await page.goto(`/v/${created.slug}`)
  await expect(page.locator('.hud-resources')).toBeVisible()
  await expect(page.locator('.game-canvas canvas').first()).toBeVisible()
  return created.slug as string
}
const trigger = (page: import('@playwright/test').Page) => page.getByRole('button',{name:'Сообщить об ошибке или предложить идею'})
const form = (page: import('@playwright/test').Page) => page.getByRole('dialog',{name:'Обратная связь'})

test('small symbol stays above actual menu; cancel, X and Escape discard unsent screenshots',async({page},info)=>{
  test.slow()
  await village(page);await openMenu(page)
  await expect.poll(()=>page.locator('.modal-backdrop').evaluate(el=>getComputedStyle(el).opacity)).toBe('1')
  const expected = await page.locator('.modal.menu').evaluate(el=>{
    const body=el.querySelector('.modal-body')!
    const rect=body.getBoundingClientRect()
    // Compare a background region rather than one border/text pixel.
    return {x:rect.x+16,y:rect.y+20,w:rect.width-32,h:rect.height-40,width:innerWidth,color:getComputedStyle(el).backgroundColor.match(/\d+/g)!.slice(0,3).map(Number)}
  })
  const writes: string[]=[]
  page.on('request',r=>{if(r.url().includes('/api/reports'))writes.push(r.method())})
  const storageBefore=await page.evaluate(()=>({local:{...localStorage},session:{...sessionStorage}}))
  await expect(trigger(page)).toHaveText('!')
  const rect=await trigger(page).boundingBox();expect(rect!.width).toBeLessThanOrEqual(36);expect(rect!.height).toBeLessThanOrEqual(36)
  await trigger(page).click();await expect(form(page)).toBeVisible()
  await expect(form(page).locator('img')).toBeVisible()
  const source=await form(page).locator('img').getAttribute('src')
  const bytes=Buffer.from(source!.split(',')[1],'base64')
  expect(bytes.length).toBeLessThanOrEqual(350000)
  const stats=await sharp(bytes).stats();expect(stats.entropy).toBeGreaterThan(2)
  await writeFile(info.outputPath('captured-menu.jpg'),bytes)
  const meta=await sharp(bytes).metadata(),scale=meta.width!/expected.width
  const pixels=await sharp(bytes).removeAlpha().extract({left:Math.round(expected.x*scale),top:Math.round(expected.y*scale),width:Math.floor(expected.w*scale),height:Math.floor(expected.h*scale)}).raw().toBuffer()
  let opaque=0
  for(let offset=0;offset<pixels.length;offset+=3)if(expected.color.every((color:number,channel:number)=>Math.abs(pixels[offset+channel]-color)<18))opaque++
  expect(opaque/(pixels.length/3),'Menu background must remain opaque in the captured image').toBeGreaterThan(.2)
  await page.screenshot({path:info.outputPath('report-form.png')})
  await form(page).getByLabel('Описание').fill('Do not send this cancelled draft')
  await form(page).getByRole('button',{name:'Отмена',exact:true}).click()
  await expect(form(page)).toBeHidden();await expect(page.locator('.report-preview')).toHaveCount(0)
  await trigger(page).click();await expect(form(page)).toBeVisible()
  await expect(form(page).getByLabel('Описание')).toHaveValue('')
  await form(page).getByRole('button',{name:'Закрыть отчёт'}).click()
  await trigger(page).click();await expect(form(page)).toBeVisible()
  await page.keyboard.press('Escape');await expect(form(page)).toBeHidden()
  expect(writes).toEqual([])
  expect(await page.evaluate(()=>({local:{...localStorage},session:{...sessionStorage}}))).toEqual(storageBefore)
  await expect(page.getByRole('dialog',{name:'Menu',exact:true})).toBeVisible()
})

test('explicit submit includes snapshot context; retry reuses the entire original request',async({page})=>{
  test.slow();const slug=await village(page);await openMenu(page)
  const attempts: Record<string,unknown>[]=[]
  await page.route('**/api/reports',async route=>{
    attempts.push(route.request().postDataJSON())
    if(attempts.length===1)return route.abort('failed')
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({id:attempts[0].id,state:'saved'})})
  })
  await trigger(page).click();await form(page).getByLabel('Описание').fill('Menu failed')
  await form(page).getByRole('button',{name:'Отправить',exact:true}).click()
  await expect(form(page).getByRole('alert')).toBeVisible()
  await expect(form(page).getByLabel('Описание')).toBeDisabled()
  await form(page).getByRole('button',{name:'Отправить',exact:true}).click()
  await expect(page.getByRole('dialog',{name:'Отчёт сохранён'})).toBeVisible()
  expect(attempts).toHaveLength(2);expect(attempts[0]).toEqual(attempts[1])
  expect(attempts[0]).toMatchObject({slug,kind:'bug',text:'Menu failed',screen:{page:'village',menu:'menu:village',clientVersion:GAME_VERSION}})
  expect(typeof attempts[0].screenshot).toBe('string')
  expect(attempts[0]).not.toHaveProperty('accountId')
  await expect(page.locator('.report-preview')).toHaveCount(0)
})

test('removing screenshot still allows submitting a suggestion to actual API',async({page})=>{
  test.slow();await village(page)
  await trigger(page).click();await form(page).getByLabel('Тип обращения').selectOption('suggestion')
  await form(page).getByLabel('Описание').fill('Test-only proposal in isolated database')
  await form(page).getByRole('button',{name:'Убрать снимок'}).click()
  const result=page.waitForResponse(r=>r.url().endsWith('/api/reports')&&r.request().method()==='POST')
  await form(page).getByRole('button',{name:'Отправить',exact:true}).click()
  const response=await result;expect(response.status()).toBe(200)
  expect(response.request().postDataJSON()).toMatchObject({kind:'suggestion',screenshot:null})
  await expect(page.getByRole('dialog',{name:'Отчёт сохранён'})).toBeVisible()
})

test('capture failure leaves a usable cancellable form without uploading anything',async({page})=>{
  test.slow();await village(page)
  await page.route('**/*html2canvas*',route=>route.abort('failed'))
  const writes:string[]=[]
  page.on('request',r=>{if(r.url().includes('/api/reports'))writes.push(r.method())})
  await trigger(page).click();await expect(form(page)).toBeVisible()
  await expect(form(page).getByText('Без снимка экрана.',{exact:false})).toBeVisible()
  await form(page).getByRole('button',{name:'Отмена',exact:true}).click()
  expect(writes).toEqual([])
})

test('report control also covers the actual dungeon page and records its run ID',async({page})=>{
  test.slow();const slug=await village(page)
  const runId='12345678-1234-1234-1234-123456789abc'
  await page.goto(`/v/${slug}/dungeon/${runId}`)
  await expect(page.locator('.dungeon-root .game-canvas canvas')).toBeVisible()
  await trigger(page).click();await expect(form(page).locator('img')).toBeVisible()
  await form(page).getByLabel('Описание').fill('Dungeon report fixture')
  let body:Record<string,unknown>|null=null
  await page.route('**/api/reports',async route=>{
    body=route.request().postDataJSON()
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({id:body!.id,state:'saved'})})
  })
  await form(page).getByRole('button',{name:'Отправить',exact:true}).click()
  await expect(page.getByRole('dialog',{name:'Отчёт сохранён'})).toBeVisible()
  expect(body).toMatchObject({slug,screen:{page:'dungeon',dungeonId:runId}})
})


for(const night of [false,true])test(`report preserves the populated village map ${night?'at night':'by day'}`,async({page},info)=>{
  test.slow()
  await page.request.post('/auth/dev-login',{data:{name:uniqueName('report-map')}})
  await page.goto('/e2e/fences-harness.html?report=1')
  await page.waitForFunction(()=>!!(window as unknown as {fenceStand?:unknown}).fenceStand)
  const stand=async(action:'night'|'pause',value?:boolean)=>page.evaluate(({action,value})=>{
    const harness=(window as unknown as {fenceStand:{night:(v:boolean)=>void;pause:()=>void}}).fenceStand
    if(action==='night')harness.night(!!value);else harness.pause()
  },{action,value})
  await stand('night',night)
  if(night)await expect.poll(()=>page.evaluate(()=>(window as unknown as {fenceStand:{info:()=>{night:number}}}).fenceStand.info().night)).toBeGreaterThan(.95)
  await stand('pause')
  const canvas=page.locator('#fence-map canvas').first(),rect=(await canvas.boundingBox())!
  const original=await canvas.screenshot(),width=await page.evaluate(()=>innerWidth)
  await trigger(page).click();await expect(form(page).locator('img')).toBeVisible()
  const src=await form(page).locator('img').getAttribute('src'),bytes=Buffer.from(src!.split(',')[1],'base64')
  await writeFile(info.outputPath('populated-map-report.jpg'),bytes)
  await writeFile(info.outputPath('populated-map-original.png'),original)
  const meta=await sharp(bytes).metadata(),scale=meta.width!/width
  const area={left:Math.round(32*scale),top:Math.round(48*scale),width:Math.floor((rect.width-80)*scale),height:Math.floor((rect.height-96)*scale)}
  const normalised=await sharp(original).resize(Math.round(rect.width*scale),Math.round(rect.height*scale)).toBuffer()
  const expected=await sharp(normalised).extract(area).resize(160,120).removeAlpha().raw().toBuffer()
  const captured=await sharp(bytes).extract({...area,left:area.left+Math.round(rect.x*scale),top:area.top+Math.round(rect.y*scale)}).resize(160,120).removeAlpha().raw().toBuffer()
  let error=0;for(let i=0;i<expected.length;i++)error+=Math.abs(expected[i]-captured[i])
  expect(error/expected.length,'Report must contain the same terrain, buildings and fence as the visible canvas').toBeLessThan(15)
  await form(page).getByRole('button',{name:'Отмена',exact:true}).click()
})


test('definitive report rejection allows correcting attachment and resubmitting without locking the draft',async({page})=>{
  test.slow();await village(page);const requests:Record<string,unknown>[]=[];
  await page.route('**/api/reports',async route=>{
    requests.push(route.request().postDataJSON());
    await route.fulfill({status:requests.length===1?400:200,contentType:'application/json',body:JSON.stringify(requests.length===1?{code:'report.invalid',message:'Check report'}:{id:requests[0].id,state:'saved',screenshotIncluded:false})});
  });
  await trigger(page).click();await form(page).getByLabel('Описание').fill('Keep my report description');
  await form(page).getByRole('button',{name:'Отправить',exact:true}).click();
  await expect(form(page).getByRole('alert')).toContainText('Проверь описание');
  await form(page).getByRole('button',{name:'Убрать снимок'}).click();
  await expect(form(page).getByLabel('Описание')).toHaveValue('Keep my report description');
  await form(page).getByRole('button',{name:'Отправить',exact:true}).click();
  await expect(page.getByRole('dialog',{name:'Отчёт сохранён'})).toBeVisible();
  expect(requests).toHaveLength(2);expect(requests[1].screenshot).toBeNull();expect(requests[0].id).toBe(requests[1].id);
});

test('report symbol uses the top left corner and is hidden inside its own form',async({page})=>{
  test.slow();await village(page);await openMenu(page);
  const report=await trigger(page).boundingBox(),close=await page.locator('.modal-header .close').boundingBox();
  expect(report!.x).toBeLessThan(50);expect(report!.y).toBeLessThan(70);
  if(close)expect(report!.x+report!.width).toBeLessThan(close.x);
  await trigger(page).click();await expect(trigger(page)).toHaveCount(0);
  await form(page).getByRole('button',{name:'Отмена',exact:true}).click();await expect(trigger(page)).toBeVisible();
});


test('opening pointer-up cannot click a newly mounted popup control; deliberate second tap can',async({page})=>{
  test.slow();const slug=await village(page);
  // A newly founded village has no completed storage. Place a real blueprint before selecting its card.
  const placed=await page.request.post(`/api/villages/${slug}/blueprints`,{data:{kind:'Heap',x:5,y:5}});
  expect(placed.ok(),await placed.text()).toBe(true);
  await page.reload();await expect(page.locator('.hud-resources')).toBeVisible();
  await page.evaluate(()=>window.dispatchEvent(new CustomEvent('desertovia-e2e-select',{detail:{
    target:{kind:'site',plotId:'5,5',building:'Heap',area:{x:5,y:5,width:3,height:3}},
    screen:{x:innerWidth/2,y:innerHeight/2},point:{x:5,y:5}
  }})));
  const popup=page.locator('.target-popup');await expect(popup).toBeVisible();
  await popup.locator('button.close').dispatchEvent('click',{detail:1});await expect(popup).toBeVisible();
  await popup.locator('button.close').click();await expect(popup).toBeHidden();
});

test('communal resource slots use a dark readable surface in dark theme',async({page})=>{
  test.slow();await village(page);
  await page.evaluate(()=>document.documentElement.dataset.uiTheme='dark');
  await page.locator('.hud-buttons button').last().click();
  const slot=page.locator('.inventory .slot').first();await expect(slot).toBeVisible();
  const colors=await slot.evaluate(el=>({bg:getComputedStyle(el).backgroundColor,fg:getComputedStyle(el).color}));
  expect(colors.bg).toBe('rgb(37, 43, 43)');expect(colors.fg).toBe('rgb(237, 230, 212)');
});
