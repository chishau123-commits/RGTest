"""Original 48 kHz stereo cues for the planar-to-perspective album entry."""
from pathlib import Path
import numpy as np, wave, uuid, json
R=48000
root=Path(__file__).resolve().parents[2]
out=root/'Assets/RhythmDemo/Resources/Audio/UI'
rng=np.random.default_rng(261002)
def bell(t,f,decay):
 e=(1-np.exp(-np.maximum(t,0)/.009))*np.exp(-np.maximum(t,0)*decay)*(t>=0)
 return e*(np.sin(2*np.pi*f*t)+.13*np.sin(2*np.pi*f*2.003*t)+.025*np.sin(2*np.pi*f*4.01*t))
def write(name,x,peak):
 t=np.arange(len(x))/R;x-=x.mean(axis=0);x*=np.minimum(1,t/.004)[:,None]*np.minimum(1,(len(x)/R-t)/.08)[:,None]
 x*=peak/max(np.max(abs(x)),1e-9)
 with wave.open(str(out/(name+'.wav')),'wb') as w:
  w.setnchannels(2);w.setsampwidth(2);w.setframerate(R);w.writeframes(np.round(x*32767).astype('<i2').tobytes())
 m=out/(name+'.wav.meta')
 if not m.exists():
  template=(out/'open.wav.meta').read_text();old=template.split('guid: ')[1].splitlines()[0];m.write_text(template.replace(old,uuid.uuid4().hex))
 return {'cue':name,'seconds':len(x)/R,'peak_dbfs':float(20*np.log10(np.max(abs(x)))),'rms_dbfs':float(20*np.log10(np.sqrt(np.mean(x*x))))}
t=np.arange(int(2.04*R))/R
n=rng.normal(0,1,len(t));air=np.convolve(n,np.ones(48)/48,'same')-np.convolve(n,np.ones(280)/280,'same')
e=np.sin(np.pi*np.clip((t-.30)/1.57,0,1))**2
s=.17*air*e+.040*np.sin(2*np.pi*(130*t+62*t*t))*e
s+=.12*bell(t,293.665,24)+.060*bell(t-.48,440,6)+.035*bell(t-.88,659.25,7)
a=np.column_stack((s,s));d=int(.027*R);a[d:,1]+=.16*s[:-d];a[:,0]*=1-.12*e;a[:,1]*=1+.12*e
report=[write('album-unfold',a,.24)]
t=np.arange(int(.62*R))/R;s=.20*bell(t,587.33,12)+.075*bell(t-.032,880,13)+.02*bell(t-.058,1174.66,17)
a=np.column_stack((s,s));d=int(.042*R);a[d:,1]+=.10*s[:-d]
report.append(write('album-settle',a,.16))
(out/'album-transition.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
