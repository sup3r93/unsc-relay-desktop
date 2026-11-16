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
def frame(t,closing):
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
 # Outro uses a six-blade iris seal; large structural movement ends in darkness.
 if closing and departure>0:
  for blade in range(6):
   a=blade*math.tau/6+t*.12;rr=1100*(1-departure)+20
   pts=[]
   for dx,dy in [(rr,-520),(rr+1600,-1000),(rr+1600,1000),(rr,520)]:pts.append((cx+dx*math.cos(a)-dy*math.sin(a),cy+dx*math.sin(a)+dy*math.cos(a)))
   d.polygon(pts,fill=(5,12,16),outline=color(accent,.2),width=3)
 fade=ease(t/.24)*(1-ease((t-4.55)/.45))
 if fade<1:im=Image.blend(Image.new('RGB',(W,H)),im,fade)
 return im

def render(kind):
 log=ART/(kind+'-encode.log')
 cmd=['ffmpeg','-y','-v','error','-f','rawvideo','-pix_fmt','rgb24','-s',f'{W}x{H}','-r',str(FPS),'-i','pipe:0','-i',str(ROOT/(kind+'.wav')),'-map','0:v','-map','1:a','-t','5','-c:v','libx264','-preset','medium','-crf','16','-pix_fmt','yuv420p','-profile:v','high','-level:v','5.2','-color_primaries','bt709','-color_trc','bt709','-colorspace','bt709','-c:a','aac','-b:a','256k','-movflags','+faststart',str(OUT/(kind+'.mp4'))]
 with log.open('w') as err:
  process=subprocess.Popen(cmd,stdin=subprocess.PIPE,stderr=err)
  for i in range(FPS*DURATION):
   image=frame(i/FPS,kind=='shutdown');process.stdin.write(image.tobytes())
   if i in [30,120,240]:image.save(ART/f'{kind}-{i:03d}.png')
   if i%60==0:print(kind,i//60,'/5',flush=True)
  process.stdin.close()
  if process.wait()!=0:raise RuntimeError(log.read_text())
 print('FINISHED',kind,flush=True)
if __name__=='__main__':
 if '--poster' in sys.argv:
  for kind in ['startup','shutdown']:frame(2,kind=='shutdown').save(ART/(kind+'-poster.png'))
 else:
  for kind in ['startup','shutdown']:render(kind)
