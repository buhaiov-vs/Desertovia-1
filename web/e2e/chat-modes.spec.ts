import {test,expect,developmentLogin,expectVillageView,DEMO_SLUG,uniqueName} from './fixtures.ts'

test.beforeEach(async({page},info)=>{
 const language=info.title.startsWith('real game')?'en':'ru'
 await page.addInitScript(language=>{if(localStorage.getItem('desertod.chat.mode')===null)localStorage.setItem('desertod.chat.mode','hidden');localStorage.setItem('desertod.language',language)},language)
 await page.goto('/e2e/chat-harness.html')
 await expect(page.getByRole('button',{name:language==='en'?'Open chat':'Открыть чат'})).toBeVisible()
})

test('hidden chat notifies only new foreign arrivals, caps bursts and opens compact chat',async({page})=>{
 await expect(page.locator('.chat-toast')).toHaveCount(0)
 await page.locator('#history').click();await page.locator('#own').click();await expect(page.locator('.chat-toast')).toHaveCount(0)
 for(let i=0;i<4;i++)await page.locator('#arrive').click()
 await expect(page.locator('.chat-toast')).toHaveCount(2)
 await expect(page.locator('.chat-launch .hud-card-count')).toHaveText('4')
 await page.locator('.chat-toast').last().click()
 await expect(page.locator('.chat-panel')).toHaveAttribute('data-mode','quarter')
 await expect(page.locator('.chat-toast')).toHaveCount(0)
 await expect(page.locator('.chat-head .hud-card-count')).toHaveCount(0)
})

test('draft, modes and device preference survive resizing and full-screen viewport changes',async({page})=>{
 await page.getByRole('button',{name:'Открыть чат'}).click()
 const panel=page.locator('.chat-panel'),input=panel.locator('input')
 await input.fill('Не потерять черновик')
 if(await panel.getAttribute('data-mode')!=='full')await page.getByRole('button',{name:'На весь экран'}).click()
 await expect(panel).toHaveAttribute('data-mode','full')
 await page.setViewportSize({width:320,height:440})
 const rect=await panel.evaluate(el=>{const b=el.getBoundingClientRect();return {left:b.left,right:b.right,top:b.top,bottom:b.bottom}})
 expect(rect.left).toBeGreaterThanOrEqual(0);expect(rect.right).toBeLessThanOrEqual(320);expect(rect.top).toBeGreaterThanOrEqual(0);expect(rect.bottom).toBeLessThanOrEqual(440)
 for(const button of await panel.locator('.chat-control').all())expect((await button.boundingBox())!.width).toBeGreaterThanOrEqual(44)
 await page.getByRole('button',{name:'Чат на четверть экрана'}).click();await expect(input).toHaveValue('Не потерять черновик')
 await page.getByRole('button',{name:'Свернуть чат'}).click();await page.getByRole('button',{name:'Открыть чат'}).click();await expect(input).toHaveValue('Не потерять черновик')
 await page.reload();await expect(panel).toHaveAttribute('data-mode','quarter')
})

test('scrollback stays still while new messages arrive; jump marks messages read',async({page})=>{
 await page.getByRole('button',{name:'Открыть чат'}).click()
 const lines=page.locator('.chat-lines')
 await lines.evaluate(el=>{el.scrollTop=0;el.dispatchEvent(new Event('scroll',{bubbles:true}))})
 await page.locator('#arrive').click()
 await expect(page.getByRole('button',{name:/Новые сообщения/})).toBeVisible()
 expect(await lines.evaluate(el=>el.scrollTop)).toBe(0)
 await page.getByRole('button',{name:/Новые сообщения/}).click()
 await expect(page.getByRole('button',{name:/Новые сообщения/})).toHaveCount(0)
 expect(await lines.evaluate(el=>el.scrollHeight-el.scrollTop-el.clientHeight)).toBeLessThan(24)
})

test('toast expires, sending works, and full chat makes underlying HUD inert',async({page})=>{
 await page.locator('#arrive').click();await expect(page.locator('.chat-toast')).toHaveCount(1)
 await expect(page.locator('.chat-toast')).toHaveCount(0,{timeout:6000})
 await page.getByRole('button',{name:'Открыть чат'}).click()
 const panel=page.locator('.chat-panel')
 await panel.locator('input').fill('Тест отправки')
 if(await panel.getAttribute('data-mode')!=='full')await page.getByRole('button',{name:'На весь экран'}).click()
 expect(await page.locator('.hud-bottom').evaluate(el=>(el as HTMLElement).inert)).toBe(true)
 await panel.getByRole('button',{name:'Отправить',exact:true}).click();await expect(page.locator('#sent')).toHaveText('Тест отправки')
 await page.keyboard.press('Escape');await expect(panel).toHaveAttribute('data-mode','quarter')
 expect(await page.locator('.hud-bottom').evaluate(el=>(el as HTMLElement).inert)).toBe(false)
})


test('real game chat buttons and panels fit the phone and desktop map',async({page})=>{
 test.setTimeout(120000) // Software WebGL renders both full-map screenshots on the CPU.
 await page.evaluate(()=>localStorage.setItem('desertod.language','en'))
 await page.goto(`/v/${DEMO_SLUG}`);await developmentLogin(page,uniqueName('chat-ui'));await expectVillageView(page)
 const panel=page.locator('.chat-panel')
 await expect(panel).toHaveAttribute('data-mode','hidden')
 await panel.getByRole('button',{name:'Open chat'}).click();await expect(panel).toHaveAttribute('data-mode','quarter')
 const viewport=page.viewportSize()!
 const compact=await panel.boundingBox();expect(compact!.x).toBeGreaterThanOrEqual(0);expect(compact!.x+compact!.width).toBeLessThanOrEqual(viewport.width+1)
 await page.screenshot({path:test.info().outputPath('chat-quarter-map.png')})
 await panel.getByRole('button',{name:'Full screen'}).click();await expect(panel).toHaveAttribute('data-mode','full')
 const full=await panel.boundingBox();expect(full!.width).toBeCloseTo(viewport.width,0);expect(full!.height).toBeCloseTo(viewport.height,0)
 await expect(page.locator('.ui-toggle')).toBeHidden()
 await page.screenshot({path:test.info().outputPath('chat-full-map.png')})
 await panel.getByRole('button',{name:'Hide chat'}).click();await expect(panel).toHaveAttribute('data-mode','hidden')
})
