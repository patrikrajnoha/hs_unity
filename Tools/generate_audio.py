#!/usr/bin/env python3
"""Rebuild the original soundtrack and effects using only Python standard libraries."""
from pathlib import Path
from math import sin,pi,exp,tanh
from array import array
import wave,random
out=Path(__file__).resolve().parents[1]/'Assets/Resources/Audio';out.mkdir(parents=True,exist_ok=True);sr=22050;r=random.Random(711)
def save(name,x):
 with wave.open(str(out/(name+'.wav')),'wb') as f:
  f.setnchannels(1);f.setsampwidth(2);f.setframerate(sr);f.writeframes(array('h',(int(max(-.94,min(.94,v))*32767) for v in x)).tobytes())
def tone(freq,dur,vol=.2):
 return [vol*(sin(2*pi*freq*i/sr)+.18*sin(4*pi*freq*i/sr))*min(i/(sr*.012),1)*exp(-i*4/(sr*dur)) for i in range(int(sr*dur))]
def seq(name,notes,space=.13):
 x=[0.] * int(sr*(space*len(notes)+.65))
 for k,n in enumerate(notes):
  for i,v in enumerate(tone(440*2**((n-69)/12),.6,.19)): x[int(k*space*sr)+i]+=v
 save(name,x)
def music(name,bpm,chase=False):
 beat=60/bpm;count=int(beat*32*sr);x=[0.]*count
 def add(at,v):
  j=int(at*sr)
  for i,a in enumerate(v):x[(j+i)%count]+=a
 for bar,root in enumerate([45,41,48,43]*2):
  for n in [root+12,root+19,root+24]:
   f=440*2**((n-69)/12)
   add(bar*4*beat,[.035*sin(2*pi*f*i/sr)*(1-exp(-i*3/sr))*exp(-i/(sr*beat*2)) for i in range(int(beat*4.5*sr))])
  for b in range(4):
   at=(bar*4+b)*beat;add(at,tone(440*2**((root-69)/12),beat*.8,.17))
   if chase or b%2==0:add(at,[.16*sin(2*pi*(48*i/sr+14*(1-exp(-i*30/sr))/30))*exp(-i*23/sr) for i in range(int(.21*sr))])
   if b%2==1:add(at,[.032*r.gauss(0,1)*exp(-i*36/sr) for i in range(int(.13*sr))])
   for h in range(2 if chase else 1):add(at+h*beat/2,[.017*r.gauss(0,1)*exp(-i*85/sr) for i in range(int(.065*sr))])
   if b%2==0 or chase:add(at+beat*.5,tone(440*2**((root+24+[0,7,3,10][(bar+b)%4]-69)/12),beat*.7,.07 if not chase else .09))
 save(name,[tanh(v) for v in x])
music('quarter_theme',84);music('pursuit_theme',112,True)
x=[];lo=slow=0
for i in range(sr*24):
 lo=.89*lo+.11*r.gauss(0,1);slow=.99*slow+.01*lo;x.append((lo-slow)*(.15+.04*sin(2*pi*i/(sr*8))))
# Match end to start with a short cyclic crossfade.
fade=sr//2
for i in range(fade):x[-fade+i]=x[-fade+i]*(1-i/fade)+x[i]*i/fade
save('city_air',x[fade:])
for name,freq in [('step_stone',100),('step_indoor',78)]:save(name,[(.13*sin(2*pi*freq*i/sr)+.055*r.gauss(0,1))*exp(-i*35/sr)*min(i/(sr*.002),1) for i in range(int(.17*sr))])
save('door',[(.07*r.gauss(0,1)*exp(-i*20/sr)+.12*sin(2*pi*(130*i/sr-35*(i/sr)**2))*exp(-i*10/sr))*min(i/(sr*.003),1) for i in range(int(.45*sr))])
for name,notes,spacing in [('ui_click',[76],.07),('pickup',[72,79],.1),('objective',[64,71,76],.12),('success',[60,64,67,72,79],.16),('failure',[59,55,48],.16),('warning',[81,74,81],.1)]:seq(name,notes,spacing)
print('Created',len(list(out.glob('*.wav'))),'original WAV assets')
