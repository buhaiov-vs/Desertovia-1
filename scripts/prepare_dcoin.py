"""Bake the approved generated Dcoin using the shared 1-world-pixel outline."""
from pathlib import Path
import json
from PIL import Image, ImageColor
from outline_sprites import outline, outer_edge
root=Path(__file__).resolve().parent.parent
style=json.loads((root/'art/style/building-outline.json').read_text())
source=Image.open(root/'art/generated/dcoin/dcoin-source.png').convert('RGBA')
source=source.crop(source.getchannel('A').getbbox())
source.thumbnail((40,40),Image.Resampling.NEAREST)
canvas=Image.new('RGBA',(48,48));canvas.paste(source,((48-source.width)//2,(48-source.height)//2))
ink=ImageColor.getrgb(style['color']);width=2*style['worldWidth']
result=outline(canvas,width,ink)
edge=outer_edge(canvas.getchannel('A').point(lambda a:255 if a>=style['alphaThreshold'] else 0),width)
assert all(pixel==(*ink,255) for pixel,selected in zip(result.getdata(),edge.getdata()) if selected)
result.save(root/'web/public/assets/dcoin.png')
