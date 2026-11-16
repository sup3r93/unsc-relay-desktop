from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import math
root=Path(__file__).resolve().parent
out=root/'installer-art';out.mkdir(exist_ok=True)
logo=Image.open(root.parent/'src/UnscRelay.App/Assets/UNSC.png').convert('RGBA')
def font(size,bold=False):return ImageFont.truetype('C:/Windows/Fonts/'+('bahnschrift.ttf' if bold else 'consola.ttf'),size)
W,H=1312,2512
im=Image.new('RGB',(W,H),(8,18,21));d=ImageDraw.Draw(im)
for y in range(H):
 v=int(8+6*max(0,1-abs(y-850)/900));d.line((0,y,W,y),fill=(v,18+v//2,21+v//2))
for x in range(0,W,80):d.line((x,0,x,H),fill=(18,33,34),width=1)
for y in range(0,H,80):d.line((0,y,W,y),fill=(18,33,34),width=1)
for r in [390,440,510,610]:d.ellipse((W//2-r,830-r,W//2+r,830+r),outline=(39,58,45),width=2)
for a in range(0,360,5):
 rad=math.radians(a);r=610;d.line((W/2+math.cos(rad)*r,830+math.sin(rad)*r,W/2+math.cos(rad)*(r+18),830+math.sin(rad)*(r+18)),fill=(60,78,53),width=2)
d.rectangle((0,0,15,H),fill=(169,192,121));d.text((96,105),'F L E E T C O M  /  0 1',font=font(37),fill=(164,188,129));d.line((96,190,W-96,190),fill=(66,87,63),width=3)
mark=logo.resize((800,800),Image.Resampling.LANCZOS);im.paste(mark,((W-800)//2,430),mark)
d=ImageDraw.Draw(im);d.text((96,1390),'UNSC',font=font(160,True),fill=(220,231,211));d.text((96,1554),'RELAY',font=font(160,True),fill=(177,197,133));d.text((100,1760),'REMOTE OPERATIONS COMMAND',font=font(35),fill=(160,181,158));d.line((96,1860,W-96,1860),fill=(66,87,63),width=3)
for n,(title,detail) in enumerate([('01  CONNETTI','SSH  /  RDP  /  VNC'),('02  TRASFERISCI','SFTP  /  EDITOR REMOTO'),('03  ORGANIZZA','CHIAVI  /  WORKSPACE')]):
 y=1930+n*150;d.rectangle((96,y,104,y+85),fill=(167,190,119));d.text((136,y),title,font=font(32,True),fill=(206,221,192));d.text((136,y+52),detail,font=font(26),fill=(138,162,141))
d.text((100,2420),'LOCAL / PERSONAL EDITION',font=font(28),fill=(141,162,136));im.save(out/'wizard.png')
small=Image.new('RGBA',(440,440),(13,25,28,255));mark=logo.resize((350,350),Image.Resampling.LANCZOS);small.alpha_composite(mark,(45,45));small.save(out/'header.png')
