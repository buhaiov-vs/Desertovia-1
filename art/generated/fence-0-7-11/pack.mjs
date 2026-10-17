// Mechanical atlas export; all artwork comes from the inspected image_gen source.
import {createRequire} from 'node:module'
import {fileURLToPath} from 'node:url'
import {writeFile,mkdir} from 'node:fs/promises'
const require=createRequire(new URL('../../../web/package.json',import.meta.url))
const sharp=require('sharp')
const source=fileURLToPath(new URL('./revision-source.png',import.meta.url))
const out=fileURLToPath(new URL('./preview-export/',import.meta.url))
await mkdir(out,{recursive:true})
const meta=await sharp(source).metadata()
const halfX=meta.width/2,halfY=meta.height/2
const names=['nw','h','ne','north-end','v','gate-closed','v-east','damaged-v','sw','h-south','se','south-end','damaged-h','rubble','east-end','gate-open']
// The source did not keep a uniform tile grid. Explicit source rectangles extract
// straight rails/poles without neighbouring posts. South/east aliases use the
// same actual straight segment so repeated fences have matching edge masks.
const rects=[
 [0,0,156,156],[128,0,140,156],[392,0,124,156],[502,0,156,156],
 [0,156,156,156],[168,166,236,170],[0,156,156,156],[502,156,156,156],
 [0,312,156,156],[128,0,140,156],[392,312,124,156],[502,312,156,156],
 [0,466,220,161],[202,466,120,161],[306,466,140,161],[442,466,185,161]
]
const reports=[]
for(const night of [false,true]){
 const parts=[],frames={}
 for(const [material,quadrantY] of [['logs',0],['stone',1]]){
  for(let i=0;i<16;i++){
   const [rx,ry,rw,rh]=rects[i],q=halfX/627
   const x=Math.round((night?halfX:0)+rx*q),y=Math.round(quadrantY*halfY+ry*q)
   const right=Math.min(Math.round((night?2:1)*halfX),Math.round(x+rw*q)),bottom=Math.min(Math.round((quadrantY+1)*halfY),Math.round(y+rh*q))
   let extracted=sharp(source).extract({left:x,top:y,width:right-x,height:bottom-y})
   const pad=Math.max(0,Math.round(rw*q)-(right-x))+(names[i]==='ne'||names[i]==='se'?Math.round(32*q):0)
   if(pad>0)extracted=extracted.extend({right:pad,background:'#00000000'})
   const region=await extracted.png().toBuffer()
   const tile=await sharp(region).resize(16,16,{kernel:'nearest'}).png().toBuffer()
   const left=(material==='stone'?64:0)+(i%4)*16,top=Math.floor(i/4)*16
   parts.push({input:tile,left,top});frames[`${material}-${names[i]}`]={frame:{x:left,y:top,w:16,h:16},spriteSourceSize:{x:0,y:0,w:16,h:16},sourceSize:{w:16,h:16}}
   if(['h','h-south','v','v-east'].includes(names[i])){
    const data=await sharp(tile).ensureAlpha().raw().toBuffer();const alpha=(x,y)=>data[(y*16+x)*4+3]
    reports.push({night,material,tile:names[i],left:Array.from({length:16},(_,y)=>alpha(0,y)>127?y:null).filter(x=>x!==null),right:Array.from({length:16},(_,y)=>alpha(15,y)>127?y:null).filter(x=>x!==null),top:Array.from({length:16},(_,x)=>alpha(x,0)>127?x:null).filter(x=>x!==null),bottom:Array.from({length:16},(_,x)=>alpha(x,15)>127?x:null).filter(x=>x!==null)})
   }
  }
 }
 const file=`fences-${night?'night':'day'}.png`
 const packed=await sharp({create:{width:128,height:64,channels:4,background:'#00000000'}}).composite(parts).png().toBuffer()
 await writeFile(`${out}/${file}`,packed)
 await sharp(packed).resize(1024,512,{kernel:'nearest'}).png().toFile(`${out}/preview-${night?'night':'day'}.png`)
 await writeFile(`${out}/fences-${night?'night':'day'}.json`,JSON.stringify({frames,meta:{image:file,scale:'1',size:{w:128,h:64}}},null,2)+'\n')
}
await writeFile(`${out}/edge-report.json`,JSON.stringify({source:{width:meta.width,height:meta.height},reports},null,2)+'\n')
console.log(JSON.stringify({source:[meta.width,meta.height],out,edges:reports}))

if(process.argv.includes('--export')){
 const target=fileURLToPath(new URL('../../../web/src/assets/fences/',import.meta.url))
 await mkdir(target,{recursive:true})
 const {readFile}=await import('node:fs/promises')
 for(const variant of ['day','night']){
  await writeFile(`${target}/${variant}.png`,await readFile(`${out}/fences-${variant}.png`))
  const atlas=JSON.parse(await readFile(`${out}/fences-${variant}.json`,'utf8'));atlas.meta.image=`${variant}.png`
  await writeFile(`${target}/${variant}.json`,JSON.stringify(atlas,null,2)+'\n')
 }
}
