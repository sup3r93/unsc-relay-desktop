"""Native 3440x1440 / 60 fps motion graphics; no desktop still used."""
from pathlib import Path
import math,subprocess,sys
import numpy as np
from PIL import Image,ImageDraw,ImageFont,ImageFilter,ImageChops
ROOT=Path(__file__).resolve().parent;REPO=ROOT.parent.parent
OUT=REPO/'src/UnscRelay.App/Web/media';ART=REPO/'artifacts/cinematic';ART.mkdir(parents=True,exist_ok=True)
W,H,FPS,DURATION=3440,1440,60,5
FONT='C:/Windows/Fonts/bahnschrift.ttf';MONO='C:/Windows/Fonts/consola.ttf'
fonts={}
def font(n,mono=False):
 k=(n,mono)
 if k not in fonts:fonts[k]=ImageFont.truetype(MONO if mono else FONT,n)
 return fonts[k]
def clamp(x):return max(0,min(1,x))
def ease(x):x=clamp(x);return x*x*(3-2*x)
def color(c,a=1):return tuple(int(v*clamp(a)) for v in c)
def text(d,xy,s,size,c,center=False,mono=False):
 f=font(size,mono);x,y=xy
 if center:x-=d.textlength(s,font=f)/2
 d.text((x,y),s,font=f,fill=c)
