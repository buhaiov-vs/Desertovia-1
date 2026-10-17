import {createRequire} from 'node:module'
import {readFile} from 'node:fs/promises'
import {fileURLToPath} from 'node:url'
import assert from 'node:assert/strict'
const require=createRequire(new URL('../../../web/package.json',import.meta.url)),sharp=require('sharp')
const dir=fileURLToPath(new URL('../../../web/src/assets/fences/',import.meta.url))
const atlas=JSON.parse(await readFile(dir+'day.json','utf8'))
const day=await sharp(dir+'day.png').ensureAlpha().raw().toBuffer(),night=await sharp(dir+'night.png').ensureAlpha().raw().toBuffer()
const alpha=(pixels,x,y)=>pixels[(y*128+x)*4+3]
let mismatch=0,filled=0
for(let i=3;i<day.length;i+=4){if(day[i]>127||night[i]>127)filled++;if((day[i]>127)!==(night[i]>127))mismatch++}
assert.ok(mismatch/Math.max(1,filled)<.04,`Day/night silhouettes drift: ${mismatch}/${filled}`)
for(const material of ['logs','stone'])for(const light of [day,night]){
 const h=atlas.frames[material+'-h'].frame,v=atlas.frames[material+'-v'].frame
 const hl=Array.from({length:16},(_,i)=>alpha(light,h.x,h.y+i)>127),hr=Array.from({length:16},(_,i)=>alpha(light,h.x+15,h.y+i)>127)
 const vt=Array.from({length:16},(_,i)=>alpha(light,v.x+i,v.y)>127),vb=Array.from({length:16},(_,i)=>alpha(light,v.x+i,v.y+15)>127)
 assert.deepEqual(hl,hr,material+' horizontal seam');assert.deepEqual(vt,vb,material+' vertical seam')
 assert.ok(hl.some(Boolean)&&vt.some(Boolean),material+' transparent seam')
}
console.log(JSON.stringify({frames:Object.keys(atlas.frames).length,silhouetteMismatch:mismatch,opaqueUnion:filled,seams:'pass'}))
