from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
assets = root / 'Assets'
assets.mkdir(exist_ok=True)
svg = '''<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
<defs><linearGradient id="bg" x2="1" y2="1"><stop stop-color="#243549"/><stop offset="1" stop-color="#101824"/></linearGradient><linearGradient id="mint" x2="1" y2="1"><stop stop-color="#81f7dc"/><stop offset="1" stop-color="#3cceb6"/></linearGradient></defs>
<rect x="8" y="8" width="240" height="240" rx="57" fill="url(#bg)"/>
<rect x="46" y="47" width="112" height="111" rx="20" fill="none" stroke="#559f9d" stroke-width="12"/>
<rect x="96" y="98" width="114" height="112" rx="21" fill="url(#mint)"/>
<path d="M119 129h66M119 149h43" stroke="#18313a" stroke-width="9" stroke-linecap="round"/>
<path d="M45 184v11q0 18 19 18h13m-10-11 11 11-11 11M211 72V61q0-18-19-18h-13m10-11-11 11 11 11" fill="none" stroke="#edfdf9" stroke-width="10" stroke-linecap="round" stroke-linejoin="round"/>
</svg>'''
(assets / 'AltTabFlow.svg').write_text(svg, encoding='utf-8')
# Rasterize the same simple vector geometry at 4x for crisp ICO sizes.
s = 4
im = Image.new('RGBA', (256*s, 256*s))
d = ImageDraw.Draw(im)
def box(coords, radius, fill, outline=None, width=1):
    d.rounded_rectangle(tuple(int(x*s) for x in coords), radius*s, fill=fill, outline=outline, width=width*s)
def line(points, fill, width):
    d.line([(int(x*s),int(y*s)) for x,y in points],fill=fill,width=width*s,joint='curve')
    r=width*s/2
    for x,y in (points[0],points[-1]):d.ellipse((x*s-r,y*s-r,x*s+r,y*s+r),fill=fill)
box((8,8,248,248),57,'#172535')
box((46,47,158,158),20,None,'#559f9d',12)
box((96,98,210,210),21,'#63e7ce')
line([(119,129),(185,129)],'#18313a',9)
line([(119,149),(162,149)],'#18313a',9)
line([(45,184),(45,196),(49,207),(60,213),(77,213)],'#edfdf9',10)
line([(67,202),(78,213),(67,224)],'#edfdf9',10)
line([(211,72),(211,60),(207,49),(196,43),(179,43)],'#edfdf9',10)
line([(189,32),(178,43),(189,54)],'#edfdf9',10)
im = im.resize((256,256),Image.Resampling.LANCZOS)
im.save(assets/'AltTabFlow.png')
im.save(assets/'AltTabFlow.ico',sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(128,128),(256,256)])
print('Created SVG, PNG, and multi-resolution ICO')
