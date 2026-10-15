#!/usr/bin/env python3
"""Rebuild aligned hospital pair from drafts with the shared baked outline."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageChops
from outline_sprites import outline, STYLE
ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'art/hospital-proposal/prepared'
OUT.mkdir(exist_ok=True)
images=[]
for phase in ('day','night'):
    source=Image.open(ROOT/f'art/hospital-proposal/hospital-{phase}-draft.png').convert('RGBA')
    source.putalpha(source.getchannel('A').point(lambda a:255 if a>=128 else 0))
    body=source.crop((85,74,810,815)).resize((92,94),Image.Resampling.NEAREST)
    image=Image.new('RGBA',(100,100));image.paste(body,(4,3));images.append(image)
mask=ImageChops.lighter(images[0].getchannel('A'),images[1].getchannel('A'))
mask_image=Image.new('RGBA',mask.size);mask_image.putalpha(mask)
atlas=Image.new('RGBA',(200,100));frames={}
for index,phase in enumerate(('day','night')):
    result=outline(images[index],2,(43,29,43),mask=mask_image)
    result.save(OUT/f'hospital-{phase}.png');atlas.paste(result,(index*100,0))
    frames[f'building-hospital-{phase}']={'frame':{'x':index*100,'y':0,'w':100,'h':100},'rotated':False,'trimmed':False,'spriteSourceSize':{'x':0,'y':0,'w':100,'h':100},'sourceSize':{'w':100,'h':100}}
    bounds=result.getchannel('A').getbbox();assert bounds[0]>0 and bounds[2]<100
atlas.save(ROOT/'web/src/assets/night/hospital.png')
(ROOT/'web/src/assets/night/hospital.json').write_text(json.dumps({'frames':frames,'meta':{'scale':1,'size':{'w':200,'h':100}}},indent=2)+'\n')
(OUT/'registration.json').write_text(json.dumps({'sourceCrop':[85,74,810,815],'bodyPixels':[92,94],'canvas':[100,100],'outline':STYLE,'sha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(OUT.glob('*.png'))}},indent=2)+'\n')
p=ROOT/'web/src/assets/night/registration.json';reg=json.loads(p.read_text());reg['Hospital']={'day':'building-hospital-day','preparedDay':'building-hospital-day','night':'building-hospital-night','dayBounds':[4,3,96,97],'nightBounds':[4,3,96,97]};p.write_text(json.dumps(reg,indent=2)+'\n')