y,x=np.mgrid[0:H,0:W];light=np.exp(-(((x-W*.5)/(W*.38))**2+((y-H*.48)/(H*.8))**2)*2)
base=Image.fromarray(np.stack([3+light*5,8+light*15,12+light*20],-1).astype('uint8'))
logo=Image.open(ROOT/'unsc-logo.png').convert('RGBA');mask=logo.getchannel('A')
rng=np.random.default_rng(117);dust=rng.uniform(0,1,(130,4))
def armoured_doors(t):
 # Three recognisable sliding bulkheads close from the rear of a corridor.
 im=Image.new('RGB',(W,H),(5,13,18));d=ImageDraw.Draw(im)
 for side in (-1,1):
  for k in range(6):
   x=W/2+side*(300+k*240);d.line((x,H*.35,W/2+side*(700+k*460),H),fill=(23,44,47),width=4)
 for k in range(9):
  y=H*.55+(k/8)**2*H*.45;d.line((0,y,W,y),fill=(24,44,46),width=4)
 for n,scale in enumerate((.46,.72,1.04)):
  width=int(W*.87*scale);height=int(H*.79*scale);left=(W-width)//2;top=(H-height)//2;right=left+width;bottom=top+height
  border=max(12,int(42*scale));metal=(30+n*5,48+n*5,53+n*5)
  # Fixed frame stays still; each pair of leaves moves horizontally into it.
  d.rectangle((left-border,top-border,right+border,bottom+border),outline=(83,111,113),width=border)
  d.line((left,top-border,right,top-border),fill=(142,190,179),width=max(3,int(7*scale)))
  progress=ease((t-(.55+n*1.03))/.95);gap=int(width*(1-progress));half=width//2
  leaves=Image.new('RGB',(width,height),(0,0,0));leaf=ImageDraw.Draw(leaves)
  for side in (-1,1):
   a=0 if side<0 else half;b=half if side<0 else width
   leaf.rectangle((a,0,b,height),fill=metal,outline=(14,26,31),width=max(3,int(10*scale)))
   inset=int(38*scale);chamfer=int(65*scale)
   for row in (0,1):
    y1=int(height*(.08 if row==0 else .57));y2=int(height*(.43 if row==0 else .92))
    points=[(a+inset+chamfer,y1),(b-inset,y1),(b-inset,y2-chamfer),(b-inset-chamfer,y2),(a+inset,y2),(a+inset,y1+chamfer)]
    leaf.polygon(points,fill=(19+n*3,32+n*4,38+n*4),outline=(76,100,105),width=max(2,int(7*scale)))
   stripe=int(height*.49);leaf.rectangle((a+inset,stripe,b-inset,stripe+int(26*scale)),fill=(127,149,73))
   for k in range(6):
    y=int(height*(.18+k*.1));x=b-int(24*scale) if side<0 else a+int(12*scale)
    leaf.rectangle((x,y,x+int(12*scale),y+int(25*scale)),fill=(8,19,23))
  # Crop leaves to the aperture rather than painting outside their rails.
  left_width=max(0,half-gap//2);right_start=half+(gap-gap//2)
  if left_width:im.paste(leaves.crop((half-left_width,0,half,height)),(left,top))
  if right_start<width:im.paste(leaves.crop((half,0,half+width-right_start,height)),(left+right_start,top))
  d=ImageDraw.Draw(im)
  if progress>.98:
   lock_w=int(60*scale);lock_h=int(100*scale);cx=W//2;cy=H//2
   d.rounded_rectangle((cx-lock_w,cy-lock_h,cx+lock_w,cy+lock_h),radius=max(3,int(10*scale)),fill=(21,35,40),outline=(96,123,121),width=max(2,int(5*scale)))
   d.rounded_rectangle((cx-lock_w*.65,cy-int(12*scale),cx+lock_w*.65,cy+int(12*scale)),radius=4,fill=(199,79,55))
 if t>3.9:
  a=ease((t-3.9)/.4);text(d,(W/2,H*.22),'UNSC  RELAY',int(H*.045),color((220,233,208),a),center=True)
 fade=ease(t/.25)*(1-ease((t-4.6)/.4))
 return Image.blend(Image.new('RGB',(W,H)),im,fade)

def frame(t,closing):
 if closing:return armoured_doors(t)
 p=t/DURATION;accent=(223,177,112) if closing else (168,205,133);ice=(101,202,216);white=(221,234,220)
 im=base.copy();d=ImageDraw.Draw(im);fx=Image.new('RGB',(W,H));g=ImageDraw.Draw(fx)
 arrival=ease(t/.9);departure=ease((t-3.15)/1.45) if closing else 0
 # Travelling perspective floor and converging depth lanes.
 horizon=H*.54
 for j in range(-15,16):d.line([(W/2+j*70,horizon),(W/2+j*340,H)],fill=(13,30,32),width=2)
 for j in range(22):
  z=((j/22+t*.17)%1)**2;yy=horizon+z*(H-horizon)
  d.line([(0,yy),(W,yy)],fill=color((28,57,57),.2+z*.6),width=2)
 for i,v in enumerate(dust):
  z=(v[2]+t*(.12+v[3]*.16))%1;xx=W/2+(v[0]-.5)*W*z*1.45;yy=H*.46+(v[1]-.5)*H*z*1.7
  length=2+z*z*20;g.line([(xx,yy),(xx+(xx-W/2)/W*length,yy+(yy-H/2)/H*length)],fill=color(ice,z*.6),width=max(1,int(z*3)))
 # Four armoured panels translate into their anchors, then seal the outro.
 slide=(1-arrival)*420
 for side in [-1,1]:
  edge=90 if side==-1 else W-90
  for upper in [True,False]:
   yy=80 if upper else H-200;depth=140
   points=[(edge+side*slide,yy),(edge+side*slide-side*620,yy),(edge+side*slide-side*680,yy+45),(edge+side*slide-side*680,yy+depth),(edge+side*slide,yy+depth)]
   d.polygon(points,fill=(9,20,24),outline=(37,56,55),width=3)
   for k in range(3):d.line([(edge+side*(40+k*20),yy+30),(edge+side*(65+k*20),yy+30)],fill=color(accent,.45),width=4)
 # Mechanical segmented 3D orbital layers, not a stationary background overlay.
 cx,cy=W/2,H*.43;radius=390*(.55+.45*arrival)*(1-.6*departure)
 tilt=.28+.1*math.sin(t*1.2)
 for layer in range(4):
  r=radius+layer*36;spin=t*(.55+layer*.17)*(-1 if layer%2 else 1)
  for seg in range(32):
   if seg%7==layer:continue
   pts=[]
   for a in np.linspace(seg*math.tau/32+spin,(seg+.64)*math.tau/32+spin,7):
    xx=math.cos(a)*r;yy=math.sin(a)*r;zz=math.sin(a)*r*tilt
    perspective=1600/(1600+zz)
    pts.append((cx+xx*perspective,cy+yy*perspective*.83))
   g.line(pts,fill=color(accent if layer%2==0 else ice,.22+.35*(seg%4==0)),width=3 if layer%2 else 5)
 # Rotating meridians form a holographic globe behind the crest.
 spin=t*.68
 for meridian in range(10):
  angle=meridian*math.pi/10+spin;pts=[]
  for a in np.linspace(-math.pi/2,math.pi/2,48):pts.append((cx+radius*.72*math.cos(a)*math.cos(angle),cy+radius*.72*math.sin(a)))
  g.line(pts,fill=color(ice,.12),width=2)
 # Crisp UNSC crest assembled in moving horizontal scan strips.
 appear=ease((t-.35)/.9) if not closing else 1-departure
 crest_h=int((455+18*math.sin(t*.8))*(1-.35*departure));crest_w=max(8,int(crest_h*800/1047*(.86+.14*math.cos(t*.85))))
 crest_mask=mask.resize((crest_w,crest_h),Image.Resampling.LANCZOS)
 crest=Image.new('RGBA',(crest_w,crest_h),accent+(0,));crest.putalpha(crest_mask.point(lambda v:int(v*appear)))
 lx=int(cx-crest_w/2);ly=int(cy-crest_h/2)
 for strip in range(20):
  a=strip*crest_h//20;b=(strip+1)*crest_h//20
  assembled=ease((t-.18-strip*.018)/.75) if not closing else 1-departure
  offset=int((1-assembled)*(-160 if strip%2 else 160))
  im.paste(crest.crop((0,a,crest_w,b)),(lx+offset,ly+a),crest.crop((0,a,crest_w,b)))
 # Scanner moves across the actual emblem, with a restrained bloom.
 scan=cy-260+(t*.7%1)*520
 g.line([(cx-230,scan),(cx+230,scan)],fill=color(ice,.48*(1-departure)),width=3)
 for s in [-1,1]:
  xx=cx+s*570;g.line([(xx,cy-80),(xx-s*85,cy-80),(xx-s*130,cy-35)],fill=color(accent,.45),width=3)
  for k in range(5):g.rectangle((xx-14,cy+15+k*17,xx+14,cy+20+k*17),fill=color(accent,.2+.4*((int(t*8)+k)%5==0)))
 glow=fx.resize((860,360),Image.Resampling.BILINEAR).filter(ImageFilter.GaussianBlur(4)).resize((W,H),Image.Resampling.BILINEAR)
 im=ImageChops.add(im,glow);im=ImageChops.add(im,fx);d=ImageDraw.Draw(im)
 # Typography and telemetry remain readable at native ultrawide resolution.
 text(d,(140,145),'FLEETCOM / REMOTE OPERATIONS',26,color(accent,.8),mono=True)
 text(d,(140,185),'RELAY  //  LOCAL COMMAND INTERFACE',18,(102,129,124),mono=True)
 text(d,(W-555,145),'SECURE OPERATIONS TERMINAL',22,color(accent,.8),mono=True)
 text(d,(W-555,185),'UNSC  /  PERSONAL EDITION',18,(102,129,124),mono=True)
 title_alpha=ease((t-.7)/.6)*(1-ease((t-4.4)/.5))
 text(d,(cx,1000),'UNSC  RELAY',112,color(white,title_alpha),center=True)
 text(d,(cx,1140),'FINE SESSIONE' if closing else 'ACCESSO ALLA CONSOLE',29,color(accent,title_alpha),center=True,mono=True)
 # Actual sequence time, not fake security checks or fabricated live telemetry.
 text(d,(165,1275),'SEQUENZA DI CHIUSURA' if closing else 'SEQUENZA DI AVVIO',23,color(accent,.8),mono=True)
 text(d,(W-520,1275),f'T + {t:04.1f}  /  05.0 SEC',23,color(accent,.8),mono=True)
 d.line((W*.32,1240,W*.68,1240),fill=(29,51,49),width=3);d.line((W*.32,1240,W*.32+W*.36*p,1240),fill=color(accent,.8),width=4)
 fade=ease(t/.24)*(1-ease((t-4.55)/.45))
 if fade<1:im=Image.blend(Image.new('RGB',(W,H)),im,fade)
 return im

def render(kind):
 log=ART/(kind+'-encode.log')
 audio=ROOT/(kind+'.wav')
 if not audio.exists():
  audio=ART/(kind+'-preserved-audio.m4a')
  if not audio.exists():subprocess.run(['ffmpeg','-y','-v','error','-i',str(OUT/(kind+'.mp4')),'-vn','-c:a','copy',str(audio)],check=True)
 temporary=ART/(kind+'-rendering.mp4')
 cmd=['ffmpeg','-y','-v','error','-f','rawvideo','-pix_fmt','rgb24','-s',f'{W}x{H}','-r',str(FPS),'-i','pipe:0','-i',str(audio),'-map','0:v','-map','1:a','-t','5','-c:v','libx264','-preset','medium','-crf','16','-pix_fmt','yuv420p','-profile:v','high','-level:v','5.2','-color_primaries','bt709','-color_trc','bt709','-colorspace','bt709','-c:a','aac','-b:a','256k','-movflags','+faststart',str(temporary)]
 with log.open('w') as err:
  process=subprocess.Popen(cmd,stdin=subprocess.PIPE,stderr=err)
  for i in range(FPS*DURATION):
   image=frame(i/FPS,kind=='shutdown');process.stdin.write(image.tobytes())
   if i in [30,120,240]:image.save(ART/f'{kind}-{i:03d}.png')
   if i%60==0:print(kind,i//60,'/5',flush=True)
  process.stdin.close()
  if process.wait()!=0:raise RuntimeError(log.read_text())
 temporary.replace(OUT/(kind+'.mp4'))
 print('FINISHED',kind,flush=True)
if __name__=='__main__':
 if '--poster' in sys.argv:
  for kind in ['startup','shutdown']:
   poster=frame(4.2 if kind=='shutdown' else 2,kind=='shutdown');poster.save(ART/(kind+'-poster.png'));poster.save(OUT/(kind+'-poster.png'))
 else:
  for kind in (['shutdown'] if '--shutdown-only' in sys.argv else ['startup','shutdown']):render(kind)
