"""Original frontend sound palette. Rebuild with Python + NumPy; no third-party samples."""
from pathlib import Path
import numpy as np
import wave, json, uuid
SR=48000
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/RhythmDemo/Resources/Audio/UI'
OUT.mkdir(parents=True,exist_ok=True)
rng=np.random.default_rng(20461002)
def pluck(t,f,decay=12):
    e=(1-np.exp(-t/0.003))*np.exp(-t*decay)
    return (np.sin(2*np.pi*f*t)+.14*np.sin(2*np.pi*f*2.003*t)*np.exp(-t*9)+.045*np.sin(2*np.pi*f*3.98*t)*np.exp(-t*22))*e
def render(name,duration,notes,peak,body=0,air=0):
    n=round(duration*SR);t=np.arange(n)/SR;s=np.zeros(n)
    for start,f,amp,decay in notes:
        j=round(start*SR);s[j:]+=amp*pluck(t[:n-j],f,decay)
    if body:s+=body*np.sin(2*np.pi*(138*t+4*(1-np.exp(-t*35))))*(1-np.exp(-t/.002))*np.exp(-t*55)
    if air:
        noise=np.convolve(rng.normal(0,1,n),np.ones(32)/32,mode='same')
        s+=air*noise*(1-np.exp(-t/.013))*np.exp(-t*8)
    stereo=np.column_stack((s.copy(),s.copy()))
    for delay,gain,ch in [(.037,.065,0),(.053,.06,1),(.091,.028,0),(.113,.025,1)]:
        d=round(delay*SR)
        if d<n:stereo[d:,ch]+=s[:-d]*gain
    stereo-=stereo.mean(axis=0)
    fade=np.minimum(1,t/.002)*np.minimum(1,(duration-t)/min(.045,duration*.25))
    stereo*=np.maximum(0,fade[:,None]);stereo*=peak/max(np.max(np.abs(stereo)),1e-9)
    pcm=np.round(np.clip(stereo,-1,1)*32767).astype('<i2')
    path=OUT/(name+'.wav')
    with wave.open(str(path),'wb') as w:w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR);w.writeframes(pcm.tobytes())
    meta=Path(str(path)+'.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'''\nAudioImporter:
  externalObjects: {}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 48000
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
''')
    return {'name':name,'seconds':duration,'peak_dbfs':round(20*np.log10(peak),2),'rms_dbfs':round(20*np.log10(np.sqrt(np.mean(stereo**2))),2)}
D=587.33;A=880;E=659.25
specs=[
('hover',.095,[(0,A,.3,65)],.035,.1,.12),
('click',.16,[(0,D,.15,48)],.11,.7,.20),
('select',.22,[(0,D,.45,28),(.027,A,.12,32)],.12,.25,.1),
('open',.38,[(0,D/2,.38,15),(.035,A/2,.5,14),(.07,E,.15,16)],.15,.12,.13),
('close',.30,[(0,A/2,.40,20),(.038,D/2,.38,22)],.13,.18,.10),
('minimize',.25,[(0,E,.25,30),(.04,D/2,.42,25)],.12,.25,.1),
('restore',.32,[(0,D/2,.3,20),(.038,E,.35,20),(.07,A,.16,22)],.15,.12,.08),
('confirm',.46,[(0,D,.50,13),(.048,A,.3,13),(.095,E*2,.10,20)],.15,.18,.04),
('cancel',.27,[(0,E,.2,30),(.04,D,.4,25)],.12,.13,.04),
('like',.36,[(0,A,.3,18),(.04,D*2,.28,18)],.14,.12,.03),
('send',.40,[(0,E,.4,20),(.045,A,.3,20)],.15,.16,.08),
('error',.34,[(0,D/2,.4,24),(.105,D/2,.22,28)],.14,.1,.04),
('notification',.76,[(0,D,.45,9),(.09,E,.28,10),(.18,A,.24,11)],.20,0,.025),
('save',.85,[(0,D/2,.42,10),(.065,A/2,.30,10),(.14,D,.28,8)],.30,.12,.02),
('login',1.25,[(0,D/4,.38,5),(.09,D/2,.4,5),(.17,A/2,.26,5),(.26,E,.15,7),(.34,A,.17,7)],.36,.14,.05),
('logout',.90,[(0,A/2,.4,8),(.075,E/2,.27,8),(.15,D/2,.35,8)],.28,.08,.03),
('performance',1.62,[(0,D/4,.40,4),(.035,D/2,.22,6),(.105,A/2,.31,7),(.18,E,.22,8),(.29,A,.25,7),(.43,D*2,.17,7),(.58,D,.30,5)],.40,.30,.12),
('ready',.57,[(0,D,.40,11),(.06,A,.25,12),(.1,D*2,.10,15)],.22,.08,.025),
('stage-start',.72,[(0,D/4,.4,12),(.035,A/2,.32,13),(.09,D,.4,12),(.16,A,.2,13)],.34,.28,.05),
]
report=[render(*s) for s in specs]
for i in range(6):report.append(render('key-'+str(i),.09,[(0,175+i*9,.08,60)],.055,.7,.2))
(OUT/'palette.json').write_text(json.dumps(report,indent=2)+'\n')
# Audition at the same 0.72 gain used by Unity. Timed silence separates each cue.
preview=ROOT/'Docs/Screenshots/mainui-sound-palette.wav'
chunks=[];cues=[];cursor=0
for name in ['hover','click','select','open','close','minimize','restore','confirm','cancel','like','send','error','notification','save','login','logout','performance','ready','stage-start']:
    with wave.open(str(OUT/(name+'.wav')),'rb') as w:data=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').reshape(-1,2)
    cues.append({'time':round(cursor/SR,3),'sound':name});chunks.append((data*.72).astype('<i2'));chunks.append(np.zeros((int(.45*SR),2),dtype='<i2'));cursor+=len(data)+int(.45*SR)
with wave.open(str(preview),'wb') as w:w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR);w.writeframes(np.concatenate(chunks).tobytes())
(ROOT/'Docs/Screenshots/mainui-sound-palette.json').write_text(json.dumps(cues,indent=2))
print(json.dumps(report,indent=2))
