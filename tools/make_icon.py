from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
im = Image.new('RGBA', (256, 256), (0, 0, 0, 0))
d = ImageDraw.Draw(im)
d.rounded_rectangle((4, 4, 252, 252), radius=53, fill='#214A3D')
d.polygon([(48, 76), (114, 88), (114, 190), (48, 175)], fill='#F4F7E7')
d.polygon([(128, 88), (195, 76), (195, 175), (128, 190)], fill='#DDEAAF')
d.line([(121, 90), (121, 192)], fill='#95B48F', width=5)
d.line([(62, 109), (98, 117)], fill='#74936D', width=5)
d.line([(62, 129), (98, 137)], fill='#74936D', width=5)
d.line([(143, 116), (180, 108)], fill='#74936D', width=5)
d.line([(143, 136), (180, 128)], fill='#74936D', width=5)
im.save(root / 'src' / 'app.ico', sizes=[(16,16), (24,24), (32,32), (48,48), (64,64), (128,128), (256,256)])
im.save(root / 'verification' / 'app-icon.png')
